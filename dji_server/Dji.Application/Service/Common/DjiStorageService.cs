// 麻省理工学院许可证
//
// 版权所有 (c) 2021-2023  联系电话/微信：15100305  QQ：15100305
//
// 特此免费授予获得本软件的任何人以处理本软件的权利，但须遵守以下条件：在所有副本或重要部分的软件中必须包括上述版权声明和本许可声明。
//
// 软件按“原样”提供，不提供任何形式的明示或暗示的保证，包括但不限于对适销性、适用性和非侵权的保证。
// 在任何情况下，作者或版权持有人均不对任何索赔、损害或其他责任负责，无论是因合同、侵权或其他方式引起的，与软件或其使用或其他交易有关。

using Dji.Application.Cloud.Dto.Media;
using Microsoft.Extensions.Options;
using OnceMi.AspNetCore.OSS;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Dji.Application.Service.Common;

/// <summary>
/// 对象存储 ↔ 大疆协议 的映射服务。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么需要单独一层</b>：项目里对象存储配置是 <c>Upload.json</c> 的 <c>OSSProvider</c> 段
/// （供后端自身上传用，支持 Minio/Aliyun/QCloud/Qiniu/HuaweiCloud），
/// 而机场只认大疆协议里的三种 <c>provider</c> 取值（<c>ali</c> / <c>aws</c> / <c>minio</c>），
/// 且要求 endpoint 带 scheme、凭证字段名是下划线风格。
/// 这套映射散落在多处会很快失配，因此集中在这里，<c>storage_config_get</c> 应答与媒体访问地址拼接共用同一份规则。
/// </para>
/// <para>
/// <b>关于临时凭证</b>：大疆协议字段名为 <c>security_token</c>（会话凭证），
/// 只有走 STS AssumeRole 才需要。本项目用 MinIO 长期 AK/SK，故留空；
/// 若将来换成阿里云 OSS，把 <c>security_token</c> 与 <c>expire</c> 填上即可，
/// <b>调用方无需改动</b> —— 这正是把这层抽出来的收益。
/// </para>
/// </remarks>
public class DjiStorageService(ILogger<DjiStorageService> logger, IOptions<OSSProviderOptions> ossOptions,
    IOSSServiceFactory ossServiceFactory) : ITransient
{
    private readonly ILogger<DjiStorageService> _logger = logger;
    private readonly OSSProviderOptions _oss = ossOptions.Value;

    /// <summary>
    /// 预签名下载地址的默认有效期（秒）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>这个数字是个跨厂商的折中，改动前请先读这段。</b>
    /// <c>OnceMi.AspNetCore.OSS</c> 的 <c>PresignedGetObjectAsync(bucket, key, expiresInt)</c>
    /// 把 <c>expiresInt</c> 直接透传给各家 SDK，而<b>各家对它的单位理解并不一致</b>：
    /// MinIO 的 <c>expiresInt</c> 是<b>秒</b>（且必须在 1 ~ 604800 之间，否则直接抛异常），
    /// 部分云厂商的实现则是<b>天</b>。
    /// </para>
    /// <para>
    /// 本项目配置的是 MinIO（<c>Upload.json</c> 的 <c>Provider = Minio</c>），所以这里按<b>秒</b>取值：
    /// 2 小时足够设备完成一次下载，又不会把地址长期暴露出去。
    /// 若换成把该参数当天解释的厂商，2 小时会被算成「约 19 年」——
    /// 功能上仍可下载（不会失败），只是有效期偏长，届时应改小这个常量。
    /// </para>
    /// <para>
    /// 另外 MinIO 对超过 604800 的值会<b>抛异常</b>而不是钳制，所以取值绝不能随手写大。
    /// </para>
    /// </remarks>
    private const int DefaultSignedUrlExpiresSeconds = 7200;

    /// <summary>
    /// 对象存储客户端（按配置的 Provider 创建）。
    /// </summary>
    /// <remarks>
    /// <c>Create</c> 在 Provider 非法时会返回 null 或抛异常，因此这里延迟创建并吞掉异常 ——
    /// 取不到客户端时上层会退化为「直接用公开地址」，而不是让整个请求失败。
    /// </remarks>
    private IOSSService ResolveClient()
    {
        try
        {
            return ossServiceFactory?.Create(System.Enum.GetName(_oss.Provider));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "按 Provider={Provider} 创建对象存储客户端失败，将退回公开地址", _oss.Provider);
            return null;
        }
    }

    /// <summary>对象存储是否已启用</summary>
    public bool IsEnabled => _oss.IsEnable;

    /// <summary>当前配置的存储桶</summary>
    public string Bucket => _oss.Bucket;

    /// <summary>
    /// 构造 <c>storage_config_get</c> 的应答体。
    /// </summary>
    /// <param name="objectKeyPrefix">对象存储 Key 前缀，本平台传工作空间 ID</param>
    /// <remarks>
    /// 未启用对象存储时返回 <c>null</c>，由调用方回错误码 —— 不能返回一份空壳配置，
    /// 否则机场会拿空凭证去直传，失败信息出现在机场侧，排查成本高得多。
    /// </remarks>
    public StorageConfigGetOutput BuildStorageConfig(string objectKeyPrefix)
    {
        if (!IsEnabled)
        {
            _logger.LogWarning("机场请求媒体存储凭证，但 OSSProvider.IsEnable = false，无法下发；" +
                               "请在 Upload.json 的 OSSProvider 段启用对象存储");
            return null;
        }

        if (_oss.Bucket.IsNullOrWhiteSpace() || _oss.Endpoint.IsNullOrWhiteSpace())
        {
            _logger.LogWarning("对象存储配置不完整（Bucket / Endpoint 不能为空），无法向机场下发凭证");
            return null;
        }

        return new StorageConfigGetOutput
        {
            Bucket = _oss.Bucket,
            // 下发给机场的直传 endpoint 必须是「机场侧可达」的公网地址：
            // 配了 PublicBaseUrl 就用它（如 https://air-matrix.crmappai.xyz/minio），
            // 否则退回内网 Endpoint（仅适合本机联调）。绝不把 air-matrix-minio:9000 下发到机场。
            Endpoint = NormalizeEndpoint(_oss.PublicBaseUrl.IsNullOrWhiteSpace()
                ? _oss.Endpoint
                : _oss.PublicBaseUrl),
            Provider = MapProvider(_oss.Provider.ToString()),
            Region = _oss.Region ?? string.Empty,
            ObjectKeyPrefix = objectKeyPrefix ?? string.Empty,
            Credentials = new StorageCredentials
            {
                AccessKeyId = _oss.AccessKey ?? string.Empty,
                AccessKeySecret = _oss.SecretKey ?? string.Empty,
                // 长期 AK/SK 没有过期时间与会话令牌，填 0 / 空
                Expire = 0,
                SecurityToken = string.Empty,
            },
        };
    }

    /// <summary>
    /// 拼接媒体文件的对外访问地址。
    /// </summary>
    /// <remarks>
    /// 各厂商 URL 规则不同：阿里云走<b>虚拟主机风格</b>（bucket 作为子域），
    /// MinIO 默认走<b>路径风格</b>（bucket 作为路径段）。规则与 <c>SysFileService</c> 里的既有实现保持一致，
    /// 避免同一个桶里两份文件出现两种地址形态。
    /// </remarks>
    public string BuildFileUrl(string objectKey)
    {
        if (!IsEnabled || objectKey.IsNullOrWhiteSpace()) return null;

        var key = objectKey.TrimStart('/');

        // 公开基址优先：机场/前端经 nginx /minio 反代的公网 HTTPS 地址，
        // 绝暴露内网 Endpoint（air-matrix-minio:9000）。公开基址已含桶所在路径前缀（如 /minio），
        // 直接拼接 {PublicBaseUrl}/{bucket}/{key} 即可，由 nginx 去前缀反代到 MinIO。
        if (!_oss.PublicBaseUrl.IsNullOrWhiteSpace())
        {
            var publicBase = _oss.PublicBaseUrl.Trim().TrimEnd('/');
            return $"{publicBase}/{_oss.Bucket}/{key}";
        }

        var scheme = _oss.IsEnableHttps ? "https" : "http";
        var endpoint = _oss.Endpoint.Trim().TrimEnd('/');
        // Endpoint 可能已带 scheme，去掉后再拼，避免出现 http://https://...
        endpoint = endpoint.Replace("https://", string.Empty).Replace("http://", string.Empty);

        return MapProvider(_oss.Provider.ToString()) switch
        {
            // 阿里云：https://{bucket}.{endpoint}/{key}
            "ali" => $"{scheme}://{_oss.Bucket}.{endpoint}/{key}",
            // MinIO / S3 兼容：{scheme}://{endpoint}/{bucket}/{key}
            _ => $"{scheme}://{endpoint}/{_oss.Bucket}/{key}",
        };
    }

    /// <summary>
    /// 拼接飞行区文件在桶里的 Key。
    /// </summary>
    /// <remarks>
    /// 与媒体 / 日志同一套空间隔离规则（<c>{工作空间}/flightarea/{文件名}</c>），
    /// 只是独立成 <c>flightarea</c> 目录 —— 飞行区文件会被设备反复拉取，体积小但要求长期存在，
    /// 与媒体一起配生命周期规则会导致「飞行区文件被清理掉，设备同步持续失败」。
    /// </remarks>
    public string BuildFlightAreaKey(string workspaceId, string fileName)
    {
        if (fileName.IsNullOrWhiteSpace()) return null;

        var prefix = workspaceId.IsNullOrWhiteSpace() ? "default" : workspaceId.Trim();
        // 文件名可能带路径（历史遗留数据），只取最后一段，防止越出目录
        var name = fileName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return $"{prefix}/flightarea/{name}";
    }

    /// <summary>
    /// 把「对象 Key 或完整 URL」统一归一化成桶内 Key。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 运维在页面上不一定会填 Key —— 先用平台的上传功能传文件、再把拿到的 URL 粘过来是很自然的操作。
    /// 因此这里两种输入都要接住。两个方向的形态都要认：
    /// 阿里云是<b>虚拟主机风格</b>（bucket 在子域，路径里没有 bucket 段），
    /// MinIO / S3 兼容是<b>路径风格</b>（<c>/{bucket}/{key}</c>）。
    /// </para>
    /// <para>
    /// 判断依据是「有没有 <c>://</c>」而不是「有没有点号」：Key 里本来就允许出现点（<c>geofence_v1.2.json</c>），
    /// 用点号判会把 Key 误当成域名。
    /// </para>
    /// </remarks>
    /// <returns>桶内 Key；输入无法解析时返回 null</returns>
    public string ExtractObjectKey(string urlOrKey)
    {
        if (urlOrKey.IsNullOrWhiteSpace()) return null;

        var value = urlOrKey.Trim();

        // 不带协议头的按 Key 原样处理
        if (!value.Contains("://")) return value.TrimStart('/');

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return null;

        // AbsolutePath 天然丢掉查询串，预签名地址尾部的 ?Expires=... 不会混进 Key
        var path = uri.AbsolutePath.TrimStart('/');
        if (path.IsNullOrWhiteSpace()) return null;

        // 公开基址前缀：nginx 反代会把公开 URL 变成 https://<domain>/minio/<bucket>/<key>，
        // 其 AbsolutePath 为 minio/<bucket>/<key>。若配了 PublicBaseUrl，先把它自身的 path
        // 前缀（如 minio/）剥掉，否则后面按 bucket 剥离会失败、整段被误当成 Key。
        if (!_oss.PublicBaseUrl.IsNullOrWhiteSpace())
        {
            var publicBaseUri = TryCreateUri(_oss.PublicBaseUrl);
            if (publicBaseUri != null)
            {
                var publicPath = publicBaseUri.AbsolutePath.Trim('/');
                if (!publicPath.IsNullOrWhiteSpace())
                {
                    var publicPrefix = publicPath + "/";
                    if (path.StartsWith(publicPrefix, StringComparison.OrdinalIgnoreCase))
                        path = path[publicPath.Length..].TrimStart('/');
                }
            }
        }

        // 路径风格：剥掉首段的 bucket
        if (!_oss.Bucket.IsNullOrWhiteSpace())
        {
            var prefix = _oss.Bucket.Trim('/') + "/";
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return path[prefix.Length..];
        }

        return path;
    }

    /// <summary>
    /// 生成一个<b>可对外暴露的下载地址</b>（优先带签名，失败则退回公开地址）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 设备来取自定义飞行区文件时<b>每次都要现签</b>：设备可能在云端下发通知后任意时刻才来要，
    /// 若缓存一份地址，等设备真的来取时很可能已经过期，表现为「云端说下发成功、设备持续报下载失败」。
    /// </para>
    /// <para>
    /// 签名失败时退回 <see cref="BuildFileUrl"/>：桶若被配成公开读，退回后设备仍能下载成功。
    /// 两种都拿不到才返回 null，由调用方决定是否回错误码。
    /// </para>
    /// </remarks>
    public async Task<string> BuildDownloadUrlAsync(string objectKey, int expiresSeconds = DefaultSignedUrlExpiresSeconds)
    {
        if (!IsEnabled || objectKey.IsNullOrWhiteSpace()) return null;

        var client = ResolveClient();
        if (client != null && _oss.Bucket.IsNullOrWhiteSpace() == false)
        {
            try
            {
                var url = await client.PresignedGetObjectAsync(_oss.Bucket, objectKey, expiresSeconds);
                if (!url.IsNullOrWhiteSpace())
                    // 预签名地址由内网 Endpoint（air-matrix-minio:9000）生成，机场/浏览器不可达；
                    // 配了 PublicBaseUrl 时改写为公网基址（https://air-matrix.crmappai.xyz/minio/...）。
                    return RewriteToPublicUrl(url);
            }
            catch (Exception ex)
            {
                // 常见原因：MinIO 对 expiresInt 的取值区间校验不通过；日志里带上取值便于定位
                _logger.LogWarning(ex, "生成预签名下载地址失败（key={Key}，expires={Expires}），将退回公开地址",
                    objectKey, expiresSeconds);
            }
        }

        return BuildFileUrl(objectKey);
    }

    /// <summary>
    /// 从对象存储取回文件内容并计算 <b>SHA256 摘要</b>（十六进制小写）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>为什么要服务端算而不是让调用方传</b>：飞行区文件的 <c>checksum</c> 是「云端认为设备该跑哪一版」
    /// 与「设备实际同步到哪一版」的<b>唯一对齐依据</b> —— 设备在
    /// <c>flight_areas_sync_progress</c> 里只报摘要、不报内容。若这个值由人工填写，
    /// 一旦填错（少一位字符、用了 MD5 而非 SHA256），设备永远同步不上且<b>双方日志都不报错</b>，
    /// 排查成本极高。因此直接从桶里的真实内容算。
    /// </para>
    /// <para>
    /// 飞行区文件是几十 KB 的 JSON，整份读进内存没有任何压力，故不做流式处理。
    /// </para>
    /// </remarks>
    /// <returns>成功返回 (小写十六进制摘要, 字节数)；对象不存在或存储未启用时返回 (null, 0)</returns>
    public async Task<(string Checksum, long Size)> ComputeFileDigestAsync(string objectKey)
    {
        if (!IsEnabled || objectKey.IsNullOrWhiteSpace()) return (null, 0);

        var client = ResolveClient();
        if (client == null || _oss.Bucket.IsNullOrWhiteSpace()) return (null, 0);

        try
        {
            using var buffer = new MemoryStream();
            await client.GetObjectAsync(_oss.Bucket, objectKey, stream =>
            {
                stream.CopyTo(buffer);
            }, CancellationToken.None);

            if (buffer.Length == 0) return (null, 0);

            buffer.Position = 0;
            var hash = await SHA256.HashDataAsync(buffer);
            return (Convert.ToHexString(hash).ToLowerInvariant(), buffer.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取对象存储文件失败，无法计算摘要：bucket={Bucket}，key={Key}", _oss.Bucket, objectKey);
            return (null, 0);
        }
    }

    /// <summary>
    /// 把项目内的 OSS 提供商标识映射为大疆协议的 <c>provider</c> 枚举值。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 项目使用的 <c>OnceMi.AspNetCore.OSS</c> 提供 <c>Invalid / Minio / Aliyun / QCloud / Qiniu / HuaweiCloud</c> 六种，
    /// 而大疆协议<b>只接受 <c>ali</c> / <c>aws</c> / <c>minio</c></b>。
    /// 因此只有前两种是原生对齐的，其余（QCloud / Qiniu / HuaweiCloud）都是 S3 兼容但官方未列，
    /// 统一按 <c>aws</c> 下发并打警告日志 —— 机场至少能按 S3 协议尝试上传，比直接回错误码好定位。
    /// </para>
    /// <para>
    /// 这里刻意用字符串比较而不是引用 <c>OSSProvider</c> 枚举成员：
    /// 该库的枚举成员名在版本间调整过（当前 1.2.0 无 <c>AwsS3</c>），
    /// 字符串映射可在升级库时保持不变。
    /// </para>
    /// </remarks>
    private string MapProvider(string providerName)
    {
        var name = providerName?.Trim().ToLowerInvariant();

        switch (name)
        {
            case "minio":
                return "minio";
            case "aliyun" or "ali":
                return "ali";
            case "invalid" or null or "":
                _logger.LogWarning("对象存储 Provider 未正确配置（当前：{Provider}），已按 minio 下发，请检查 Upload.json", providerName);
                return "minio";
            default:
                _logger.LogWarning("对象存储提供商 {Provider} 不在大疆协议枚举（ali/aws/minio）内，已按 aws(S3 兼容) 下发；"
                                   + "若机场上传失败，请改用 MinIO 或阿里云 OSS", providerName);
                return "aws";
        }
    }

    /// <summary>补全 endpoint 的 scheme（配置里通常只填域名）</summary>
    private string NormalizeEndpoint(string endpoint)
    {
        var value = endpoint.Trim().TrimEnd('/');
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return (_oss.IsEnableHttps ? "https://" : "http://") + value;
    }

    /// <summary>
    /// 把由<b>内网 Endpoint</b>生成的预签名/直链 URL 改写为<b>公网公开基址</b>形式。
    /// </summary>
    /// <remarks>
    /// 对象存储客户端按 <c>Upload.json</c> 的 <c>Endpoint</c>（如 <c>air-matrix-minio:9000</c>）签名，
    /// 产出的 URL 形如 <c>http://air-matrix-minio:9000/dji-media/key?Expires=...</c>，
    /// 机场/浏览器无法直连。配了 <c>PublicBaseUrl</c>（如 <c>https://air-matrix.crmappai.xyz/minio</c>）时，
    /// 仅替换「协议+主机+路径前缀」部分，保留原 path 中桶与对象 Key 以及查询串：
    /// <c>https://air-matrix.crmappai.xyz/minio/dji-media/key?Expires=...</c>，经 nginx 去 <c>/minio</c> 反代到 MinIO。
    /// 未配置 PublicBaseUrl 或解析失败时原样返回（本地联调）。
    /// </remarks>
    private string RewriteToPublicUrl(string internalUrl)
    {
        if (_oss.PublicBaseUrl.IsNullOrWhiteSpace() || internalUrl.IsNullOrWhiteSpace())
            return internalUrl;

        try
        {
            var internalUri = new Uri(internalUrl);
            var publicBaseUri = TryCreateUri(_oss.PublicBaseUrl);
            if (publicBaseUri == null) return internalUrl;

            var publicOrigin = publicBaseUri.GetLeftPart(UriPartial.Authority); // https://air-matrix.crmappai.xyz
            var publicPath = publicBaseUri.AbsolutePath.TrimEnd('/');           // /minio
            var rewritten = publicOrigin + publicPath + internalUri.AbsolutePath + internalUri.Query;
            return rewritten;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "预签名 URL 改写为公网基址失败，退回原始内网地址：{Url}", internalUrl);
            return internalUrl;
        }
    }

    /// <summary>
    /// 安全地把字符串解析为绝对 URI（失败返回 null，不抛异常）。
    /// </summary>
    private static Uri TryCreateUri(string raw)
    {
        if (raw.IsNullOrWhiteSpace()) return null;
        return Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri) ? uri : null;
    }
}

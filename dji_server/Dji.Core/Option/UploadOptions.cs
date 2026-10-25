// 麻省理工学院许可证
//
// 版权所有 (c) 2021-2023 yanyi  联系电话/微信：18600766045  QQ：15100305
//
// 特此免费授予获得本软件的任何人以处理本软件的权利，但须遵守以下条件：在所有副本或重要部分的软件中必须包括上述版权声明和本许可声明。
//
// 软件按“原样”提供，不提供任何形式的明示或暗示的保证，包括但不限于对适销性、适用性和非侵权的保证。
// 在任何情况下，作者或版权持有人均不对任何索赔、损害或其他责任负责，无论是因合同、侵权或其他方式引起的，与软件或其使用或其他交易有关。

using OnceMi.AspNetCore.OSS;

namespace Dji.Core;

/// <summary>
/// 文件上传配置选项
/// </summary>
public sealed class UploadOptions : IConfigurableOptions
{
    /// <summary>
    /// 路径
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// 大小
    /// </summary>
    public long MaxSize { get; set; }

    /// <summary>
    /// 上传格式
    /// </summary>
    public List<string> ContentType { get; set; }

    /// <summary>
    /// 启用文件MD5验证
    /// </summary>
    /// <remarks>防止重复上传</remarks>
    public bool EnableMd5 { get; set; }
}

/// <summary>
/// 对象存储配置选项
/// </summary>
public sealed class OSSProviderOptions : OSSOptions, IConfigurableOptions
{
    /// <summary>
    /// 是否启用OSS存储
    /// </summary>
    public bool IsEnable { get; set; }

    /// <summary>
    /// 自定义桶名称 不能直接使用Provider来替代桶名称
    /// 例：阿里云 1.只能包括小写字母，数字，短横线（-）2.必须以小写字母或者数字开头 3.长度必须在3-63字节之间
    /// </summary>
    public string Bucket { get; set; }

    /// <summary>
    /// 对象存储的<b>对外公开基址</b>（可选）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 生产环境通过 nginx 反代把 MinIO(9000) 暴露成公网 HTTPS 地址时使用：
    /// 例如 <c>https://air-matrix.crmappai.xyz/minio</c>。
    /// 一旦配置，<c>DjiStorageService</c> 对外拼接的媒体访问地址、
    /// 下发给机场的直传 endpoint、以及预签名 URL 都会以此基址为前缀，
    /// <b>绝不</b>把内网 <c>Endpoint</c>（如 <c>air-matrix-minio:9000</c>）暴露给前端/机场。
    /// 留空则维持原有「内网 Endpoint 直拼」逻辑（本地联调场景）。
    /// </para>
    /// </remarks>
    public string PublicBaseUrl { get; set; } = string.Empty;
}
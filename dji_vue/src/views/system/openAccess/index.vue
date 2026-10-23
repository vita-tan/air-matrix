<template>
	<div class="sys-open-access-container">
		<el-alert type="info" :closable="false" show-icon style="margin-bottom: 8px">
			<template #title>
				开放接口身份用于第三方系统以「签名鉴权」方式调用本平台开放接口。每个身份由 <b>身份标识(AccessKey)</b> +
				<b>密钥(AccessSecret)</b> 组成，并绑定到一个平台用户 —— 该身份的接口权限等同于所绑定用户的权限。
			</template>
		</el-alert>

		<el-card shadow="hover" :body-style="{ paddingBottom: '0' }">
			<el-form :model="state.queryParams" ref="queryForm" :inline="true">
				<el-form-item label="身份标识">
					<el-input v-model="state.queryParams.accessKey" placeholder="AccessKey" clearable style="width: 220px" @keyup.enter="handleQuery" />
				</el-form-item>
				<el-form-item label="绑定用户">
					<el-input v-model="state.queryParams.bindUserAccount" placeholder="账号 / 姓名" clearable style="width: 200px" @keyup.enter="handleQuery" />
				</el-form-item>
				<el-form-item>
					<el-button-group>
						<el-button type="primary" icon="ele-Search" @click="handleQuery" v-auth="'sysOpenAccess:page'"> 查询 </el-button>
						<el-button icon="ele-Refresh" @click="resetQuery"> 重置 </el-button>
					</el-button-group>
				</el-form-item>
				<el-form-item>
					<el-button type="primary" icon="ele-Plus" @click="openAdd" v-auth="'sysOpenAccess:add'"> 新增 </el-button>
				</el-form-item>
			</el-form>
		</el-card>

		<el-card class="full-table" shadow="hover" style="margin-top: 8px">
			<el-table :data="filteredList" style="width: 100%" v-loading="state.loading" border>
				<el-table-column type="index" label="序号" width="55" align="center" />
				<el-table-column prop="accessKey" label="身份标识" min-width="170" align="center" show-overflow-tooltip />
				<el-table-column label="密钥" min-width="190" align="center">
					<template #default="scope">
						<span v-if="scope.row._showSecret" class="secret-text">{{ scope.row.accessSecret }}</span>
						<span v-else class="secret-text">{{ maskSecret(scope.row.accessSecret) }}</span>
						<el-button size="small" text type="primary" @click="toggleSecret(scope.row)">
							{{ scope.row._showSecret ? '隐藏' : '显示' }}
						</el-button>
					</template>
				</el-table-column>
				<el-table-column prop="bindUserAccount" label="绑定用户" min-width="130" align="center" show-overflow-tooltip>
					<template #default="scope">
						<span v-if="scope.row.bindUserAccount">{{ scope.row.bindUserAccount }}</span>
						<el-tag v-else type="info" size="small">未绑定</el-tag>
					</template>
				</el-table-column>
				<el-table-column prop="bindTenantName" label="绑定租户" min-width="120" align="center" show-overflow-tooltip>
					<template #default="scope">
						<span v-if="scope.row.bindTenantName">{{ scope.row.bindTenantName }}</span>
						<span v-else class="muted">默认租户</span>
					</template>
				</el-table-column>
				<el-table-column prop="createTime" label="创建时间" width="170" align="center" show-overflow-tooltip />
				<el-table-column label="操作" width="140" fixed="right" align="center">
					<template #default="scope">
						<el-button icon="ele-Edit" size="small" text type="primary" @click="openEdit(scope.row)" v-auth="'sysOpenAccess:update'"> 编辑 </el-button>
						<el-button icon="ele-Delete" size="small" text type="danger" @click="del(scope.row)" v-auth="'sysOpenAccess:delete'"> 删除 </el-button>
					</template>
				</el-table-column>
				<template #empty>
					<el-empty description="暂无开放接口身份，点击右上角「新增」创建" :image-size="70" />
				</template>
			</el-table>
			<div class="pager">
				<span class="muted">共 {{ state.total }} 条</span>
				<el-pagination
					v-model:current-page="state.page"
					v-model:page-size="state.pageSize"
					:page-sizes="[10, 20, 50, 100]"
					:total="state.total"
					layout="sizes, prev, pager, next"
					background
					@size-change="handleQuery"
					@current-change="handleQuery"
				/>
			</div>
		</el-card>

		<el-dialog v-model="state.dialogVisible" :title="state.dialogTitle" width="580px" draggable destroy-on-close>
			<el-form ref="formRef" :model="state.form" :rules="state.rules" label-width="96px">
				<el-form-item label="身份标识" prop="accessKey">
					<el-input v-model="state.form.accessKey" placeholder="如 agri-dock-01，创建后建议不要修改" maxlength="128">
						<template #append><el-button @click="randomKey">随机</el-button></template>
					</el-input>
				</el-form-item>
				<el-form-item label="密钥" prop="accessSecret">
					<el-input v-model="state.form.accessSecret" placeholder="AccessSecret" type="password" show-password maxlength="256">
						<template #append><el-button @click="genSecret">生成</el-button></template>
					</el-input>
				</el-form-item>
				<el-form-item label="绑定用户" prop="bindUserId">
					<el-select
						v-model="state.form.bindUserId"
						filterable
						remote
						reserve-keyword
						clearable
						placeholder="输入账号或姓名搜索"
						:remote-method="searchUsers"
						:loading="state.userLoading"
						style="width: 100%"
					>
						<el-option v-for="u in state.users" :key="u.id" :label="userLabel(u)" :value="u.id" />
					</el-select>
				</el-form-item>
				<el-form-item label="绑定租户">
					<el-input-number v-model="state.form.bindTenantId" :min="0" :precision="0" controls-position="right" style="width: 100%" placeholder="0 表示默认租户" />
				</el-form-item>
				<el-alert v-if="state.dialogTitle === '编辑开放接口身份'" type="warning" :closable="false" show-icon style="margin: 0 0 4px 96px">
					修改密钥后，使用旧密钥的第三方系统将立即失效，请谨慎操作。
				</el-alert>
			</el-form>
			<template #footer>
				<el-button @click="state.dialogVisible = false">取消</el-button>
				<el-button type="primary" :loading="state.submitting" @click="submit">确定</el-button>
			</template>
		</el-dialog>
	</div>
</template>

<script lang="ts" setup name="sysOpenAccess">
import { computed, onMounted, reactive, ref } from 'vue';
import { ElMessageBox, ElMessage } from 'element-plus';
import { axiosInstance, serveConfig } from '/@/utils/axios-utils';

/** API 基址（来自 .env 的 VITE_API_URL，默认 http://localhost:5005） */
const BASE = (serveConfig.basePath || '').replace(/\/+$/, '');

interface OpenAccessRow {
	id?: number;
	accessKey: string;
	accessSecret: string;
	bindUserId?: number;
	bindTenantId?: number;
	bindUserAccount?: string;
	bindTenantName?: string;
	createTime?: string;
	_showSecret?: boolean;
}

interface UserRow {
	id: number;
	account: string;
	realName?: string;
	nickName?: string;
}

/** 统一响应体请求封装：返回 result，失败时抛错并提示 */
const request = async <T>(method: 'get' | 'post', url: string, data?: any): Promise<T> => {
	const full = BASE + url;
	const res = method === 'get' ? await axiosInstance.get(full, { params: data }) : await axiosInstance.post(full, data ?? {});
	const body: any = res.data;
	if (body && body.code !== 200) {
		ElMessage.error(body.message || '请求失败');
		throw new Error(body.message || '请求失败');
	}
	return body?.result as T;
};

const formRef = ref();
const state = reactive({
	loading: false,
	submitting: false,
	userLoading: false,
	list: [] as OpenAccessRow[],
	users: [] as UserRow[],
	total: 0,
	page: 1,
	pageSize: 10,
	dialogVisible: false,
	dialogTitle: '新增开放接口身份',
	isEdit: false,
	queryParams: { accessKey: '', bindUserAccount: '' },
	form: { id: undefined as number | undefined, accessKey: '', accessSecret: '', bindUserId: undefined as number | undefined, bindTenantId: 0 },
	rules: {
		accessKey: [{ required: true, message: '请输入身份标识', trigger: 'blur' }],
		accessSecret: [{ required: true, message: '请输入或生成密钥', trigger: 'blur' }],
		bindUserId: [{ required: true, message: '请选择绑定用户', trigger: 'change' }],
	},
});

/** 后端 Page 暂仅支持 accessKey 过滤，绑定用户在前端按当前页过滤 */
const filteredList = computed(() => {
	const kw = state.queryParams.bindUserAccount.trim();
	if (!kw) return state.list;
	return state.list.filter((r) => (r.bindUserAccount || '').includes(kw) || `用户${r.bindUserId}`.includes(kw));
});

const maskSecret = (s: string) => (s && s.length > 8 ? s.slice(0, 6) + '*'.repeat(Math.min(20, s.length - 6)) : '******');
const toggleSecret = (row: OpenAccessRow) => {
	row._showSecret = !row._showSecret;
};
const userLabel = (u: UserRow) => `${u.account}${u.realName ? ' / ' + u.realName : ''}`;

// 查询
const handleQuery = async () => {
	state.loading = true;
	try {
		const res: any = await request('post', '/api/sysOpenAccess/page', {
			page: state.page,
			pageSize: state.pageSize,
			accessKey: state.queryParams.accessKey?.trim() || undefined,
		});
		state.list = (res?.items ?? []).map((r: OpenAccessRow) => ({ ...r, _showSecret: false }));
		state.total = res?.total ?? 0;
	} catch {
		state.list = [];
		state.total = 0;
	} finally {
		state.loading = false;
	}
};

const resetQuery = () => {
	state.queryParams.accessKey = '';
	state.queryParams.bindUserAccount = '';
	state.page = 1;
	handleQuery();
};

// 用户远程搜索
const loadUsers = async (keyword = '') => {
	state.userLoading = true;
	try {
		const res: any = await request('post', '/api/sysUser/page', { page: 1, pageSize: 20, account: keyword?.trim() || undefined });
		state.users = res?.items ?? [];
		// 编辑回显：确保当前绑定用户在下拉里
		const cur = state.form.bindUserId;
		if (cur && !state.users.some((u) => u.id === cur) && state.form.bindUserId) {
			const info = state.list.find((r) => r.id === state.form.id);
			if (info?.bindUserAccount) state.users.unshift({ id: cur, account: info.bindUserAccount } as UserRow);
		}
	} catch {
		state.users = [];
	} finally {
		state.userLoading = false;
	}
};
const searchUsers = (q: string) => loadUsers(q);

// 新增 / 编辑
const openAdd = async () => {
	state.dialogTitle = '新增开放接口身份';
	state.isEdit = false;
	state.form = { id: undefined, accessKey: '', accessSecret: '', bindUserId: undefined, bindTenantId: 0 };
	state.dialogVisible = true;
	await loadUsers('');
};
const openEdit = async (row: OpenAccessRow) => {
	state.dialogTitle = '编辑开放接口身份';
	state.isEdit = true;
	state.form = { id: row.id, accessKey: row.accessKey, accessSecret: row.accessSecret, bindUserId: row.bindUserId, bindTenantId: row.bindTenantId || 0 };
	state.dialogVisible = true;
	await loadUsers('');
};

const randomKey = () => {
	const rnd = Math.random().toString(36).slice(2, 8);
	state.form.accessKey = `agri-${Date.now().toString(36)}-${rnd}`;
};
const genSecret = async () => {
	try {
		const secret = await request<string>('post', '/api/sysOpenAccess/createSecret', {});
		if (secret) {
			state.form.accessSecret = secret;
			ElMessage.success('已生成新密钥，请妥善保存');
		}
	} catch {
		/* 已提示 */
	}
};

const submit = async () => {
	await formRef.value?.validate(async (valid: boolean) => {
		if (!valid) return;
		state.submitting = true;
		try {
			const payload = {
				id: state.form.id,
				accessKey: state.form.accessKey.trim(),
				accessSecret: state.form.accessSecret,
				bindUserId: state.form.bindUserId,
				bindTenantId: state.form.bindTenantId || 0,
			};
			await request('post', state.isEdit ? '/api/sysOpenAccess/update' : '/api/sysOpenAccess/add', payload);
			ElMessage.success(state.isEdit ? '修改成功' : '新增成功');
			state.dialogVisible = false;
			handleQuery();
		} catch {
			/* 已提示 */
		} finally {
			state.submitting = false;
		}
	});
};

// 删除
const del = (row: OpenAccessRow) => {
	ElMessageBox.confirm(`确定删除开放接口身份：【${row.accessKey}】？删除后使用该身份的第三方系统将无法访问。`, '提示', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	})
		.then(async () => {
			await request('post', '/api/sysOpenAccess/delete', { id: row.id });
			ElMessage.success('删除成功');
			if (state.list.length === 1 && state.page > 1) state.page--;
			handleQuery();
		})
		.catch(() => {});
};

onMounted(() => {
	handleQuery();
});
</script>

<style scoped lang="scss">
.sys-open-access-container {
	.secret-text {
		font-family: Consolas, Monaco, monospace;
		font-size: 12.5px;
	}
	.muted {
		color: var(--el-text-color-placeholder);
	}
	.pager {
		display: flex;
		align-items: center;
		justify-content: flex-end;
		gap: 12px;
		padding: 10px 0 0;
	}
}
</style>

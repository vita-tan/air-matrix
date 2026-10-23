<template>
	<div class="layout-navbars-more">
		<!-- 左上角「更多」启动器 -->
		<div class="more-btn" :class="{ active: state.open }" title="更多功能" @click.stop="state.open = !state.open">
			<svg viewBox="0 0 24 24" width="17" height="17" fill="currentColor" aria-hidden="true">
				<circle cx="5" cy="5" r="1.7" /><circle cx="12" cy="5" r="1.7" /><circle cx="19" cy="5" r="1.7" />
				<circle cx="5" cy="12" r="1.7" /><circle cx="12" cy="12" r="1.7" /><circle cx="19" cy="12" r="1.7" />
				<circle cx="5" cy="19" r="1.7" /><circle cx="12" cy="19" r="1.7" /><circle cx="19" cy="19" r="1.7" />
			</svg>
		</div>

		<!-- 全功能面板 -->
		<transition name="more-fade">
			<div v-if="state.open" class="more-panel" @click.stop>
				<div class="more-search">
					<svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
						<circle cx="11" cy="11" r="7" /><path d="M20 20l-3.5-3.5" />
					</svg>
					<input v-model="state.keyword" placeholder="输入关键字以搜索功能" />
				</div>
				<div class="more-body" v-if="filteredGroups.length">
					<div class="more-group" v-for="(g, gi) in filteredGroups" :key="gi">
						<div class="more-group-title">{{ g.title }}</div>
						<div class="more-items">
							<div class="more-item" v-for="(it, ii) in g.items" :key="ii" @click="onGo(it)">
								<SvgIcon v-if="it.icon" :name="it.icon" class="more-item-icon" />
								<span class="more-item-text">{{ it.title }}</span>
							</div>
						</div>
					</div>
				</div>
				<div class="more-body more-empty" v-else>未找到匹配的功能</div>
			</div>
		</transition>
	</div>
</template>

<script setup lang="ts" name="moreMenu">
import { computed, reactive, onMounted, onUnmounted } from 'vue';
import { useRouter } from 'vue-router';
import { storeToRefs } from 'pinia';
import { useRoutesListStore } from '/@/stores/useRoutesListStore';

const router = useRouter();
const routesStore = useRoutesListStore();
const { routesList } = storeToRefs(routesStore);

const state = reactive({ open: false, keyword: '' });

interface MoreItem {
	title: string;
	icon?: string;
	path?: string;
	link?: string;
}

/** 将权限路由树拍平成「分组 + 功能项」，作为「更多」面板内容 */
const groups = computed<Array<{ title: string; items: MoreItem[] }>>(() => {
	const list = (routesList.value || []) as any[];
	return list
		.filter((top) => !top.meta?.isHide)
		.map((top) => {
			const children = ((top.children && top.children.length ? top.children : [top]) as any[]).filter((c) => !c.meta?.isHide);
			return {
				title: (top.meta?.title as string) || '其他',
				items: children.map((c) => ({
					title: (c.meta?.title as string) || '未命名',
					icon: c.meta?.icon as string,
					path: c.path as string,
					link: c.meta?.isLink as string,
				})),
			};
		})
		.filter((g) => g.items.length);
});

/** 关键字搜索 */
const filteredGroups = computed(() => {
	const kw = state.keyword.trim();
	if (!kw) return groups.value;
	return groups.value
		.map((g) => ({ ...g, items: g.items.filter((i) => i.title.includes(kw)) }))
		.filter((g) => g.items.length);
});

const onGo = (it: MoreItem) => {
	state.open = false;
	if (it.link) window.open(it.link, '_blank');
	else if (it.path) router.push(it.path);
};

const onClose = () => {
	state.open = false;
};

onMounted(() => document.addEventListener('click', onClose));
onUnmounted(() => document.removeEventListener('click', onClose));
</script>

<style scoped lang="scss">
.layout-navbars-more {
	position: relative;
	display: flex;
	align-items: center;
}
.more-btn {
	width: 30px;
	height: 30px;
	border-radius: 6px;
	display: grid;
	place-items: center;
	cursor: pointer;
	color: var(--el-text-color-regular);
	transition: all 0.2s;
	&:hover {
		background: var(--el-fill-color-light);
		color: var(--el-color-primary);
	}
	&.active {
		color: var(--el-color-primary);
		background: var(--el-color-primary-light-9);
	}
}
.more-panel {
	position: absolute;
	top: 38px;
	left: 0;
	z-index: 100;
	width: 620px;
	max-width: calc(100vw - 40px);
	max-height: 70vh;
	overflow: auto;
	background: var(--el-bg-color);
	border: 1px solid var(--el-border-color-lighter);
	border-radius: 10px;
	box-shadow: 0 12px 32px var(--el-box-shadow-light);
	padding: 12px 14px 14px;
}
.more-search {
	display: flex;
	align-items: center;
	gap: 8px;
	height: 34px;
	padding: 0 10px;
	border-radius: 7px;
	background: var(--el-fill-color-light);
	color: var(--el-text-color-placeholder);
	margin-bottom: 10px;
	input {
		flex: 1;
		border: 0;
		outline: none;
		background: transparent;
		font-size: 13px;
		color: var(--el-text-color-primary);
	}
}
.more-group {
	margin-bottom: 12px;
	&:last-child {
		margin-bottom: 0;
	}
}
.more-group-title {
	font-size: 12px;
	font-weight: 600;
	color: var(--el-color-primary);
	margin-bottom: 6px;
	display: flex;
	align-items: center;
	gap: 6px;
	&::before {
		content: '';
		width: 3px;
		height: 11px;
		border-radius: 2px;
		background: var(--el-color-primary);
	}
}
.more-items {
	display: grid;
	grid-template-columns: repeat(4, minmax(0, 1fr));
	gap: 2px;
}
.more-item {
	display: flex;
	align-items: center;
	gap: 7px;
	padding: 8px 9px;
	border-radius: 7px;
	cursor: pointer;
	font-size: 12.5px;
	color: var(--el-text-color-regular);
	transition: all 0.15s;
	&:hover {
		background: var(--el-color-primary-light-9);
		color: var(--el-color-primary);
	}
}
.more-item-icon {
	font-size: 15px;
	flex: 0 0 auto;
	opacity: 0.85;
}
.more-item-text {
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}
.more-empty {
	text-align: center;
	color: var(--el-text-color-placeholder);
	font-size: 12px;
	padding: 24px 0;
}
.more-fade-enter-active,
.more-fade-leave-active {
	transition: all 0.18s ease;
}
.more-fade-enter-from,
.more-fade-leave-to {
	opacity: 0;
	transform: translateY(-6px);
}
</style>

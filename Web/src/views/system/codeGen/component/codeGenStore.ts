import { defineStore } from 'pinia';
import {
	AddCodeGenInput,
	ColumnOutput,
	DatabaseOutput,
	DefaultColumnConfigInput,
	EffectTreeConfigInput,
	SysCodeGenApi,
	SysDictType,
	SysDictTypeApi,
	SysMenu,
	SysMenuApi,
	TableOutput,
	UpdateCodeGenInput,
} from '/@/api-services/system';
import { getAPI } from '/@/utils/axios-utils';
import { useUserInfo } from '/@/stores/userInfo';
import { nextTick, watch } from 'vue';

const apiManager = {
	detail: (id: number) => {
		return getAPI(SysCodeGenApi)
			.apiSysCodeGenDetailGet(id)
			.then((res) => res.data.result);
	},
	add: (body: AddCodeGenInput) => {
		return getAPI(SysCodeGenApi).apiSysCodeGenAddPost(body);
	},
	update: (body: UpdateCodeGenInput) => {
		return getAPI(SysCodeGenApi).apiSysCodeGenUpdatePost(body);
	},
	getColumnConfigList: (id: number) => {
		return getAPI(SysCodeGenApi)
			.apiSysCodeGenTableDetailGet(id)
			.then((res) => res.data.result);
	},
	getMenuList: () => {
		return getAPI(SysMenuApi)
			.apiSysMenuListGet()
			.then((res) => res.data.result ?? []);
	},
	getDictTypeList: () => {
		return getAPI(SysDictTypeApi)
			.apiSysDictTypeListGet()
			.then((res) => res.data.result ?? []);
	},
	getQuickConfigMap: () => {
		return getAPI(SysCodeGenApi)
			.apiSysCodeGenQuickConfigMapGet()
			.then((res) => res.data.result ?? {});
	},
	getNamespaceList: () => {
		return getAPI(SysCodeGenApi)
			.apiSysCodeGenApplicationNamespacesGet()
			.then((res) => res.data.result ?? []);
	},
	getDatabaseList: () => {
		return getAPI(SysCodeGenApi)
			.apiSysCodeGenDatabaseListGet()
			.then((res) => res.data.result ?? []);
	},
	getTableList: (configId: string) => {
		return getAPI(SysCodeGenApi)
			.apiSysCodeGenTableListConfigIdGet(configId)
			.then((res) => res.data.result ?? []);
	},
	getColumnList: (configId: string, tableName: string) => {
		return getAPI(SysCodeGenApi)
			.apiSysCodeGenColumnListByTableNameTableNameConfigIdGet(tableName, configId)
			.then((res) => res.data.result ?? []);
	},
	getDefaultColumnConfigList: (body: DefaultColumnConfigInput) => {
		return getAPI(SysCodeGenApi)
			.apiSysCodeGenDefaultColumnConfigListPost(body)
			.then((res) => res.data.result ?? []);
	},
};

interface CodeGenStore {
	enabledWatch: boolean;
	loading: boolean;
	constList: any[];
	menuList?: SysMenu[];
	dictList: SysDictType[];
	enumList: SysDictType[];
	namespaceList?: string[];
	databaseList?: DatabaseOutput[];
	tableMap: { [key: string]: TableOutput[] };
	columnMap: { [key: string]: ColumnOutput[] };
	quickConfigMap?: { [key: string]: EffectTreeConfigInput };
	ruleForm: UpdateCodeGenInput;
	apiManager: typeof apiManager;
}

const useStore = useUserInfo();
export const useCodeGenStore = defineStore('codeGenStore', {
	state: (): CodeGenStore => ({
		enabledWatch: false,
		loading: false,
		dictList: [],
		enumList: [],
		constList: [],
		tableMap: {},
		columnMap: {},
		ruleForm: {} as any,
		apiManager: apiManager,
	}),
	getters: {
		sceneLabel: (state) => useStore.dictList['CodeGenSceneEnum'].find((u: any) => u.value === state.ruleForm.scene)?.label,
		tableListBy: (state) => {
			return async (configId: string) => {
				if (configId && !state.tableMap[configId]) state.tableMap[configId] = await state.apiManager.getTableList(configId);
				return state.tableMap[configId];
			};
		},
		columnListBy: (state) => {
			return async (configId: string, tableName: string) => {
				const key = `${configId}:${tableName}`;
				if (tableName && configId && !state.columnMap[key]) state.columnMap[key] = await state.apiManager.getColumnList(configId, tableName);
				return state.columnMap[key];
			};
		},
	},
	actions: {
		async init() {
			this.menuList ??= await this.apiManager.getMenuList();
			this.databaseList ??= await this.apiManager.getDatabaseList();
			this.namespaceList ??= await this.apiManager.getNamespaceList();
			this.quickConfigMap ??= await this.apiManager.getQuickConfigMap();

			// 预加载数据库表
			if (this.databaseList?.length > 0) {
				for (const db of this.databaseList) {
					if (db.configId && !this.tableMap[db.configId]) {
						nextTick(async () => {
							const configId = db.configId as any;
							this.tableMap[configId] = await this.apiManager.getTableList(configId);
						});
					}
				}
			}

			this.constList = useUserInfo().constList;
			const dictList = await this.apiManager.getDictTypeList();
			this.dictList = dictList.filter((item) => !item.code.endsWith('Enum'));
			this.enumList = dictList.filter((item) => item.code.endsWith('Enum'));

			// 监听场景改变
			watch(
				() => this.ruleForm.scene,
				(val: any) => {
					if (!this.enabledWatch) return;
					val = parseInt(val ?? '1000');
					const tableList = [{} as any, {} as any];
					this.ruleForm!.treeConfig = (val! % 100 == 10 ? { multiple: false } : undefined) as any;
					//this.ruleForm.configObj = undefined;
					switch (val) {
						case 1000: //单表
							this.ruleForm.tableList = [tableList[0]];
							break;
						case 1010: //单表树组件
							this.ruleForm.tableList = [tableList[0]];
							break;
						case 2000: //主从表
							this.ruleForm.tableList = tableList;
							this.ruleForm.isHorizontal = false;
							break;
						case 2010: //主从表树组件
							this.ruleForm.tableList = tableList;
							this.ruleForm.isHorizontal = false;
							break;
						case 3000: //关系对照
							//this.ruleForm.configObj = {};
							this.ruleForm.tableList = tableList;
							this.ruleForm.isHorizontal = false;
							break;
						case 3010: //关系对照树组件
							//this.ruleForm.configObj = {};
							this.ruleForm.tableList = tableList;
							this.ruleForm.isHorizontal = false;
							break;
						default:
							this.ruleForm.tableList = [];
							this.ruleForm.treeConfig = undefined;
					}
				}
			);
		},
		showTree() {
			return this.ruleForm.treeConfig && this.ruleForm.scene! % 100 === 10;
		},
		showRela() {
			//return this.ruleForm.configObj && Math.round(this.ruleForm.scene! / 1000) === 3;
			return Math.round(this.ruleForm.scene! / 1000) === 3;
		},
		showMaster() {
			return (this.showRela() || Math.round(this.ruleForm.scene! / 1000) <= 2) && this.ruleForm.tableList?.[0];
		},
		showSlave() {
			return (this.showRela() || Math.round(this.ruleForm.scene! / 1000) == 2) && this.ruleForm.tableList?.[1];
		},
		getLastLinkLabel(index: number) {
			if (index == 0) {
				return this.ruleForm.scene! % 1000 === 10 ? '树联表字段' : undefined;
			} else if (index == 1) {
				return [2000, 2010, 3000, 3010].includes(this.ruleForm.scene!) ? '主表联表字段' : undefined;
			}
		},
		getNextLinkLabel(index: number) {
			return index == 0 && [2000, 2010, 3000, 3010].includes(this.ruleForm.scene!) ? '从表联表字段' : undefined;
		},
		getSyncColumnListButtonDisabled(index: number) {
			const data = this.ruleForm.tableList?.[index];
			return !(data?.configId && data?.tableName);
		},
		async getDetail(id: number) {
			this.loading = true;
			try {
				const res = await this.apiManager.detail(id);
				res?.tableList?.forEach((item: any) => {
					item.columnList?.forEach((item2: any) => {
						try {
							item2.config = JSON.parse(item2.config);
						} catch (e) {}
					});
				});
				return res;
			} finally {
				this.loading = false;
			}
		},
		async getColumnConfigList(id: number) {
			this.loading = true;
			try {
				return (await this.apiManager.getColumnConfigList(id)) as any;
			} finally {
				this.loading = false;
			}
		},
		async getDefaultColumnConfigList(index: number) {
			this.loading = true;
			try {
				const data = this.ruleForm.tableList?.[index];
				if (data?.configId && data?.tableName) {
					data!.columnList = (await this.apiManager.getDefaultColumnConfigList({
						configId: data?.configId,
						tableName: data?.tableName,
					})) as any[];
					data!.columnList?.forEach((item: any) => {
						if (item.config) item.config = JSON.parse(item.config);
						item.config ??= {};
					});
				}
				return data?.columnList ?? [];
			} finally {
				this.loading = false;
			}
		},
		async save(data: any) {
			this.loading = true;
			try {
				for (const item of data.tableList) {
					item.columnList.forEach((column: any) => {
						try {
							column.config = JSON.stringify(column.config);
						} catch (e) {}
					});
				}
				return data?.id ? await this.apiManager.update(data) : await this.apiManager.add(data);
			} finally {
				this.loading = false;
			}
		},
	},
});

<template>
	<div>
		<el-card shadow="hover" v-loading="state.isLoading">
			<el-descriptions title="系统信息配置" :column="2" :border="true">
				<template #title>
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Setting /> </el-icon> 系统信息配置
				</template>
				<el-descriptions-item label="系统图标">
					<template #label>
						<el-icon><ele-School /></el-icon> 系统图标
					</template>
					<el-upload ref="uploadRef" class="avatar-uploader" :showFileList="false" :autoUpload="false" accept=".jpg,.png,.svg" action :limit="1" :onChange="handleUploadLogoChange">
						<img v-if="state.sysInfo.logo" :src="state.sysInfo.logo" class="avatar" />
						<SvgIcon v-else class="avatar-uploader-icon" name="ele-Plus" :size="28" />
					</el-upload>
				</el-descriptions-item>
				<el-descriptions-item label="租户标识">
					<template #label>
						<el-icon><ele-User /></el-icon> 租户标识
					</template>
					{{ state.sysInfo.tenantId }}
					<p>
						<el-tag style="border: 1 solid var(--el-border-color)">访问地址：{{ host }}/#/login?tid={{ state.sysInfo.tenantId }}</el-tag>
					</p>
				</el-descriptions-item>
				<el-descriptions-item label="系统主标题">
					<template #label>
						<el-icon><ele-Star /></el-icon> 系统主标题
					</template>
					<el-input v-model="state.sysInfo.title" />
				</el-descriptions-item>
				<el-descriptions-item label="系统副标题">
					<template #label>
						<el-icon><ele-Star /></el-icon> 系统副标题
					</template>
					<el-input v-model="state.sysInfo.viceTitle" />
				</el-descriptions-item>
				<el-descriptions-item label="系统描述" :span="2">
					<template #label>
						<el-icon><ele-Guide /></el-icon> 系统描述
					</template>
					<el-input v-model="state.sysInfo.viceDesc" type="textarea" />
				</el-descriptions-item>
				<el-descriptions-item label="水印内容">
					<template #label>
						<el-icon><ele-Football /></el-icon> 水印内容
					</template>
					<el-input v-model="state.sysInfo.watermark" placeholder="若此处留空，则不显示水印" />
				</el-descriptions-item>
				<el-descriptions-item label="版权说明">
					<template #label>
						<el-icon><ele-Stamp /></el-icon> 版权说明
					</template>
					<el-input v-model="state.sysInfo.copyright" />
				</el-descriptions-item>
				<el-descriptions-item label="版本号">
					<template #label>
						<el-icon><ele-Clock /></el-icon> 版本号
					</template>
					<el-input v-model="state.sysInfo.version" />
				</el-descriptions-item>
				<el-descriptions-item label="主题颜色">
					<template #label>
						<el-icon><ele-Grid /></el-icon> 主题颜色
					</template>
					<el-input v-model="state.sysInfo.themeColor">
						<template #prepend>
							<div :style="`background-color: ${state.sysInfo.themeColor}; color: white; width: 100px; height: 100%; margin: -20px; text-align: center;`">{{ state.colorName }}</div>
							<!-- <div style="background-color: red; color: white; width: 100px; height: 100%; margin: -20px; text-align: center">{{ state.colorName }}</div> -->
						</template>
						<template #append>
							<el-button type="primary" icon="ele-Grid" @click="state.dialogChineseColorVisible = true" style="display: flex">主题颜色</el-button>
						</template>
					</el-input>
				</el-descriptions-item>
				<el-descriptions-item label="布局模式">
					<template #label>
						<el-icon><ele-Menu /></el-icon> 布局模式
					</template>
					<el-select v-model="state.sysInfo.layout" placeholder="布局模式">
						<el-option label="默认布局" value="defaults" />
						<el-option label="经典布局" value="classic" />
						<el-option label="横向布局" value="transverse" />
						<el-option label="分栏布局" value="columns" />
					</el-select>
				</el-descriptions-item>
				<el-descriptions-item label="页面动画">
					<template #label>
						<el-icon><ele-Position /></el-icon> 页面动画
					</template>
					<el-input v-model="state.sysInfo.animation">
						<template #append>
							<el-button type="primary" icon="ele-Promotion" @click="handTransfromWindow" style="display: flex">页面动画</el-button>
						</template>
					</el-input>
				</el-descriptions-item>
				<el-descriptions-item label="ICP备案号">
					<template #label>
						<el-icon><ele-Share /></el-icon> ICP备案号
					</template>
					<el-input v-model="state.sysInfo.icp" />
				</el-descriptions-item>
				<el-descriptions-item label="ICP地址">
					<template #label>
						<el-icon><ele-Link /></el-icon> ICP地址
					</template>
					<el-input v-model="state.sysInfo.icpUrl" />
				</el-descriptions-item>
				<el-descriptions-item label="图形验证码">
					<template #label>
						<el-icon><ele-Unlock /></el-icon> 图形验证码
					</template>
					<el-radio-group v-model="state.sysInfo.captcha">
						<el-radio :value="true">启用</el-radio>
						<el-radio :value="false">禁用</el-radio>
					</el-radio-group>
				</el-descriptions-item>
				<el-descriptions-item label="登录二次验证">
					<template #label>
						<el-icon><ele-Unlock /></el-icon> 登录二次验证
					</template>
					<el-radio-group v-model="state.sysInfo.secondVer">
						<el-radio :value="true">启用</el-radio>
						<el-radio :value="false">禁用</el-radio>
					</el-radio-group>
				</el-descriptions-item>
				<el-descriptions-item label="首页轮播图" :rowspan="5">
					<template #label>
						<el-icon><ele-Picture /></el-icon> 首页轮播图
					</template>
					<el-upload
						:file-list="state.carouselFileList"
						list-type="picture-card"
						:autoUpload="false"
						accept=".jpg,.png,.svg"
						:onChange="handleCarouselChange"
						:on-preview="handleCarouselPreview"
						:before-remove="handleCarouselRemove"
					>
						<el-icon><ele-Plus /></el-icon>
					</el-upload>
				</el-descriptions-item>

				<template #extra>
					<el-button type="primary" icon="ele-SuccessFilled" @click="saveSysInfo">保存配置</el-button>
				</template>
			</el-descriptions>
		</el-card>

		<el-dialog v-model="state.dialogChineseColorVisible">
			<template #header>
				<div style="color: #fff">
					<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-MagicStick /> </el-icon>
					<span> 中国传统颜色 </span>
				</div>
			</template>
			<div style="height: 70vh; overflow-y: scroll; overflow-x: hidden">
				<el-row :gutter="20">
					<el-col :xs="6" :sm="4" :md="4" :lg="4" :xl="3" class="mb15" v-for="i in chineseColors" :key="i" @click="setThemeColor(i)">
						<div class="traditionalColors" :style="'background:' + i.hex">
							<el-icon class="traditionalColors-circleCheck" v-if="state.sysInfo.themeColor == i.hex" size="20" color="#fff"><ele-CircleCheck /></el-icon>
						</div>
						<div class="traditionalColors-chines mt10" style="cursor: grab">{{ i.name }}</div>
						<div class="traditionalColors-chines" style="cursor: grab; color: gray">{{ i.hex }}</div>
					</el-col>
				</el-row>
			</div>
		</el-dialog>

		<transform ref="transformRef" @getAnimation="getAnimation" />
	</div>
</template>

<!-- 系统设置 -->
<script setup lang="ts" name="sysInfoSetting">
import { nextTick, onMounted, reactive, ref } from 'vue';
import { ElMessage, UploadFile, UploadFiles, UploadInstance } from 'element-plus';
import { loadSysInfo } from '/@/utils/sysInfo';

import chineseColors from '/@/layout/navBars/topBar/colors.json';
import transform from './component/transform.vue';

import { getAPI } from '/@/utils/axios-utils';
import { SysFileApi, SysTenantApi } from '/@/api-services/system/api';

const transformRef = ref<InstanceType<typeof transform>>();
const host = window.location.host;
const uploadRef = ref<UploadInstance>();
const state = reactive({
	isLoading: false,
	sysInfo: {} as any, // 系统信息数据
	logoFile: undefined as any, // logo上传文件
	dialogChineseColorVisible: false, // 中国传统颜色弹窗
	colorName: '飞燕草蓝', // 主题颜色名称
	carouselFileList: [] as any, // 轮播图文件列表
});

// 页面初始化
onMounted(async () => {
	state.isLoading = true;
	state.sysInfo = await getAPI(SysTenantApi)
		.apiSysTenantSysInfoTenantIdGet(0)
		.then((res) => res.data.result ?? []);
	if (state.sysInfo.carouselFiles) state.carouselFileList = state.sysInfo.carouselFiles;
	state.isLoading = false;
});

// 打开选择动画
const handTransfromWindow = () => {
	transformRef.value?.openDialog({ name: state.sysInfo.animation });
};

// 动画返回
const getAnimation = (e: any) => {
	if (e.name) state.sysInfo.animation = e.value;
};

// 保存
const saveSysInfo = async () => {
	try {
		state.isLoading = true;
		if (state.logoFile != undefined && state.logoFile.raw != undefined) {
			var res = await getAPI(SysFileApi).apiSysFileUploadLogoTenantIdPostForm(state.sysInfo.tenantId, state.logoFile.raw);
			state.sysInfo.logo = res.data.result?.url;
		}
		if (state.carouselFileList != undefined && state.carouselFileList.length > 0) {
			// 已有的轮播图
			state.sysInfo.carouselFileIds = state.carouselFileList.filter((file: any) => file.raw == undefined && file.id != undefined).map((file: any) => file.id);

			// 新增的轮播图
			var carouselFiles = state.carouselFileList.filter((file: any) => file.raw != undefined).map((file: any) => file.raw);
			if (carouselFiles.length > 0) {
				var sysFileIds = await getAPI(SysFileApi)
					.apiSysFileUploadCarouselTenantIdPostForm(state.sysInfo.tenantId, carouselFiles)
					.then((res) => res.data.result?.map((file: any) => file.id) ?? []);
				state.sysInfo.carouselFileIds.push(...sysFileIds);
			}
		}
		await getAPI(SysTenantApi).apiSysTenantSaveSysInfoPost(state.sysInfo);
		ElMessage.success('保存成功');

		// 置空上传文件
		state.logoFile = undefined;

		// 更新系统信息
		await loadSysInfo(state.sysInfo.tenantId);
	} finally {
		nextTick(() => {
			state.isLoading = false;
		});
	}
};

// 上传系统logo
const handleUploadLogoChange = (file: any) => {
	uploadRef.value!.clearFiles();
	state.logoFile = file;
	state.sysInfo.logo = URL.createObjectURL(state.logoFile.raw); // 显示预览logo
};

// 上传轮播图文件
const handleCarouselChange = async (file: UploadFile, files: UploadFiles) => {
	state.carouselFileList = files;
};

// 删除轮播图文件
const handleCarouselRemove = (file: UploadFile) => {
	let index = state.carouselFileList.indexOf(file);
	state.carouselFileList.splice(index, 1);
};

// 预览轮播图
const handleCarouselPreview = (file: UploadFile) => {
	window.open(file.url);
};

// 选择主题色
const setThemeColor = (e: any) => {
	navigator.clipboard.writeText(e.hex);
	state.sysInfo.themeColor = e.hex;
	state.colorName = e.name;
	state.dialogChineseColorVisible = false;
};
</script>

<style lang="scss" scoped>
.avatar-uploader .avatar {
	width: 100px;
	height: 100px;
	display: block;
	object-fit: contain;
}

:deep(.avatar-uploader) .el-upload {
	border: 1px dashed var(--el-border-color);
	cursor: pointer;
	position: relative;
	overflow: hidden;
	transition: var(--el-transition-duration-fast);
}

:deep(.avatar-uploader) .el-upload:hover {
	border-color: var(--el-color-primary);
}

.el-icon.avatar-uploader-icon {
	color: #8c939d;
	width: 100px;
	height: 100px;
	text-align: center;
}

.traditionalColors {
	height: 50px;
	position: relative;
}
.traditionalColors-chines {
	text-align: center;
}
.traditionalColors-circleCheck {
	position: absolute;
	left: calc(50% - 10px);
	top: calc(50% - 10px);
}

.fade:hover {
	animation: fadeIn;
	animation-duration: 0.3s;
}
</style>

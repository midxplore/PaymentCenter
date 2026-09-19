<template>
	<el-card shadow="hover" :header="$t('message.list.version')">
		<template #header>
			<el-icon style="display: inline; vertical-align: middle"> <ele-InfoFilled /> </el-icon>
			<span> {{ $t('message.list.version') }} </span>
		</template>
		<div style="height: 210px; text-align: center">
			<img :src="logo" style="height: 110px; margin-top: 30px" />
			<h2 style="margin-top: 15px">{{ themeConfig.globalTitle }}</h2>
			<p style="margin-top: 5px">{{ $t('message.list.versionDescription') }} {{ themeConfig.version ?? '—' }}</p>
		</div>
	</el-card>
</template>

<script lang="ts">
export default {
	title: 'Version Information',
	icon: 'ele-InfoFilled',
	description: '当前版本',
};
</script>

<script setup lang="ts" name="version">
// ★ 版本号与系统名都取自后端下发的租户信息（themeConfig.version / globalTitle），
//   不在这里写死 —— 之前硬编码了版本号与上游框架名，改品牌时必然漏掉这一处。
import { storeToRefs } from 'pinia';
import { useThemeConfig } from '/@/stores/themeConfig';
import logo from '/@/assets/logo.svg';

const storesThemeConfig = useThemeConfig();
const { themeConfig } = storeToRefs(storesThemeConfig);
</script>

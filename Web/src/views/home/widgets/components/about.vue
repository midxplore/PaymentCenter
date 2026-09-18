<template>
	<el-card shadow="hover" :header="$t('message.list.aboutproject')" class="item-background">
		<template #header>
			<el-icon style="display: inline; vertical-align: middle"> <ele-QuestionFilled /> </el-icon>
			<span> {{ $t('message.list.aboutproject') }} </span>
		</template>
		<p>
			{{ $t('message.list.aboutprojectDescription') }}
		</p>
		<p>
			<a href="https://gitee.com/zuohuaijun/Admin.NET.git" target="_blank">
				<img src="https://gitee.com/zuohuaijun/Admin.NET/badge/star.svg?theme=dark" alt="star" style="vertical-align: middle" />
				<img src="https://gitee.com/zuohuaijun/Admin.NET/badge/fork.svg?theme=dark" alt="fork" style="vertical-align: middle" />
				<img src="https://img.shields.io/badge/license-MIT-yellow" alt="license" style="vertical-align: middle" />
			</a>
		</p>
	</el-card>
</template>

<script lang="ts">
export default {
	title: 'About Project',
	icon: 'ele-QuestionFilled',
	description: '点个星星支持一下',
};
</script>
<script setup lang="ts" name="about"></script>

<style scoped>
.item-background p {
	color: #999;
	margin-top: 10px;
	line-height: 1.8;
	/* height: 100px; */
}
</style>

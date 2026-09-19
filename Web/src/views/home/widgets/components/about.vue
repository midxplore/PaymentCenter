<template>
	<el-card shadow="hover" :header="$t('message.list.aboutproject')" class="item-background">
		<template #header>
			<el-icon style="display: inline; vertical-align: middle"> <ele-QuestionFilled /> </el-icon>
			<span> {{ $t('message.list.aboutproject') }} </span>
		</template>
		<p>
			{{ $t('message.list.aboutprojectDescription') }}
		</p>
	</el-card>
</template>

<script lang="ts">
export default {
	title: 'About Project',
	icon: 'ele-QuestionFilled',
	description: '系统用途与额度规则',
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

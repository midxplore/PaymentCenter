<template>
	<div class="el-card box">
		<div class="card mb10">
			<h4 class="title">
				<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Postcard /> </el-icon>简介(About)
			</h4>
			<span class="text">
				PulsePay · 收款账号分配平台。后台维护收款账号/收款码与额度；下游按「商家订单号 + 收款类型 + 金额」调用
				<code>POST /api/pay/allocate</code> 获取收款信息并生成订单；到账由平台确认后经 Webhook 通知下游，形成可审计闭环。
			</span>
		</div>

		<div class="card mb10">
			<h4 class="title">
				<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-Warning /> </el-icon>项目信息(Information)
			</h4>
			<el-descriptions :column="2" border>
				<el-descriptions-item label="名称">
					<el-tag>{{ name }}</el-tag> <el-tag type="info">{{ author }}</el-tag>
				</el-descriptions-item>

				<el-descriptions-item label="系统说明">
					<el-tag>{{ description }}</el-tag>
				</el-descriptions-item>

				<el-descriptions-item label="版本号">
					<el-tag>{{ version }}</el-tag> <el-tag type="success">{{ license }}</el-tag>
				</el-descriptions-item>

				<el-descriptions-item label="发布时间">
					<el-tag>{{ lastBuildTime }}</el-tag>
				</el-descriptions-item>
			</el-descriptions>
		</div>
		<div class="card mb10">
			<h4 class="title">
				<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-SetUp /> </el-icon>生产环境依赖(Dependencies)
			</h4>
			<el-descriptions :column="3" border>
				<el-descriptions-item v-for="(value, key) in dependencies" :key="key" width="400px" :label="key">
					<el-tag type="success" effect="plain">
						{{ value }}
					</el-tag>
				</el-descriptions-item>
			</el-descriptions>
		</div>
		<div class="card">
			<h4 class="title">
				<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-SetUp /> </el-icon>开发环境依赖(devDependencies)
			</h4>
			<el-descriptions :column="3" border>
				<el-descriptions-item v-for="(value, key) in devDependencies" :key="key" width="400px" :label="key">
					<el-tag type="danger" effect="plain">
						{{ value }}
					</el-tag>
				</el-descriptions-item>
			</el-descriptions>
		</div>
		<div class="card">
			<h4 class="title">
				<el-icon size="16" style="margin-right: 3px; display: inline; vertical-align: middle"> <ele-SetUp /> </el-icon>关键词(Keywords)
			</h4>
			<el-descriptions :column="4" border>
				<el-descriptions-item v-for="(value, key) in keywords" :key="value" width="400px" :label="key + 1">
					<el-text type="primary">
						{{ value }}
					</el-text>
				</el-descriptions-item>
			</el-descriptions>
		</div>
	</div>
</template>

<script setup lang="ts" name="about">
// ★ 原写法是 '/package.json'（Vite 会按**项目根**解析，运行时没问题），
//   但 TS 把以 / 开头的说明符当**文件系统绝对路径**，开了 resolveJsonModule 后报 TS2307。
//   改成相对路径后 Vite 与 TS 解析到同一个文件（src/views/about/ → Web/package.json）。
import PackageJson from '../../../package.json';

const { dependencies, devDependencies, keywords, version, lastBuildTime, author, description, license, name } = PackageJson;
</script>

<style lang="scss" scoped>
.box {
	overflow-y: auto;
}
el-descriptions-item {
	width: 50%;
}
.card {
	padding: 10px;
	.title {
		margin: 5px 5px 10px;
		font-size: 17px;
		font-weight: bold;
		color: var(--el-text-color-primary);
	}
	.text {
		text-indent: 50px;
		font-size: 15px;
		line-height: 30px;
		padding: 10px 20px;
		color: var(--el-text-color-regular);
		.el-link {
			font-size: 15px;
		}
	}
}
</style>

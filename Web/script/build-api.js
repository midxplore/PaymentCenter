import {spawn} from 'child_process';
import { resolve } from 'path';
import { fileURLToPath } from 'url';
import os from 'os';

// 获取当前文件的目录（兼容 ES 模块）
const __filename = fileURLToPath(import.meta.url);
const apiBuildDir = resolve(resolve(__filename, '..'), '..', 'api_build');

const isWin = os.platform() === 'win32';
const moduleName = process.argv.slice(2)?.[0] ?? '';
const shell = isWin ? 'cmd.exe' : '/bin/bash';
const script = isWin ? `build${moduleName}.bat` : `./build${moduleName}.sh`;

// 构建完整命令
const child = spawn(shell, [script], {
    cwd: apiBuildDir,
    stdio: 'pipe', // 捕获输出流
    windowsVerbatimArguments: isWin, // Windows 下正确处理引号和空格
});

// 实时输出 stdout
child.stdout.on('data', (data) => {
    console.log(data.toString().trimEnd());
});

// 实时输出 stderr
child.stderr.on('data', (data) => {
    console.error(data.toString().trimEnd());
});

// 监听进程结束
// ★ 必须把子进程的退出码**传播出去**（process.exitCode）。原实现只 console.error 一个
//   Error 对象，自己仍以 0 退出 —— 于是「生成失败」在 npm / CI 侧看起来是成功。
//   本项目踩过一次：后端没起 → java 连不上 Swagger → 生成 0 个文件，
//   但 npm 打印「✅ 执行成功」，若没人看中间输出就会当成生成好了。
child.on('close', (code) => {
    if (code === 0) {
        console.log(`✅ ${script} 执行成功\n`);
    } else {
        console.error(`❌ ${script} 执行失败，退出码: ${code}`);
        process.exitCode = code === null ? 1 : code;
    }
});

// 处理进程异常
child.on('error', (err) => {
    console.error(`❌ 启动进程失败: ${err.message}`);
    process.exitCode = 1;
});
import { spawn } from 'child_process';
import { resolve } from 'path';
import { fileURLToPath } from 'url';
import os from 'os';

// 根据操作系统选择执行器
const isWin = os.platform() === 'win32';
const shell = isWin ? 'powershell.exe' : '/bin/zsh';

// 获取当前文件的目录
const __filename = fileURLToPath(import.meta.url);
const buildScriptsDir = resolve(resolve(__filename, '..'), '..', '..', 'Build');

// 获取第一个参数
const target = process.argv.slice(2)?.[0]?.toLowerCase() || 'all';

/**
 * 执行单个发布脚本
 * @param {string} scriptType - 脚本类型
 * @returns {Promise<void>}
 */
function runScript(scriptType) {
    return new Promise((resol, reject) => {
        const scriptName = (scriptType === 'web' ? 'vue-release' : 'core-release') + (isWin ? '.ps1' : '.zsh');
        const command = resolve(buildScriptsDir, scriptName);

        console.log('==============================================================================');
        console.log(`执行: ${scriptName}`);
        console.log(`命令: ${shell} ${command}`);
        console.log('==============================================================================');

        const args = [];
        args.push(command);
        const child = spawn(shell, args, {
            cwd: buildScriptsDir,
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
        child.on('close', (code) => {
            if (code === 0) {
                console.log(`✅ ${scriptName} 执行成功\n`);
                resolve();
            } else {
                reject(new Error(`${scriptName} 执行失败，退出码: ${code}`));
            }
        });

        // 处理进程异常
        child.on('error', (err) => {
            reject(new Error(`启动进程失败: ${err.message}`));
        });
    });
}

/**
 * 主发布流程
 */
async function main() {
    try {
        if (target === 'all' || target === 'server') await runScript('server');
        if (target === 'all' || target === 'web') await runScript('web');
    } catch (error) {
        console.error(`\n发布流程中断: ${error.message}`);
        process.exitCode = 1;
    }
}

// 启动主流程
main();
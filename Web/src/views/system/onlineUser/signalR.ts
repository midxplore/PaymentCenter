import * as SignalR from '@microsoft/signalr';
import { ElNotification } from 'element-plus';
import { getToken } from '/@/utils/axios-utils';

// 初始化SignalR对象
const connection = new SignalR.HubConnectionBuilder()
	.configureLogging(SignalR.LogLevel.Warning)
	.withUrl(`${window.__env__.VITE_API_URL}/hubs/onlineUser?token=${getToken()}`, { transport: SignalR.HttpTransportType.WebSockets, skipNegotiation: true })
	.withAutomaticReconnect({
		nextRetryDelayInMilliseconds: () => {
			return 5000; // 每5秒重连一次
		},
	})
	.build();

// 心跳检测：若15s内没有向服务器发送任何消息，则ping一下服务器端
connection.keepAliveIntervalInMilliseconds = 15 * 1000;
// 超时时间：若30s内没有收到服务器端发过来的信息，则认为服务器端异常
connection.serverTimeoutInMilliseconds = 30 * 1000;

// 启动连接
connection.start().then(() => {
	console.log('启动连接');
});
// 断开连接
connection.onclose(async () => {
	console.log('断开连接');
});
// 重连中
connection.onreconnecting(() => {
	ElNotification({
		title: '提示',
		message: '连接已断开  >>>>>',
		type: 'error',
		position: 'bottom-right',
	});
});
// 重连成功
connection.onreconnected(() => {
	ElNotification({
		title: '提示',
		message: '连接已恢复  >>>>>',
		type: 'success',
		position: 'bottom-right',
	});
});
// 用户列表
connection.on('OnlineUserList', () => {});
// 接收消息
connection.on('ReceiveMessage', (message: any) => {
	var tmpMsg = `<div style="white-space: pre-wrap;">${message.message}<div><br/>`;
	tmpMsg += `<p style="color:#808080; font-size:10px;float:right"> ${message.sendUserName}  ${message.sendTime}<p>`;

	ElNotification({
		title: `${message.title}`,
		message: tmpMsg,
		type: message.messageType.toString().toLowerCase(),
		position: 'top-right',
		dangerouslyUseHTMLString: true,
		duration: 5000,
	});
});

export { connection as signalR };

# 是什么？

IoT Gateway 是一款基于 .NET 8 的工业边缘网关：支持多协议设备接入（Modbus TCP/RTU、西门子/三菱/欧姆龙 PLC 等）、可扩展插件协议、物模型（寄存器）配置、周期性采集与 MQTT 上云；内置 JWT 鉴权的 Web 管理台、SQLite 本地存储，以及设备/物模型/MQTT 的 JSON 备份与恢复，便于部署与迁移。适合现场网关、设备上云与私有化运维场景。




# 怎么用？

假设你将项目部署到一台 Linux 服务器（如 Ubuntu/CentOS/Debian）。

## 第一阶段：打包
 后端打包发布
   进入后端项目目录，执行 .NET 发布命令。这里我们推荐独立发布 (Self-Contained)，这样目标服务器上即使没有安装 .NET 运行时也能直接运行。

## 第二阶段：上传与部署
1. 上传文件到服务器
   使用 SCP、SFTP 或其他工具，将 IoTGateway/publish 目录上传到你的 Linux 服务器。

## 进入文件夹启动
sudo chmod +x IoTGateway

nohup ./IoTGateway > gateway.log 2>&1 &

tail -f gateway.log

tail -n 100 gateway.log

部署成功访问【设备ip:5000端口】

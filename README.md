# 假设你将项目部署到一台 Linux 服务器（如 Ubuntu/CentOS/Debian）。

## 第一阶段：打包
 后端打包发布
   进入后端项目目录，执行 .NET 发布命令。这里我们推荐独立发布 (Self-Contained)，这样目标服务器上即使没有安装 .NET 运行时也能直接运行。

code
Bash
cd IoTGateway

~~~
# 发布为 Linux 64位独立程序
# -c Release 表示使用 Release 配置
# -r linux-x64 表示目标平台是 Linux 64位
# --self-contained true 表示包含 .NET 运行时
# -o ./publish 表示输出到当前目录的 publish 文件夹
dotnet publish -c Release -r linux-x64 --self-contained true -o ./publish

~~~
执行完毕后，IoTGateway/publish 目录中就包含了完整的前后端可执行程序。

## 第二阶段：上传与部署
1. 上传文件到服务器
   使用 SCP、SFTP 或其他工具，将 IoTGateway/publish 目录上传到你的 Linux 服务器。
   假设我们将其存放在服务器的 /opt/iotgateway 目录下。

code
Bash
### 在服务器上创建目录
sudo mkdir -p /opt/iotgateway

### (在本地电脑执行) 将 publish 目录下的所有文件上传到服务器的 /opt/iotgateway
scp -r ./publish/* user@your_server_ip:/opt/iotgateway/
2. 赋予执行权限
   登录到服务器，进入部署目录，并赋予主程序执行权限：

code
Bash
cd /opt/iotgateway
sudo chmod +x IoTGateway
## 第三阶段：配置 systemd 后台运行与日志管理
在 Linux 中，使用 systemd 是管理后台服务最标准、最推荐的方式。它可以实现开机自启、崩溃自动重启，并且会自动接管程序的标准输出（Console.WriteLine）作为日志。

1. 创建 systemd 服务文件
   使用你喜欢的文本编辑器（如 nano 或 vim）创建一个新的服务配置文件：
~~~
code
Bash
sudo nano /etc/systemd/system/iotgateway.service
将以下内容粘贴进去（请根据实际情况修改 WorkingDirectory 和 ExecStart 的路径）：

code
Ini
[Unit]
Description=IoT Gateway .NET Web API and React App
~~~

## 确保网络服务启动后再启动本服务
After=network.target

[Service]
## 应用程序所在的目录
WorkingDirectory=/opt/iotgateway
## 应用程序的可执行文件路径
ExecStart=/opt/iotgateway/IoTGateway

## 总是自动重启（如果程序崩溃）
Restart=always
## 重启前的等待时间（秒）
RestartSec=10

## 运行此服务的用户（如果不需要 root 权限，可以改为普通用户，如 www-data）
# User=root

## 环境变量配置
## 强制 .NET 运行在 Production 环境
Environment=ASPNETCORE_ENVIRONMENT=Production
## 确保控制台输出不被缓冲，直接写入日志
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
## 随系统启动
WantedBy=multi-user.target
保存并退出（在 nano 中按 Ctrl+O，Enter，然后 Ctrl+X）。

2. 启动服务并设置开机自启
   重新加载 systemd 配置，启动服务，并设置为开机自启：

code
Bash
## 重新加载 systemd 守护进程
sudo systemctl daemon-reload

## 启动服务
sudo systemctl start iotgateway.service

## 设置开机自启
sudo systemctl enable iotgateway.service

## 查看服务当前状态
sudo systemctl status iotgateway.service
如果状态显示为 active (running)，说明程序已经成功在后台运行了！此时你可以通过 http://服务器IP:5000 访问你的系统。

# 第四阶段：查看与管理日志 (journalctl)
因为我们使用了 systemd，你的程序中所有的 Console.WriteLine 和 .NET 内部的日志都会被 journald 自动收集和管理。你不需要自己写日志文件轮转逻辑。

你可以使用 journalctl 命令来查看和过滤日志：

1. 查看实时滚动日志（类似 tail -f）：

code
Bash
sudo journalctl -fu iotgateway.service
2. 查看所有日志（按空格翻页，按 q 退出）：

code
Bash
sudo journalctl -u iotgateway.service
3. 查看最近 100 行日志：

code
Bash
sudo journalctl -u iotgateway.service -n 100
4. 查看特定时间段的日志：

code
Bash
# 查看今天的日志
sudo journalctl -u iotgateway.service --since today

# 查看过去 1 小时的日志
sudo journalctl -u iotgateway.service --since "1 hour ago"

# 查看指定时间范围的日志
sudo journalctl -u iotgateway.service --since "2026-03-24 10:00:00" --until "2026-03-24 12:00:00"
5. 过滤包含特定关键字的日志（例如查找 "Error" 或 "MQTT"）：

code
Bash
sudo journalctl -u iotgateway.service | grep "Error"


# 日志清理与限制（可选）
随着时间推移，日志可能会占用较多磁盘空间。你可以配置 journald 的日志大小限制。
编辑 /etc/systemd/journald.conf：

code
Bash
sudo nano /etc/systemd/journald.conf
找到或添加以下行，限制日志最大占用 500MB：

code
Ini
SystemMaxUse=500M
保存后重启 journald 服务：

code
Bash
sudo systemctl restart systemd-journald
总结
通过以上步骤，你完成了：

前后端分别编译并整合。

独立发布为无需环境依赖的 Linux 程序。

使用 systemd 实现了进程守护、崩溃重启和开机自启。

使用 journalctl 实现了专业、高效的日志管理。






## Debian13配置启动案例
~~~

Debian首先安装 ICU 库（不装置 ICU 库，程序会报错）
sudo apt update
sudo apt install -y libicu-dev


1、创建文件：
sudo nano /etc/systemd/system/iotgateway.service

2、编写内容
[Unit] 
Description=IoT Gateway Service (.NET Web API & React)
# 确保在网络服务启动之后再启动我们的程序
After=network.target [Service]
# 【进程守护】指定程序运行的工作目录和启动命令
WorkingDirectory=/opt/iotgateway ExecStart=/home/iotgataway/publish/IoTGateway
# 【崩溃重启】如果进程退出（无论是因为崩溃还是被 kill），总是自动重启
Restart=always
# 崩溃后等待 5 秒再重启，防止无限死循环重启导致 CPU 占用过高
RestartSec=10
# 【安全建议】指定运行该服务的用户。 如果你的网关不需要特殊的 root 权限（如直接操作底层硬件），建议使用普通用户。 如果需要读取串口设备，可以将用户加入 dialout 组，或者在这里直接填 root。
User=root
# 【环境变量】配置 .NET 运行环境为生产环境
Environment=ASPNETCORE_ENVIRONMENT=Production
# 强制控制台输出不缓冲，确保 systemd 能实时抓取到日志
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false 
# 【关键修复】开启全球化不变模式，免除 libicu 依赖
Environment="DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1"
[Install]
# 【开机自启】告诉 systemd 在系统进入多用户模式（即正常启动完毕）时启动此服务
WantedBy=multi-user.target


3、重新加载 systemd 守护进程：
sudo systemctl daemon-reload

4、启动服务（进程守护生效）：
sudo systemctl start iotgateway

5、设置开机自启：
sudo systemctl enable iotgateway

6、检查运行状态：
sudo systemctl status iotgateway



查看日志


~~~

~~~
另外的启动方式
nohup ./IoTGateway > gateway.log 2>&1 &

tail -f gateway.log

tail -n 100 gateway.log
~~~

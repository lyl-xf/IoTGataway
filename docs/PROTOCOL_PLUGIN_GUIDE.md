# 协议插件：开发、部署、维护操作指南

本文说明在本 IoT 网关中，**自定义协议插件**从开发到上线运维的完整流程。阅读前请确认网关版本使用 **.NET 8**，且仓库内包含 **`ProtocolSdk`** 工程。

---

## 一、先搞懂三件事

### 1. 插件是什么？

- 一个（或多个）**独立的 .NET 类库 DLL**。
- DLL 里有一个**公开的、无参构造**的类，实现了契约接口 **`IProtocolPlugin`**。
- 网关启动时扫描 **`plugins` 文件夹**，加载 DLL，反射找到 `IProtocolPlugin`，用其 **`PluginId`** 与设备表里的 **`Device.PluginId`** 对应。

### 2. 网关什么时候会调用你的代码？

- 你为某台设备选择了「插件协议」并保存后，该设备的 **`ProtocolType = Custom(6)`**，且带有 **`PluginId`**。
- 网关为这台设备创建 **`IDeviceProtocolSession`**（你的 `Open` / `Read` / `Write` / `Close`），用于：
  - 周期采集（后台 Worker）
  - MQTT 下发读写
  - Web **设备调试** 里的读/写

### 3. 和「内置协议（Modbus 等）」的区别？

- 内置协议走 **IoTClient**，逻辑写在网关主程序里。
- 自定义协议逻辑**全部在你的 DLL** 里；网关只负责：选插件、传连接参数、按物模型调 `Read`/`Write`。

---

## 二、环境准备

| 项目 | 说明 |
|------|------|
| SDK | 安装 **.NET 8 SDK**（与网关 `TargetFramework` 一致）。 |
| IDE | Visual Studio 2022 / Rider / VS Code 均可。 |
| 引用 | 插件工程**只引用** `IoTGateway.ProtocolSdk`（仓库路径：`ProtocolSdk/IoTGateway.ProtocolSdk.csproj`），**不要**引用网关主工程 `IoTGateway.csproj`，避免循环依赖与部署臃肿。 |

---

## 三、开发：从零新建一个插件项目

### 步骤 1：复制示例或新建类库

推荐在仓库内 **与示例并列** 建项目，便于统一版本：

- 示例：`samples/SampleEchoPlugin/`
- 你可新建：`samples/MyCompany.MyProtocol/`

或使用命令行（在 `IoTGateway` 目录下）：

```bash
dotnet new classlib -n MyCompany.MyProtocol -f net8.0 -o samples/MyCompany.MyProtocol
```

### 步骤 2：添加对 ProtocolSdk 的工程引用

在 `MyCompany.MyProtocol.csproj` 中增加：

```xml
<ItemGroup>
  <ProjectReference Include="..\..\ProtocolSdk\IoTGateway.ProtocolSdk.csproj" />
</ItemGroup>
```

路径按你实际放置 `samples` 与 `ProtocolSdk` 的相对位置调整。

### 步骤 3：实现 `IProtocolPlugin`

必须实现：

- **`PluginId`**：全局唯一字符串，建议 `公司或产品.协议名`，例如 `acme.pressure-v1`。  
  - 与界面里设备绑定的 **插件 ID** 一致；**不要**随意改名，否则已配置设备会找不到插件。
- **`DisplayName`**：界面展示用中文/英文名称。
- **`CreateSession(ProtocolConnectionContext context)`**：返回你实现的 **`IDeviceProtocolSession`**。

要求：

- 实现类必须是 **`public`**。
- 必须有 **公共无参构造函数**（`new MyPlugin()`），网关用 `Activator.CreateInstance` 创建插件实例。

### 步骤 4：实现 `IDeviceProtocolSession`

| 成员 | 含义 |
|------|------|
| `Open()` | 建立连接（TCP 连接、打开串口等）。成功返回 `ProtocolResult.Ok()`，失败返回 `ProtocolResult.Fail("原因")`。 |
| `Close()` | 断开连接，释放资源。 |
| `IsConnected` | 当前是否已连接可用。 |
| `Read(address, dataType)` | 按物模型里配置的 **地址字符串** 和 **数据类型** 读一个值；成功返回 `ProtocolResult<object?>.Ok(值)`。 |
| `Write(address, dataType, value)` | 写入；字符串 `value` 来自上位机/MQTT，需按 `dataType` 解析。 |
| `Dispose()` | 最终释放（如托管非托管资源）。 |

**线程说明**：网关对**同一会话**会加锁调用，但实现上仍建议避免在 `Read`/`Write` 里长时间阻塞；若需异步，可在内部同步等待并设超时。

### 步骤 5：理解 `ProtocolConnectionContext`（网关传给你的参数）

网关从 **设备表** 映射而来，字段包括：

| 字段 | 说明 |
|------|------|
| `PluginId` | 当前插件 ID。 |
| `PluginConfigJson` | 设备上填的 **「插件专用配置 JSON」**，由你的代码解析（如 `System.Text.Json`）。 |
| `Ip` / `Port` | TCP 场景常用。 |
| `PortName` / `BaudRate` / `DataBits` / `StopBits` / `Parity` | 串口场景常用。 |
| `PlcVersion` | 内置 PLC 协议用；插件一般可忽略，除非你自己约定复用该字段。 |

**注意**：界面上 **TCP 与串口字段会同时展示**（插件协议下），你只需使用你协议需要的那部分；其余可为空。

### 步骤 6：`address` 与 `ProtocolDataType` 约定

- **`address`**：来自 **物模型变量**里的「寄存器地址」等字段，**字符串**。  
  - 如何解析由你定义：**例如** `40001`、`1:100`、`holding:10` 等，与现场工程文档对齐即可。
- **`ProtocolDataType`**：与网关 `DataType` **枚举数值一致**（0–11：Bool、Int16、Int32、Float、Double、String、Coil、Discrete、Short、UShort、Long、ULong）。  
  - 读返回的 `object` 类型应能与 JSON 序列化兼容（数字、布尔、字符串等）。

### 步骤 7：编译

在插件项目目录执行：

```bash
dotnet build -c Release
```

输出 DLL 一般在：`samples/MyCompany.MyProtocol/bin/Release/net8.0/MyCompany.MyProtocol.dll`

若需拷贝到网关 `plugins` 目录，可在 `.csproj` 末尾增加（路径按项目位置改）：

```xml
<Target Name="CopyPluginToHostPlugins" AfterTargets="Build">
  <Copy SourceFiles="$(TargetPath)" DestinationFolder="$(MSBuildThisFileDirectory)..\..\plugins\" SkipUnchangedFiles="true" />
</Target>
```

（示例 `SampleEchoPlugin` 已包含类似拷贝。）

---

## 四、部署：把 DLL 放到网关能加载的位置

### 步骤 1：确认目标目录

- **开发调试**：网关从项目输出目录运行时，一般为：`IoTGateway/bin/Debug/net8.0/` 或 `Release/net8.0/`。
- **正式环境**：你实际部署的可执行文件所在目录。

插件必须放在该目录下的 **`plugins`** 子目录：

```
<网关运行目录>/
  IoTGateway.exe（或 dotnet 托管入口）
  plugins/
    MyCompany.MyProtocol.dll
    （如有依赖的其他 DLL，一并放入）
```

### 步骤 2：拷贝文件

- 至少拷贝：**你的插件主 DLL**。
- 若插件依赖 **其它 NuGet 或私有 DLL**，需一并拷贝到 `plugins`（或与主程序同目录，视加载方式而定）；**缺少依赖会导致加载失败**，网关日志里会有警告。

### 步骤 3：重启网关进程

- 插件在**启动时**扫描 `plugins`，**不重启不会加载新 DLL**。
- Windows 服务 / Linux systemd / Docker：重启对应服务或容器。

### 步骤 4：在 Web 上确认

1. 登录控制台。
2. 打开左侧 **「自定义协议」**。
3. 点击 **「刷新列表」**。
4. 表格中应出现你的 **`PluginId`** 与 **`DisplayName`**。

若始终为空：检查 DLL 是否在正确路径、是否 net8、是否有公开 `IProtocolPlugin` 实现、查看网关控制台/日志中的加载错误。

---

## 五、使用：在界面上绑定设备

1. **设备管理 → 添加设备**。
2. **协议类型** 展开 **「插件协议」**，选择你的插件（显示名会带插件 ID）。
3. 填写 **IP/端口** 或 **串口参数**（按你的协议实际需要）。
4. 若有额外参数，在 **插件专用配置 (JSON)** 中填写，并在代码里解析 `PluginConfigJson`。
5. 保存设备后，在 **物模型配置** 中为该设备添加变量（**地址**、类型、Alias 等）。
6. 配置 **MQTT**（若需上云）；后台会按轮询间隔读点并上报。

---

## 六、维护：升级、回滚与排查

### 升级新版本插件

1. **备份**：备份当前 `plugins` 目录下的旧 DLL、以及网关数据库/配置备份（界面 **配置备份** 可导出 JSON）。
2. **替换**：停止网关 → 用新 DLL 覆盖旧文件（建议保留旧文件副本）→ 启动网关。
3. **验证**：「自定义协议」刷新列表；抽测一台设备 **设备调试** 读/写。

若 **`PluginId` 不变**，一般无需改设备配置；若你**改名 PluginId**，已存设备仍指向旧 ID，需在设备编辑里**重新选择插件**或批量改库（不推荐直接改库，除非你很清楚表结构）。

### 回滚

- 停止网关，用旧 DLL 覆盖，重启即可。

### 多 DLL / 依赖管理

- 优先把插件及依赖放在 **`plugins`**；若运行时提示找不到程序集，再尝试放在网关主目录。
- 复杂依赖可考虑：**插件发布为单文件** 或 **专用文件夹 + 文档说明路径**（需与运维约定）。

### 日志与故障排查

- 网关控制台会输出插件加载失败原因（例如反射异常、缺少类型）。
- 设备读失败时，你的 `Read` 应返回 **`ProtocolResult<object?>.Fail("可读的错误信息")`**，便于在调试界面看到原因。

### 安全说明

- 插件与网关**同一进程、完全信任**，请勿加载来源不明的 DLL。
- 生产环境建议：**版本固定、校验来源、变更走审批**。

---

## 七、对照：示例插件 `sample.echo`

仓库路径：`samples/SampleEchoPlugin/`。

- **`PluginId`**：`sample.echo`
- **`Open`**：直接标记已连接（无真实硬件）。
- **`Read`**：返回 `address` + 可选的 `PluginConfigJson` 片段，用于验证链路。
- **`Write`**：总是成功。

部署后：在 **自定义协议** 能看到 `sample.echo`；**添加设备** 选该插件即可联调。

---

## 八、常见问题（FAQ）

**Q：改完插件代码，网页里看不到？**  
A：需重新编译、覆盖 DLL，并**重启网关**；再在「自定义协议」里刷新。

**Q：设备选了插件，采集没数据？**  
A：检查物模型地址格式是否与你的 `Read` 解析一致；检查 `Open` 是否成功、`IsConnected` 是否为 true；看 Worker 日志。

**Q：能否一个 DLL 里多个插件？**  
A：当前加载逻辑会扫描程序集中**所有**实现 `IProtocolPlugin` 的公共类；若多个类都实现且 `PluginId` 不同，会注册多个插件。**禁止**两个类使用相同 `PluginId`。

**Q：PluginId 能用中文吗？**  
A：技术上可用，但建议使用 **ASCII**（字母、数字、点、横线），避免路径与工具链问题。

---

## 九、相关文件索引（仓库内）

| 路径 | 说明 |
|------|------|
| `ProtocolSdk/` | 契约：`IProtocolPlugin`、`IDeviceProtocolSession`、`ProtocolConnectionContext`、`ProtocolDataType`、`ProtocolResult` |
| `samples/SampleEchoPlugin/` | 最小可运行示例 |
| `plugins/` | 网关运行时加载目录（开发时可将编译输出拷入此处） |
| `Services/ProtocolPluginHost.cs` | 插件扫描与加载逻辑 |

---

按上述步骤，即可完成 **开发 → 编译 → 部署到 plugins → 重启 → 界面验证 → 长期维护** 的闭环。若你后续固定某一种地址格式或 JSON 规范，建议在本文件或团队 Wiki 中追加一节「字段约定」，便于运维与现场对齐。

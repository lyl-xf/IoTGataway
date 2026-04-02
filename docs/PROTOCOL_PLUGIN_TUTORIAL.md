# 案例：从零新建「模拟电表」插件到部署验证

本案例带你**完整走一遍**：在仓库里新建驱动工程 → 实现接口 → 编译 → 放入 `plugins` → 启动网关 → 在网页上建设备、配物模型、做调试读/写。

案例插件已落在仓库：**`samples/TutorialMeterPlugin/`**，你也可以对照本文**自己手敲**一遍以加深理解。

| 项目 | 说明 |
|------|------|
| 插件 ID | `tutorial.meter` |
| 显示名称 | 教程案例：模拟电表 (tutorial.meter) |
| 模拟行为 | 读地址 `U`/`I` 等返回固定电压电流；写 `SETPOINT` 更新内部阈值；读 `LIMIT` 可看到该阈值 |

---

## 第一部分：准备

1. 已安装 **.NET 8 SDK**（命令行执行 `dotnet --version` 确认）。
2. 已克隆本仓库，根目录下有 **`ProtocolSdk`**、主工程 **`IoTGateway`**。
3. 已阅读 **`docs/PROTOCOL_PLUGIN_GUIDE.md`**（概念与接口说明）。

---

## 第二部分：新建插件工程（两种方式任选）

### 方式 A：直接使用仓库里的示例（推荐跟做）

仓库已包含完整工程：

```
samples/TutorialMeterPlugin/
  TutorialMeterPlugin.csproj
  MeterProtocolPlugin.cs
```

跳到 **「第三部分」** 直接编译即可。

### 方式 B：从零新建（理解每一步）

#### 步骤 1：创建类库目录

在仓库 `IoTGateway` 目录下执行：

```bash
dotnet new classlib -n TutorialMeterPlugin -o samples/TutorialMeterPlugin -f net8.0
```

删除自动生成的 `Class1.cs`。

#### 步骤 2：添加对 ProtocolSdk 的引用

在 `samples/TutorialMeterPlugin` 目录执行：

```bash
dotnet add reference ../../ProtocolSdk/IoTGateway.ProtocolSdk.csproj
```

#### 步骤 3：编辑 `TutorialMeterPlugin.csproj`

在 `<Project Sdk="Microsoft.NET.Sdk">` 内补充程序集名（可选）与**编译后复制到网关 `plugins` 目录**（便于调试）：

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <AssemblyName>TutorialMeterPlugin</AssemblyName>
  <RootNamespace>TutorialMeterPlugin</RootNamespace>
</PropertyGroup>
<ItemGroup>
  <ProjectReference Include="..\..\ProtocolSdk\IoTGateway.ProtocolSdk.csproj" />
</ItemGroup>
<Target Name="CopyPluginToHostPlugins" AfterTargets="Build">
  <Copy SourceFiles="$(TargetPath)" DestinationFolder="$(MSBuildThisFileDirectory)..\..\plugins\" SkipUnchangedFiles="true" />
</Target>
```

> 说明：`Copy` 目标会把生成的 `TutorialMeterPlugin.dll` 复制到 **`IoTGateway/plugins/`**，与主程序从同一仓库运行时目录一致。若你只在别处编译，请手动把 DLL 拷到**网关运行目录**下的 `plugins`。

#### 步骤 4：实现插件代码

将 **`MeterProtocolPlugin.cs`** 放入同目录。实现要点（与仓库中文件一致）：

1. **公开类** `MeterProtocolPlugin` 实现 `IProtocolPlugin`：
   - `PluginId` 返回固定字符串 **`tutorial.meter`**（全局唯一，勿与现场其他插件冲突）。
   - `DisplayName` 返回界面展示用名称。
   - `CreateSession` 返回 `MeterSession`。

2. **内部类** `MeterSession` 实现 `IDeviceProtocolSession`：
   - `Open()`：教程里直接标记已连接；真实项目里在此建立 TCP/串口。
   - `Read(address, dataType)`：按**地址字符串**分支（如 `U` 表示电压，`I` 表示电流），把数值装箱成 `object` 并匹配 `ProtocolDataType`。
   - `Write(address, dataType, value)`：演示对 `SETPOINT`/`SP` 写入数字，更新内部 `_setpoint`。
   - `Close()` / `Dispose()`：释放资源。

完整源码请直接打开仓库中的：

`samples/TutorialMeterPlugin/MeterProtocolPlugin.cs`

#### 步骤 5：加入解决方案（可选）

在仓库根目录：

```bash
dotnet sln IoTGateway.sln add samples/TutorialMeterPlugin/TutorialMeterPlugin.csproj
```

便于在 Visual Studio / Rider 里与主工程同开。

---

## 第三部分：编译

在 `samples/TutorialMeterPlugin` 目录：

```bash
dotnet build -c Release
```

成功标志：

- 生成 `bin/Release/net8.0/TutorialMeterPlugin.dll`（或 Debug）。
- 若启用了上面的 `Copy` 目标，检查 **`IoTGateway/plugins/TutorialMeterPlugin.dll`** 是否出现。

若未自动复制，请**手动**将 `TutorialMeterPlugin.dll` 复制到：

- 开发时：`<你的仓库>/IoTGateway/plugins/`
- 部署时：`<网关 exe 所在目录>/plugins/`

---

## 第四部分：部署与加载

1. **停止**正在运行的 IoTGateway 进程（若已在跑）。
2. 确认 **`plugins` 目录下只有需要的 DLL**（至少包含 `TutorialMeterPlugin.dll`；本示例仅依赖 `IoTGateway.ProtocolSdk`，已编译进主程序，插件 DLL 即可）。
3. **启动**网关（`dotnet run` 或运行发布后的可执行文件）。
4. 观察控制台：无异常则继续；若有「Failed to load plugin」类日志，根据提示检查路径、目标框架、依赖。

---

## 第五部分：网页端配置与验证

以下假设网关监听 **`http://localhost:5000`**（默认端口以你 `Program.cs` 为准）。

### 5.1 确认插件已加载

1. 浏览器打开网关，**登录**。
2. 左侧进入 **「自定义协议」**。
3. 点击 **「刷新列表」**。

应看到一行：

| 插件 ID | 显示名称 |
|---------|----------|
| `tutorial.meter` | 教程案例：模拟电表 (tutorial.meter) |

若为空：检查 DLL 是否在运行目录的 `plugins`、是否已重启、是否 net8。

### 5.2 新建使用该插件的设备

1. 进入 **「设备管理」** → **「添加设备」**。
2. **协议类型** → 展开 **「插件协议」** → 选择 **「教程案例：模拟电表 …」**。
3. **连接参数**：本插件不真正连网，**IP 可填 `127.0.0.1`，端口填 `0` 或任意**；串口字段可留默认。
4. **插件专用配置 (JSON)**：可填 `{"note":"tutorial"}` 或留空；读地址 **`CFG`** 时会回显该 JSON 字符串（类型选 String）。
5. **设备 ID**：例如 `9001`（勿与现有设备重复）；**名称**：如 `教程-电表`；**轮询间隔**：如 `2000` ms；勾选 **启用**。
6. **保存**。

### 5.3 配置物模型变量

进入 **「物模型配置」**，设备选择刚建的 `9001`，**添加变量**示例：

| 寄存器地址 | 数据类型 | 物模型标识 (Alias) | 读写权限 |
|------------|----------|-------------------|----------|
| `U` | Float | voltage | 只读 |
| `I` | Float | current | 只读 |
| `LIMIT` | Float | limit | 只读 |
| `SETPOINT` | Float | sp | 读写 |

保存各变量。

### 5.4 设备调试

1. 打开 **「设备调试」**。
2. 设备选 **`9001`**。
3. 选属性 **`voltage`（地址 U）**，数据类型 **Float**，点击 **读取数据**。

应看到接近 **`220.5`** 的 JSON 返回值。

4. 再选 **`sp`（地址 SETPOINT）**，写入值 **`150.5`**，点 **写入数据**。
5. 再读 **`limit`（地址 LIMIT）**，应返回 **`150.5`**（与写入一致）。

### 5.5 采集与 MQTT（可选）

配置 **MQTT** 后，后台会按轮询间隔读取物模型点位并上报；本插件无真实硬件，数值仍为上述模拟逻辑。

---

## 第六部分：维护提示

| 场景 | 建议 |
|------|------|
| 改代码后重新发布 | 重新 `dotnet build`，覆盖 `plugins` 下 DLL，**重启网关**。 |
| 改 `PluginId` | 已绑定的设备仍指向旧 ID，需在 **设备管理** 里重新选择插件或改库（不推荐直接改库）。 |
| 现场多台网关 | 每个实例的 `plugins` 目录各自维护；备份用 **配置备份** 导出 JSON。 |

---

## 第七部分：本案例中的地址约定（小结）

插件源码里约定（与物模型「寄存器地址」一致，**大小写不敏感**）：

| 地址 | 读 | 写 |
|------|----|----|
| `U` / `VOLT` | 电压约 220.5 | — |
| `I` / `AMP` | 电流约 1.25 | — |
| `P` | 功率近似 U×I | — |
| `LIMIT` | 当前内部阈值（默认 100，可被 SETPOINT 改变） | — |
| `CFG` | 回显 `PluginConfigJson`（建议变量类型 String） | — |
| `SETPOINT` / `SP` | — | 写入数字，更新内部阈值 |

真实项目里，请把「地址 ↔ 报文/寄存器」的映射写进团队文档，与 **`PROTOCOL_PLUGIN_GUIDE.md`** 一起使用。

---

## 附录：命令速查

```bash
# 仅编译教程插件
cd samples/TutorialMeterPlugin
dotnet build -c Release

# 编译整个解决方案（含主站与所有示例）
cd ../..
dotnet build IoTGateway.sln -c Release
```

完成以上步骤，即完成 **从零新建驱动 → 部署 → 网页验证** 的全流程。若某一步与现场环境不一致（端口、路径），以实际部署目录为准，仅 **`plugins` 位置**与**重启**要求不变。

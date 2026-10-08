<div align="center">

# Codex Swap Account · 中文增强版

Windows 上的 Codex 桌面账号浮层工具：切换账号、查看剩余额度、调整位置，快捷键可以不设置。

[下载中文增强版](https://github.com/babatuosi936/codex-swap-account-zh-cn/releases/latest) · [升级记录](CHANGELOG_ZH.md) · [原项目](https://github.com/ZOONGG/codex-swap-account) · [MIT 许可证](LICENSE)

</div>

![中文展开浮层与剩余额度](docs/images/zh-CN/expanded-mode.png)

## 来源与致谢

**本项目基于 [ZOONGG/codex-swap-account](https://github.com/ZOONGG/codex-swap-account) 二次开发。** 原项目的账号管理、切换事务、回滚、托盘、浮层和快捷键等基础能力来自 ZOONGG 与上游贡献者；本仓库在这些能力上增加简体中文适配、Windows 桌面兼容修复和交互优化。

这是独立维护的中文增强版本。保留上游 [MIT 许可证及版权声明](LICENSE)，原版英文说明保存在 [README_UPSTREAM.md](README_UPSTREAM.md)，俄文说明保存在 [README_RU.md](README_RU.md)。上游可参考 [v1.0.0](https://github.com/ZOONGG/codex-swap-account/tree/v1.0.0)；本仓库首个提交保存的是引入时的源码快照，后续提交记录本次升级过程。

本工具是社区项目，与 OpenAI 没有隶属或官方合作关系。此仓库为私有仓库，查看代码、图片和下载发布附件需要对应的 GitHub 访问权限。

## 这次升级了什么

| 方向 | 中文增强版的变化 |
| --- | --- |
| 简体中文 | 设置、账号管理、菜单、额度和提示支持简体中文，保留英文与俄文。 |
| 剩余额度 | 浮层直接显示服务器返回的 5 小时、每周剩余百分比。启用自动查询时，默认当前账号每 30 秒、其他账号每 60 秒刷新，可在设置中调整。 |
| 浮层布局 | 默认高度为 44 个逻辑像素；支持拖动、位置记忆和 Windows DPI 缩放。 |
| 位置预设 | 菜单右侧、底部左侧、底部居中、底部右侧及自定义位置；底部位置保留边距，并限制在窗口范围内。 |
| 快捷键 | 可以显示为“未设置”；支持单项“清除”、右键清空、Backspace 清除；**只有点击“保存”才生效**。 |
| 切换兼容 | 兼容本地验证过的 Windows Codex 桌面进程与启动入口，限制关闭范围，并让助手独立于 Codex 的重启运行。 |
| 交互修复 | 修复紧凑菜单刷新后的错位、刷新时收起、按住第三个账号拖动时主账号被滚走等问题。 |
| 窗口与外观 | 确认框按窗口与屏幕范围定位；最小化设置窗口时隐藏，重新打开时复用；操作菜单与高亮项增加圆角。 |

原项目已经提供多账号切换、共享本地工作区与聊天数据、授权备份及失败回滚等基础功能。这些功能不作为本仓库新增功能宣传。

## 界面预览

以下图片由程序的真实 WPF 控件渲染。**账号名称和额度均为演示数据**，没有使用真实邮箱、账号凭据或聊天内容。

### 展开与紧凑模式

展开模式直接展示多个账号与额度；紧凑模式适合较窄的窗口。

![展开模式](docs/images/zh-CN/expanded-mode.png)

![紧凑模式](docs/images/zh-CN/compact-mode.png)

紧凑菜单中的“刷新全部额度”会保留菜单，刷新结果更新后可以继续查看账号。

<img src="docs/images/zh-CN/profile-menu.png" alt="中文账号菜单与演示额度" width="560">

### 圆角操作菜单

添加账号、刷新额度、账号管理、设置及隐藏浮层集中在这里。

<img src="docs/images/zh-CN/action-menu.png" alt="带圆角的操作菜单" width="280">

### 位置、显示模式与缩放

拖动可以调整浮层位置，也可以在设置中选择位置预设。界面缩放只影响账号浮层。

![中文外观设置](docs/images/zh-CN/appearance.png)

### 快捷键可以不设置

点“清除”或右键清空后会显示“未设置”。录入时，框内直接提示 **Backspace 清除 · Esc 取消**。修改、清除、重置都先暂存，点击“保存”才会应用；没有保存就关闭窗口，会保留原来的快捷键。

![快捷键单项清除与保存](docs/images/zh-CN/hotkeys.png)

![录入框内的 Backspace 提示](docs/images/zh-CN/hotkey-recording.png)

### 额度与自动刷新

显示可查询到的剩余比例。未知或暂不可查询的额度显示为不可用，不能当作 0%。

![中文额度设置](docs/images/zh-CN/quota-settings.png)

额度通过定时查询更新，可能有延迟；它不是服务器主动推送的实时读数，也不代表还能发送多少条固定长度的消息。

## 下载与使用

1. 打开 [本仓库 Releases](https://github.com/babatuosi936/codex-swap-account-zh-cn/releases/latest)，下载 `CodexProfileOverlay-win-x64-portable.zip`。
2. 解压到固定文件夹，运行 `CodexProfileOverlay.exe`。便携包包含运行时，无需额外安装 .NET。
3. 打开 Codex，工具会在识别到 Codex 窗口后显示浮层；也可以通过托盘菜单显示或隐藏。
4. 在设置的“语言”页选择简体中文；中文系统默认语言也可自动使用中文。
5. 使用“添加账号”完成本人账号的登录，再通过浮层或托盘切换。
6. 如不需要快捷键，在“快捷键”页逐项清除，再点击“保存”。

运行环境：Windows 10/11、x64、Codex 桌面版。添加账号与自动额度查询需要可用的 Codex CLI。单独下载的 EXE 同样包含运行时；发布页提供 `SHA256SUMS.txt` 供校验。

切换账号会关闭并重新启动 Codex。建议在当前工作结束后切换；切换过程中会备份授权，失败时尝试恢复。Windows 可能对未签名程序显示 SmartScreen 提示，可根据来源与发布页校验值自行判断是否运行。

## 数据与配置

- 账号授权与切换备份保存在本机，不应提交或上传 `auth.json`、账号目录、日志或备份。
- 常规设置目录为 `%LOCALAPPDATA%\CodexProfileOverlay`；检测到已有 Codex 打包环境的数据目录时，会继续复用它。
- 切换流程针对授权数据，不复制整个用户目录或工作区；共享会话等行为沿用上游设计。
- 额度查询会与服务端通信。网络、登录状态、服务端响应以及 Codex CLI 兼容性会影响可用性。
- 本仓库仅包含源码、说明和演示配图，运行时生成的数据与凭据被排除。

## 构建与验证

开发环境需要 .NET 8 SDK。交互界面检查需要 Windows 桌面环境。

```powershell
dotnet build CodexProfileOverlay.sln -c Release -p:Platform=x64
dotnet test CodexProfileOverlay.sln -c Release -p:Platform=x64
.\publish.ps1 -Configuration Release
```

便携包输出到 `artifacts/`。Windows CI 会构建、运行核心测试、检查仓库文件，并生成下载附件。

快捷键保存及窗口布局检查可以单独运行：

```powershell
dotnet run --project tests/OverlayUiRegression/OverlayUiRegression.csproj -c Release -p:Platform=x64 -- --hotkeys-only
dotnet run --project tests/OverlayUiRegression/OverlayUiRegression.csproj -c Release -p:Platform=x64 -- --header-only
```

演示图可重复生成，过程使用模拟账号与额度，不读取真实授权：

```powershell
dotnet run --project tests/OverlayUiRegression/OverlayUiRegression.csproj -c Release -p:Platform=x64 -- --docs docs/images/zh-CN
```

更详细的内部说明见 [架构文档](docs/ARCHITECTURE.md) 和 [界面检查说明](tests/OverlayUiRegression/README.md)。

## 当前验证范围

本次增强版在本地 Windows 与 .NET 8 环境完成构建、核心测试及针对性 WPF 交互检查。账号切换、额度查询依赖本机 Codex 和实际登录状态，隔离界面检查不等同于真实账号切换的端到端验证。尚未声称覆盖所有 Codex 版本或显示器组合。

问题反馈请说明 Codex 版本、显示缩放、浮层模式和复现步骤；截图前请遮挡真实邮箱和账号信息。

## 许可证

沿用 [MIT License](LICENSE)。感谢 [ZOONGG](https://github.com/ZOONGG) 与原项目贡献者提供的基础实现。
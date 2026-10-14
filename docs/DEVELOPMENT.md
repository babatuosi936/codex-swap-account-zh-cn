# 开发与构建 / Development

需要 .NET 8 SDK；WPF 界面检查与文档配图生成需要 Windows 桌面环境。

## 构建、测试与打包

```powershell
dotnet build CodexProfileOverlay.sln -c Release -p:Platform=x64
dotnet test CodexProfileOverlay.sln -c Release -p:Platform=x64
.\publish.ps1 -Configuration Release
```

`publish.ps1` 生成自带运行时的 EXE、包含文档的便携 ZIP，以及 `SHA256SUMS.txt`，输出位于 `artifacts/`。不将构建输出提交到源码仓库。

2026-10-14 的公开更新仍显示 1.3.1，源码标签为 `v1.3.1-update.20261014`。原 `v1.3.1` 发布保留，便于区分两次构建。

Windows CI 会构建、运行核心测试、扫描仓库文件并生成下载附件，见 [Windows CI](../.github/workflows/windows-ci.yml)。

## 界面检查

```powershell
dotnet run --project tests/OverlayUiRegression/OverlayUiRegression.csproj -c Release -p:Platform=x64
```

也可选取单一场景，例如：

```powershell
dotnet run --project tests/OverlayUiRegression/OverlayUiRegression.csproj -c Release -p:Platform=x64 -- --isolated-accounts-only
dotnet run --project tests/OverlayUiRegression/OverlayUiRegression.csproj -c Release -p:Platform=x64 -- --tooltip-only
dotnet run --project tests/OverlayUiRegression/OverlayUiRegression.csproj -c Release -p:Platform=x64 -- --advanced-only
```

测试使用合成账号、演示额度和测试窗口，不启动控制器、不读取真实授权，也不执行账号切换。详细场景见[界面检查说明](../tests/OverlayUiRegression/README.md)。

## 生成当前版本配图

```powershell
dotnet run --project tests/OverlayUiRegression/OverlayUiRegression.csproj -c Release -p:Platform=x64 -- --docs docs/images/zh-CN
dotnet run --project tests/OverlayUiRegression/OverlayUiRegression.csproj -c Release -p:Platform=x64 -- --docs docs/images/en English
```

配图从当前程序的真实控件渲染。账号名称、套餐、额度和重置时间为合成数据，不捕获桌面或用户对话。包括账号栏、账号总览、操作菜单、悬浮球与设置页；中文配图额外包含账号操作选择框。

## 验证与数据边界

2026-10-14 的公开版 1.3.1 更新通过了 282 项核心测试、334 项合成 WPF 检查、格式核验及 Release 编译。此前独立多开的 22 项桌面启动策略检查、真实账号窗口的只读核验、合成多开探针与尚未验证的行为分别记录在[多开实现与验证范围](ISOLATED_MULTI_ACCOUNT.md)。

- 独立窗口流程不复制、导入、合并或重建对话历史，不更换默认账号授权。
- 额度查询会与服务端通信，结果受网络、登录状态和 CLI 兼容性影响。
- 账号授权、运行配置、数据库、日志和备份保存在本机，不进入源码仓库或公开下载包。
- 共享本机历史不等同于云端聊天合并或保证实时刷新。

进一步阅读：[架构](ARCHITECTURE.md)、[高级设置对应表](ADVANCED_SETTINGS.md)、[隐私说明](PRIVACY.md)、[发布检查表](RELEASE_CHECKLIST.md)。

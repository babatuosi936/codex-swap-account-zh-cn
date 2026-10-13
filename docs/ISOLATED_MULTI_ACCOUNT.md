# 账号独立多开 · 1.3.1-local

本地试用版，尚未发布到 GitHub。现有公开下载仍为 v1.2.0。第一次试用面向 Windows 商店版 Codex 26.1007.2314.0；已验证两个未登录合成账号目录同时打开独立官方窗口，真实登录身份与对话展示仍待验证。

## 使用方式

在“设置 → 常规”保持“默认打开独立窗口”开启，添加并登录账号后点击账号标签。程序会先聚焦配置匹配的已有独立窗口；未打开时显示“打开新的独立窗口”和“切换当前账户”，前者为默认操作。已有默认窗口和登录保持原样，无需 Cockpit Tools。

同一个对话在一个窗口中继续。共享历史无需复制后同步，但其他窗口何时重新显示最新消息仍由 Codex 客户端决定；目前没有承诺即时刷新或自动接管正在执行的对话。

若账号目录已有独立历史或附件，或已有账号窗口使用旧配置，准备会停止并说明原因。原有文件不会清空、覆盖或自动合并。旧 VS Code 隔离环境的写入锁目录不会自动修改。

已有账号窗口的环境预检只读取配置，不调用准备操作。账号进程运行时会阻止重命名账号目录或移除账号，以免改变正在使用的目录。

“默认打开独立窗口”只控制选择框默认操作；仍可随时选择旧的单实例授权切换流程。该流程保留原有备份、授权替换、重启和旧状态迁移行为，与新窗口流程分开。

## 目录与进程结构

`AppPaths.SharedCodexDirectory` 为默认用户目录中的 `.codex`。已保存账号位于 `.codex-profiles/<账号目录>`。新窗口使用现有账号的认证文件，准备服务不写入、复制或替换 `auth.json`。

| 内容 | 新账号窗口使用的位置 |
| --- | --- |
| 账号认证与配置 | 选定已保存账号的目录，作为 `CODEX_HOME` |
| SQLite 数据库 | 默认 `.codex`，通过子进程的 `CODEX_SQLITE_HOME` 指定 |
| 原始会话 | 账号目录的 `sessions` 链接到默认 `.codex/sessions` |
| 归档会话 | 账号目录的 `archived_sessions` 链接到默认对应目录 |
| 对话附件 | 账号目录的 `attachments` 链接到默认对应目录 |
| 原生写入锁 | 账号目录的 `thread-writer-locks` 链接到默认对应目录 |
| 桌面缓存和界面状态 | 应用数据目录中的 `desktop-instances/<账号绝对路径的稳定摘要>` |

账号窗口的启动环境为：

```text
CODEX_HOME = 已保存账号目录
CODEX_SQLITE_HOME = 默认共享 .codex 目录
CODEX_ELECTRON_USER_DATA_PATH = 该账号的独立桌面缓存目录
--user-data-dir = 同一独立桌面缓存目录
```

只为新进程设置这些值，不改变用户或机器级环境变量。认证、配置、插件及沙箱目录不链接共享。已有账号本地数据库继续保留；新窗口通过共享 SQLite 设置使用默认数据库，不导入本地副本。`CODEX_SQLITE_HOME` 的作用来自当前安装原生运行时的实际验证；这里没有把它当作官方保证长期稳定的桌面接口。

## 准备与启动

`SharedDesktopProfileService` 先核对账号名称、账号根和认证文件路径，再读取可识别的账号身份。账号根、认证文件或数据根中的重解析路径会拒绝；共享目录必须指向准确的默认目标。

所有四个目录统一预检完成后才开始准备。不存在的目录可以创建；空的普通账号目录可以替换为链接；已指向正确目标的链接直接复用。非空普通目录、错误目标链接和占用路径的文件都拒绝处理。共享数据库、原始历史和附件文件不被改写，旧的状态迁移服务不参与此流程。

`IsolatedDesktopLauncher` 查找已注册的官方 Windows 商店包，通过包内辅助启动过程传入明确的目录环境，启动官方 `ChatGPT.exe`。启动辅助程序使用临时回执确认执行情况；回执未确认时不盲目重复启动，由控制器检查实际窗口。

控制器核对窗口进程的账号目录、共享数据库目录、独立桌面缓存以及账号身份，确认后再聚焦。已运行的默认账号窗口可直接聚焦；非默认账号窗口必须满足这套配置。旧 Cockpit 实例或其他目录中的旧账号窗口不会被自动迁移。

## 对话写入与验证范围

原生 CLI 0.162.0-alpha.17.2 的合成测试使用两个不同 `CODEX_HOME`，共用六个 SQLite 数据库及四个链接目录。测试不使用真实认证或用户历史，也不发起模型推理。

已核实原生对话写入锁行为：第一个进程恢复并持有对话后，第二个进程恢复同一对话会被拒绝；第一个进程退出并释放锁后，第二个进程可以恢复。将写入锁目录共用能使这两个账号目录使用同一锁域。它不等同于桌面客户端已经实现只读 follower 或实时更新。

192 项核心测试、22 项启动器策略检查和 205 项 WPF 检查通过。准备服务的针对性测试覆盖认证和配置保留、已有 SQLite 与原始附件保留、链接目标准确、桌面缓存隔离、重复准备、独立历史和附件拒绝、错误链接拒绝、路径逃逸及未知身份拒绝。

实际桌面探针在 Windows 商店版 Codex 26.1007.2314.0 中，以两个没有 `auth.json` 的合成账号目录同时打开两个可见官方窗口。已核实进程 ID、账号目录和桌面缓存各自独立，三个环境目录值及 Windows 包身份正确，此前已打开窗口的进程 ID 全部保持运行。探针没有使用真实账号、历史或模型推理。

探针关闭两个测试窗口后仍留下后台进程；清理时显式停止了这两个已核实的合成进程。这个结果确认独立窗口启动与目录隔离，不确认桌面程序可以通过关闭窗口正常完整退出。

以下真实账号行为仍待端到端验证：

- 已保存账号登录后，确认各窗口实际使用正确身份。
- 点击实际已登录账号时，准确聚焦对应窗口。
- 已有图片附件显示、历史重新加载及窗口之间的刷新表现。
- 同一对话的查看行为与首个窗口退出后的接管体验。

没有承诺覆盖其他 Codex 包版本、非商店安装方式或任意多窗口客户端组合。旧 VS Code 环境的写入锁及非空独立历史不会自动修复。

## 实现与许可

账号多开、目录准备与包启动代码为本项目独立编写的 C# 实现，没有复制 Cockpit Tools 受许可限制的源码，也不需要其程序或服务。原项目的 MIT 许可证与版权信息继续保留。历史版本中对 Cockpit Tools 的兼容说明保留在旧版升级记录中，不代表当前新流程依赖它。

## English summary

1.3.0-local is a local trial targeting the official Windows Store Codex package 26.1007.2314.0. Public GitHub downloads still provide v1.2.0. Keep **Settings → General → Open accounts in separate windows** enabled to open or focus the selected account's window while preserving the default window.

Credentials/config remain account-specific; SQLite, sessions, archives, attachments, and native writer locks are shared. Electron user data is separate. Preparation refuses independent private history or attachments and incorrect links before making changes. It does not copy, merge, or reconstruct histories, and does not change saved credentials.

Validation passed 192 core tests, 22 launcher-policy checks, and 205 WPF checks. Native synthetic tests verify shared storage and refusal of a second resume while another process owns the thread writer lock. A real desktop probe launched two visible signed-out official windows, verified separate processes, homes, caches, all three environment paths and package identity, and preserved existing window processes. It used no real credentials, history or inference. Cleanup stopped only the two verified synthetic processes after window closure left background processes; graceful complete desktop exit is not proven.

Real signed-in identities, follower behavior, attachment display, refresh, and desktop ownership handoff still need verification. `CODEX_SQLITE_HOME` sharing is established empirically for this native runtime, not promised as a permanent official desktop interface. Existing VS Code lock directories are not repaired automatically. Disabling separate account windows retains the explicit legacy authorization-switch flow.

# OpenCodexHelper —— Codex助手 2.1.1 反编译源码

本目录是对 `Codex助手2.1.1-Windows.exe`（71,906,824 字节，.NET 8 单文件，WinForms，**无数字签名**）
的反编译还原结果，用于安全分析与行为披露。

> ⚠️ **这不是可用的工具，也不建议运行原程序。** 本目录只提供源码与证据。

---

## 目录内容

```
decompiled/                              ILSpy 还原的完整 C# 工程
  CodexLauncherOnline/
    LauncherForm.cs                       核心：1685 行，全部捆绑 / 注入 / 持久化逻辑
    Program.cs                            入口、命令行分支、微信门槛调用
    WechatFollowGateForm.cs               公众号"关注"门槛页（纯客户端假校验）
    Colors.cs / GradientButton.cs / RoundedPanel.cs
    ResponsiveFormLayout.cs
    Properties/AssemblyInfo.cs
  ApplicationConfiguration.cs
  Codex助手2.1.1-Windows.csproj           可编译项目文件
  *.ico / *.jpg                           从原程序提取的内嵌资源
Codex注入脚本.js                          从二进制中还原的 CDP 注入脚本（194 行）
分析报告.md                                捆绑机制与 API 锁定分析
安全审计报告.md                             完整审计结论（含本仓库另一目标：汉化工具）
```

## 目标文件指纹

| 项目 | 值 |
|---|---|
| 文件大小 | 71,906,824 字节（68.6 MB） |
| PE 编译时间戳 | 2026-08-20 21:04:36 |
| 格式 | Windows PE32+ (x86-64) |
| 框架 | .NET 8 单文件发布（single-file bundle） |
| 程序集 | `CodexLauncherOnline`，入口 `Codex助手2.1.1.dll` |
| UI 框架 | Windows Forms |
| 捆绑内容 | 387 个托管程序集（.NET 运行时 + 应用） |
| 数字签名 | **无** |

**反编译路线**：PE 结构分析 → 识别 .NET 单文件 bundle → 用 ILSpy 11.1（`ilspycmd`，
原生支持 single-file bundle）反编译入口程序集。

**覆盖度**：自有代码只有一个程序集（26 个类型），不存在第二个未审的应用 DLL，
因此分析结论是全量覆盖而非抽样。

## 它能做什么（摘要）

- 覆盖用户 `~/.codex/config.toml`，把 `base_url` 锁死到作者的中转站，并**删除**用户已有的
  `[model_providers.custom]` 段与 5 个顶层键；
- 覆盖 `~/.codex/auth.json`，把用户的 API 密钥换成作者签发的充值兑换码；
- 通过 CDP（`--remote-debugging-port=9229`）向 Codex 界面注入伪装成原生功能的按钮；
- 写注册表 `Run` 键实现开机自启，并常驻一个隐藏守护进程，每 3 秒重新注入；
- 一条**明文 HTTP 更新链**（`http://156.238.239.133/...`，SHA-256 校验可被服务端省略，
  下载物由隐藏 PowerShell 以 `-ExecutionPolicy Bypass` 覆盖自身并重启）。

完整证据与逐条代码位置见 [`安全审计报告.md`](安全审计报告.md) 与
[`分析报告.md`](分析报告.md)。

## 复现

```powershell
# 用 ILSpy 自带的官方 bundle 读取器直接反编译原始 exe
ilspycmd --bundle-entry "Codex助手2.1.1.dll" -p `
         -o decompiled "Codex助手2.1.1-Windows.exe"
```

工具链：[ILSpy 11.1 / ilspycmd](https://github.com/icsharpcode/ILSpy)

## 立场说明

- 本目录只做**行为披露与技术分析**。
- 反编译产物中的程序权利归原作者；本仓库不主张任何权利。
- 不提供可用于运行的破解版，也不指导如何绕过任何验证。
- 若权利人认为本目录内容不当，请通过 Issue 联系，会配合处理。

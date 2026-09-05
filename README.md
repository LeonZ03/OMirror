# OPhoneMirror

OPhoneMirror 是一个 Windows 桌面工具，通过 USB ADB 和 scrcpy 投屏并控制 Android 手机，同时提供双向文件管理与针对手机输入法的键盘模式。

## 功能

- 两台已配置 Android 设备的 USB 在线检测；主界面通过设备列表选择当前手机，并记住上次选择。
- 紧凑的 Apple 风格设备控制中心，支持自动、浅色和深色外观，并可跟随 Windows 应用主题。
- 投屏按钮会显示连接中、投屏中和停止中状态；运行后可直接从控制面板停止投屏。
- 清晰的系统中文字体、柔和卡片、矢量图标和克制的状态色，并支持完整键盘焦点操作。
- H.264、60 fps、低缓冲的键鼠控制预设。
- 控制面板中的“保持在最顶层”可在投屏运行中热切换，并会记住上次选择。
- “仅熄手机屏幕”可在投屏运行中通过当前投屏会话热切换，不重启主投屏窗口。
- 投屏会话具有启动、运行、停止和自动恢复状态；意外断线最多自动重试两次，并保留脱敏诊断日志。
- 搜狗短语模式与数字选词模式，可在投屏运行时自动重连切换。
- 电脑与手机文字剪贴板互通。
- 双栏文件管理器：浏览两侧目录，多选文件/文件夹并双向传输；传输活动按需从底部展开。
- 中文文件名、多层中文目录、目标覆盖确认和万级手机目录分批加载。
- 投屏获得焦点时，`Alt+Tab` 打开手机最近任务，`Win` 返回手机桌面；点到其他窗口后恢复 Windows 行为。
- 投屏获得焦点时不会修改 Windows 当前输入法或右下角状态；按键通过 scrcpy 原始事件或手机按键注入处理，音量、媒体键、截图键和常见系统组合不会作用于电脑。

## 环境

- Windows 10/11
- .NET Framework 4.x
- [scrcpy 4.1](https://github.com/Genymobile/scrcpy/releases/tag/v4.1) Windows 版
- 手机已开启开发者选项、USB 调试，并授权当前电脑

## 配置与构建

```powershell
# 使用已下载的 scrcpy 中的 ADB 查看设备；复制需要使用的 adb serial。
& "C:\path\to\scrcpy-win64-v4.1\adb.exe" devices -l

# 创建本机配置。devices.local.txt 已被 Git 忽略，不会上传设备序列号。
Copy-Item .\devices.example.txt .\devices.local.txt
notepad .\devices.local.txt

# 编译 EXE，并把指定的官方 scrcpy 目录复制进可运行包。
# 使用 scrcpy 4.1，并用新版 Platform Tools 覆盖其自带 ADB。
.\build.ps1 -ScrcpyDir "C:\path\to\scrcpy-win64-v4.1" -AdbDir "C:\path\to\platform-tools"

# 启动构建产物；运行时不依赖源码目录。
.\dist\OPhoneMirror\OPhoneMirror.exe

# 可选：无界面检查设备配置、ADB 和 scrcpy 是否齐全；退出码 0 表示通过。
$process = Start-Process .\dist\OPhoneMirror\OPhoneMirror.exe -ArgumentList "--self-test" -Wait -PassThru
$process.ExitCode

# 对 DeviceIndex 0（第一台设备）执行可重放的 10 分钟实机稳定性测试。
# 会保存随机种子和回放文件到 .artifacts\stability（已被 Git 忽略）。
.\tests\Run-StabilityStress.ps1 -DeviceIndex 0 -DurationMinutes 10 -Seed 20260905

# 用某次失败生成的 replay.json 精确重放。
.\tests\Run-StabilityStress.ps1 -Replay ".artifacts\stability\<session>\replay.json"

# 仅当其他手机均已断开时，才模拟 10 次 ADB 服务瞬断恢复。
.\tests\Run-StabilityStress.ps1 -DeviceIndex 0 -InjectAdbFaults -FaultCycles 10
```

设备配置每行格式如下，最多读取两行：

```text
显示名称|型号说明|ADB序列号|投屏窗口X坐标
```

## 常用操作

| 操作 | 快捷键 |
| --- | --- |
| 手机桌面 | `Win`、`Ctrl+Esc` 或 `Alt+H` |
| 手机最近任务 | `Alt+Tab` 或 `Alt+S` |
| 手机返回 | `Alt+B` 或鼠标右键 |
| 投屏焦点内返回 | `Alt+F4`（不会关闭投屏窗口） |
| 电脑内容粘贴到手机 | `Ctrl+V` |
| 搜狗短语模式中英切换 | `Shift` |
| 数字选词模式中英切换 | `Shift+Space` |

点击投屏画面后，键盘归手机；点击投屏窗口外后，键盘立即恢复为 Windows 使用。Windows 的 `Ctrl+Alt+Delete` 是系统安全序列，不能被应用拦截，是唯一例外。

文件互传默认打开手机的 `/sdcard/Download`，工具栏可快速进入 `/sdcard/DCIM`。向相册发送照片时，通常选择 `/sdcard/DCIM/Camera`。

## 隐私

真实设备配置保存在 `devices.local.txt`，运行设置保存在 `%LOCALAPPDATA%\OPhoneMirror`。仅在异常退出或压力测试失败时，会在 `%LOCALAPPDATA%\OPhoneMirror\Diagnostics` 留下最近五份脱敏日志。仓库忽略构建产物、设备标识、密钥文件和本机临时数据；提交前仍应检查暂存内容，避免上传凭据或个人文件。

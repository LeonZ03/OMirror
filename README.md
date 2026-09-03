# OPhoneMirror

OPhoneMirror 是一个 Windows 桌面工具，通过 USB ADB 和 scrcpy 投屏并控制 Android 手机，同时提供双向文件管理与针对手机输入法的键盘模式。

## 功能

- 两台已配置 Android 设备的 USB 在线检测与一键投屏。
- H.264、60 fps、低缓冲的键鼠控制预设。
- 可选择“启动时仅熄手机屏幕”，从投屏启动阶段直接关闭实体屏幕并保持电脑端画面。
- 搜狗短语模式与数字选词模式，可在投屏运行时自动重连切换。
- 电脑与手机文字剪贴板互通。
- 双栏文件管理器：浏览两侧目录，多选文件/文件夹并双向传输。
- 中文文件名、多层中文目录、目标覆盖确认和万级手机目录分批加载。
- 投屏获得焦点时，`Alt+Tab` 打开手机最近任务，`Win` 返回手机桌面；点到其他窗口后恢复 Windows 行为。

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
.\build.ps1 -ScrcpyDir "C:\path\to\scrcpy-win64-v4.1"

# 启动构建产物；运行时不依赖源码目录。
.\dist\OPhoneMirror\OPhoneMirror.exe

# 可选：无界面检查设备配置、ADB 和 scrcpy 是否齐全；退出码 0 表示通过。
$process = Start-Process .\dist\OPhoneMirror\OPhoneMirror.exe -ArgumentList "--self-test" -Wait -PassThru
$process.ExitCode
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
| 电脑内容粘贴到手机 | `Ctrl+V` |
| 搜狗短语模式中英切换 | `Shift` |
| 数字选词模式中英切换 | `Shift+Space` |
| 投屏时点亮手机实体屏幕 | `Alt+Shift+O` |
| 投屏时再次关闭手机实体屏幕 | `Alt+O` |

文件互传默认打开手机的 `/sdcard/Download`，工具栏可快速进入 `/sdcard/DCIM`。向相册发送照片时，通常选择 `/sdcard/DCIM/Camera`。

## 隐私

真实设备配置保存在 `devices.local.txt`，运行设置保存在 `%LOCALAPPDATA%\OPhoneMirror`。仓库忽略构建产物、设备标识、密钥文件和本机临时数据；提交前仍应检查暂存内容，避免上传凭据或个人文件。

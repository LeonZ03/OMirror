# scrcpy 亮屏恢复问题调查

调查日期：2026-09-09。官方仓库已克隆到本项目同级的 `../scrcpy`，检视主分支提交 `19c1261d2e2cbf2b5e6a71a8b64cc1dd3ede06ac`。本记录区分上游实现、社区反馈与本机验证，不将接口成功视为实体屏幕亮起。

## 上游相关问题

- [#6570：熄屏后快捷键无法恢复实体屏幕](https://github.com/Genymobile/scrcpy/issues/6570)，调查时仍开放。最初报告为 vivo / Android 15，日志显示 ON，但实体屏幕黑；关闭会话后可恢复。
- [同一问题的 OPPO / ColorOS 用户反馈](https://github.com/Genymobile/scrcpy/issues/6570#issuecomment-4872346644)：Find X9s Pro / Android 16 / scrcpy-server 4.0 同样无法恢复，且 `requestDisplayPower(true)` 与 `cmd display power-on` 均无效。这是其他机型用户的报告；其“系统限制”解释尚不能作为本机根因定论。
- [#5530：Android 15 新亮灭屏接口导致异常](https://github.com/Genymobile/scrcpy/issues/5530)。维护者复现后回退旧接口，修复已随 3.0.1 发布；回退提交为 `3d1f036c`。这不是可再次套用到 4.1 的新补丁。

## 源码核对

- `server/.../device/Device.java` 的 `USE_ANDROID_15_DISPLAY_POWER` 仍为 `false`，实际使用 SurfaceControl 设置物理显示电源。主分支的 Device.java、Controller.java 与 v4.1 对比无差异。
- `wrappers/SurfaceControl.java` 的 `setDisplayPowerMode()` 在反射调用未抛异常时返回 true；Controller 据此打印 `Device display turned on`。这里没有实体面板或背光反馈。因此日志、ADB 成功及系统报告 ON 均不足以证明用户看到亮屏。
- Controller 的 `keepDisplayPowerOff` 在成功处理 ON 后清除。熄屏模式中经 scrcpy 发送 POWER/WAKEUP 会安排约 200 ms 后再次熄屏；该延迟动作不能误当成厂商亮屏故障。当前 OMirror 的 ADB 唤醒不走该输入分支，不能据此认定当前故障由它导致。
- [官方设备文档](https://github.com/Genymobile/scrcpy/blob/19c1261d2e2cbf2b5e6a71a8b64cc1dd3ede06ac/doc/device.md)说明 `--keep-active` 周期性报告用户活动，避免无操作超时。它解决保持活动的问题，不保证从厂商异常黑屏状态恢复。
- 同一文档列出 Android 15 起的 `cmd display power-on 0`。可作为兼容性尝试，但 #6570 表明不能仅按 Android 版本承诺成功。

## OMirror 当前结论与后续验证

- 用户已确认：锁屏持续亮着，解锁后按偏好自动熄屏。保留锁屏检测与 `--keep-active`。
- 用户曾确认直接恢复主屏的指令使屏幕亮起，但完整开关流程尚未稳定复验。当前实验性的 Android 15+ 恢复分支不能标记为已修复。
- 保留同一投屏窗口切换；不要以重启会话作为正常亮屏实现。
- 后续以同一台手机、同一会话的可重复开关测试为准，先对照原版 scrcpy，区分上游/OEM问题与 OMirror 时序问题；避免多个控制程序或自动压力测试同时改变亮灭屏状态。
- 若继续试验 Android 显示管理接口，应分别记录调用结果与用户观察，不将一次成功推广为所有 Android 15+ 机型的保证。找到重复有效路径后再固化兼容性处理。

后续收拢：已从正式源码移除未稳定验证的 Android 15+ DisplayManager 恢复分支，实验方法与局限保留在本调查记录中。保留用户实测通过的锁屏检测、逻辑唤醒与 `--keep-active`；熄屏后的实体屏幕恢复仍未宣称解决。

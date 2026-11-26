# 显示器型号与自定义快捷键

2026-11-10：显示器列表改为读取 Windows 活动显示路径提供的型号及设备接口，不再显示驱动的通用名称。实机 CA27D0 显示为 `CA27D0 · DISPLAY2 · 2560×1440`；内屏未提供型号时明确显示“内置显示器”。设备接口身份保持一致，每屏参数仍按设备对应。

柔光开关默认快捷键改为 `Ctrl + Alt + S`，智能跟随默认 `Ctrl + Alt + A`。偏好设置 → 快捷操作中，点击输入框后按组合键，立即生效并保存；每个操作均可恢复默认。支持 Ctrl、Alt 或 Win 搭配字母、数字、功能键，可加 Shift；F12 为 Windows 保留键。

录制时暂停两组全局快捷键，避免触发实际操作。Esc、离开输入框或切换窗口结束录制并恢复注册。组合重复、占用或不支持时明确提示；替换失败不会保存新组合或释放原绑定。

本次定向验证：

- Release 编译通过，0 警告、0 错误。
- 显示器验证 7 项通过：实际 CA27D0 型号、设备身份、现有面板列表增减、顺序变化与每屏参数刷新。
- 快捷键验证 15 项通过：默认 S/A、移除旧 G 绑定、两个操作自定义、旧组合释放、冲突和保留键拒绝、消息派发、保存与重启恢复、录制时暂停、Esc 取消恢复、恢复默认。
- 测试设置与用户设置分离。列表增减调用实际重建方法模拟，未物理拔插；未回归未修改的柔光算法、音效、皮肤与玻璃效果。

结果位于 `dist/display-name-verification/` 和 `dist/shortcut-verification/checks.txt`。

实现参考 Windows 官方接口：[QueryDisplayConfig](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig)、[DISPLAYCONFIG_TARGET_DEVICE_NAME](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_target_device_name)、[RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)。

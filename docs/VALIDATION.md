# ScreenGlass 0.6 Windows 适配验证

日期：2026-11-10。仅本地构建，未上传 GitHub。

适配目标：Win10 22H2（19045）、Win10 企业版 21H2 / Enterprise LTSC 2021（19044）、Win11 22H2（22621）及更新版本，均为 x64。其他旧版 Win10、Win11 21H2 和 32 位系统不在范围内。启动检查按系统内部版本执行，不添加企业版名称识别。

## 实现与依赖

没有新增功能包。Win11 保留 DWM Acrylic 与原生圆角；Win10 使用原生 Accent Acrylic，窗口区域提供随窗口尺寸和 DPI 更新的 8 DIP 圆角。两个系统共用一个自包含 EXE。接口失败时报错，不静默关闭材质。尊重系统透明效果与高对比度设置。

Win10 Accent 策略是未正式公开的系统接口，采用 [FluentWPF 已使用的模式](https://github.com/sourcechord/FluentWPF/blob/master/FluentWPF/Utility/AcrylicHelper.cs)。为少量互操作引入整套控件库会扩大依赖，也无法消除系统接口限制，因此使用独立的 Windows10Glass 模块。没有截图模拟模糊、旧系统 fallback 或采样算法修改。[窗口区域所有权遵循微软说明](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowrgn)。

## 本机结果与边界

- 构建机器：Windows 11，10.0.26200，x64。Release 编译通过，零警告、零错误。
- 专用 --platform-test 检查允许 / 拒绝的系统版本边界。
- 在本机单独调用 Win10 Acrylic 接口成功，验证 HWND 圆角区域、尺寸变化、收起重开和主题通知处理。这不代表 Win10 实机材质已经验收。
- 当前宿主系统的完整面板、快捷框初始化与重开通过，启动保持柔光暂停。
- 发布后的自包含 EXE 运行同一组专用检查，结果见 dist/platform-verification/platform-verification.txt。
- 本轮不重复回归未修改的亮度算法、音频、皮肤，不反复计算未修改文件哈希。

**Win10 实机视觉品质、拖动性能、多屏 / 混合 DPI 尚待验收。** 原生 Acrylic 在部分 Win10 环境有已知拖动迟滞风险，不能用本机接口成功来排除。为遵守品质要求，没有加入“拖动期间关闭磨砂”的处理；如实机出现迟滞，需要继续解决后再验收。

## 两台 Win10 的验收

1. 托盘退出旧版，解压 ScreenGlass-0.6.0-win-x64.zip，运行 EXE，确认显示 0.6。
2. 系统透明效果开启时，检查主面板 / 快捷框实时磨砂、四套皮肤、圆角，不能黑底、闪烁或漏出矩形底色。
3. 拖动主面板，检查鼠标跟随与流畅度；反复收起重开快捷框；有混合 DPI 显示器时跨屏移动检查边缘。
4. 检查柔光、滚轮、智能跟随、低敏、音效与快捷键，确认开启柔光后鼠标正常穿透、画面稳定。
5. 可运行专用检查并反馈日志及异常现象。日志只含系统版本与检查结果，测试偏好写在输出目录，不修改个人偏好。

```powershell
# 在解压目录运行专用检查并等待完成
Start-Process -FilePath ./ScreenGlass.exe -ArgumentList '--platform-test --output ./platform-verification' -Wait
# 查看检查结果
Get-Content ./platform-verification/platform-verification.txt
```

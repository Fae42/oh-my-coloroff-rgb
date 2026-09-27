![banner](assets/banner.png)

[English](README.md) | [简体中文](README.zh-CN.md)

**屏幕熄灭 → 灯灭。屏幕点亮 → 灯效回来。**

Windows 托盘守护程序：把七彩虹（Colorful）主板 RGB 风扇的灯效和显示器电源状态联动。
系统关闭显示器时风扇灯自动熄灭，亮屏瞬间恢复你保存的灯效。

## TL;DR

前提：Windows 10/11、七彩虹主板、已安装 [iGC.Lite](https://www.colorful.cn)。

```powershell
git clone https://github.com/Fae42/oh-my-coloroff-rgb
cd oh-my-coloroff-rgb
powershell -ExecutionPolicy Bypass -File scripts\setup_autostart.ps1   # 注册开机自启 + 看门狗
.\bin\RgbAuto.exe                                                   # 或直接重新登录；完成。
```

不想编译就从 [Releases](../../releases) 下载编译好的 `RgbAuto.exe`。
卸载：运行 `scripts\remove_autostart.ps1`，删除文件夹即可。

## 为什么会有这个项目

RGB 风扇很炫——直到你离开座位，显示器都睡了，风扇还在空房间里狂闪。
七彩虹主板的灯控只走厂商私有通道（`iGC.Lite`），OpenRGB 并不支持。
本守护程序只读复用 iGC.Lite 自带的 LED 服务栈（不修改 `C:\Program Files\iGC.Lite` 下任何文件），
监听 Windows 的 `GUID_CONSOLE_DISPLAY_STATE` 电源通知——零轮询，空闲开销几乎为零。

## 特性

- **跟随屏幕状态**：无论是空闲超时、快捷键还是 `SC_MONITORPOWER` 触发的息屏，都能正确联动。
- **带防抖**：熄屏稳定 1.5 秒才关灯、亮屏稳定 1.0 秒才恢复，并带单向回声守卫——
  灯效写入本身会产生假的电源事件，只吞掉"不安全方向"的事件即可杜绝回环。
- **系统托盘**：黄点 = 灯亮，灰点 = 灯灭；右键菜单"恢复灯效 / 立即关灯 / 退出"
  （退出会同时停用看门狗）。
- **与 iGC.Lite 友好共处**：打开 Lite 调灯效，守护自动让位；关掉 Lite 几秒内自动接管。
- **自愈能力**：看门狗计划任务每 5 分钟巡检，守护意外死亡自动拉起；单实例互斥锁；日志超 1 MB 自动轮转。
- **多设备**：对栈枚举出的所有灯控设备推送睡眠效果（主板，以及未来可能的七彩虹显卡）。

## 日常使用

- 平时忘掉它的存在。想调灯效就打开 iGC.Lite，调完关掉即可。
- 托盘"退出" = 真的停用（看门狗同时暂停；登录任务保留——重跑 setup 脚本即可恢复，
  运行 `scripts\remove_autostart.ps1` 则完全卸载）。
- `tools\关闭显示器.cmd` / `turn-off-display.cmd`：手动熄屏小工具，不用等空闲超时就能测试联动。

## 工作原理

1. 通过 `RegisterPowerSettingNotification` 注册电源通知，隐藏的消息窗口接收
   `GUID_CONSOLE_DISPLAY_STATE` 事件（注册时 Windows 会立即回放当前状态，所以启动时总能收敛到正确状态）。
2. 防抖状态机（500ms 定时tick）：屏幕稳定熄灭后对设备推送 `SetLightingEffect(id, Sleep)`；
   稳定点亮后调用 `InitLightingEffect()` 恢复保存的灯效。
3. `iGC.Lite.Service.LED.LEDAPIService` 为手工构造（其 DI 容器是 internal 的——
   用反射填充 `ConfigManager<T>` 的非公开构造函数），与厂商自己的 UI 用法完全一致。

## 给 Agent 与贡献者

硬件实测发现、架构不变量与厂商栈注意事项见 [AGENTS.md](AGENTS.md)（英文）——
改代码前请先读。

## 局限性

- 仅支持七彩虹：复用 iGC.Lite 私有栈，厂商更新可能使其失效。
- 只覆盖显示器熄屏，不覆盖系统睡眠/休眠（睡眠后风扇可能冻结在亮着的状态——
  硬件层面没找到"睡眠时关灯"的钩子）。
- 只在一款主板型号上验证过；其他七彩虹型号的设备枚举行为可能不同。

## 从源码构建

无需 IDE：

```bat
csc -nologo -platform:x64 -target:winexe -win32icon:assets\app.ico -out:bin\RgbAuto.exe ^
  "-r:C:\Program Files\iGC.Lite\iGameAPI.Contracts.dll" "-r:C:\Program Files\iGC.Lite\iGC.Lite.Service.dll" ^
  "-r:C:\Program Files\iGC.Lite\iGameCenter.ConfigManager.dll" "-r:C:\Program Files\iGC.Lite\Castle.Core.dll" ^
  "-r:C:\Program Files\iGC.Lite\iGameCenter.Hardware.dll" "-r:C:\Program Files\iGC.Lite\iGameCenter.Contracts.dll" ^
  -r:System.dll -r:System.Windows.Forms.dll -r:System.Drawing.dll src\RgbAuto.cs
```

## 许可证

MIT — 见 [LICENSE](LICENSE)。

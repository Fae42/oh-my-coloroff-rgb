# RGB Auto Off —— 息屏关灯守护程序

## 功能

- **屏幕熄灭**（Windows 关闭显示器，超时自动或手动都行）：约 1.5 秒后自动把 RGB 风扇灯推为"睡眠"效果（灯灭）。
- **屏幕点亮**：灯自动恢复为你保存的灯效（Rainbow 等，读取自 iGC.Lite 配置）。
- 守护进程常驻后台推流，灯效始终保持流动，不会冻结。
- **系统托盘图标**：黄点 = 灯亮，灰点 = 灯灭；右键菜单可"恢复灯效 / 立即关灯 / 退出"。

## 日常使用

- 平时**不需要打开 iGC.Lite**，守护程序全权管灯。
- **想调灯效时**：打开 iGC.Lite 慢慢调 → 调完**直接退出 iGC.Lite 即可**。
  守护程序检测到 iGC.Lite 打开会自动让位（灯交给 Lite 管），
  检测到 iGC.Lite 关闭后会在几秒内**自动重新接管**，无需手动重启。

## 原理

七彩虹主板的灯控只走 iGame 私有通道。本程序复用 iGC.Lite 安装目录自带的
`iGC.Lite.Service` LED 原生栈（不修改、不替换 iGC.Lite 的任何文件），
通过 Windows 的 `GUID_CONSOLE_DISPLAY_STATE` 电源通知感知屏幕开关：

- 息屏 → `SetLightingEffect(device, Sleep 参数)`
- 亮屏 → `InitLightingEffect()`（恢复你在 Lite 里选的灯效）

屏幕状态带防抖（熄屏稳定 1.5s 才关灯、亮屏稳定 1s 才恢复、每次灯操作后 3s 不应期），
避免灯效写入本身引发的状态回环导致灯狂闪。

## 文件

| 文件 | 说明 |
|---|---|
| `RgbAuto.exe` | 守护程序本体（托盘图标，后台运行） |
| `RgbAuto.cs` | 守护程序源代码（只含运行所需核心） |
| `RgbAuto.Tests.exe` / `.cs` | 诊断/测试工具（不进守护流程，平时不用）：`listen` 只记录息屏事件；`test svc sleep 45` 单次推睡眠效果 45 秒等 |
| `rgbrun.log` | 运行日志（息屏/亮屏事件、灯操作记录） |
| `setup_autostart.ps1` | 注册开机自启（计划任务 "RGB Auto Off"，登录时静默启动） |
| `remove_autostart.ps1` | 停用：删除计划任务并结束守护进程 |

## 常用操作

- 手动启动：双击 `RgbAuto.exe`
- 停止：托盘右键 → 退出；或任务管理器结束 `RgbAuto.exe`
- 开机自启：`powershell -ExecutionPolicy Bypass -File setup_autostart.ps1`
- 停用自启：`powershell -ExecutionPolicy Bypass -File remove_autostart.ps1`

## 注意事项

- 请勿移动本文件夹；计划任务里的路径是写死的。
- 若重装 iGC.Lite 后灯不响应，确认 `C:\Program Files\iGC.Lite` 仍在，再重启守护。
- 编译命令见 `RgbAuto.cs` 文件头部注释。

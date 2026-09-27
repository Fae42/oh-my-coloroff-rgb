![banner](assets/banner.png)

简体中文 | [English](README.en.md)

**屏幕熄灭 → 灯灭。屏幕点亮 → 灯效回来。**

晚上想开着电脑挂机跑东西，机箱 RGB 灯却亮得人睡不着。这个 Windows 托盘小程序
把七彩虹主板的风扇灯效和显示器电源状态绑在一起：熄屏即灯灭，亮屏即恢复。

## 快速开始（30 秒）

前提：Windows 10/11、**七彩虹主板**、**已安装 [iGC.Lite](https://www.colorful.cn)**。

**1. 下载** [`oh-my-coloroff-rgb-v1.0-win-x64.zip`](../../releases) 并解压到任意位置。

**2. 安装**：在解压后的文件夹空白处右键 →「在终端中打开」（Windows 10：按住 Shift 右键
→「在此处打开 PowerShell 窗口」），粘贴运行：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\setup_autostart.ps1
```

完成后开机自启，程序意外退出也会被自动拉起。

**3. 启动**：双击 `bin\RgbAuto.exe`，托盘出现黄点即成功（跳过也行，下次开机自动启动）。

> 只想先看看？下载裸 `RgbAuto.exe` 直接运行，不安装、不带自启。
> 卸载：运行 `scripts\remove_autostart.ps1`，再删除文件夹。

## 特性

- **跟着屏幕走**：空闲超时、快捷键、`SC_MONITORPOWER` 导致的息屏都会触发关灯。
- **防抖**：熄屏稳定 1.5 秒才关灯，亮屏稳定 1.0 秒才恢复；写灯指令诱发的假电源事件会被挡住，
  灯不会来回闪。
- **系统托盘**：黄点 = 灯亮，灰点 = 灯灭；右键菜单"恢复灯效 / 立即关灯 / 退出"
  （退出后不会再被自动拉起）。
- **和 iGC.Lite 和平共处**：打开 Lite 调灯效时本程序自动让位，关掉后几秒内自动接管。
- **挂了能自己起来**：每 5 分钟有定时任务巡检，意外退出就重新拉起；日志超 1 MB 自动轮转。
- **多设备**：识别到的每个灯控设备都会收到睡眠灯效（主板，以及将来可能的七彩虹显卡）。

## 日常使用

- 平时当它不存在。想调灯效就打开 iGC.Lite，调完关掉即可。
- 托盘"退出"是真的退出（不会再被自动拉起；开机自启任务保留——重跑 setup 脚本即恢复；
  运行 `scripts\remove_autostart.ps1` 则完全卸载）。
- `tools\关闭显示器.cmd` / `turn-off-display.cmd`：手动熄屏小工具，不用等空闲超时就能测试联动。

## 工作原理

1. 监听 Windows 的 `GUID_CONSOLE_DISPLAY_STATE` 显示器电源事件（注册时系统会立刻回放当前状态，
   启动即拿到正确状态）。
2. 每 500ms 检查一次状态是否稳定：屏幕稳定熄灭就对每个设备推送 `SetLightingEffect(id, Sleep)`；
   稳定点亮就调用 `InitLightingEffect()` 恢复保存的灯效。
3. 灯控用的是 iGC.Lite 自带的服务组件（`LEDAPIService`，只读调用），用法和厂商自己的界面完全一致。

## 局限性

- 仅支持七彩虹：它的灯控只有厂商自家的 iGC.Lite 能调（OpenRGB 不支持），本项目复用的
  就是这个私有栈，厂商一更新可能就会失效。
- 只覆盖显示器熄屏，不管系统睡眠/休眠（睡眠后风扇可能冻在亮着的状态）。
- 只在一款主板上验证过（BATTLE-AX B760M-WHITE WIFI D5）；其他七彩虹型号可能表现不同。

## 从源码构建

<details>
<summary>无需 IDE，一条 csc 命令（点开查看）</summary>

```bat
csc -nologo -platform:x64 -target:winexe -win32icon:assets/app.ico -out:bin\RgbAuto.exe ^
  "-r:C:\Program Files\iGC.Lite\iGameAPI.Contracts.dll" "-r:C:\Program Files\iGC.Lite\iGC.Lite.Service.dll" ^
  "-r:C:\Program Files\iGC.Lite\iGameCenter.ConfigManager.dll" "-r:C:\Program Files\iGC.Lite\Castle.Core.dll" ^
  "-r:C:\Program Files\iGC.Lite\iGameCenter.Hardware.dll" "-r:C:\Program Files\iGC.Lite\iGameCenter.Contracts.dll" ^
  -r:System.dll -r:System.Windows.Forms.dll -r:System.Drawing.dll src\RgbAuto.cs
```

</details>

## 给 Agent 与贡献者

硬件实测结论、架构不变量、厂商栈的脆弱点都写在 [AGENTS.md](AGENTS.md)（英文），改代码前先读。

## 声明

这是作者为自己主板写的非商业个人项目，代码大部分由 AI 辅助完成（vibe coding），仅供学习交流。
本项目与七彩虹（Colorful）官方无关，未获得其授权或认可；"七彩虹"、"iGC.Lite" 等名称和商标
归原厂商所有，文中提及仅为说明兼容性。

程序只读调用 iGC.Lite 安装目录中的组件，不修改、不分发其任何文件。
如有内容侵犯您的权益，请开 Issue 联系，核实后第一时间处理，包括下架删除。

## 许可证

MIT — 见 [LICENSE](LICENSE)。

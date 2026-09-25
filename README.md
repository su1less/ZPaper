# TechRainWallpaper — 桌面美化套件

> **2026-09-22 更名**: 可执行文件已更名为 **ZPapaer.exe**（开机自启与开始菜单快捷方式同步更新；源文件仍为 TechRainWallpaper.cs，编译输出 -out:ZPapaer.exe）。dock 末端新增 🔍 搜索图标，点击打开应用启动器（搜索框筛选 + 已安装应用平铺，回车启动）。

一站式桌面改造程序：动态壁纸 + Mac 式 Dock + 桌面小组件，全部原生 Win32/.NET 实现，无第三方依赖，内存约 90MB。

## 功能

1. **动态壁纸**（挂载 Windows 桌面壁纸层 WorkerW）
   - 4 个主题：雨夜霓虹城市 / 极光雪夜 / 深空星云 / 黑洞吸积盘（380 粒子开普勒吸积盘 + 光子环 + 引力透镜弧 + 极向喷流）
   - 与 wttr.in 实时天气联动（雨量/风力跟随真实天气）
   - 全屏应用自动暂停渲染；每 3 分钟压缩内存
2. **Mac 式 Dock**（UpdateLayeredWindow 逐像素 Alpha 透明）
   - 弹簧放大（半隐式欧拉积分，阻尼比 0.58，放大目标用静止位置计算防反馈振荡），60fps、静止跳帧
   - 末端常驻 ⚙ 设置菜单（音量调节/网络/切主题/小组件/临时显示任务栏/暂停/退出）
   - **🔊 音量调节**：dock 样式的滑块弹窗（默认输出设备 IAudioEndpointVolume），点击轨道跳转/拖动/滚轮 ±5，左侧喇叭图标点按切换静音，失焦自动关闭
   - **🌐 网络**：子菜单显示有线连接状态（网卡+速率）；WiFi 列表按信号排序（●○ 点阵 + ✓ 已连 + ·需密码 标记）；点击直连已有配置/开放网络，加密新网络弹密码框（创建每用户 profile 后连接，无需管理员）；结果以 toast 菜单回报到原菜单位置；"刷新网络列表"触发 WlanScan 后自动重开菜单
   - 🔍 搜索启动器 + 桌面面板：亚克力半模糊背景（显示前抓屏模糊 + LiveBackdrop 跟随动态壁纸）；桌面面板按"最近使用+类型"分区
   - 应用有多个可见窗口时，点击图标在图标上方弹出窗口列表供用户选择激活（单窗口仍直接激活）
   - 全屏时 dock 自动隐藏；鼠标紧贴屏幕底边 0.5s 呼出，移开即再隐藏
   - 运行中的应用自动出现在分隔符右侧；右键可固定/移除
   - 原生任务栏运行期间彻底隐藏（SW_HIDE），退出自动恢复
3. **桌面小组件**
   - 右上：天气 + 时钟（城市/省份/湿度/三日预报）
   - 左下：WASAPI 音量律动条
   - 拒绝"显示桌面"最小化（拦截 SC_MINIMIZE + 看门狗）

## 运行与编译

- 直接运行：双击 `ZPapaer.exe`（开机自启已注册到 HKCU Run，指向本目录）
- 重新编译（改完源码后）：
  ```
  C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe -nologo -target:winexe -platform:anycpu -codepage:65001 -win32icon:app.ico -out:ZPapaer.exe -r:System.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll -r:System.Web.Extensions.dll TechRainWallpaper.cs
  ```
  （编译前必须 Stop-Process，否则 exe 被锁；app.ico 由 tools/gen_icon.cs 生成，改图标后先跑 tools/gen_icon.exe app.ico docs/icon_preview.png）
- 退出：Dock 末端 ⚙ 菜单 → 退出（恢复任务栏）

## 目录结构

```
TechRainWallpaper/
├── TechRainWallpaper.cs/.exe   主程序源码与可执行文件
├── theme.txt                   当前主题（1-4）
├── log.txt                     运行日志（[ACT] 段是每次点击图标的完整决策记录）
├── bg/                         四套主题底图（city1/aurora1/nebula1/nebula2）
├── dockApps/                   Dock 固定应用（放入 .lnk 即自动上船）
├── tools/                      诊断与测试工具（见下）
├── scripts/                    部署/验证 PowerShell 脚本
└── docs/                       项目文档与调试截图
```

## 诊断工具（tools/）

| 工具 | 用途 |
|---|---|
| inspect_windows.exe | 列出指定进程的全部窗口（标题/类名/样式/属主）——窗口选择问题第一现场工具 |
| who_owns.exe | 多点 WindowFromPoint，探测"隐形点击膜"（全屏点击都落在一个看不见的窗口上） |
| e2e_test.exe | 用户仿真端到端测试：真实点击 Dock 图标 → 检查窗口 → 点击会话行 → 像素 diff 验证 |
| dock_map.exe | 扫描 Dock 图标颜色分布，定位各图标坐标 |
| enter_wechat.exe | 自动点击微信登录页绿色"进入微信"按钮（测试辅助） |
| audprobe / netprobe | 音量 COM 互操作 / WLAN 结构体布局探针（改 AudioCtl/NetCtl 前先跑） |
| voltest | 端到端：找滑块窗口→点击轨道→读回系统音量→恢复 |
| menukey / menuclick / escmenu / zwins | 原生菜单键盘驱动、带悬停的菜单点击、ESC 收菜单、按 PID 列窗口（菜单自动化测试四件套） |
| battery / wake_test / focus_test / highlight_test / seq_test / min_restore / activate_msg_test | 焦点与渲染问题的系列实验工具（历史排障用） |

## 已知问题（重要历史，改代码前必读）

**⭐ 唤醒策略铁律（2026-09-24 用户两次强调，改激活逻辑前必读）**

> **dock 图标唤醒应用：能直连恢复的，绝不走托盘舞步！**
> 托盘路径慢（数秒级浮现）且 bug 多。只允许两类应用走托盘路径（`DockForm.TrayDoctrine`）：
> 1. 外部恢复会变"输入死寂僵尸"的 Electron/Qt 壳：**QQ / 微信 / ZCode**（实测结论）
> 2. 未来逐个实测确认"直连必然死窗"的新应用，加入时必须在代码里注明证据
>
> 其余应用（含网易云音乐、Chrome）一律直接恢复隐藏窗口（ShowWindow 路径，~1s 浮现）。
> `TrayResidents`/`IsTray`（应用常驻通知区）会把应用拉回托盘路径——网易云曾被它翻回去
> 一次，现由 `DirectRestore` 名单强制压回直连（已含网易云音乐/cloudmusic、Google Chrome/chrome）。
> **禁止再扩大托盘路径的适用范围。**

**微信/QQ"僵尸窗口"问题（部分残留）**——完整病例史：

1. ✅ 已修：登录窗误选（假分层窗/无标题宿主窗赢过主窗）→ 排序规则 + 登录标题过滤 + 无标题窗降权
2. ✅ 已修：误启动新实例弹登录窗（微信4.x/QQNT 支持多开）→ 对运行中应用不再 Process.Start
3. ✅ 已修：不渲染的可见窗口（全屏隐形点击膜）→ 激活后绘制验证，不达标自动隐藏
4. ✅ 已修：破损渲染（纯白/黑块）→ 方差检测自动隐藏
5. ✅ 已修：被忙进程挂死（AttachThreadInput/同步调用）→ 激活全部移到 ThreadPool，永不 AttachThreadInput
6. ⚠️ **残留**：微信主窗激活后可能"画了一半但输入路由未醒"（paint diff 正常、窗口可见、前台正确，但 hwndFocus 为空，单击被吞、双击才响应）。已验证无效的方案：WM_ACTIVATE/WM_SETFOCUS 投递、TAB 键、搜索框点击唤醒。有效的部分缓解：激活链中的标题栏真实点击 + 客户区真实点击（多数情况一次点击即通，少数仍需用户手点一下标题栏）。彻底解决方案的候选方向：hook 微信自身的托盘恢复回调、或用 UIA InvokePattern 调用窗口的还原逻辑。

日志是第一现场：每次点击图标的完整决策（进程列表/候选窗口打分/激活结果/paint diff）都记录在 `log.txt` 的 `[ACT]` 段。

## 关键实现坑（源码修改者必读）

- 壁纸窗口挂 WorkerW；小组件是顶级窗口沉底（TransparencyKey 对 WorkerW 子窗口无效）
- 一切窗口透明用 UpdateLayeredWindow 逐像素 Alpha（颜色键透明有紫色镶边）
- WinForms 窗口会被"显示桌面"最小化：拦截 WM_SYSCOMMAND SC_MINIMIZE + 看门狗
- 对目标窗口的任何同步调用（SetForegroundWindow/ShowWindow/SetWindowPos）都可能被忙进程挂死本线程：激活必须整段丢 ThreadPool
- 永远不要 AttachThreadInput（死锁双方）；永远不要 PostMessage 伪造鼠标消息（毒死 Qt/Chromium 输入状态机）
- 弹簧积分必须半隐式欧拉 + exp 精确阻尼 + dt 钳制；放大距离用静止位置算
- 应用扫描/图标加载/GetProcessesByName 在后台线程；列表重建不碰运行状态；变化检测用排序后的名字集合（z 序无关）
- 微信4.x 进程名 Weixin（主窗标题=用户昵称）；QQNT 主窗标题='QQ'（多个同名窗口，最大的那个）；网易云主窗标题=歌名，真主窗可能无标题
- MenuItem 不能跨菜单复用（NRE）；主题切换必须后台线程构建
- 合成输入驱动原生菜单（#32768）：PostMessage 鼠标点击/带 MOVE 前导的点击都不激活菜单项，可靠手段是键盘序 DOWN×N + ENTER（子菜单项不可靠，勿盲发 ENTER——会误触"退出"）；FindWindowW/FindWindow 必须 CharSet.Unicode（默认 ANSI 静默失败）；控制台测试工具会抢前台 → "失焦即关"的弹窗（音量滑块）会自动关闭，测试需单进程内完成
- 编译部署脚本：scripts/deploy_netvol.ps1（停进程→ShowWindow 恢复任务栏→换 exe→重启→验证）

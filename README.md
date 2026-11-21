# FTK Quick SL

[简体中文](#简体中文) | [English](#english)

A native-style **Quick Save (`S`) / Quick Load (`L`)** and **Hold-to-End-Turn** Quality-of-Life BepInEx 5 plugin for **For The King** (FTK1, Unity 2017.2.2 x64 Mono).

---

## 简体中文

### 简介

在游玩《为了吾王》（For The King 初代）时，如果战斗掷骰失利或遭遇非期望事件，传统做法只能趁尚未自动保存前手动 `Alt+F4` 强退并重新启动游戏；而在大地图跑图过程中每次都去点击右上角的紫色沙漏结束回合也比较繁琐。

**FTK Quick SL** 为游戏注入了外观与音效均贴近原生 UI 的快捷存读档系统，并附带大地图鼠标长按跳过回合功能，让整个 SL 与过回合体验顺滑自然。

### 核心功能

1. **顶部黑条原生风格 `S` / `L` 并排按钮**
   - 自动停靠在顶部信息黑条中「天数」与「学识」之间的空白区域（自动为学识书本图标留出间距，不遮挡原生元素）。
   - 直接克隆游戏原生菜单按钮样式与 `AudioManager` Wwise 悬停/点击音效。
   - 支持**右键按住任意按钮整体拖拽位置**并自动记忆坐标。
2. **只保存不退出（`S` 按钮 / `F11`）**
   - 在大地图非移动待机状态或地牢探索界面（`GameLogic.CanSave()` 为真时），一键立即写入当前本局 `.run` 存档并显示原生自动保存提示，按钮短暂显示 `✓` 反馈，不退出对局。
3. **无保存一键热重载回档（`L` 按钮 / `F10`）**
   - 触发瞬间通过 Harmony 前置锁死 `GameLogic.AutoSave`、`Save`、`SaveGame` 与 `DeleteSaveFile`，防止过渡黑屏期间竞态覆盖存档或团灭删档。
   - **跨场景无缝黑屏遮罩 + 镜头加速**：使用独立高优先级 `DontDestroyOnLoad` 黑幕遮罩全程覆盖重载过程（直到进入法汝渲染就绪后平滑淡出），并将选人镜头平移 `Animator.speed` 提至 `50x`，消除返回主界面与选人界面的闪屏与等待。
   - **安全校验**：首回合尚未生成任何存档时按钮自动置灰拦截，防止误载入其他旧局存档；线上多人模式（`GameMode.Multiplayer`）会给出提示禁止强行 SL。
4. **ESC 暂停菜单双位置原生注入**
   - 主暂停菜单（`uiOptionsMain`）：在「保存游戏」下方插入 **「快捷读档」**。
   - 保存与退出子菜单（`uiOptionsSave`）：在「直接退出」下方插入 **「快速保存（不退出）」** 与 **「重载上一自动存档」**，完整串联手柄与键盘上下导航。
5. **大地图鼠标静止长按跳过回合**
   - 在大地图非 UI 区域、非战斗且右上角沙漏（`uiEndTurnButton`）可交互时，**左键静止长按 `0.45s`** 自动结束当前角色回合。
   - 按住超过 `0.12s` 在指针下方显示轻量暗金进度条；按下期间鼠标移动超过 `14px` 自动取消，避免拖镜头或误触。

### 环境要求

- 游戏本体：**For The King**（初代，PC Steam / Mono x64 版本）
- 插件加载器：**BepInEx 5.4.x (`win_x64`)**（已内置 Harmony 2）

### 安装方法

#### 方式一：直接下载预编译 DLL（推荐）

1. 如果游戏目录尚未安装 BepInEx，请先前往 [BepInEx Releases](https://github.com/BepInEx/BepInEx/releases) 下载 `BepInEx_win_x64_5.4.x.zip` 解压至《为了吾王》游戏根目录（含 `FTK.exe` 的目录），并先启动一次游戏完成初始化。
2. 从本仓库 [Releases](https://github.com/Elosiad/ftk-quick-sl/releases) 页面下载 `FtkQuickSL.dll`。
3. 将 `FtkQuickSL.dll` 放入 `<For The King 游戏目录>\BepInEx\plugins\FtkQuickSL\FtkQuickSL.dll`。
4. 启动游戏即可使用。

#### 方式二：从源码编译并安装

仓库脚本会自动通过 Steam 注册表与 `libraryfolders.vdf` 探测游戏安装位置（Windows 上使用系统内置 `.NET Framework 4.x` 的 `csc.exe` 以 `/langversion:5` 编译链接游戏的 `.NET 3.5` 程序集，无需安装 Visual Studio）：

```powershell
git clone https://github.com/Elosiad/ftk-quick-sl.git
cd ftk-quick-sl
powershell -ExecutionPolicy Bypass -File .\scripts\install.ps1 -Rebuild
```

若你的游戏未安装在常见或 Steam 注册表路径下，可显式指定路径：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install.ps1 -Rebuild -GameDir "C:\Path\To\For The King"
```

### 操作与默认快捷键

| 操作 / 入口 | 效果 | 备注 |
| --- | --- | --- |
| 顶栏 `S` 按钮 / **`F11`** | 快速保存（只写盘不退出） | 仅大地图待机或地牢探索界面可保存 |
| 顶栏 `L` 按钮 / **`F10`** | 快捷读档（重载上一自动/快速存档） | 聊天框输入时自动屏蔽热键 |
| 右键拖拽 `S` 或 `L` | 整体移动顶栏快捷按钮位置 | 松开后自动保存坐标到配置文件 |
| 非 UI 区域左键静止长按 `0.45s` | 跳过当前大地图回合 | 移动超过 `14px` 取消；可在配置改为右键/中键 |
| `ESC` 菜单 / 保存与退出子菜单 | 原生「快捷读档」/「快速保存」入口 | 支持鼠标与键盘/手柄导航 |

### 配置文件说明

首次加载插件后会在 `<游戏目录>\BepInEx\config\com.ftk.quicksl.cfg` 生成配置文件：

- **`[General]`**
  - `ShowHudButton` (`true`)：是否显示顶部黑条 `[S] [L]` 按钮
  - `ConfirmDialog` (`false`)：点击读档前是否弹出二次确认弹窗（`false` = 点一下直接回档）
  - `AutoEnterFahrul` (`true`)：单人模式读档后是否自动进入法汝（本地多人会停留在选位界面）
  - `SeamlessReloadOverlay` (`true`)：读档过渡期间是否开启跨场景全黑加载遮罩隐藏主界面闪烁
  - `AccelerateReloadCamera` (`true`)：读档时是否将选人界面镜头平移动画加速至 50 倍
  - `SaveButtonLabel` (`S`) / `LoadButtonLabel` (`L`)：自定义 HUD 按钮文本
- **`[Hotkey]`**
  - `SaveKey` (`F11`) / `ReloadKey` (`F10`)：自定义热键，设为 `None` 可关闭
- **`[HudPosition]`**
  - `HudUseCustomPosition` (`false`)：为 `false` 时自动停靠在顶栏「天」与「学识」之间；右键拖拽后自动设为 `true`
  - `HudOffsetX` / `HudOffsetY`：自定义右上角偏移坐标
- **`[EndTurnHold]`**
  - `EnableHoldEndTurn` (`true`)：开关大地图长按跳过回合
  - `HoldMouseButton` (`0`)：`0` = 左键、`1` = 右键、`2` = 中键
  - `HoldDurationSeconds` (`0.45`)：触发所需静止长按时长（秒）
  - `MaxDragPixels` (`14`)：允许的最大指针抖动像素阈值
  - `ShowHoldProgress` (`true`)：是否显示指针下方暗金长按进度条

### 技术原理与已知限制

- **为什么必须重载场景而非“内存原地撤销一步”？**
  FTK 初代底层基于 `GameStatesSerialize` + PlayMaker FSM 构建，引擎本身不维护步级撤销栈，也不支持对已脏化的局内场景做原地反序列化（即便是游戏原生读档 `GameLogic.LoadGame` 也必须先 `SceneManager.LoadScene("FTK_main")` 重置全局对象与状态机）。配合本作提供的 **`S` 按钮**，在战斗、神龛或开箱前点一下 `S` 即可精准锚定当前格子与行动点，需要时点 `L` 无闪屏一键回到该步。
- **战斗中不能保存**：遵循游戏原生 `GameLogic.CanSave()` 机制，仅大地图待机/停格与地牢探索界面支持保存；战斗过程中可随时点 `L` 退回进战前的自动/手动存档点。
- **线上多人模式（`GameMode.Multiplayer`）**：不支持房客单方面强行 SL，点击时将弹窗提示并拦截。
- **左键长按的地面点击说明**：由于大地图左键本身绑定了行走选格，若在可通行六边形格子上长按左键，游戏底层在按下瞬间仍可能识别选格指令。如希望完全避免误走，可在配置 `[EndTurnHold]` 中将 `HoldMouseButton` 设为 `1`（右键）或 `2`（中键）。

### 卸载

运行仓库内的卸载脚本，或直接删除 `<游戏目录>\BepInEx\plugins\FtkQuickSL\` 文件夹与 `BepInEx\config\com.ftk.quicksl.cfg` 即可（不会影响 BepInEx 与其他插件）：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\uninstall.ps1
```

---

## English

### Overview

In **For The King** (FTK1), recovering from an unexpected combat outcome or misplay normally requires hitting `Alt+F4` before the next autosave, restarting the entire client, and clicking back through the main menu—and repeatedly clicking the top-right hourglass to end turns on the overworld quickly gets tedious.

**FTK Quick SL** is a BepInEx 5 plugin that adds native-styled Quick Save (`S`) / Quick Load (`L`) controls, seamless cross-scene reload masking with camera speedup, and stationary hold-to-end-turn mouse support on the overworld.

### Features

1. **Native-Style Top Bar `[S] [L]` Buttons**
   - Automatically docks between the Day counter and Lore counter on the top HUD bar without overlapping the lore book icon or quest tracker.
   - Cloned directly from FTK's native menu button prefab with native sprites, hover/press states, and `AudioManager` Wwise sound effects.
   - **Right-click drag** either `S` or `L` to reposition the bar anywhere; coordinates persist automatically.
2. **Quick Save Without Quitting (`S` / `F11`)**
   - Saves your current run in-place via `GameLogic.Save(false, false)` whenever `GameLogic.CanSave()` allows (overworld idle/stopped or dungeon exploration menu), flashes `✓` on `S`, and triggers the native autosave toast.
3. **One-Click Quick Load (`L` / `F10`)**
   - Immediately engages a Harmony guard blocking `GameLogic.AutoSave`, `Save`, `SaveGame`, and `DeleteSaveFile` so fade-out transitions or party-wipe cleanup cannot overwrite or erase your `.run` file.
   - **Seamless Loading Mask + Camera Speedup**: Uses a persistent `DontDestroyOnLoad` black overlay (`sortingOrder = 32760`) across `SceneManager.LoadScene("FTK_main")` until `uiStartGame.m_FahrulEntered` is ready, while boosting `SelectScreenCamera`'s `Animator.speed` to `50x` to eliminate menu flashes and cut reload time.
   - **Safety Guards**: Disables `L` when no `.run` file exists for the current run yet (e.g., Turn 1 before any save) and blocks online multiplayer (`GameMode.Multiplayer`) with a native dialog.
4. **ESC Pause & Save/Quit Menu Integration**
   - Injects **Quick SL** into the main ESC options menu (`uiOptionsMain`), plus **Quick Save** and **Reload Last Autosave** into the Save & Quit submenu (`uiOptionsSave`), preserving keyboard/gamepad navigation via `uiItemIcon.SetNavigatePairUpDown`.
5. **Hold Mouse Button to End Turn (Overworld)**
   - Holding the configured mouse button stationary for `0.45s` on a non-UI area of the overworld (when not in combat/menus and `uiEndTurnButton.interactable` is true) triggers `uiEndTurnButton.OnEndTurn()`.
   - Shows a subtle dark-gold progress bar below the cursor after `0.12s` and cancels immediately if the pointer moves past `14px` or enters UI.

### Requirements

- **For The King** (FTK1, PC Steam / Mono x64)
- **BepInEx 5.4.x (`win_x64`)**

### Installation

#### Option A: Download Prebuilt DLL (Recommended)

1. Install [BepInEx 5.4.x (`win_x64`)](https://github.com/BepInEx/BepInEx/releases) into your `For The King` root directory (next to `FTK.exe`) and launch the game once if you haven't already.
2. Download `FtkQuickSL.dll` from [Releases](https://github.com/Elosiad/ftk-quick-sl/releases).
3. Place it at `<For The King>\BepInEx\plugins\FtkQuickSL\FtkQuickSL.dll`.
4. Launch the game.

#### Option B: Build & Install from Source

No Visual Studio required—the build script auto-detects your Steam library via Windows registry / `libraryfolders.vdf` and compiles against the game's `.NET 3.5` Mono assemblies using Windows' built-in `csc.exe` (`/langversion:5`):

```powershell
git clone https://github.com/Elosiad/ftk-quick-sl.git
cd ftk-quick-sl
powershell -ExecutionPolicy Bypass -File .\scripts\install.ps1 -Rebuild
```

To specify a custom game path manually:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install.ps1 -Rebuild -GameDir "C:\Path\To\For The King"
```

### Controls & Default Hotkeys

| Action / Entry | Behavior | Notes |
| --- | --- | --- |
| Top Bar `S` / **`F11`** | Quick Save in-place | Available when `GameLogic.CanSave()` is true |
| Top Bar `L` / **`F10`** | Quick Load last save | Hotkeys disabled while chat input is focused |
| Right-click drag `S` or `L` | Move the `[S][L]` HUD bar | Persists custom offset automatically |
| Hold Left Mouse (`0.45s`, stationary) | End overworld turn | Cancels if moved `> 14px` or over UI |
| `ESC` -> Options / Save & Exit | Native menu buttons | Supports mouse & keyboard/controller nav |

### Configuration

Generated after first launch at `<GameDir>\BepInEx\config\com.ftk.quicksl.cfg`:

- **`[General]`**: `ShowHudButton` (`true`), `ConfirmDialog` (`false`), `AutoEnterFahrul` (`true`), `SeamlessReloadOverlay` (`true`), `AccelerateReloadCamera` (`true`), `SaveButtonLabel` (`S`), `LoadButtonLabel` (`L`)
- **`[Hotkey]`**: `SaveKey` (`F11`), `ReloadKey` (`F10`) — set to `None` to disable
- **`[HudPosition]`**: `HudUseCustomPosition` (`false` = auto-dock in top bar; turns `true` after dragging), `HudOffsetX`, `HudOffsetY`
- **`[EndTurnHold]`**: `EnableHoldEndTurn` (`true`), `HoldMouseButton` (`0` = Left, `1` = Right, `2` = Middle), `HoldDurationSeconds` (`0.45`), `MaxDragPixels` (`14`), `ShowHoldProgress` (`true`)

### Architecture Notes & Known Limitations

- **Why scene reload instead of in-memory undo?** FTK1 serializes world/character state via `GameStatesSerialize` + PlayMaker FSMs and does not maintain a step-level undo stack; even its native `GameLogic.LoadGame` must reload `FTK_main` before deserializing. Pairing `S` (before risky fights/events) with seamless `L` reload achieves reliable step-level SL without corrupting game state.
- **No saving mid-combat**: Matches native `GameLogic.CanSave()` rules (overworld idle/stopped or dungeon menu only); `L` remains usable mid-combat to return to your last save.
- **Online multiplayer**: Blocked with a dialog (`GameMode.Multiplayer`).
- **Left-click hold on reachable hexes**: Pressing LMB over a walkable hex may also be seen by the game's movement input. If you prefer zero overlap with movement clicks, change `HoldMouseButton` to `1` (Right) or `2` (Middle) in the config file.

### Uninstallation

Run the uninstall script or delete `<GameDir>\BepInEx\plugins\FtkQuickSL\` and `BepInEx\config\com.ftk.quicksl.cfg`:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\uninstall.ps1
```

## License

[MIT](LICENSE)

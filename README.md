# 感染日（D-Day）

> 模型資料夾（Google Drive）：https://drive.google.com/drive/folders/1GTENIeQ3OesRPqbu8blcmWbO_TY1LvXQ?usp=sharing


Unity 3D 第一人稱探索 / 潛行 / 解謎 / 逃脫遊戲，全程使用藍牙手把操作。
四個關卡、三個環境、全遊戲共用 5 條命。完整設計請看遊戲規劃書。

- **Unity 版本：** 6000.0.83f1（Built-in Render Pipeline）
- **主要套件：** Input System、AI Navigation、glTFast（讀 .glb）、uGUI
- **目前狀態：** 四關完整可玩，已接上團隊提供的模型（見下方「模型」）。

## 開啟專案

1. 用 Unity Hub 加入這個資料夾，選 **6000.0.83f1** 開啟（第一次會花幾分鐘匯入套件與模型）。
2. 開啟 `Assets/Scenes/MainMenu.unity`，按 Play（建議把 Game 視窗開 **Maximize On Play**）。
   任何場景都可以單獨按 Play，常駐系統會自動建立。

## 操作（Xbox 配置）

| 手把 | 功能 | 鍵盤備援（沒手把時測試用） |
|---|---|---|
| 左類比搖桿 | 移動 | WASD |
| 右類比搖桿 | 視角 | 滑鼠 |
| RB | 奔跑（按住） | Shift |
| LB | 跳躍 | Space |
| B（遊戲中） | 蹲下（按住）——可以鑽過矮縫；蹲著守衛比較難發現你 | Ctrl / C |
| A | 互動 / 確認 | E / Enter |
| B（介面中） | 返回 / 關閉 | Backspace |
| X / Y | 任務資訊 / 照片收集 | M / Tab |
| Start | 暫停 | Esc |
| 方向鍵 / 左搖桿 | 介面選項切換 | 方向鍵 |
| LB / RB（拼圖中） | 逆 / 順時針旋轉碎片 | Q / E |
| LB / RB（組語題目） | 縮小 / 放大文字 | - / = |
| 右搖桿（組語題目） | 捲動；**按下右搖桿**切換捲動「程式碼 / 題目」 | PageUp / PageDown；Tab 切換 |

## 關卡流程

1. **第一關 · 現代實驗室**：Guide 教學 → 找另外半張照片 → 拼接兩半 → 時空穿越。
2. **第二關 · 過去實驗室（ViewFinder）**：
   - 躲避 1 名守衛，找出 4 塊照片碎片（分散在不同區域）。
   - 實驗室裡有 4 個**編號相框**（01~04），散在不同的位置、遠近、高度。把碎片放進編號相同的相框（A 鍵）。
   - 四個都放好後，要**找一個視角**：從那個視角看過去，四塊碎片剛好在畫面上拼成完整的照片。
     判定是寬鬆的（大約站在那點附近 ±1 公尺、視線方向 ±15° 內就算），越接近邊框越亮。
   - 拼合成功後碎片飛向中央，**照片裡的房間直接在原地的實驗室裡長出來**（不換場景、沒有轉場），門是開著的，自己走進去。
3. **第三關 · 隱藏密室**：走進房間，門在身後關上；用中央電腦完成五題組合語言（第五題兩階段）→ 解除自毀 → 門打開。
   題目畫面的程式碼區與題目區都可以**縮放與上下（左右）捲動**。
4. **第四關 · 逃脫**：走出密室門外，3 名守衛開始追逐；要回到最初穿越過來的位置。
   第二、三、四關都在同一個 PastLab 場景裡接續進行。

## 模型

匯入的模型放在 `Assets/Models/`，整理好（比例、原點、材質）的版本在 `Assets/Prefabs/Models/`。

| 模型 | 用在哪裡 |
|---|---|
| Location underground laboratory（地下實驗室） | 第一關（現代 / 爆炸後：灰白色）與第二、四關（過去 / 爆炸前：深色鋼藍、藍白燈光）的環境 |
| guard.glb | 守衛 |
| player（Ready Player Me） | Guide 引導人（T-Pose 由 `RelaxedPose` 在執行時放下手臂） |
| sci-fi-interior-pack-lowpoly | 隱藏密室的地板、牆板、貨箱、門；實驗室內的貨箱掩體（原本是合併網格，已拆成單件） |
| office_ceiling_tiles.glb | 密室天花板 |
| door2 | 密室儲藏櫃的鉸鏈門（可互動） |
| door1 | 密室出口通道盡頭的門 |
| table1（concrete bench） | 實驗桌（含放照片碎片 / 文件的桌子） |
| table2（Stol Lab） | 實驗室裡的抽屜桌（原本是 .blend，已用 Blender 轉成 .glb，貼圖接好） |
| table3（Sci-Fi Table） | 科幻長桌（實驗室、密室）與中央電腦底下的平板（原本是 .blend，已轉成 .glb） |

> table2、table3 原本只有 .blend 檔（Unity 不能直接匯入），已用 Blender 轉成 `.glb` 放在對應資料夾。
> 原始 .blend 與原始 zip 備份在專案外的 `Documents/Unity/model-sources-not-importable/`。
> 之後遇到 .blend：用 Blender 開啟 → File → Export → glTF 2.0 (.glb) 即可，不用 Blender 也可以用 Python 的 `bpy` 套件轉。

### 換模型 / 加新模型

每個物件都掛了 `ModelSlot` 元件：把模型 Prefab 拖進 `Model Prefab` 欄位，執行時會隱藏替代品並放入正式模型；
替代品的 Collider 會保留，所以碰撞、互動、導航都不用重做。`Assets/Models/ModelSlots.md` 列出所有插槽。
新模型放進 `Assets/Models/` 後，用 **Tools → D-Day → 0. Prepare Model Prefabs** 重新整理（調比例與原點）。

其他替代品：

| 替代品 | 怎麼換 |
|---|---|
| 照片（用實景拍攝） | `Assets/Resources/Photos/`（由 **4. Capture Photos** 產生；也可直接換成別張圖，第一關 2:1、第二關 1:1） |
| 音效 / 背景音樂（程式合成） | 放進 `Assets/Resources/Audio/`，檔名同 `Sfx` 名稱（如 `Pickup.wav`），BGM 用 `BGM_場景名` |
| 中央電腦（目前是方塊 + 螢幕） | 場景 / Prefab 內的 `ControlPanel` 插槽 |
| 時空穿越演出（光環 + 白光） | `Assets/Scripts/Systems/TimeTravelEffect.cs` |

## 編輯器工具（Tools → D-Day）

| 選單 | 功能 |
|---|---|
| 0. Prepare Model Prefabs | 把匯入的原始模型整理成可用 Prefab（比例、原點、材質、拆分組件包） |
| 3. Build All Scenes | 重新產生四個場景。物件位置用 NavMesh 驗證（走得到），ViewFinder 相框的位置由程式自動求解 |
| 4. Capture Photos | 從場景拍攝兩張照片（需要圖形裝置） |
| Run Smoke Test | 自動把四關完整走一遍，含 ViewFinder、組語題目、縮放捲動、跳 / 蹲，結果印在 Console（`[SMOKE]`） |

另外還有兩道「通行性」檢查（避免出現玩家過不去的地方）：

- **建置場景時自動執行**：沿導航網格上通往全地圖的路徑，用真實玩家的碰撞尺寸逐點檢查，印出「需要跳 / 需要蹲 / 過不去」的位置；關鍵地點的路線若要繞上層天橋或走不到，建置會直接失敗。
- **`WalkProbe`**（命令列：`-executeMethod WalkProbe.RunBatch -probeScene PastLab`，不要加 `-quit`）：用真實的 CharacterController 從起點實際走到每個關鍵地點（碎片、相框、視角、密室門口與控制電腦、守衛路線…），逐一回報成功或卡住的位置。

建置順序（第一次或模型更新後）：0 → 3 → 4。

## 專案結構

```
Assets/
├── Scenes/        MainMenu, ModernLab（第一關）, PastLab（第二、三、四關；隱藏密室是場景裡長出來的房間）
├── Scripts/
│   ├── Core/        GameManager（5 條命、跨關卡資料）, LevelManager, 各關控制器
│   ├── Player/      移動、視角、互動（Raycast + A）、重生點
│   ├── Input/       GameInput（所有按鍵定義）, ControllerManager（斷線處理）
│   ├── Guard/       GuardAI（Patrol/Alert/Chase/Search）, GuardCatch
│   ├── Puzzle/      照片拼圖（第一關）、ViewfinderPuzzle（第二關）、組語題目資料與邏輯
│   ├── Interaction/ 門、照片、ViewfinderFrame、PhotoPortal…
│   ├── Systems/     AudioManager, ModelSlot, 導航（DDayNav / RuntimeNavMesh）, 自毀系統…
│   └── UI/          HUD、選單、組語解謎介面（全部程式建立，不用手動拉 Canvas）
├── Editor/        ModelPrep, SceneBuilder（+ LabScenes / SecretRoomScene = 隱藏密室的建置）, PhotoCapture, SmokeTest
├── Models/ Prefabs/ Materials/ Resources/ Shaders/ Input/
```

## 已知簡化（之後可加強）

- 時空穿越是光環擴張 + 白光，不是「光線環繞玩家、逐步換場景」的 Shader 版本。
- 守衛與 Guide 沒有動畫（只有姿勢 / 移動）。
- 實驗室是開放式屋頂（模型沒有天花板），背景為純黑。
- 沒有後處理與烘焙光照。

## 移動手感的設計備註

- 玩家碰撞膠囊半徑 0.25（導航網格以 0.3 烘焙，所以導航說走得到的縫隙玩家一定鑽得過）。
- 階梯高度 0.3：不要設太大。CharacterController 每次移動前會先把膠囊往上抬一個「階梯高度」，抬 0.5 時頭會頂到 2 公尺高的門楣而卡在門口（這就是之前「有些地方過不去」的主因之一）。比 0.3 高的落差用跳的（跳躍約 1.1 公尺）。
- 桌子、長椅原本沒有碰撞（會直接穿過去），現在都加上碰撞了。

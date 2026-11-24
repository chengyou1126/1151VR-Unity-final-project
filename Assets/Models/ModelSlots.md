# 模型插槽清單（自動產生）

目前所有 3D 物件都是基本幾何體「替代品」。找到正式模型後，**不用重做場景**：

1. 把模型匯入 `Assets/Models/` 對應的子資料夾（Environment / Characters / Items / Interactive）。
2. 做成 Prefab（或直接用模型的 Prefab）。
3. 選取場景裡（或 Prefab 裡）掛有 **ModelSlot** 的物件，把 Prefab 拖到 `Model Prefab` 欄位，必要時調整位置 / 旋轉 / 縮放。
   執行時會自動隱藏替代品並放入正式模型；替代品的 Collider 會保留，所以碰撞、互動、守衛 NavMesh 都不用重做。
4. 角色（Guard / Guide）建議在模型 Prefab 內放好 Animator，之後再把 Animator 參數接到 GuardAI。

> 想一次換掉整間房間：在 `EnvShell_*` 這個插槽放整間模型即可（會隱藏該場景 Environment 底下的所有替代品，只保留碰撞）。

| 位置（場景 / Prefab） | Slot ID | 物件名稱 | 說明 |
|---|---|---|---|
| Prefab | `Guard` | Visuals | 守衛（目前用 guard.glb；沒有動畫） |
| Prefab | `Guide` | Visuals | 引導人（目前用 player 模型，手臂由 RelaxedPose 放下） |
| Prefab | `PhotoPiece` | Visuals | 照片碎片（發光薄片） |
| Prefab | `PhotoFrame` | FrameVisual | 第二關相框外框（會被縮放成不同大小；照片面朝 -Z） |
| Prefab | `ControlPanel` | Visuals | 中央控制電腦（桌 + 三螢幕）。螢幕朝 -Z |
| Prefab | `TimePortal` | Visuals | 時空穿越裝置 / 逃脫終點（發光圓盤） |
| MainMenu | `Debris` | Debris | 爆炸碎片 / 瓦礫 |
| ModernLab | `UndergroundLab` | Environment | 地下實驗室（現代 / 爆炸後：灰白色） |
| ModernLab | `LabBench` | M_Bench | 實驗桌（Table1 混凝土長椅） |
| ModernLab | `Crate` | SciFi_Crate0 | 貨箱（SciFi pack） |
| ModernLab | `StolLab` | M_StolLab | 實驗室抽屜桌（table2） |
| ModernLab | `SciFiTable` | M_SciFiTableB | 科幻長桌（table3） |
| ModernLab | `Debris` | Debris | 爆炸碎片 / 瓦礫 |
| ModernLab | `Document` | Document | 可閱讀的文件 |
| PastLab | `UndergroundLab` | Environment | 地下實驗室（過去 / 爆炸前：深色鋼藍） |
| PastLab | `EnvShell_HiddenRoom` | Shell | 整個隱藏密室外殼（放整間模型時用） |
| PastLab | `SciFiWall` | Walls | 牆板（SciFi pack，3.5×4.06 m） |
| PastLab | `SciFiFloor` | Floor | 地板磚（SciFi pack，3.5×3.5 m） |
| PastLab | `CeilingTile` | CeilingTiles | 天花板磚（office_ceiling_tiles.glb） |
| PastLab | `Crate` | Crates | 貨箱（SciFi pack） |
| PastLab | `SciFiTable` | WorkTables | 科幻長桌 / 平板（table3） |
| PastLab | `EntranceDoor` | SciFi_Door0 | 密室入口門板（SciFi door，向下滑開） |
| PastLab | `SelfDestructDisplay` | SelfDestructDisplay | 自毀系統狀態顯示器 |
| PastLab | `Door` | ClosetDoor | 儲藏櫃的門（Door2 模型，鉸鏈門） |
| PastLab | `Document` | Document | 可閱讀的文件 |
| PastLab | `LabBench` | M_Bench | 實驗桌（Table1 混凝土長椅） |
| PastLab | `StolLab` | M_StolLab | 實驗室抽屜桌（table2） |

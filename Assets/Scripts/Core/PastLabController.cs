using System.Collections;
using UnityEngine;

/// <summary>
/// 過去實驗室。第二、三、四關都在這個場景裡接續進行（中間沒有換場景）：
/// 第二關：躲一名守衛、找四塊碎片、放進四個相框，再找出一個視角讓四塊碎片拼成完整照片（ViewFinder）；
///         拼合後，照片裡的房間（隱藏密室）直接在實驗室裡長出來。
/// 第三關：走進長出來的房間，門關上；用中央電腦完成五題組合語言，解除自毀。
/// 第四關：門打開，走出密室後三名守衛開始追逐，要回到最初穿越過來的位置。
/// </summary>
public class PastLabController : MonoBehaviour
{
    public GameObject[] stage2Only;
    public GameObject[] stage4Only;
    public ViewfinderPuzzle viewfinder;
    public HiddenRoom room;
    public ProximityZone escapeZone;       // 時空穿越位置（第四關終點）
    public AlarmLight[] stage4Alarms;
    public Transform playerStartStage4;    // 密室門外

    bool stage3Started, stage4Started, ending;

    IEnumerator Start()
    {
        var lm = LevelManager.Current;
        var ui = UIManager.Instance;
        var gm = GameManager.Instance;

        foreach (var go in stage2Only) if (go != null) go.SetActive(true);
        foreach (var go in stage4Only) if (go != null) go.SetActive(false);
        escapeZone.zoneActive = false;
        escapeZone.fireOnce = false;
        room.exitZone.zoneActive = false;
        room.insideZone.zoneActive = false;
        foreach (var a in stage4Alarms) a.SetRunning(false);

        var pm = PhotoPuzzleManager.Instance;
        var d = gm.Data;

        ui.SetPhotoProgress(pm.CollectedCount, pm.TotalPieces);
        UpdateMission(pm, d);

        pm.OnPieceCollected += id =>
        {
            ui.SetPhotoProgress(pm.CollectedCount, pm.TotalPieces);
            ui.Toast("取得照片碎片 " + (id + 1).ToString("00") + "  （" + pm.CollectedCount + " / " + pm.TotalPieces + "）\n把它放進編號相同的相框", 3f);
            UpdateMission(pm, d);
        };

        foreach (var f in viewfinder.frames)
        {
            f.OnPlaced += fr =>
            {
                UpdateMission(pm, d);
                if (viewfinder.AllPlaced)
                    ui.Toast("四塊碎片都就位了。\n它們散在不同的位置——換個角度看看，也許能拼成一張完整的照片。", 5f);
                else
                    ui.Toast("碎片 " + (fr.index + 1).ToString("00") + " 已放入相框", 2f);
            };
        }

        viewfinder.OnMerged += () =>
        {
            // 房間長出來的同時，第二關的東西收起來（守衛不會闖進密室）
            foreach (var go in stage2Only) if (go != null) go.SetActive(false);
            ui.SetPhotoProgress(0, 0);
        };
        viewfinder.OnOpened += () =>
        {
            UpdateMission(pm, d);
            room.insideZone.zoneActive = true;
            ui.Toast("碎片拼合了——照片裡的房間出現在眼前。\n走進去。", 4.5f);
        };
        room.insideZone.OnPlayerEnter += () => { if (!stage3Started) StartCoroutine(Stage3Routine()); };
        room.console.OnUse += OpenConsole;
        room.exitZone.OnPlayerEnter += () => { if (!stage4Started) BeginStage4(); };
        escapeZone.OnPlayerEnter += () => { if (stage4Started && !ending) ReturnToGuide(); };

        yield return lm.BeginLevel(true, Color.white);
        ui.Toast("你回到了事故發生之前的實驗室。小心守衛。", 4f);
    }

    void UpdateMission(PhotoPuzzleManager pm, RunData d)
    {
        var ui = UIManager.Instance;
        int placed = viewfinder.PlacedCount;
        if (d.portalOpen) ui.SetMission("走進出現的隱藏密室");
        else if (placed >= 4) ui.SetMission("找出一個視角，讓四個相框的碎片拼成完整的照片");
        else if (pm.AllCollected) ui.SetMission("把四塊碎片放進編號相同的相框（" + placed + " / 4）");
        else ui.SetMission("躲避守衛，找出 4 塊照片碎片（" + pm.CollectedCount + " / 4），放進編號相同的相框");
    }

    // ───────────── 第三關：隱藏密室 ─────────────

    IEnumerator Stage3Routine()
    {
        stage3Started = true;
        var gm = GameManager.Instance;
        var ui = UIManager.Instance;
        var lm = LevelManager.Current;

        gm.SetMode(InputMode.Locked);
        lm.SetStage(3);
        AudioManager.SetMusic("SecretRoom");
        ui.SetPhotoProgress(0, 0);
        ui.SetMission("使用中央控制電腦，修復緊急停止系統");

        room.door.Close();            // 門在身後關上
        room.selfDestruct.Arm();

        yield return new WaitForSeconds(0.8f);
        yield return ui.SayRoutine("SYSTEM", new[]
        {
            "密室中央有一台仍在運作的控制電腦。螢幕顯示：「緊急停止功能：無法使用」。",
            "研究紀錄指出，爆炸的原因是緊急停止系統異常。必須修復五個系統模組，才能阻止爆炸。",
            "走近電腦，按 " + GameInput.InteractLabel + " 開始。答錯一題就會失去一條生命，要小心。"
        });
        gm.SetMode(InputMode.Gameplay);
    }

    void OpenConsole()
    {
        if (!stage3Started) return;
        UIManager.Instance.Assembly.Open(AssemblyPuzzleManager.Instance, OnShutdownPressed);
    }

    void OnShutdownPressed()
    {
        StartCoroutine(ShutdownRoutine());
    }

    IEnumerator ShutdownRoutine()
    {
        var gm = GameManager.Instance;
        var ui = UIManager.Instance;

        room.console.usable = false;
        gm.SetMode(InputMode.Locked);
        ui.Toast("緊急停止程序執行中……", 3f);
        AudioManager.Play(Sfx.Alarm);
        yield return new WaitForSeconds(2.5f);

        room.selfDestruct.Disarm();
        AudioManager.Play(Sfx.Success);
        ui.Flash(new Color(0.4f, 1f, 0.6f, 0.35f), 1.2f);
        yield return new WaitForSeconds(1.2f);

        yield return ui.SayRoutine("SYSTEM", new[]
        {
            "EMERGENCY SHUTDOWN SUCCESSFUL\n緊急停止程序執行成功。實驗室爆炸已成功阻止，病毒洩漏事件已避免。",
            "請返回時間穿越裝置所在位置。"
        });

        room.door.Open();
        room.exitZone.zoneActive = true;
        ui.SetMission("離開密室，返回最初穿越到過去的位置");
        gm.SetMode(InputMode.Gameplay);
    }

    // ───────────── 第四關：逃脫 ─────────────

    /// <summary>玩家走出密室門外：自毀解除了，但三名守衛開始追捕。</summary>
    public void BeginStage4()
    {
        if (stage4Started) return;
        stage4Started = true;
        var lm = LevelManager.Current;
        var ui = UIManager.Instance;

        // 坦克室和大廳之間的一樓門，二樓通道保持暢通。
        var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "GroundFloorPassageDoor";
        door.transform.SetParent(transform, true);
        door.transform.position = new Vector3(17.63f, 1.4f, 38.6f);
        door.transform.localScale = new Vector3(0.28f, 2.8f, 3.8f);
        var panel = room.door.panel.GetComponentInChildren<MeshRenderer>(true);
        if (panel != null) door.GetComponent<MeshRenderer>().sharedMaterial = panel.sharedMaterial;
        var obstacle = door.AddComponent<UnityEngine.AI.NavMeshObstacle>();
        obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
        obstacle.size = Vector3.one;
        obstacle.carving = true;

        room.ShowExterior();
        lm.SetStage(4);
        AudioManager.SetMusic("PastLab");
        foreach (var go in stage4Only) if (go != null) go.SetActive(true);
        foreach (var a in stage4Alarms) a.SetRunning(true);
        ui.SetPhotoProgress(0, 0);
        ui.SetMission("一樓通道已關閉，請經二樓回到升降台東側的時空穿越點");

        // 被抓到時從密室門外重新開始
        var cp = PlayerCheckpoint.Instance;
        if (cp != null && playerStartStage4 != null) cp.SetStart(playerStartStage4.position, playerStartStage4.eulerAngles.y);

        escapeZone.zoneActive = true;
        AudioManager.Play(Sfx.Alarm);
        ui.Toast("自毀程序已解除——但守衛開始追捕你！按住 RB 奔跑！", 4.5f);
    }

    void ReturnToGuide()
    {
        ending = true;
        escapeZone.zoneActive = false;
        UIManager.Instance.SetHudVisible(false);
        AudioManager.Play(Sfx.TimeTravel);
        GameManager.Instance.Data.returningToGuide = true;
        SceneLoader.Load(GameManager.SceneNameForStage(1), Color.white);
    }
}

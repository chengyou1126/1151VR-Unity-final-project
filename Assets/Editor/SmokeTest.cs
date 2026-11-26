using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 自動化冒煙測試：在 Play Mode 裡把四關流程走一遍（直接呼叫遊戲邏輯，不需要手把）：
/// 場景載入 → NavMesh 烘焙 → 守衛狀態 → 被抓扣命重生 → 第三關五題（含第五題兩階段）→ 第四關三守衛 → 抵達終點通關 → 生命歸零 Game Over → 重新開始。
/// 選單：Tools/D-Day/Run Smoke Test；命令列：-executeMethod SmokeTest.RunBatch（不要加 -quit）。
/// </summary>
public static class SmokeTest
{
    static IEnumerator<Func<bool>> script;
    static readonly List<string> failures = new List<string>();
    static readonly List<string> logErrors = new List<string>();
    static bool batch;
    static string shotDir;
    static bool prevOptsEnabled;
    static EnterPlayModeOptions prevOpts;

    public static void RunBatch()
    {
        batch = true;
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-shotDir") { shotDir = args[i + 1]; System.IO.Directory.CreateDirectory(shotDir); }
        Start();
    }

    [MenuItem("Tools/D-Day/Run Smoke Test")]
    public static void Start()
    {
        failures.Clear();
        logErrors.Clear();
        prevOptsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        prevOpts = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

        Application.logMessageReceived += OnLog;
        EditorSceneManager.OpenScene("Assets/Scenes/ModernLab.unity");
        script = Script();
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            logErrors.Add(type + ": " + msg + "\n" + stack);
    }

    static Func<bool> current;

    static void Tick()
    {
        if (script == null) return;
        try
        {
            if (current != null && !current()) return;
            if (!script.MoveNext()) { Finish(); return; }
            current = script.Current;
        }
        catch (Exception e)
        {
            failures.Add("Script exception: " + e);
            Finish();
        }
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        script = null;
        Application.logMessageReceived -= OnLog;

        Debug.Log("[SMOKE] ───────── RESULT ─────────");
        foreach (var f in failures) Debug.Log("[SMOKE] FAIL: " + f);
        foreach (var l in logErrors.Distinct().Take(15)) Debug.Log("[SMOKE] LOGERROR: " + l);
        bool ok = failures.Count == 0 && logErrors.Count == 0;
        Debug.Log("[SMOKE] " + (ok ? "ALL PASSED" : "FAILED  (checks failed: " + failures.Count + ", log errors: " + logErrors.Count + ")"));

        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () =>
        {
            EditorSettings.enterPlayModeOptionsEnabled = prevOptsEnabled;
            EditorSettings.enterPlayModeOptions = prevOpts;
            if (batch) EditorApplication.Exit(ok ? 0 : 1);
        };
    }

    // ───────────── 輔助 ─────────────

    static Func<bool> Until(Func<bool> cond, float timeoutSec, string what)
    {
        double end = EditorApplication.timeSinceStartup + timeoutSec;
        return () =>
        {
            bool ok;
            try { ok = cond(); } catch { ok = false; }
            if (ok) return true;
            if (EditorApplication.timeSinceStartup > end) { failures.Add("Timeout: " + what); return true; }
            return false;
        };
    }

    static Func<bool> Seconds(float s)
    {
        double end = EditorApplication.timeSinceStartup + s;
        return () => EditorApplication.timeSinceStartup >= end;
    }

    static void Check(bool cond, string msg)
    {
        if (cond) Debug.Log("[SMOKE] pass: " + msg);
        else { failures.Add(msg); Debug.Log("[SMOKE] FAIL: " + msg); }
    }

    static GameManager GM => GameManager.Instance;
    static bool InScene(SceneKind k) { return LevelManager.Current != null && LevelManager.Current.kind == k && GM != null; }

    static Func<bool> WaitScene(SceneKind k, float t = 40f)
    {
        return Until(() => InScene(k) && !SceneLoader.IsLoading && (k == SceneKind.PastLab ? GM.Mode == InputMode.Gameplay : true), t, "scene " + k + " ready");
    }

    // ───────────── 測試腳本 ─────────────

    static IEnumerator<Func<bool>> Script()
    {
        // ── 第一關 ──
        yield return Until(() => EditorApplication.isPlaying && GM != null && LevelManager.Current != null && Time.frameCount > 20, 60, "play mode start");
        Check(InScene(SceneKind.ModernLab), "ModernLab loaded");
        Check(LevelManager.Current.Stage == 1, "stage == 1");
        Check(GM.Data.lives == 5, "lives == 5 at start");
        Check(PlayerCheckpoint.Instance != null, "player exists");
        Check(UIManager.Instance != null && UIManager.Instance.Assembly != null && UIManager.Instance.PhotoPuzzle != null, "UI built");
        Check(UnityEngine.EventSystems.EventSystem.current != null, "EventSystem exists");
        Check(GameInput.Move != null && GameInput.Interact != null, "input actions built");

        // 開場對話結束後操作教學，這裡直接跳過：把介面關掉、交還控制
        yield return Seconds(3f);
        Shot("01_modernlab_intro_dialogue");
        UIManager.Instance.StopAllCoroutines(); UIManager.Instance.ResetTransient();
        GM.SetMode(InputMode.Gameplay);
        UIManager.Instance.ClearSelection();
        yield return Seconds(0.5f);
        Shot("02_modernlab_start");
        FacePlayer(LevelManager.Current.playerStart.position, 250f);
        yield return Seconds(0.5f);
        Shot("03_modernlab_guide");
        FacePlayer(new Vector3(9f, 0.05f, 38f), 250f);
        yield return Seconds(0.5f);
        Shot("04_modernlab_tank_room");
        TopDown("05_modernlab_topdown", new Vector3(36, 0, 32), 70f);
        TeleportPlayer(LevelManager.Current.playerStart.position);

        // ── 跳躍 / 蹲下 ──
        {
            var pmv = PlayerCheckpoint.Instance.GetComponent<PlayerMovement>();
            var ccv = pmv.GetComponent<CharacterController>();
            pmv.debugInput = Vector2.zero;
            yield return Seconds(0.5f);
            float y0 = pmv.transform.position.y, maxY = y0;
            pmv.debugJump = true;
            double jumpEnd = EditorApplication.timeSinceStartup + 1.0;
            yield return () => { maxY = Mathf.Max(maxY, pmv.transform.position.y); return EditorApplication.timeSinceStartup > jumpEnd; };
            bool overhead = Physics.Raycast(pmv.transform.position + Vector3.up * 1.7f, Vector3.up, out var ohHit, 3f, ~0, QueryTriggerInteraction.Ignore);
            Check(maxY - y0 > 0.5f, "jump lifts the player about 1 m (" + (maxY - y0).ToString("F2") + " m; overhead obstacle " + (overhead ? ohHit.collider.name + " at +" + ohHit.distance.ToString("F2") : "none") + ")");
            yield return Seconds(0.8f);
            Check(pmv.IsGrounded && Mathf.Abs(pmv.transform.position.y - y0) < 0.3f, "player lands back on the ground");

            pmv.debugCrouch = true;
            yield return Seconds(0.6f);
            Check(pmv.IsCrouching && ccv.height < 1.1f, "crouch shrinks the collider (" + ccv.height.ToString("F2") + " m)");
            Check(Camera.main.transform.localPosition.y < 1.0f, "crouch lowers the camera");
            // 頭上有低矮的天花板：放開蹲鍵也不能站起來
            var lowCeiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lowCeiling.transform.position = pmv.transform.position + Vector3.up * 1.4f;
            lowCeiling.transform.localScale = new Vector3(3f, 0.2f, 3f);
            Physics.SyncTransforms();
            pmv.debugCrouch = false;
            yield return Seconds(0.6f);
            Check(pmv.IsCrouching, "stays crouched under a low ceiling");
            UnityEngine.Object.Destroy(lowCeiling);
            yield return Seconds(0.8f);
            Check(!pmv.IsCrouching && ccv.height > 1.7f, "stands back up when there is room");
            pmv.debugInput = null;
        }

        // 撿起另一半照片 → 應該自動開啟拼圖
        var pm = PhotoPuzzleManager.Instance;
        Check(pm.TotalPieces == 2 && !pm.AllCollected, "stage1 puzzle needs 2 pieces, 1 held");
        pm.Collect(1);
        Check(pm.AllCollected, "both halves collected");
        yield return Until(() => UIManager.Instance.PhotoPuzzle.IsOpen, 10, "photo puzzle auto-opens");
        Check(GM.Mode == InputMode.Puzzle, "puzzle input mode active");
        yield return Seconds(0.5f);
        Shot("06_photo_puzzle");

        // 用反射把拼圖設成已完成，驗證「完成 → 時空穿越 → 進入第二關」
        var pz = UIManager.Instance.PhotoPuzzle;
        var t = typeof(PhotoPuzzleController);
        const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var piece = (int[])t.GetField("piece", F).GetValue(pz);
        var rot = (int[])t.GetField("rot", F).GetValue(pz);
        for (int i = 0; i < piece.Length; i++) { piece[i] = i; rot[i] = 0; }
        t.GetMethod("CheckSolved", F).Invoke(pz, null);

        // ── 時空穿越 → 第二關 ──
        yield return WaitScene(SceneKind.PastLab, 60);
        Check(LevelManager.Current.Stage == 2, "stage == 2 after time travel");
        Check(Vector3.Distance(PlayerCheckpoint.Instance.transform.position, LevelManager.Current.playerStart.position) < 1.5f, "player at time-travel start point");
        Check(RuntimeNavMesh.Ready, "NavMesh baked at runtime");
        yield return Seconds(3f);
        Shot("07_pastlab_start");
        TopDown("08_pastlab_topdown", new Vector3(36, 0, 32), 70f);

        var guards = UnityEngine.Object.FindObjectsByType<GuardAI>(FindObjectsSortMode.None).Where(g => g.gameObject.activeInHierarchy).ToList();
        Check(guards.Count == 1, "stage 2 has exactly 1 active guard (found " + guards.Count + ")");
        if (guards.Count > 0)
        {
            var agent = guards[0].GetComponent<UnityEngine.AI.NavMeshAgent>();
            Check(agent.enabled && agent.isOnNavMesh, "guard agent is on the NavMesh");
            Check(guards[0].State == GuardState.Patrol || guards[0].State == GuardState.Alert || guards[0].State == GuardState.Chase, "guard AI running (" + guards[0].State + ")");
        }
        Check(UnityEngine.Object.FindObjectsByType<PhotoPickup>(FindObjectsSortMode.None).Count(p => p.gameObject.activeInHierarchy) == 4, "4 photo pieces present");

        // 巡邏一段時間後位置有變化
        Vector3 g0 = guards.Count > 0 ? guards[0].transform.position : Vector3.zero;
        yield return Seconds(4f);
        if (guards.Count > 0) Check(Vector3.Distance(g0, guards[0].transform.position) > 0.5f, "guard actually moves (patrols)");

        // 被抓 → 扣命 → 回到起點
        TeleportPlayer(LevelManager.Current.playerStart.position + new Vector3(8f, 0, 0));
        yield return null;
        int livesBefore = GM.Data.lives;
        LevelManager.Current.OnPlayerCaught(null);
        yield return Until(() => GM.Data.lives == livesBefore - 1 && GM.Mode == InputMode.Gameplay, 10, "caught: lose 1 life and regain control");
        Check(GM.Data.lives == 4, "lives == 4 after being caught");
        Check(Vector3.Distance(PlayerCheckpoint.Instance.transform.position, LevelManager.Current.playerStart.position) < 1.5f, "respawned at stage start");

        // 守衛的巡邏是隨機時機，會在這段測試中途抓到玩家而讓扣命數不固定；ViewFinder 邏輯測試期間先讓守衛待命
        foreach (var gd in guards) if (gd != null) gd.gameObject.SetActive(false);

        // ── ViewFinder：收集四張碎片 → 放進四個相框 → 找視角 → 隱藏房間長出來 ──
        var vp = UnityEngine.Object.FindFirstObjectByType<ViewfinderPuzzle>();
        Check(vp != null && vp.frames.Length == 4, "viewfinder puzzle with 4 frames exists");
        Check(!UIManager.Instance.PhotoPuzzle.IsOpen, "stage 2 does not open the jigsaw UI");

        // 沒有碎片就不能放；放錯編號的相框不行
        vp.frames[0].Interact();
        Check(!vp.frames[0].Placed, "frame refuses placement without its piece");
        for (int i = 0; i < 4; i++) PhotoPuzzleManager.Instance.Collect(i);
        yield return Seconds(0.3f);
        Check(!UIManager.Instance.PhotoPuzzle.IsOpen, "collecting 4 pieces does not open a puzzle");

        // 不在正確視角：不會開
        TeleportPlayer(LevelManager.Current.playerStart.position);
        for (int i = 0; i < 4; i++) vp.frames[i].Interact();
        Check(vp.AllPlaced, "all four frames accept their pieces");
        yield return Seconds(1.0f);
        Check(!vp.Opened, "room stays hidden when viewed from the start point (score " + vp.Score.ToString("F2") + ")");
        Shot("08b_frames_from_start");

        // 站到建置時求解出的視角：四塊碎片對齊成一張完整照片
        FacePlayer(vp.solutionPosition, vp.solutionYaw);
        yield return Seconds(0.4f);
        Shot("08c_frames_aligned");
        float scoreAtSolution = vp.Score;
        Check(scoreAtSolution > 0.8f, "score at the solved viewpoint is high (" + scoreAtSolution.ToString("F2") + " : " + vp.LastFailReason + ")");

        // 判定寬鬆：在正確視角周圍 1 公尺、視線偏 ±5 度的幾個點，大多數仍然算數
        var cam = Camera.main;
        int valid = 0, good = 0; var detail = new System.Text.StringBuilder();
        for (int k = 0; k < 8; k++)
        {
            float ang = k * 45f * Mathf.Deg2Rad;
            var off = Quaternion.Euler(0, vp.solutionYaw, 0) * new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * 1.0f;
            var target = vp.solutionPosition + off;
            if (!UnityEngine.AI.NavMesh.SamplePosition(target, out var nh, 0.25f, UnityEngine.AI.NavMesh.AllAreas)) continue;   // 站不到的點不算
            TeleportPlayer(nh.position);
            PlayerCheckpoint.Instance.GetComponent<PlayerLook>().SetRotation(vp.solutionYaw + (k % 2 == 0 ? 5f : -5f));
            yield return null; yield return null;
            float sc = vp.Evaluate(cam);
            valid++; if (sc > 0.25f) good++;
            detail.Append(k).Append(':').Append(sc.ToString("F2")).Append(sc <= 0.25f ? "(" + vp.LastFailReason + ")" : "").Append("  ");
        }
        Check(valid == 0 || good * 2 >= valid, "lenient: " + good + "/" + valid + " points within 1 m and +-5 deg still count   [" + detail + "]");

        // 回到正確視角，等待房間長出來
        var room = vp.room;
        Check(!room.body.activeSelf && !room.IsRevealed, "hidden room is not there before the puzzle is solved");
        FacePlayer(vp.solutionPosition, vp.solutionYaw);
        yield return Until(() => vp.Opened && GM.Mode == InputMode.Gameplay, 25, "hidden room appears from the aligned viewpoint");
        Check(GM.Data.portalOpen, "room-revealed flag set");
        yield return Seconds(0.5f);
        Shot("08d_room_appeared");
        Check(room.IsRevealed && room.body.activeSelf && room.door.IsOpen, "hidden room grew in place with its door open");
        Check(LevelManager.Current.Stage == 2 && InScene(SceneKind.PastLab), "still in the same scene (no transition), stage 2");
        Check(!UnityEngine.Object.FindObjectsByType<GuardAI>(FindObjectsSortMode.None).Any(g => g.gameObject.activeInHierarchy), "stage-2 guard is gone once the room appears");

        // 玩家從外面一路走到房間門口：真的走得進去（用真實的 CharacterController）
        {
            var pmv = PlayerCheckpoint.Instance.GetComponent<PlayerMovement>();
            var ccv = pmv.GetComponent<CharacterController>();
            Vector3 doorWorld = room.transform.position;
            var look = PlayerCheckpoint.Instance.GetComponent<PlayerLook>();
            var from = doorWorld - room.transform.forward * 4.5f;
            UnityEngine.AI.NavMesh.SamplePosition(from, out var fh, 2f, UnityEngine.AI.NavMesh.AllAreas);
            TeleportPlayer(fh.position);
            look.SetRotation(room.transform.eulerAngles.y);
            pmv.debugInput = new Vector2(0, 1);
            double t0 = Time.timeAsDouble;
            yield return Until(() => LevelManager.Current.Stage == 3, 12, "walking through the doorway starts stage 3");
            pmv.debugInput = null;
            Check(room.transform.InverseTransformPoint(pmv.transform.position).z > 0.8f, "player walked into the room on foot (local z " + room.transform.InverseTransformPoint(pmv.transform.position).z.ToString("F1") + ")");
        }
        Check(LevelManager.Current.Stage == 3 && GM.Data.stage == 3, "stage == 3 inside the revealed room");
        Check(GM.Data.lives == 4, "lives unchanged entering the room");
        yield return Seconds(1.5f);
        Check(!room.door.IsOpen, "room door closes behind the player");

        // ── 第三關：組語解謎 ──
        var asm = AssemblyPuzzleManager.Instance;
        Check(asm.Steps.Count == 6, "6 steps (5 questions, Q5 has 2 phases)");
        UIManager.Instance.StopAllCoroutines(); UIManager.Instance.ResetTransient();
        GM.SetMode(InputMode.Gameplay);
        var ctrl = UnityEngine.Object.FindFirstObjectByType<PastLabController>();
        FacePlayer(room.transform.TransformPoint(new Vector3(0f, 0.05f, 5.0f)), room.transform.eulerAngles.y);
        yield return Seconds(0.5f);
        Shot("09_room_console");
        TeleportPlayer(room.transform.TransformPoint(new Vector3(0f, 0.05f, 0.6f)) + room.transform.forward * -4.5f);
        PlayerCheckpoint.Instance.GetComponent<PlayerLook>().SetRotation(room.transform.eulerAngles.y);
        yield return Seconds(0.4f);
        Shot("09b_room_from_outside");
        FacePlayer(room.transform.TransformPoint(new Vector3(0f, 0.05f, 5.0f)), room.transform.eulerAngles.y);
        UIManager.Instance.Assembly.Open(asm, null);
        Check(UIManager.Instance.Assembly.IsOpen, "assembly UI opens");
        yield return Seconds(0.6f);
        Shot("10_assembly_q1");
        // 檢視第五題兩個階段的版面
        UIManager.Instance.Assembly.Close();
        GM.Data.quizStep = 4;
        UIManager.Instance.Assembly.Open(asm, null);
        yield return Seconds(0.6f);
        Shot("11_assembly_q5_phase1");
        // 縮放 + 捲動：放大後程式碼比視窗高，往下捲動
        var asmUi = UIManager.Instance.Assembly;
        float z0 = asmUi.Zoom;
        asmUi.SetZoom(1.6f);
        yield return Seconds(0.4f);
        Check(asmUi.Zoom > z0 + 0.5f, "zoom increases text size (" + z0.ToString("F2") + " -> " + asmUi.Zoom.ToString("F2") + ")");
        Check(asmUi.CodeContentHeight > asmUi.CodeViewHeight, "zoomed code is taller than its window (" + asmUi.CodeContentHeight.ToString("F0") + " > " + asmUi.CodeViewHeight.ToString("F0") + ")");
        asmUi.ScrollTo(true, 600f);
        yield return Seconds(0.3f);
        Check(asmUi.CodeScrollY > 100f, "code pane scrolls down (y=" + asmUi.CodeScrollY.ToString("F0") + ")");
        Shot("11b_assembly_zoomed_scrolled");
        asmUi.SetZoom(0.5f);
        yield return Seconds(0.3f);
        Check(asmUi.Zoom <= 0.51f, "zoom out clamps at minimum");
        UIManager.Instance.Assembly.Close();
        GM.Data.quizStep = 5;
        UIManager.Instance.Assembly.Open(asm, null);
        yield return Seconds(0.6f);
        Shot("12_assembly_q5_phase2");
        UIManager.Instance.Assembly.Close();
        GM.Data.quizStep = 0;
        UIManager.Instance.Assembly.Open(asm, null);
        yield return Seconds(0.3f);

        // 答錯 → -1 命、不前進
        var q1 = asm.Current;
        int wrong = (q1.correct + 1) % 4;
        var r = asm.Submit(wrong);
        Check(r == AnswerResult.Wrong && GM.Data.lives == 3 && asm.CurrentIndex == 0, "wrong answer: lives 4→3, stays on question 1");

        // 逐題答對（Q5 需要兩階段）
        for (int i = 0; i < asm.Steps.Count; i++)
        {
            var step = asm.Current;
            Check(step != null && !asm.IsModuleRestored(step.module) || step.module == 4, "step " + (i + 1) + " available");
            var res = asm.Submit(step.correct);
            Check(res == AnswerResult.Correct, "step " + (i + 1) + " correct answer accepted");
        }
        Check(asm.AllDone && asm.RestoredCount == 5, "all 5 modules restored");
        Check(GM.Data.lives == 3, "correct answers do not restore lives");

        UIManager.Instance.Assembly.Close();
        UIManager.Instance.Assembly.Open(asm, null);
        yield return Seconds(0.6f);
        Shot("13_assembly_restored");
        UIManager.Instance.Assembly.Close();
        ctrl.GetType().GetMethod("OnShutdownPressed", F).Invoke(ctrl, null);
        yield return Until(() => GM.Data.selfDestructDisarmed, 15, "self-destruct disarmed");
        ctrl.StopAllCoroutines(); UIManager.Instance.StopAllCoroutines(); UIManager.Instance.ResetTransient(); // 略過結束對話
        GM.SetMode(InputMode.Gameplay);
        // ShutdownRoutine 被我們中斷（略過對話），直接補上它最後做的事
        room.door.Open();
        room.exitZone.zoneActive = true;
        yield return Seconds(2f);
        Check(room.door.IsOpen && room.exitZone.zoneActive, "door opens and exit zone is active after the shutdown");

        // 走出密室門外 → 第四關（同一個場景，沒有換場景）
        {
            var pmv = PlayerCheckpoint.Instance.GetComponent<PlayerMovement>();
            var look = PlayerCheckpoint.Instance.GetComponent<PlayerLook>();
            GM.SetMode(InputMode.Gameplay);
            TeleportPlayer(room.transform.TransformPoint(new Vector3(0f, 0.05f, 4.5f)));
            look.SetRotation(room.transform.eulerAngles.y + 180f);
            pmv.debugInput = new Vector2(0, 1);
            yield return Until(() => LevelManager.Current.Stage == 4, 12, "walking out of the room starts stage 4");
            pmv.debugInput = null;
            if (LevelManager.Current.Stage != 4)
                Debug.Log("[SMOKE] debug walk-out: player local " + room.transform.InverseTransformPoint(pmv.transform.position).ToString("F2") + " state=" + GM.State + " mode=" + GM.Mode + " zoneActive=" + room.exitZone.zoneActive + " doorOpen=" + room.door.IsOpen + " panelY=" + room.door.panel.localPosition.y.ToString("F2") + " lastBlock=" + pmv.LastBlockerInfo);
        }

        // ── 第四關 ──
        Check(LevelManager.Current.Stage == 4 && GM.Data.stage == 4 && InScene(SceneKind.PastLab), "stage == 4 in the same scene");
        Check(GM.Data.lives == 3, "lives carried over to stage 4");
        yield return Seconds(2f);
        var g4 = UnityEngine.Object.FindObjectsByType<GuardAI>(FindObjectsSortMode.None).Where(g => g.gameObject.activeInHierarchy).ToList();
        Check(g4.Count == 3, "stage 4 has 3 active guards (found " + g4.Count + ")");
        var downstairs = g4.Where(g => g.transform.position.y < 1f).ToArray();
        Check(downstairs.Length == 2 && Vector3.Distance(downstairs[0].transform.position, downstairs[1].transform.position) > 10f, "ground-floor guards are separated");
        Check(g4.Count(g => g.transform.position.y > 2.5f) == 1, "one upstairs guard");
        Check(Physics.Linecast(new Vector3(17f, 1f, 38.6f), new Vector3(18.3f, 1f, 38.6f), out var doorHit)
            && doorHit.collider.name == "GroundFloorPassageDoor", "ground-floor passage closed");
        TeleportPlayer(new Vector3(12f, 0.1f, 28f));
        yield return Seconds(0.2f);
        Check(g4.Count(g => g.State == GuardState.Chase) == 0, "guards wait outside the frame room");
        TeleportPlayer(new Vector3(42f, 0.1f, 38f));
        yield return Seconds(0.2f);
        Check(g4.Count(g => g.State == GuardState.Chase) == 3, "all guards chase in the destination room");
        Shot("14_pastlab_stage4");

        // 抵達最初穿越位置 → 通關
        GM.Data.lives = Mathf.Max(GM.Data.lives, 1);
        var escape = UnityEngine.Object.FindObjectsByType<ProximityZone>(FindObjectsSortMode.None).First(z => z.name == "EscapeZone");
        Check(escape.zoneActive, "escape zone active in stage 4");
        TeleportPlayer(escape.transform.position - Vector3.up * 1.15f);
        yield return WaitScene(SceneKind.ModernLab, 60);
        Check(GM.Data.returningToGuide && GM.State == GameState.Playing, "guide epilogue before results");
        yield return Until(() => GM.State == GameState.GameClear, 20, "Game Clear after guide dialogue");

        // ── 生命歸零 → Game Over → 重新開始 ──
        GM.StartNewGame();
        yield return WaitScene(SceneKind.ModernLab, 60);
        Check(GM.Data.lives == 5 && GM.Data.stage == 1 && GM.Data.PieceCount == 1, "new game resets lives / stage / photo progress");
        UIManager.Instance.StopAllCoroutines(); UIManager.Instance.ResetTransient();
        GM.SetMode(InputMode.Gameplay);
        for (int i = 0; i < 5; i++) GM.LoseLife(LifeLossReason.WrongAnswer);
        Check(GM.State == GameState.GameOver && GM.Data.lives == 0, "5 losses → Game Over");
        yield return Seconds(0.6f);
        Shot("15_gameover");
        GM.StartNewGame();
        yield return WaitScene(SceneKind.ModernLab, 60);
        Check(GM.Data.lives == 5 && GM.State == GameState.Playing, "Restart Game → lives back to 5, playing");

        GM.ReturnToMainMenu();
        yield return WaitScene(SceneKind.MainMenu, 60);
        yield return Seconds(2.5f);
        Shot("00_mainmenu", true, Camera.main);
    }



    // ───────────── 截圖（需要有圖形裝置，不能加 -nographics）─────────────

    static void Shot(string name, bool withUI = true, Camera cam = null)
    {
        if (string.IsNullOrEmpty(shotDir)) return;
        try
        {
            if (cam == null) cam = PlayerCheckpoint.Instance != null ? PlayerCheckpoint.Instance.GetComponentInChildren<Camera>() : Camera.main;
            if (cam == null) { Debug.Log("[SMOKE] shot skipped (no camera): " + name); return; }
            const int W = 1280, H = 720;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var canvas = withUI ? UIManager.Instance.Canvas : null;
            var prevMode = RenderMode.ScreenSpaceOverlay; Camera prevCam = null; float prevDist = 0f;
            if (canvas != null)
            {
                prevMode = canvas.renderMode; prevCam = canvas.worldCamera; prevDist = canvas.planeDistance;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 0.5f;
            }
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(shotDir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = prevTarget;
            if (canvas != null) { canvas.renderMode = prevMode; canvas.worldCamera = prevCam; canvas.planeDistance = prevDist; }
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log("[SMOKE] shot " + name);
        }
        catch (Exception e) { Debug.Log("[SMOKE] shot failed " + name + ": " + e.Message); }
    }

    static void FacePlayer(Vector3 pos, float yaw)
    {
        TeleportPlayer(pos);
        PlayerCheckpoint.Instance.GetComponent<PlayerLook>().SetRotation(yaw);
    }

    static void TopDown(string name, Vector3 center, float height)
    {
        var ceilings = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(x => x.name == "Ceiling").ToList();
        foreach (var c in ceilings) c.gameObject.SetActive(false);
        var go = new GameObject("TopCam");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = center + Vector3.up * height;
        cam.transform.rotation = Quaternion.Euler(90, 0, 0);
        cam.fieldOfView = 55f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
        Shot(name, false, cam);
        UnityEngine.Object.DestroyImmediate(go);
        foreach (var c in ceilings) c.gameObject.SetActive(true);
    }

    static void TeleportPlayer(Vector3 pos)
    {
        var p = PlayerCheckpoint.Instance.transform;
        var cc = p.GetComponent<CharacterController>();
        cc.enabled = false;
        p.position = pos;
        cc.enabled = true;
    }
}

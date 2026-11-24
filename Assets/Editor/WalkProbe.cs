using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 實際用 CharacterController 沿著導航路徑走過第二關所有關鍵地點，並逐筆記錄位置（找出真正卡住的地方）。
/// 命令列：-executeMethod WalkProbe.RunBatch（不要加 -quit）。結果印在 Console（[PROBE]）。
/// </summary>
public static class WalkProbe
{
    static string sceneName = "PastLab";
    static IEnumerator<Func<bool>> script;
    static Func<bool> current;
    static bool prevOptsEnabled;
    static EnterPlayModeOptions prevOpts;
    static readonly StringBuilder report = new StringBuilder();

    public static void RunBatch()
    {
        prevOptsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        prevOpts = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-probeScene") sceneName = args[i + 1];
        EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity");
        script = Script();
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        try
        {
            if (current != null && !current()) return;
            if (!script.MoveNext()) { Finish(); return; }
            current = script.Current;
        }
        catch (Exception e) { Debug.Log("[PROBE] exception " + e); Finish(); }
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Debug.Log("[PROBE] ───── REPORT ─────\n" + report);
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () =>
        {
            EditorSettings.enterPlayModeOptionsEnabled = prevOptsEnabled;
            EditorSettings.enterPlayModeOptions = prevOpts;
            EditorApplication.Exit(0);
        };
    }

    static Func<bool> Seconds(float s) { double end = EditorApplication.timeSinceStartup + s; return () => EditorApplication.timeSinceStartup >= end; }
    static Func<bool> Until(Func<bool> c, float timeout) { double end = EditorApplication.timeSinceStartup + timeout; return () => { try { if (c()) return true; } catch { } return EditorApplication.timeSinceStartup > end; }; }

    static void Teleport(Vector3 pos)
    {
        var p = PlayerCheckpoint.Instance.transform;
        var cc = p.GetComponent<CharacterController>();
        cc.enabled = false; p.position = pos; cc.enabled = true;
    }


    /// <summary>目標點周圍（0~2.6 公尺）找一個「地面層、從 from 走得到」最近的點。物件浮在桌面上方時，直接取樣可能取到頭上的天橋。</summary>
    static Vector3 GroundTarget(Vector3 pos, Vector3 from)
    {
        Vector3 best = pos; float bestD = float.MaxValue;
        for (float rad = 0f; rad <= 2.6f; rad += 0.4f)
            for (int k = 0; k < (rad == 0f ? 1 : 12); k++)
            {
                float a = k * Mathf.PI * 2f / 12f;
                if (!NavMesh.SamplePosition(new Vector3(pos.x + Mathf.Cos(a) * rad, 0.2f, pos.z + Mathf.Sin(a) * rad), out var h, 0.6f, NavMesh.AllAreas) || h.position.y > 1.0f) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(from, h.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                float d = Vector2.Distance(new Vector2(h.position.x, h.position.z), new Vector2(pos.x, pos.z));
                if (d < bestD) { bestD = d; best = h.position; }
            }
        return best;
    }

    static IEnumerator<Func<bool>> Script()
    {
        yield return Until(() => GameManager.Instance != null && LevelManager.Current != null && PlayerCheckpoint.Instance != null && Time.frameCount > 30, 60);
        yield return Seconds(2.5f);
        UIManager.Instance.StopAllCoroutines(); UIManager.Instance.ResetTransient();
        GameManager.Instance.SetMode(InputMode.Gameplay);
        foreach (var g in UnityEngine.Object.FindObjectsByType<GuardAI>(FindObjectsSortMode.None)) g.gameObject.SetActive(false);
        yield return Seconds(0.5f);

        var pm = PlayerCheckpoint.Instance.GetComponent<PlayerMovement>();
        var cc = pm.GetComponent<CharacterController>();
        report.AppendLine("mode=" + GameManager.Instance.Mode + " timeScale=" + Time.timeScale + " cc.radius=" + cc.radius + " step=" + cc.stepOffset + " height=" + cc.height + " fixedDt=" + Time.fixedDeltaTime);

        // 診斷：隔開坦克室與大廳的那道門（x≈17.6, z≈38.6）
        Physics.SyncTransforms();
        foreach (var c in Physics.OverlapSphere(new Vector3(17.6f, 1.0f, 38.6f), 2.0f, ~0, QueryTriggerInteraction.Ignore))
            report.AppendLine("near door: " + c.name + " " + c.GetType().Name + (c is MeshCollider mc ? " convex=" + mc.convex : "") + " bounds.center=" + c.bounds.center.ToString("F2") + " size=" + c.bounds.size.ToString("F2"));
        var free = new StringBuilder("free z at door plane x=17.63 (capsule r=0.25, h 0.55..1.8): ");
        for (float z = 36.8f; z <= 40.4f; z += 0.1f)
        {
            bool blocked = Physics.CheckCapsule(new Vector3(17.63f, 0.8f, z), new Vector3(17.63f, 1.55f, z), 0.25f, ~0, QueryTriggerInteraction.Ignore);
            free.Append(blocked ? "#" : ".");
        }
        report.AppendLine(free.ToString() + "   (z 36.8 -> 40.4, step 0.1)");
        var freeLow = new StringBuilder("free z for a CROUCHED capsule (sphere at 0.8): ");
        for (float z = 36.8f; z <= 40.4f; z += 0.1f) freeLow.Append(Physics.CheckSphere(new Vector3(17.63f, 0.8f, z), 0.25f, ~0, QueryTriggerInteraction.Ignore) ? "#" : ".");
        report.AppendLine(freeLow.ToString());

        var targets = new List<(string, Vector3, float)>();
        if (sceneName == "ModernLab")
        {
            // ModernLab 沒有守衛，場景裡沒有導航網格，這裡即時烘焙一份
            NavMesh.AddNavMeshData(DDayNav.Build(GameObject.Find("Environment").transform));
            foreach (var pk in UnityEngine.Object.FindObjectsByType<PhotoPickup>(FindObjectsSortMode.None)) targets.Add(("photo half", pk.transform.position, 2.4f));
            foreach (var dc in UnityEngine.Object.FindObjectsByType<DocumentInteraction>(FindObjectsSortMode.None)) targets.Add(("doc " + dc.title, dc.transform.position, 2.4f));
            targets.Add(("guide", UnityEngine.Object.FindFirstObjectByType<GuideNPC>().transform.position, 2.4f));
        }
        else
        {
            var vp = UnityEngine.Object.FindFirstObjectByType<ViewfinderPuzzle>();
            foreach (var pk in UnityEngine.Object.FindObjectsByType<PhotoPickup>(FindObjectsSortMode.None).Where(x => x.gameObject.activeInHierarchy).OrderBy(x => x.pieceId))
                targets.Add(("piece" + (pk.pieceId + 1), pk.transform.position, 2.4f));
            foreach (var f in vp.frames) targets.Add(("frame" + (f.index + 1), new Vector3(f.transform.position.x, 0, f.transform.position.z), 3.2f));
            targets.Add(("viewpoint", vp.solutionPosition, 0.8f));
            var rm = vp.room;
            rm.RevealInstant();   // 房間在場時也要走得到：門口、控制電腦、門外
            targets.Add(("room door", rm.transform.position, 2.6f));
            targets.Add(("room console", rm.console.transform.position + rm.transform.forward * -1.6f, 1.2f));
            targets.Add(("room exit spot", rm.exitSpot.position, 1.0f));
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("Patrol_")).OrderBy(t => t.name))
                targets.Add((t.name, t.position, 1.4f));
        }

        var start = LevelManager.Current.playerStart.position;
        foreach (var (label, pos, radius) in targets)
        {
            Teleport(start);
            yield return Seconds(0.3f);
            var target = GroundTarget(pos, PlayerCheckpoint.Instance.transform.position);
            var path = new NavMeshPath();
            NavMesh.CalculatePath(PlayerCheckpoint.Instance.transform.position, target, NavMesh.AllAreas, path);
            report.AppendLine("== " + label + " path=" + path.status + " corners=" + path.corners.Length);

            int cornerIdx = 1; double t0 = Time.timeAsDouble, lastProgress = t0; float best = float.MaxValue; bool arrived = false;
            while (Time.timeAsDouble - t0 < 60)
            {
                var p = PlayerCheckpoint.Instance.transform.position;
                float flatDist = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(target.x, target.z));
                if (flatDist <= radius) { arrived = true; break; }
                if (path.corners.Length > 0)
                {
                    // 目前要追的角點：和玩家的水平距離 < 0.5 就換下一個
                    while (cornerIdx < path.corners.Length - 1 && Vector2.Distance(new Vector2(p.x, p.z), new Vector2(path.corners[cornerIdx].x, path.corners[cornerIdx].z)) < 0.5f) cornerIdx++;
                    var goal = path.corners[Mathf.Min(cornerIdx, path.corners.Length - 1)];
                    var d = goal - p; d.y = 0;
                    if (d.sqrMagnitude > 0.01f) pm.transform.rotation = Quaternion.LookRotation(d.normalized);
                }
                pm.debugInput = new Vector2(0, 1);
                if (flatDist < best - 0.3f) { best = flatDist; lastProgress = Time.timeAsDouble; }
                if (Time.timeAsDouble - lastProgress > 4.0)
                {
                    report.AppendLine("   STUCK at " + p.ToString("F2") + " (corner " + cornerIdx + "/" + path.corners.Length + " -> " + path.corners[Mathf.Min(cornerIdx, path.corners.Length - 1)].ToString("F2") + ") dist-to-target=" + flatDist.ToString("F1") + " | last hit: " + pm.LastBlockerInfo);
                    break;
                }
                yield return () => true;
            }
            pm.debugInput = null;
            if (arrived) report.AppendLine("   ok in " + (Time.timeAsDouble - t0).ToString("F1") + " game-seconds");
        }
    }
}

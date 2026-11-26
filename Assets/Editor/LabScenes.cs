using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 以「地下實驗室」模型為環境的場景：ModernLab（第一關，爆炸後）、PastLab（第二、四關，爆炸前）。
/// 所有物件位置都用導航網格驗證過（走得到、不卡牆），ViewFinder 相框的位置由程式在建置時自動求解。
/// </summary>
public static partial class SceneBuilder
{
    const string ModelDir = "Assets/Prefabs/Models/";
    const string NavAssetPath = "Assets/Prefabs/Environment/LabNavMesh.asset";
    static readonly List<string> warnings = new List<string>();

    // ═════════════════════════════ 導航 / 取點工具 ═════════════════════════════

    static GameObject LoadModel(string name)
    {
        var g = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + name + ".prefab");
        if (g == null) throw new Exception("找不到模型 Prefab：" + name + "（請先執行 Tools/D-Day/0. Prepare Model Prefabs）");
        return g;
    }

    static Material PhotoUnlit
    {
        get
        {
            const string path = "Assets/Materials/PhotoUnlit.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Unlit/Texture")); AssetDatabase.CreateAsset(m, path); }
            return m;
        }
    }

    static Material FrameGlow => Mat("FrameGlow", new Color(0.1f, 0.15f, 0.2f), 0.5f, new Color(0.15f, 0.3f, 0.4f));

    static NavMeshData BakeNav(Transform env, string savePath)
    {
        NavMesh.RemoveAllNavMeshData();
        Physics.SyncTransforms();
        var data = DDayNav.Build(env);
        if (savePath != null)
        {
            AssetDatabase.DeleteAsset(savePath);
            AssetDatabase.CreateAsset(data, savePath);
        }
        NavMesh.AddNavMeshData(data);
        return data;
    }

    /// <summary>取得 (x,z) 附近導航網格上的點（找不到回傳 null）。</summary>
    static Vector3? Snap(float x, float z, float tol = 1.5f)
    {
        if (NavMesh.SamplePosition(new Vector3(x, 0.3f, z), out var hit, tol, NavMesh.AllAreas)
            && Mathf.Abs(hit.position.y) < 1.0f
            && new Vector2(hit.position.x - x, hit.position.z - z).magnitude <= tol)
            return hit.position;
        return null;
    }

    static bool Reach(Vector3 from, Vector3 to)
    {
        var path = new NavMeshPath();
        return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    /// <summary>必要的點：先試指定座標，不行就在附近螺旋找；仍失敗就中止建置。</summary>
    static bool avoidRoom;   // PastLab：擺設不能放進隱藏密室長出來的範圍

    static Vector3 Need(string what, float x, float z, Vector3? mustReach = null, float minFrom = 0f, Vector3? awayFrom = null)
    {
        for (float r = 0; r <= 6f; r += 1f)
            for (int k = 0; k < (r == 0 ? 1 : 12); k++)
            {
                float a = k * Mathf.PI * 2f / 12f;
                var p = Snap(x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r, 0.8f);
                if (!p.HasValue) continue;
                if (avoidRoom && InRoomFootprint(p.Value, 1.0f)) continue;
                if (mustReach.HasValue && !Reach(mustReach.Value, p.Value)) continue;
                if (awayFrom.HasValue && Vector3.Distance(awayFrom.Value, p.Value) < minFrom) continue;
                return p.Value;
            }
        throw new Exception("[D-Day] 找不到可用位置：" + what + " 附近 (" + x + "," + z + ")");
    }

    // ═════════════════════════════ 共用 Prefab ═════════════════════════════

    static GameObject MakeViewfinderFrame()
    {
        var root = new GameObject("ViewfinderFrame");
        var bc = root.AddComponent<BoxCollider>();
        bc.size = new Vector3(1.05f, 1.05f, 0.3f);

        var vis = Empty("FrameVisual", root.transform);
        const float t = 0.07f;
        var borders = new List<Renderer>();
        foreach (var b in new[]
        {
            Box("Top", vis.transform, new Vector3(0, 0.5f + t / 2, 0), new Vector3(1f + 2 * t, t, 0.1f), FrameGlow, false),
            Box("Bottom", vis.transform, new Vector3(0, -0.5f - t / 2, 0), new Vector3(1f + 2 * t, t, 0.1f), FrameGlow, false),
            Box("Left", vis.transform, new Vector3(-0.5f - t / 2, 0, 0), new Vector3(t, 1f, 0.1f), FrameGlow, false),
            Box("Right", vis.transform, new Vector3(0.5f + t / 2, 0, 0), new Vector3(t, 1f, 0.1f), FrameGlow, false),
        }) borders.Add(b.GetComponent<Renderer>());
        Slot(vis, "PhotoFrame", "第二關相框外框（會被縮放成不同大小；照片面朝 -Z）");

        // 照片面：Quad 的正面朝 -Z，所以玩家站在 -Z 方向看
        var pic = GameObject.CreatePrimitive(PrimitiveType.Quad);
        pic.name = "Picture";
        pic.transform.SetParent(root.transform, false);
        pic.transform.localPosition = new Vector3(0, 0, 0.03f);
        pic.transform.localScale = Vector3.one;
        pic.GetComponent<Renderer>().sharedMaterial = PhotoBlank;
        UnityEngine.Object.DestroyImmediate(pic.GetComponent<Collider>());

        // 編號
        var labelGo = Empty("Number", root.transform, new Vector3(-0.38f, 0.38f, -0.06f));
        var tm = labelGo.AddComponent<TextMesh>();
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.fontSize = 100; tm.characterSize = 0.03f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.text = "01"; tm.color = new Color(0.6f, 0.95f, 1f);
        labelGo.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;

        var glow = PointLight("Glow", root.transform, new Vector3(0, 0, -0.9f), new Color(0.5f, 0.9f, 1f), 1.2f, 5f);

        var vf = root.AddComponent<ViewfinderFrame>();
        vf.picture = pic.GetComponent<Renderer>();
        vf.glow = glow;
        vf.pictureMaterial = PhotoUnlit;
        vf.borders = borders.ToArray();
        return SavePrefab(root, "Assets/Prefabs/Items/ViewfinderFrame.prefab");
    }

    /// <summary>放一個整理好的模型 Prefab 到導航網格上（位置會被驗證為走得到）。</summary>
    static GameObject PlaceModel(Transform parent, string model, float x, float z, float yaw, Vector3? mustReach, string slotId, string note)
    {
        var pos = Need(slotId, x, z, mustReach);
        var g = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel(model), parent);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0, yaw, 0);
        AddSlotLog(slotId, g.name, note);
        return g;
    }


    /// <summary>center 周圍（1~2.6 公尺）是否有從 from 走得到的點？（互動距離 3.2 m，站在桌子旁邊就能拿到東西）</summary>
    static bool ReachableNear(Vector3 center, Vector3 from, out Vector3 found)
    {
        for (float rad = 1.0f; rad <= 2.6f; rad += 0.4f)
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI * 2f / 16f;
                var sp = Snap(center.x + Mathf.Cos(a) * rad, center.z + Mathf.Sin(a) * rad, 0.5f);
                if (sp.HasValue && Reach(from, sp.Value)) { found = sp.Value; return true; }
            }
        found = default;
        return false;
    }

    static void PlaceBench(Transform env, float x, float z, float yaw, Vector3? mustReach, out Vector3 pos)
    {
        pos = Need("bench", x, z, mustReach);
        var b = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel("M_Bench"), env);
        b.transform.position = pos;
        b.transform.rotation = Quaternion.Euler(0, yaw, 0);
        AddSlotLog("LabBench", b.name, "實驗桌（Table1 混凝土長椅）");
    }

    static void AddSlotLog(string id, string obj, string note) { slotLog.Add(new[] { container, id, obj, note }); }

    // ═════════════════════════════ ModernLab（第一關）═════════════════════════════

    static void BuildModernLab()
    {
        var sc = NewScene("ModernLab");
        warnings.Clear();
        SetupRender(new Color(0.36f, 0.35f, 0.36f), new Color(0.22f, 0.18f, 0.18f), 0.010f);   // 現代：灰白色，紅色警示燈與煙霧營造災後氣氛
        var rng = new System.Random(11);

        var env = Empty("Environment", null);
        var lab = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel("M_Lab_Modern"), env.transform);
        AddSlotLog("UndergroundLab", "Environment", "地下實驗室（現代 / 爆炸後：灰白色）");
        BakeNav(env.transform, null);

        var p0 = Need("起點", 24, 38);
        Vector3 pos;
        // 實驗桌（含另一半照片那張）
        PlaceBench(env.transform, 6f, 40f, 90f, p0, out pos);
        PlaceBench(env.transform, 14f, 30f, 0f, p0, out pos);
        PlaceBench(env.transform, 27f, 43f, 0f, p0, out var docBench1);
        PlaceBench(env.transform, 41f, 36f, 90f, p0, out var docBench2);
        PlaceBench(env.transform, 9f, 23f, 0f, p0, out var docBench3);

        // 科幻貨箱（掩體）
        int ci = 0;
        foreach (var c in new[] { (20f, 41f), (36f, 33f), (12f, 36f), (44f, 40f), (7f, 30f), (30f, 44f) })
        {
            var cp = Need("crate", c.Item1, c.Item2, p0);
            var crate = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel("SciFi_Crate" + (ci % 4)), env.transform);
            crate.transform.position = cp; crate.transform.rotation = Quaternion.Euler(0, rng.Next(0, 360), 0);
            AddSlotLog("Crate", crate.name, "貨箱（SciFi pack）");
            ci++;
        }

        // table2（Stol Lab 抽屜桌）與 table3（Sci-Fi Table）
        PlaceModel(env.transform, "M_StolLab", 17f, 44.5f, 180f, p0, "StolLab", "實驗室抽屜桌（table2）");
        PlaceModel(env.transform, "M_StolLab", 45.5f, 36f, 90f, p0, "StolLab", "實驗室抽屜桌（table2）");
        PlaceModel(env.transform, "M_StolLab", 3.5f, 30f, 270f, p0, "StolLab", "實驗室抽屜桌（table2）");
        PlaceModel(env.transform, "M_SciFiTableB", 31f, 43f, 0f, p0, "SciFiTable", "科幻長桌（table3）");
        PlaceModel(env.transform, "M_SciFiTableB", 40f, 31.5f, 0f, p0, "SciFiTable", "科幻長桌（table3）");

        // 爆炸碎片
        foreach (var d in new[] { (9f, 33f), (4f, 27f), (35f, 36f), (42f, 43f), (28f, 33f), (11f, 42f) })   // 避開 x 14~21 的門口與起點
            Debris(env.transform, rng, Need("debris", d.Item1, d.Item2, p0), 3.5f, 9);

        // 第二次烘焙：包含桌子、貨箱、碎片（守衛 / 驗證用）
        BakeNav(env.transform, null);
        AuditClearance("ModernLab", p0);
        var upperPhoto = new Vector3(15f, 3.26f, 19f);
        RequirePhotoRoute(p0, upperPhoto);
        RequireGroundRouteNear("ModernLab 文件 1", p0, docBench1);
        RequireGroundRouteNear("ModernLab 文件 2", p0, docBench2);
        RequireGroundRouteNear("ModernLab 文件 3", p0, docBench3);

        var items = Empty("Interactives", null);
        // 文件
        MakeDocument(items.transform, docBench1 + Vector3.up * 0.98f, "實驗日誌（殘頁）", "「……第三冷卻迴路再度跳脫。緊急停止系統的回應延遲已超過安全值，但上層堅持繼續實驗。」");
        MakeDocument(items.transform, docBench2 + Vector3.up * 0.98f, "警報紀錄", "「17:42 能源參數異常。17:44 緊急停止無回應。17:45 ……爆炸。」");
        MakeDocument(items.transform, docBench3 + Vector3.up * 0.98f, "撕破的備忘錄", "「如果有人看到這個——事故前，有人把緊急停止系統的設定改掉了。真相在『過去』。」");

        // 另外半張照片
        var half = Inst(photoPiecePrefab, items.transform, upperPhoto);
        half.name = "PhotoHalf_Other";
        var pp = half.GetComponent<PhotoPickup>();
        pp.pieceId = 1;
        pp.displayName = "另外半張照片";

        // Guide
        var gpos = Need("Guide", 21.5f, 36f, p0);
        var guide = Inst(guidePrefab, items.transform, gpos, Mathf.Atan2(32f - gpos.x, 38f - gpos.z) * Mathf.Rad2Deg);
        var gn = guide.GetComponent<GuideNPC>();
        gn.hintLines = new[]
        {
            "另外半張照片應該在西邊那間大廳——有很多圓柱槽的地方，找找看發光的東西。",
            "靠近照片後瞄準它，按 E 拾取（手把按 A）。兩半收齊後會自動進入拼接。"
        };

        // 燈光：紅色警示燈 + 壞掉的日光燈
        foreach (var a in new[] { (14f, 38f), (32f, 38f), (42f, 40f), (8f, 28f), (24f, 44f) })
            MakeAlarm(items.transform, Need("alarm", a.Item1, a.Item2) + Vector3.up * 5f, Color.red, 3.5f, 16f, false, 2f + (float)rng.NextDouble() * 1.5f);
        foreach (var a in new[] { (20f, 36f), (36f, 40f), (6f, 36f) })
            MakeAlarm(items.transform, Need("flicker", a.Item1, a.Item2) + Vector3.up * 5f, new Color(0.85f, 0.92f, 1f), 2.2f, 12f, true, 4f);
        foreach (var s in new[] { (18f, 40f), (30f, 36f), (8f, 32f), (40f, 38f) })
            SmokeFx(items.transform, Need("smoke", s.Item1, s.Item2) + Vector3.up * 0.1f);

        var start = Empty("PlayerStart", null, new Vector3(38f, 0.10000019f, 38f), 270f);
        Player(start.transform);

        AddManagers("LevelManager", SceneKind.ModernLab, start.transform, out var lm, out var level);
        var ctrl = level.AddComponent<ModernLabController>();
        ctrl.guide = gn;
        ctrl.laboratory = lab.transform;
        const string finishes = "Assets/Models/Environment/UndergroundLab/Materials/";
        ctrl.modernFinish = AssetDatabase.LoadAssetAtPath<Material>(finishes + "Lab_Modern.mat");
        ctrl.restoredFinish = AssetDatabase.LoadAssetAtPath<Material>(finishes + "Lab_Past.mat");
        level.AddComponent<PhotoPuzzleManager>();

        Report("ModernLab");
        SaveScene(sc, "ModernLab");
    }


    // ═════════════════════════════ 通行性檢查（找出「過不去」的地方）═════════════════════════════

    /// <summary>
    /// 沿著導航網格上「從起點到全地圖各處」的路徑，每 0.2 公尺取樣，用真實玩家的碰撞尺寸檢查：
    ///   STEP   地面突然升高超過階梯高度（0.5 m）→ 需要跳（≤1.0 m）或根本過不去
    ///   CROUCH 站著的身體（0.55~1.8 m）會撞到東西，但蹲著可以過 → 需要蹲
    ///   BLOCKED 站著、蹲著都撞到 → 導航說走得到但玩家過不去（要修）
    /// </summary>
    static int AuditClearance(string scene, Vector3 p0)
    {
        const float r = 0.25f;
        var dests = new List<Vector3>();
        for (float x = 2; x <= 48; x += 2.5f)
            for (float z = 19; z <= 46; z += 2.5f)
            {
                var sp = Snap(x, z, 0.6f);
                if (sp.HasValue && Reach(p0, sp.Value)) dests.Add(sp.Value);
            }

        var found = new Dictionary<string, string>();
        var path = new NavMeshPath();
        var buf = new Collider[8];
        int blocked = 0;
        foreach (var d in dests)
        {
            if (!NavMesh.CalculatePath(p0, d, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            var c = path.corners;
            float prevGround = float.NaN;
            for (int i = 1; i < c.Length; i++)
            {
                float seg = Vector3.Distance(c[i - 1], c[i]);
                if (seg < 0.01f) continue;
                for (float t = 0; t <= seg; t += 0.2f)
                {
                    var pt = Vector3.Lerp(c[i - 1], c[i], t / seg);
                    float gy = pt.y;
                    if (Physics.Raycast(pt + Vector3.up * 0.6f, Vector3.down, out var gh, 1.2f, ~0, QueryTriggerInteraction.Ignore)) gy = gh.point.y;

                    string key = null, desc = null;
                    if (!float.IsNaN(prevGround) && gy - prevGround > 0.32f)
                    {
                        float dy = gy - prevGround;   // 超過 0.3（玩家的步高）就要跳
                        key = (dy <= 1.0f ? "STEP-JUMP " : "STEP-BLOCKED ") + Mathf.Floor(pt.x) + "," + Mathf.Floor(pt.z);
                        desc = "地面突然升高 " + dy.ToString("F2") + " m";
                    }
                    prevGround = gy;

                    if (key == null)
                    {
                        var a0 = new Vector3(pt.x, gy + 0.55f + r, pt.z);
                        var a1 = new Vector3(pt.x, gy + 1.8f - r, pt.z);
                        int n = Physics.OverlapCapsuleNonAlloc(a0, a1, r, buf, ~0, QueryTriggerInteraction.Ignore);
                        if (n > 0)
                        {
                            bool crouchOk = !Physics.CheckSphere(new Vector3(pt.x, gy + 0.8f, pt.z), r, ~0, QueryTriggerInteraction.Ignore);
                            key = (crouchOk ? "CROUCH " : "BLOCKED ") + Mathf.Floor(pt.x) + "," + Mathf.Floor(pt.z);
                            desc = "身體撞到 " + buf[0].name + "（高度約 " + (buf[0].bounds.min.y - gy).ToString("F2") + "~" + (buf[0].bounds.max.y - gy).ToString("F2") + " m）";
                        }
                    }
                    if (key != null && !found.ContainsKey(key)) found[key] = desc + " @ (" + pt.x.ToString("F1") + "," + pt.z.ToString("F1") + ")";
                }
            }
        }

        blocked = found.Count(kv => kv.Key.StartsWith("BLOCKED") || kv.Key.StartsWith("STEP-BLOCKED"));
        Debug.Log("[D-Day][" + scene + "] 通行性檢查：驗證了 " + dests.Count + " 條路徑；需要跳 " + found.Count(kv => kv.Key.StartsWith("STEP-JUMP"))
                  + " 處、需要蹲 " + found.Count(kv => kv.Key.StartsWith("CROUCH")) + " 處、過不去 " + blocked + " 處");
        foreach (var kv in found.OrderBy(k => k.Key).Take(60)) Debug.Log("[D-Day][" + scene + "]   " + kv.Key + " : " + kv.Value);
        return blocked;
    }


    /// <summary>
    /// 關鍵地點的路線必須走地面：如果最短路線要繞到上層天橋（y &gt; 1.2），通常代表地面的門口 / 通道被擋住了
    /// （例如被瓦礫、貨箱堵住），玩家實際上會繞很遠或走不到。
    /// </summary>
    static void RequireGroundRoute(string what, Vector3 from, Vector3 target)
    {
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(from, target, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            throw new Exception("[D-Day] " + what + "：走不到");
        foreach (var c in path.corners)
            if (c.y > 1.2f) throw new Exception("[D-Day] " + what + "：最短路線要繞到上層天橋（y=" + c.y.ToString("F1") + " 於 " + c.x.ToString("F1") + "," + c.z.ToString("F1") + "），地面通道可能被擋住了");
    }

    static void RequirePhotoRoute(Vector3 start, Vector3 pickup)
    {
        if (!NavMesh.SamplePosition(pickup, out var hit, 0.85f, NavMesh.AllAreas) || !Reach(start, hit.position))
            throw new Exception("找不到前往照片碎片的路線：" + pickup);
    }

    static void RequireGroundRouteNear(string what, Vector3 from, Vector3 center)
    {
        if (!ReachableNear(center, from, out var near)) throw new Exception("[D-Day] " + what + "：周圍走不到");
        RequireGroundRoute(what, from, near);
    }

    static void Report(string scene)
    {
        foreach (var w in warnings) Debug.LogWarning("[D-Day][" + scene + "] " + w);
    }

    // ═════════════════════════════ PastLab（第二、四關）═════════════════════════════

    class FramePlan
    {
        public Vector3 viewer;                // 腳下位置
        public float yaw;
        public Vector3[] center = new Vector3[4];
        public float[] size = new float[4];
        public float[] dist = new float[4];
    }


    // 隱藏密室長出來的位置：坦克室（地板 x 0.5~17.5、z 20~44）中央。入口朝北，面向站在北邊 ViewFinder 視角上的玩家。
    // 房間的本地座標：原點 = 門口中心、+Z 往室內（轉 180° 後往 -Z 世界方向）。
    static readonly Vector3 RoomEntrance = new Vector3(8.35f, 0f, 39.5f);
    const float RoomYaw = 180f;

    static Vector3 RoomPoint(float lx, float ly, float lz)
    {
        return RoomEntrance + Quaternion.Euler(0, RoomYaw, 0) * new Vector3(lx, ly, lz);
    }

    static bool InRoomFootprint(Vector3 world, float margin)
    {
        var l = Quaternion.Euler(0, -RoomYaw, 0) * (world - RoomEntrance);
        return l.x > -RoomW / 2f - margin && l.x < RoomW / 2f + margin && l.z > -margin && l.z < RoomD + margin;
    }

    /// <summary>把房間範圍（含周圍與門外走道）內的擺設拿掉，騰出一塊空地。結構（牆、地板、支撐柱、樓梯、欄杆）不動。</summary>
    static int ClearLabProps(GameObject lab)
    {
        var c0 = RoomPoint(-RoomW / 2f - 1.2f, 0, -3f);
        var c1 = RoomPoint(RoomW / 2f + 1.2f, 0, RoomD + 1.2f);
        var region = new Bounds(new Vector3((c0.x + c1.x) / 2f, 1.5f, (c0.z + c1.z) / 2f), new Vector3(Mathf.Abs(c1.x - c0.x), 3f, Mathf.Abs(c1.z - c0.z)));
        var removed = new List<string>();
        foreach (var col in lab.GetComponentsInChildren<Collider>())
        {
            string n = col.gameObject.name;
            if (!(n.StartsWith("props_") || n.StartsWith("capsule_"))) continue;
            if (n.Contains("column") || n.Contains("stairs") || n.Contains("rail")) continue;
            var bnd = col.bounds;
            if (bnd.min.y > 2.4f || !bnd.Intersects(region)) continue;
            col.gameObject.SetActive(false);
            removed.Add(n);
        }
        Debug.Log("[D-Day] 為隱藏密室清出空地：移除 " + removed.Count + " 個擺設");
        return removed.Count;
    }

    /// <summary>烘焙導航網格時房間要「在場」（牆會擋住守衛、門打開）；烘焙完再收起來。</summary>
    static NavMeshData BakeWithRoom(RoomRefs rr, Transform env, string savePath)
    {
        rr.body.SetActive(true);
        rr.doorPanel.localPosition += rr.door.openOffset;
        Physics.SyncTransforms();
        var nav = BakeNav(env, savePath);
        rr.doorPanel.localPosition -= rr.door.openOffset;
        rr.body.SetActive(false);
        Physics.SyncTransforms();
        return nav;
    }

    static void BuildPastLab()
    {
        var sc = NewScene("PastLab");
        warnings.Clear();
        SetupRender(new Color(0.2f, 0.26f, 0.36f), new Color(0.1f, 0.16f, 0.26f), 0.005f);   // 過去：深色鋼藍的乾淨實驗室，藍白色燈光
        var rng = new System.Random(5);

        var env = Empty("Environment", null);
        var lab = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel("M_Lab_Past"), env.transform);
        AddSlotLog("UndergroundLab", "Environment", "地下實驗室（過去 / 爆炸前：深色鋼藍）");

        // 隱藏密室：先把它長出來的那塊地清乾淨，房間建好後擺進去（平常是關閉的）
        ClearLabProps(lab);
        var rr = BuildHiddenRoom(env.transform);
        rr.root.transform.position = RoomEntrance;
        rr.root.transform.rotation = Quaternion.Euler(0, RoomYaw, 0);
        avoidRoom = true;
        BakeWithRoom(rr, env.transform, null);

        var p0 = Need("大廳導航基準點", 24, 38);
        var arrival = Need("升降台東側穿越點", 38, 38, p0);
        RequireGroundRoute("升降台東側穿越點", p0, arrival);

        // 實驗桌 + 四張照片碎片的位置（分散在不同區域）
        var pieceSpots = new[] { (5f, 41f), (6f, 24f), (44f, 43f), (45f, 33f) };
        var pieceBase = new Vector3[4];
        for (int i = 0; i < 4; i++)
            PlaceBench(env.transform, pieceSpots[i].Item1, pieceSpots[i].Item2, i % 2 == 0 ? 90f : 0f, p0, out pieceBase[i]);
        PlaceBench(env.transform, 16f, 31f, 90f, p0, out _);
        PlaceBench(env.transform, 34f, 41f, 90f, p0, out _);

        int ci = 0;
        foreach (var c in new[] { (20f, 41f), (36f, 33f), (12f, 42f), (41f, 40f), (16f, 28f), (31f, 44f), (14f, 24f) })
        {
            var cp = Need("crate", c.Item1, c.Item2, p0);
            var crate = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel("SciFi_Crate" + (ci % 4)), env.transform);
            crate.transform.position = cp; crate.transform.rotation = Quaternion.Euler(0, rng.Next(0, 360), 0);
            AddSlotLog("Crate", crate.name, "貨箱（SciFi pack）");
            ci++;
        }

        PlaceModel(env.transform, "M_StolLab", 17f, 44.5f, 180f, p0, "StolLab", "實驗室抽屜桌（table2）");
        PlaceModel(env.transform, "M_StolLab", 45.5f, 36f, 90f, p0, "StolLab", "實驗室抽屜桌（table2）");
        PlaceModel(env.transform, "M_StolLab", 12.5f, 43.6f, 180f, p0, "StolLab", "實驗室抽屜桌（table2）");
        PlaceModel(env.transform, "M_SciFiTableB", 31f, 43f, 0f, p0, "SciFiTable", "科幻長桌（table3）");
        PlaceModel(env.transform, "M_SciFiTableB", 40f, 31.5f, 0f, p0, "SciFiTable", "科幻長桌（table3）");

        // 第二次烘焙（含家具與房間），存成資產；執行時直接載入
        var nav = BakeWithRoom(rr, env.transform, NavAssetPath);
        env.AddComponent<RuntimeNavMesh>().environmentRoot = env.transform;
        env.GetComponent<RuntimeNavMesh>().bakedData = nav;

        // 房間關閉的狀態下求解相框的位置（房間在場會擋住視線檢查）
        var plan = SolveFrames(p0);
        if (plan == null) throw new Exception("[D-Day] 找不到適合擺放 ViewFinder 相框的視角（實驗室內沒有足夠的開闊空間）。");

        var items = Empty("Interactives", null);
        var it = items.transform;
        Inst(portalPrefab, it, arrival);

        // 四塊碎片
        var pickupPositions = new[] { new Vector3(10.65f, 3.84f, 6.55f), new Vector3(30f, 3.25f, 49f), pieceBase[2] + Vector3.up * 1.15f, new Vector3(68f, 0.75f, 35f) };
        var pieces = new List<GameObject>();
        for (int i = 0; i < 4; i++)
        {
            var go = Inst(photoPiecePrefab, it, pickupPositions[i]);
            go.name = "PhotoPiece_0" + (i + 1);
            var pp = go.GetComponent<PhotoPickup>();
            pp.pieceId = i;
            pp.displayName = "照片碎片 " + (i + 1).ToString("00");
            pieces.Add(go);
        }

        // 四個相框（ViewFinder）+ 支架
        var frames = new ViewfinderFrame[4];
        var framesRoot = Empty("ViewfinderFrames", null);
        var poles = new List<GameObject>();
        var bases = new List<GameObject>();
        for (int i = 0; i < 4; i++)
        {
            var f = (GameObject)PrefabUtility.InstantiatePrefab(frameVfPrefab, framesRoot.transform);
            f.name = "Frame_0" + (i + 1);
            f.transform.position = plan.center[i];
            f.transform.rotation = Quaternion.LookRotation(plan.center[i] - (plan.viewer + Vector3.up * 1.65f), Vector3.up);
            f.transform.localScale = Vector3.one * plan.size[i];
            var vf = f.GetComponent<ViewfinderFrame>();
            vf.index = i;
            f.transform.Find("Number").GetComponent<TextMesh>().text = (i + 1).ToString("00");

            // 支架：從地面直立到相框下緣
            float bottom = plan.center[i].y - plan.size[i] * 0.5f;
            var pole = Prim(PrimitiveType.Cylinder, "Pole_0" + (i + 1), framesRoot.transform,
                            new Vector3(plan.center[i].x, bottom * 0.5f, plan.center[i].z), new Vector3(0.18f, bottom * 0.5f, 0.18f), DarkMat);
            var pbase = Prim(PrimitiveType.Cylinder, "PoleBase_0" + (i + 1), framesRoot.transform, new Vector3(plan.center[i].x, 0.03f, plan.center[i].z), new Vector3(0.9f, 0.03f, 0.9f), Metal, false);
            vf.pole = pole;
            vf.poleBase = pbase;
            frames[i] = vf;
            poles.Add(pole);
        }

        // 支架是實心障礙物：放進環境根目錄後重新烘焙導航（守衛會繞開、玩家路徑也會避開），再驗證關鍵地點仍然走得到
        foreach (var pole in poles) pole.transform.SetParent(env.transform, true);
        foreach (var fv in frames) fv.transform.SetParent(env.transform, true);
        nav = BakeWithRoom(rr, env.transform, NavAssetPath);
        env.GetComponent<RuntimeNavMesh>().bakedData = nav;
        avoidRoom = false;
        for (int i = 0; i < 4; i++)
        {
            if (!ReachableNear(pieceBase[i], p0, out _))
            {
                var dbg = new System.Text.StringBuilder("[D-Day][debug] piece " + (i + 1) + " base " + pieceBase[i] + " p0 " + p0 + System.Environment.NewLine);
                foreach (var q in new[] { (16f, 40f), (16f, 36f), (16f, 31f), (16f, 27f), (15f, 26f), (12f, 26f), (8f, 26f), (6f, 26f), (2f, 36f), (2f, 30f), (2f, 26f), (6f, 29f), (9f, 28.5f), (6f, 41f), (8f, 42f) })
                {
                    var sp = Snap(q.Item1, q.Item2, 0.6f);
                    dbg.AppendLine("  (" + q.Item1 + "," + q.Item2 + ") nav=" + (sp.HasValue ? sp.Value.ToString("F1") + " reach=" + Reach(p0, sp.Value) : "none"));
                }
                Debug.Log(dbg.ToString());
            }
            if (!ReachableNear(pieceBase[i], p0, out _)) throw new Exception("[D-Day] 加入相框支架後，照片碎片 " + (i + 1) + " 的位置走不到了");
            var bs = Snap(plan.center[i].x, plan.center[i].z, 3.5f);
            if (!bs.HasValue || !Reach(p0, bs.Value)) throw new Exception("[D-Day] 加入相框支架後，相框 " + (i + 1) + " 的支架底部走不到了");
        }
        AuditClearance("PastLab", p0);
        for (int i = 0; i < 4; i++)
        {
            if (i == 0)
                RequirePhotoRoute(new Vector3(13f, 3.05f, 12f), new Vector3(11.5f, 3.05f, 6.55f)); // 診療桌旁的拾取站位
            else if (i == 2) RequireGroundRouteNear("PastLab 照片碎片 3", p0, pieceBase[i]);
            else if (i == 3)
            {
                // 東翼門檻把兩區導航網格分開；從東翼室內檢查碎片的拾取路線。
                RequirePhotoRoute(new Vector3(58f, 0.55f, 40f), pickupPositions[i]);
            }
            else RequirePhotoRoute(p0, pickupPositions[i]);
            RequireGroundRouteNear("PastLab 相框 " + (i + 1) + " 支架", p0, new Vector3(plan.center[i].x, 0f, plan.center[i].z));
        }
        RequireGroundRoute("PastLab ViewFinder 視角", p0, Snap(plan.viewer.x, plan.viewer.z, 0.8f) ?? plan.viewer);
        var vs = Snap(plan.viewer.x, plan.viewer.z, 0.8f);
        if (!vs.HasValue || !Reach(p0, vs.Value)) throw new Exception("[D-Day] 加入相框支架後，ViewFinder 的視角點走不到了");

        // 隱藏密室：門口、控制電腦、門外都要從起點走得到（地面層）
        var doorOutside = Snap(RoomPoint(0, 0, -1.5f).x, RoomPoint(0, 0, -1.5f).z, 0.8f);
        var consoleStand = Snap(RoomPoint(0, 0, 6.6f).x, RoomPoint(0, 0, 6.6f).z, 0.8f);
        var exitStand = Snap(RoomPoint(0, 0, -3.5f).x, RoomPoint(0, 0, -3.5f).z, 0.8f);
        if (!doorOutside.HasValue || !Reach(p0, doorOutside.Value)) throw new Exception("[D-Day] 隱藏密室的門口走不到");
        if (!consoleStand.HasValue || !Reach(p0, consoleStand.Value)) throw new Exception("[D-Day] 隱藏密室裡的控制電腦走不到");
        if (!exitStand.HasValue || !Reach(p0, exitStand.Value)) throw new Exception("[D-Day] 隱藏密室門外的站位走不到");
        RequireGroundRoute("PastLab 隱藏密室門口", p0, doorOutside.Value);
        RequireGroundRoute("PastLab 隱藏密室控制電腦", p0, consoleStand.Value);

        var vp = items.AddComponent<ViewfinderPuzzle>();
        vp.frames = frames;
        vp.room = rr.room;
        vp.solutionPosition = plan.viewer;
        vp.solutionYaw = plan.yaw;

        // 隱藏密室要在 Interactives 之外（它在 Environment 底下，已經擺好位置）；房間門外的站位 = 第四關起點 / 重生點
        var start4 = rr.exitSpot;

        // 第四關終點：最初穿越位置
        var escapeZone = MakeZone("EscapeZone", it, arrival + Vector3.up * 1.2f, new Vector3(5f, 4f, 5f));

        // 第四關警報燈
        var alarms = new List<AlarmLight>();
        foreach (var a in new[] { (24f, 41f), (38f, 37f), (10f, 33f), (30f, 33f) })
            alarms.Add(MakeAlarm(it, Need("alarm", a.Item1, a.Item2) + Vector3.up * 5f, Color.red, 4f, 16f, false, 4f));

        // 一般照明
        foreach (var l in new[] { (24f, 38f), (36f, 38f), (44f, 40f), (28f, 33f), (10f, 38f), (8f, 28f), (14f, 34f), (40f, 34f) })
            PointLight("Lamp", it, Need("lamp", l.Item1, l.Item2) + Vector3.up * 5.5f, new Color(0.6f, 0.82f, 1f), 2.4f, 19f);
        var sun = new GameObject("SoftLight").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 0.4f; sun.color = new Color(0.6f, 0.75f, 1f); sun.shadows = LightShadows.None;
        sun.transform.rotation = Quaternion.Euler(60, 30, 0);

        // 守衛
        var guardsRoot = Empty("Guards", null);
        var patrolRoot = Empty("PatrolPoints", null);
        var wp = new List<Vector3>();
        avoidRoom = true;
        foreach (var q in new[] { (6f, 38f), (6f, 26f), (13f, 28f), (20f, 35f), (26f, 43f), (38f, 43f), (45f, 40f), (44f, 34f), (34f, 33f), (24f, 34f) })
        {
            var p = Need("patrol", q.Item1, q.Item2, p0);
            if (wp.Count == 0 || Reach(wp[wp.Count - 1], p)) wp.Add(p);
        }
        avoidRoom = false;
        var patrolTf = wp.Select((p, i) => Empty("Patrol_" + i, patrolRoot.transform, p).transform).ToArray();

        var g2 = Inst(guardPrefab, guardsRoot.transform, wp[0]);
        g2.name = "Guard_Stage2";
        g2.GetComponent<GuardAI>().patrolPoints = patrolTf;

        var g4 = new List<GameObject>();
        var posts = new[] { (45f, 43f), (34f, 49f), (36f, 36f) };
        for (int i = 0; i < posts.Length; i++)
        {
            Vector3 spawn;
            if (i == 1)
            {
                if (!NavMesh.SamplePosition(new Vector3(posts[i].Item1, 3.05f, posts[i].Item2), out var upper, 0.8f, NavMesh.AllAreas) || !Reach(p0, upper.position))
                    throw new Exception("找不到可通行的二樓守衛站位");
                spawn = upper.position;
            }
            else spawn = Need("第四關一樓守衛", posts[i].Item1, posts[i].Item2, p0);
            var g = Inst(guardPrefab, guardsRoot.transform, spawn, 90f * i);
            g.name = "Guard_Stage4_" + (i + 1);
            var ai = g.GetComponent<GuardAI>();
            ai.startInChase = true; ai.alwaysKnowsPlayer = true; ai.chaseSpeed = 4.4f;
            g.SetActive(false);
            g4.Add(g);
        }

        // 玩家
        var start = Empty("PlayerStart", null, arrival + Vector3.up * 0.05f, 270f);
        Player(start.transform);

        AddManagers("LevelManager", SceneKind.PastLab, start.transform, out var lm, out var level);
        lm.playerStartStage4 = start4;
        lm.guards = new[] { g2.GetComponent<GuardAI>() }.Concat(g4.Select(g => g.GetComponent<GuardAI>())).ToArray();
        level.AddComponent<PhotoPuzzleManager>();
        level.AddComponent<AssemblyPuzzleManager>();

        var ctrl = level.AddComponent<PastLabController>();
        ctrl.stage2Only = pieces.Concat(new[] { g2 }).ToArray();
        ctrl.stage4Only = g4.ToArray();
        ctrl.viewfinder = vp;
        ctrl.room = rr.room;
        ctrl.escapeZone = escapeZone;
        ctrl.stage4Alarms = alarms.ToArray();
        ctrl.playerStartStage4 = start4;

        // 房間平常關閉（門在關閉位置，這樣執行時 SecretDoor 記到的是關門的位置）
        rr.body.SetActive(false);

        Report("PastLab");
        Debug.Log("[D-Day] ViewFinder 解：站在 " + plan.viewer + " 面向 yaw=" + plan.yaw + "；距離 " + string.Join("/", plan.dist.Select(d => d.ToString("F1"))) + " m；隱藏密室入口 " + RoomEntrance);
        SaveScene(sc, "PastLab");
    }

    // ───────────── ViewFinder 相框求解 ─────────────

    static FramePlan SolveFrames(Vector3 p0)
    {
        var rng = new System.Random(2024);
        var spots = new List<Vector3>();
        // 視角：坦克室北邊（房間門外），面向南邊的隱藏密室。房間長出來的地方就是四個相框懸浮的地方
        for (float x = 5.5f; x <= 11.5f; x += 0.5f)
            for (float z = 41.5f; z <= 43.0f; z += 0.5f)
            {
                var s = Snap(x, z, 0.4f);
                if (s.HasValue && Vector3.Distance(p0, s.Value) >= 9f && Reach(p0, s.Value)) spots.Add(s.Value);
            }
        spots = spots.OrderBy(_ => rng.Next()).ToList();

        // 先試比較「標準」的張角，找不到再換張角 / 仰角（每塊碎片張開 13°、整張照片中心仰角 12°）
        foreach (var variant in new[] { (6.5f, 16f, 2.0f), (7.5f, 18f, 2.0f), (6f, 20f, 2.0f), (7f, 22f, 2.0f), (6.5f, 12f, 0.5f), (7.5f, 12f, 0.5f), (6f, 8f, 0.5f), (7f, 16f, 0.5f), (5.5f, 10f, 0.5f), (8f, 14f, 0.5f) })
        {
            float half = variant.Item1, elev = variant.Item2, minBottom = variant.Item3;
            foreach (var v in spots)
            {
                var eye = v + Vector3.up * 1.65f;
                for (int yaw = 160; yaw <= 200; yaw += 10)
                {
                    var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                    if (Physics.CheckSphere(eye, 0.6f, ~0, QueryTriggerInteraction.Ignore)) break;      // 腳邊 / 臉前不能貼著東西
                    if (Physics.Raycast(eye, fwd, 7f, ~0, QueryTriggerInteraction.Ignore)) continue;      // 正前方要夠開闊
                    var plan = TryPlan(eye, v, yaw, p0, half, elev, minBottom);
                    if (plan != null) return plan;
                }
            }
        }
        return null;
    }

    class Cand { public float d; public Vector3 center; public float size; }

    static FramePlan TryPlan(Vector3 eye, Vector3 viewer, int yaw, Vector3 p0, float HalfAngle, float ElevCenter, float minBottom)
    {
        float[][] dl =
        {
            new[] { 9f, 10f, 11f, 12f, 13f },
            new[] { 12f, 13f, 14f, 15f, 16f },
            new[] { 14f, 15f, 16f, 17f, 18f },
            new[] { 10.5f, 12f, 13.5f, 15f, 16.5f },
        };
        var opts = new List<Cand>[4];
        for (int i = 0; i < 4; i++)
        {
            opts[i] = new List<Cand>();
            float yawOff = (i % 2 == 0 ? -HalfAngle : HalfAngle);
            float elev = ElevCenter + (i < 2 ? HalfAngle : -HalfAngle);
            var dir = Quaternion.Euler(-elev, yaw + yawOff, 0) * Vector3.forward;
            foreach (var d in dl[i])
            {
                var c = eye + dir * d;
                float size = 2f * d * Mathf.Tan(HalfAngle * Mathf.Deg2Rad);
                float bottom = c.y - size * 0.5f;
                if (bottom < minBottom || c.y > 8f) continue;

                var b = Snap(c.x, c.z, 0.7f);
                if (!b.HasValue || !Reach(p0, b.Value)) continue;                                  // 支架底部要走得到
                if (Physics.Linecast(eye, c, ~0, QueryTriggerInteraction.Ignore)) continue;         // 視線不能被擋
                var rot = Quaternion.LookRotation(dir);
                if (Physics.CheckBox(c, new Vector3(size * 0.55f, size * 0.55f, 0.25f), rot, ~0, QueryTriggerInteraction.Ignore)) continue;   // 相框本體不能卡進東西
                if (Physics.CheckCapsule(new Vector3(c.x, b.Value.y + 0.2f, c.z), new Vector3(c.x, bottom, c.z), 0.12f, ~0, QueryTriggerInteraction.Ignore)) continue; // 支架
                opts[i].Add(new Cand { d = d, center = c, size = size });
            }
            if (opts[i].Count == 0) return null;
        }

        // 四個距離彼此至少差 1.5 公尺（不要在同一個平面上）
        var pick = new Cand[4];
        bool Dfs(int k)
        {
            if (k == 4) return true;
            foreach (var cand in opts[k])
            {
                bool ok = true;
                for (int j = 0; j < k; j++) if (Mathf.Abs(pick[j].d - cand.d) < 1.5f) { ok = false; break; }
                if (!ok) continue;
                pick[k] = cand;
                if (Dfs(k + 1)) return true;
            }
            return false;
        }
        if (!Dfs(0)) return null;

        // 判定要「寬鬆」：站在正確位置周圍 1 公尺的圓周上，大多數點也要看得到四個相框（不能全被圓柱 / 欄杆擋住）
        {
            int valid = 0, clear = 0;
            for (int k = 0; k < 8; k++)
            {
                float ang = k * 45f * Mathf.Deg2Rad;
                var tgt = viewer + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                if (!NavMesh.SamplePosition(tgt, out var nh, 0.25f, NavMesh.AllAreas)) continue;
                valid++;
                var e2 = nh.position + Vector3.up * 1.65f;
                bool ok = true;
                for (int i = 0; i < 4 && ok; i++) if (Physics.Linecast(e2, pick[i].center, ~0, QueryTriggerInteraction.Ignore)) ok = false;
                if (ok) clear++;
            }
            if (valid < 4 || clear * 4 < valid * 3) return null;
        }

        var plan = new FramePlan { viewer = viewer, yaw = yaw };
        for (int i = 0; i < 4; i++) { plan.center[i] = pick[i].center; plan.size[i] = pick[i].size; plan.dist[i] = pick[i].d; }
        return plan;
    }
}

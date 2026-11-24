using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 隱藏密室（第三關）。不是獨立場景：它是 PastLab 裡一個平常關閉的房間，ViewFinder 拼合後在實驗室中長出來。
/// 牆、地板、天花板、貨箱、門都用科幻組件包與天花板磚，碰撞由看不見的簡易牆提供。
/// 房間用「本地座標」建好（原點 = 門口中心；+Z 往室內；X -5.25..5.25，Z 0..10.5），最後再整個擺到實驗室裡。
/// </summary>
public static partial class SceneBuilder
{
    const float RoomW = 10.5f, RoomD = 10.5f, RoomH = 4.06f;

    static GameObject Piece(string model, Transform parent, Vector3 localPos, float yaw = 0f)
    {
        var g = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel(model), parent);
        g.transform.localPosition = localPos;
        g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        return g;
    }

    /// <summary>碰撞用的牆：做完後把 Renderer 關掉，只留 Collider（外觀交給模型）。</summary>
    static void HiddenWall(Transform parent, string name, Vector2 a, Vector2 b, params Vector2[] gaps)
    {
        int before = parent.childCount;
        Wall(parent, name, a, b, WallSecret, gaps);
        for (int i = before; i < parent.childCount; i++) parent.GetChild(i).GetComponent<Renderer>().enabled = false;
    }

    class RoomRefs
    {
        public GameObject root, body;
        public HiddenRoom room;
        public SecretDoor door;
        public Transform doorPanel;
        public ControlPanelInteraction console;
        public SelfDestructController selfDestruct;
        public ProximityZone insideZone, exitZone;
        public Transform mergePoint, exitSpot;
    }

    /// <summary>在 parent 底下建出隱藏密室（還在原點、尚未擺位）。</summary>
    static RoomRefs BuildHiddenRoom(Transform parent)
    {
        var r = new RoomRefs();
        r.root = Empty("HiddenRoom", parent);
        r.room = r.root.AddComponent<HiddenRoom>();
        r.body = Empty("Body", r.root.transform);
        var b = r.body.transform;

        float oldWallH = WallH, oldDoorH = DoorH;
        WallH = RoomH; DoorH = 3.9f;

        var env = Empty("Shell", b);
        var e = env.transform;
        Slot(env, "EnvShell_HiddenRoom", "整個隱藏密室外殼（放整間模型時用）");

        // 看不見的碰撞牆（南牆正中央是入口，3.7 m 寬）
        float hw = RoomW / 2f;
        HiddenWall(e, "South", new Vector2(-hw, 0), new Vector2(hw, 0), G(0f, 3.7f));
        HiddenWall(e, "North", new Vector2(hw, RoomD), new Vector2(-hw, RoomD));
        HiddenWall(e, "West", new Vector2(-hw, RoomD), new Vector2(-hw, 0));
        HiddenWall(e, "East", new Vector2(hw, 0), new Vector2(hw, RoomD));
        WallH = oldWallH; DoorH = oldDoorH;

        // ── 外觀：科幻地板 / 牆板 / 天花板磚 ──
        var vis = Empty("Visuals", e);
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                Piece("SciFi_Floor" + ((i + j) % 2 == 0 ? 3 : 5), vis.transform, new Vector3(-3.5f + 3.5f * i, 0.02f, 1.75f + 3.5f * j));
        for (int i = 0; i < 3; i++)
        {
            float x = -3.5f + 3.5f * i;
            if (i != 1) Piece("SciFi_Wall" + i, vis.transform, new Vector3(x, 0, 0.15f), 0f);          // 南牆（中間是入口）
            Piece("SciFi_Wall" + (i + 1) % 6, vis.transform, new Vector3(x, 0, RoomD - 0.15f), 180f);   // 北牆
            float z = 1.75f + 3.5f * i;
            Piece("SciFi_Wall" + (i + 2) % 6, vis.transform, new Vector3(-hw + 0.15f, 0, z), 90f);      // 西牆
            Piece("SciFi_Wall" + (i + 3) % 6, vis.transform, new Vector3(hw - 0.15f, 0, z), 270f);      // 東牆
        }
        AddSlotLog("SciFiWall", "Walls", "牆板（SciFi pack，3.5×4.06 m）");
        AddSlotLog("SciFiFloor", "Floor", "地板磚（SciFi pack，3.5×3.5 m）");
        Piece("SciFi_Doorway0", vis.transform, new Vector3(0, 0, 0), 90f);   // 入口門框

        // 天花板磚（user 提供的 office_ceiling_tiles）
        var ceil = Empty("CeilingTiles", vis.transform);
        for (float x = -hw + 0.55f; x < hw; x += 1.09f)
            for (float z = 0.3f; z < RoomD; z += 0.59f)
                Piece("M_CeilingTile", ceil.transform, new Vector3(x, RoomH - 0.02f, z));
        AddSlotLog("CeilingTile", "CeilingTiles", "天花板磚（office_ceiling_tiles.glb）");

        // 貨箱
        int ci = 0;
        foreach (var c in new[] { new Vector3(4.2f, 0, 8.9f), new Vector3(-4.1f, 0, 1.5f), new Vector3(4.2f, 0, 1.5f) })
        {
            Piece("SciFi_Crate" + (ci % 4), e, c, ci * 37f);
            ci++;
        }
        AddSlotLog("Crate", "Crates", "貨箱（SciFi pack）");

        // table3（Sci-Fi Table）：兩張工作桌靠牆；平板墊在中央電腦底下當平台
        Piece("M_SciFiTableB", e, new Vector3(-4.5f, 0, 4.3f), 90f);
        Piece("M_SciFiTableB", e, new Vector3(4.5f, 0, 5.4f), 270f);
        Piece("M_SciFiPlate", e, new Vector3(0, 0.01f, 8.1f), 0f);
        AddSlotLog("SciFiTable", "WorkTables", "科幻長桌 / 平板（table3）");

        var items = Empty("Interactives", b);

        // 入口門板：向下滑進地板裡開啟（房間沒有天花板，往上滑會露在牆外面）
        var doorRoot = Empty("EntranceDoor", items.transform, new Vector3(0, 0, 0.28f));
        var doorPanel = Piece("SciFi_Door0", doorRoot.transform, new Vector3(0, 0, 0.28f), 90f);
        Slot(doorPanel, "EntranceDoor", "密室入口門板（SciFi door，向下滑開）");
        var door = doorRoot.AddComponent<SecretDoor>();
        door.panel = doorPanel.transform;
        door.openOffset = new Vector3(0, -4.0f, 0);
        r.door = door; r.doorPanel = doorPanel.transform;

        // 觸發區：走進房間裡面 / 走出門外
        r.insideZone = MakeZone("InsideZone", items.transform, new Vector3(0, 1.2f, 4.2f), new Vector3(9.6f, 2.4f, 6.4f));
        r.exitZone = MakeZone("ExitZone", items.transform, new Vector3(0, 1.2f, -2.6f), new Vector3(3.6f, 2.4f, 2.6f));
        r.exitSpot = Empty("ExitSpot", items.transform, new Vector3(0, 0.05f, -3.6f), 180f).transform;
        Box("ExitMark", items.transform, new Vector3(0, 0.03f, -2.6f), new Vector3(2.6f, 0.02f, 2.0f), PortalMat, false);
        r.mergePoint = Empty("MergePoint", items.transform, new Vector3(0, 2.2f, 4.5f)).transform;

        // 中央控制電腦（螢幕朝 -Z，玩家從南邊進來）
        var console = Inst(computerPrefab, items.transform, new Vector3(0, 0, 8.1f));
        r.console = console.GetComponent<ControlPanelInteraction>();

        // 牆上的自毀狀態顯示器
        var disp = Empty("SelfDestructDisplay", items.transform, new Vector3(0, 2.75f, RoomD - 0.45f));
        Box("DisplayBack", disp.transform, new Vector3(0, 0, 0.06f), new Vector3(5.0f, 1.6f, 0.08f), DarkMat, false);
        var tmGo = Empty("Text", disp.transform);
        var tm = tmGo.AddComponent<TextMesh>();
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.fontSize = 80; tm.characterSize = 0.07f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.text = "SELF-DESTRUCT\nARMED"; tm.color = Color.red;
        tmGo.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
        Slot(disp, "SelfDestructDisplay", "自毀系統狀態顯示器");

        // 警報燈與補光
        var alarms = new List<AlarmLight>
        {
            MakeAlarm(items.transform, new Vector3(-4.6f, 3.6f, 1.8f), Color.red, 2.6f, 12f, false, 4f),
            MakeAlarm(items.transform, new Vector3(4.6f, 3.6f, 1.8f), Color.red, 2.6f, 12f, false, 4f),
            MakeAlarm(items.transform, new Vector3(-4.6f, 3.6f, 9.0f), Color.red, 2.6f, 12f, false, 4f),
            MakeAlarm(items.transform, new Vector3(4.6f, 3.6f, 9.0f), Color.red, 2.6f, 12f, false, 4f),
        };
        PointLight("RoomFill", items.transform, new Vector3(0, 3.4f, 5.25f), new Color(0.55f, 0.65f, 0.85f), 0.5f, 13f);
        var sdc = items.AddComponent<SelfDestructController>();
        sdc.display = tm;
        sdc.alarms = alarms.ToArray();
        r.selfDestruct = sdc;

        // 儲藏櫃（Door2 的鉸鏈門）：西北角的小隔間，裡面有研究員的筆記
        BuildCloset(e, items.transform);

        var hr = r.room;
        hr.body = r.body; hr.door = door; hr.console = r.console; hr.selfDestruct = sdc;
        hr.insideZone = r.insideZone; hr.exitZone = r.exitZone; hr.mergePoint = r.mergePoint; hr.exitSpot = r.exitSpot;
        hr.footprintMin = new Vector3(-hw, 0f, -0.2f);
        hr.footprintMax = new Vector3(hw, RoomH, RoomD);
        return r;
    }

    static void BuildCloset(Transform env, Transform items)
    {
        // 小隔間：x -5.25..-2.5, z 7.7..10.5；門開在南側（z=7.7），鉸鏈在 x=-4.85
        const float x0 = -5.25f, x1 = -2.5f, z0 = 7.7f, z1 = RoomD;
        Box("ClosetEast", env, new Vector3(x1, 1.5f, (z0 + z1) / 2f), new Vector3(0.2f, 3f, z1 - z0), Metal);
        Box("ClosetSouthR", env, new Vector3(-3.1f, 1.5f, z0), new Vector3(1.2f, 3f, 0.2f), Metal);
        Box("ClosetSouthL", env, new Vector3(-5.1f, 1.5f, z0), new Vector3(0.3f, 3f, 0.2f), Metal);
        Box("ClosetTop", env, new Vector3((x0 + x1) / 2f, 3.05f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.1f, z1 - z0), Metal);

        var door = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel("M_Door2"), items);
        door.name = "ClosetDoor";
        door.transform.localPosition = new Vector3(-4.95f, 0, z0);
        var col = door.AddComponent<BoxCollider>();
        col.center = new Vector3(0.6f, 1.17f, 0f); col.size = new Vector3(1.2f, 2.35f, 0.2f);
        var di = door.AddComponent<DoorInteraction>();
        di.openAngle = 105f;
        AddSlotLog("Door", "ClosetDoor", "儲藏櫃的門（Door2 模型，鉸鏈門）");

        MakeDocument(items, new Vector3(-4.6f, 0.92f, 9.3f), "研究員的筆記",
            "「組語備忘：LOOP 每圈會把 CX 減 1；CWD 會把 AX 的符號延伸到 DX；SAR 會保留符號位元。\n緩衝區要比寫入的資料長，別忘了還有被 PUSH 進去的暫存器。」");
        Box("ClosetBench", env, new Vector3(-4.6f, 0.4f, 9.3f), new Vector3(1.4f, 0.8f, 0.7f), Metal);
    }
}

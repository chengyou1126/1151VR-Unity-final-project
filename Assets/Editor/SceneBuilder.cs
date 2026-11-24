using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 一鍵產生三個場景（MainMenu / ModernLab / PastLab；隱藏密室是 PastLab 裡長出來的房間）與所有 Prefab。
/// 所有模型都是基本幾何體做的「替代品」，並且掛上 ModelSlot，之後找到正式模型再逐一替換。
/// 選單：Tools/D-Day/3. Build All Scenes
/// </summary>
public static partial class SceneBuilder
{
    static float WallH = 3.2f, DoorH = 2.6f;
    const float WallT = 0.3f;

    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static readonly List<string[]> slotLog = new List<string[]>();
    static string container = "";

    static GameObject playerPrefab, guardPrefab, guidePrefab, photoPiecePrefab, frameVfPrefab, computerPrefab, portalPrefab;
    static Texture2D softCircle;

    // ═════════════════════════════ 入口 ═════════════════════════════

    [MenuItem("Tools/D-Day/3. Build All Scenes (placeholder models)")]
    public static void BuildAll()
    {
        EnsureFolders();
        slotLog.Clear();
        mats.Clear();

        MakeTextures();
        TextMat();   // 建立（確保 Shader 會被打包）
        MakePrefabs();

        BuildMainMenu();
        BuildModernLab();
        BuildPastLab();

        EditorBuildSettings.scenes = new[] { "MainMenu", "ModernLab", "PastLab" }
            .Select(n => new EditorBuildSettingsScene("Assets/Scenes/" + n + ".unity", true)).ToArray();

        WriteSlotList();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[D-Day] All scenes and prefabs built with placeholder models.");
    }

    static void EnsureFolders()
    {
        foreach (var d in new[]
        {
            "Assets/Scenes", "Assets/Materials", "Assets/Textures/Placeholder", "Assets/Input",
            "Assets/Prefabs/Characters", "Assets/Prefabs/Items", "Assets/Prefabs/Interactive",
            "Assets/Prefabs/Environment", "Assets/Prefabs/Effects", "Assets/Prefabs/UI"
        })
            Directory.CreateDirectory(d);
        AssetDatabase.Refresh();
    }

    // ═════════════════════════════ 材質 / 貼圖 ═════════════════════════════

    static void MakeTextures()
    {
        const string path = "Assets/Textures/Placeholder/SoftCircle.png";
        if (!File.Exists(path))
        {
            int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y) / (n - 1), new Vector2(0.5f, 0.5f)) * 2f;
                    float a = Mathf.Clamp01(1f - d);
                    t.SetPixel(x, y, new Color(1, 1, 1, a * a));
                }
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
        }
        softCircle = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material Mat(string name, Color color, float smooth = 0.15f, Color? emission = null)
    {
        if (mats.TryGetValue(name, out var cached)) return cached;
        string path = "Assets/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        m.SetFloat("_Glossiness", smooth);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    /// <summary>只是為了讓 DDay/Text3D 被打包；實際切換由 Text3DDepthFix 在執行時完成。</summary>
    static Material TextMat()
    {
        const string path = "Assets/Resources/Text3D.mat";   // 放在 Resources：確保 DDay/Text3D 這個 Shader 會被打包進遊戲
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("DDay/Text3D"));
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    static Material SmokeMat()
    {
        const string path = "Assets/Materials/Smoke.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.mainTexture = softCircle;
        m.SetColor("_TintColor", new Color(0.55f, 0.55f, 0.58f, 0.22f));
        EditorUtility.SetDirty(m);
        return m;
    }

    // 色票
    static Material FloorModern => Mat("FloorModern", new Color(0.16f, 0.16f, 0.17f));
    static Material FloorPast => Mat("FloorPast", new Color(0.72f, 0.74f, 0.78f), 0.4f);
    static Material FloorSecret => Mat("FloorSecret", new Color(0.1f, 0.11f, 0.13f), 0.5f);
    static Material WallModern => Mat("WallModern", new Color(0.3f, 0.3f, 0.32f));
    static Material WallPast => Mat("WallPast", new Color(0.86f, 0.88f, 0.92f));
    static Material WallSecret => Mat("WallSecret", new Color(0.12f, 0.14f, 0.17f), 0.3f);
    static Material CeilModern => Mat("CeilModern", new Color(0.18f, 0.18f, 0.2f));
    static Material CeilPast => Mat("CeilPast", new Color(0.9f, 0.92f, 0.95f));
    static Material Metal => Mat("Metal", new Color(0.35f, 0.38f, 0.42f), 0.6f);
    static Material DarkMat => Mat("DarkMetal", new Color(0.08f, 0.09f, 0.1f), 0.4f);
    static Material BenchMat => Mat("Bench", new Color(0.55f, 0.6f, 0.66f), 0.3f);
    static Material WoodMat => Mat("Wood", new Color(0.4f, 0.28f, 0.18f));
    static Material CrateMat => Mat("Crate", new Color(0.5f, 0.4f, 0.25f));
    static Material DebrisMat => Mat("Debris", new Color(0.22f, 0.22f, 0.22f));
    static Material LampWhite => Mat("LampWhite", new Color(0.9f, 0.95f, 1f), 0.1f, new Color(1.6f, 1.7f, 1.9f));
    static Material LampRed => Mat("LampRed", new Color(0.3f, 0f, 0f), 0.1f, Color.black);
    static Material ScreenMat => Mat("Screen", new Color(0.02f, 0.1f, 0.12f), 0.5f, new Color(0.1f, 0.7f, 0.9f));
    static Material LedGreen => Mat("LedGreen", new Color(0f, 0.3f, 0.1f), 0.1f, new Color(0f, 1.5f, 0.4f));
    static Material PhotoPieceMat => Mat("PhotoPiece", new Color(1f, 0.9f, 0.4f), 0.2f, new Color(1f, 0.8f, 0.2f));
    static Material PhotoBlank => Mat("PhotoBlank", new Color(0.05f, 0.05f, 0.06f), 0.3f);
    static Material FrameMat => Mat("Frame", new Color(0.35f, 0.22f, 0.12f), 0.3f);
    static Material GuardMat => Mat("Guard", new Color(0.12f, 0.14f, 0.22f), 0.3f);
    static Material VisorMat => Mat("GuardVisor", new Color(1f, 0.1f, 0.1f), 0.5f, new Color(2f, 0.2f, 0.2f));
    static Material GuideMat => Mat("Guide", new Color(0.2f, 0.7f, 0.8f), 0.3f, new Color(0.05f, 0.25f, 0.3f));
    static Material SkinMat => Mat("Skin", new Color(0.85f, 0.7f, 0.6f));
    static Material PortalMat => Mat("Portal", new Color(0.3f, 0.7f, 1f), 0.5f, new Color(0.4f, 1.2f, 2f));
    static Material WarnMat => Mat("Warning", new Color(1f, 0.8f, 0.1f), 0.2f, new Color(0.6f, 0.45f, 0f));
    static Material DoorMat => Mat("Door", new Color(0.45f, 0.5f, 0.55f), 0.4f);
    static Material SecretPanelMat => Mat("SecretPanel", new Color(0.25f, 0.28f, 0.32f), 0.7f);
    static Material PaperMat => Mat("Paper", new Color(0.9f, 0.9f, 0.85f));

    // ═════════════════════════════ 基本積木 ═════════════════════════════

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m, bool collider = true, Vector3? euler = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
        go.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Empty(string name, Transform parent, Vector3? pos = null, float yaw = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos ?? Vector3.zero;
        go.transform.localEulerAngles = new Vector3(0, yaw, 0);
        return go;
    }

    /// <summary>掛上 ModelSlot，並記錄到 Assets/Models/ModelSlots.md。</summary>
    /// <summary>把 Assets/Prefabs/Models 底下整理好的模型指定給這個插槽。找不到就維持替代品。</summary>
    static GameObject SetModel(GameObject slotObject, string modelPrefabName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Models/" + modelPrefabName + ".prefab");
        if (prefab == null) { Debug.LogWarning("[D-Day] model prefab missing: " + modelPrefabName); return slotObject; }
        slotObject.GetComponent<ModelSlot>().modelPrefab = prefab;
        return slotObject;
    }

    static GameObject Slot(GameObject go, string id, string note = "")
    {
        var s = go.AddComponent<ModelSlot>();
        s.slotId = id;
        slotLog.Add(new[] { container, id, go.name, note });
        return go;
    }

    static Light PointLight(string name, Transform parent, Vector3 pos, Color c, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    static AlarmLight MakeAlarm(Transform parent, Vector3 pos, Color color, float maxIntensity, float range, bool flicker = false, float speed = 3f)
    {
        var root = Empty("AlarmLight", parent, pos);
        var lamp = Box("Lamp", root.transform, Vector3.zero, new Vector3(0.3f, 0.18f, 0.3f), LampRed, false);
        var l = PointLight("Light", root.transform, new Vector3(0, -0.3f, 0), color, maxIntensity, range);
        var al = root.AddComponent<AlarmLight>();
        al.lightSource = l;
        al.glowRenderer = lamp.GetComponent<Renderer>();
        al.color = color;
        al.maxIntensity = maxIntensity;
        al.flicker = flicker;
        al.speed = speed;
        return al;
    }

    static void CeilingLamp(Transform parent, Vector3 pos, Color c, float intensity, float range)
    {
        Box("LampPanel", parent, new Vector3(pos.x, WallH - 0.03f, pos.z), new Vector3(1.4f, 0.06f, 0.5f), LampWhite, false);
        PointLight("LampLight", parent, new Vector3(pos.x, WallH - 0.6f, pos.z), c, intensity, range);
    }

    static void Floor(Transform p, float x0, float z0, float x1, float z1, Material m)
    {
        Box("Floor", p, new Vector3((x0 + x1) / 2f, -0.1f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.2f, z1 - z0), m);
    }

    static void Ceiling(Transform p, float x0, float z0, float x1, float z1, Material m)
    {
        Box("Ceiling", p, new Vector3((x0 + x1) / 2f, WallH + 0.1f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.2f, z1 - z0), m);
    }

    /// <summary>軸向對齊的牆。gaps：x=開口中心、y=開口寬度；開口上方會補門楣。</summary>
    static void Wall(Transform parent, string name, Vector2 a, Vector2 b, Material m, params Vector2[] gaps)
    {
        bool vertical = Mathf.Approximately(a.x, b.x);
        float s = vertical ? Mathf.Min(a.y, b.y) : Mathf.Min(a.x, b.x);
        float e = vertical ? Mathf.Max(a.y, b.y) : Mathf.Max(a.x, b.x);
        float c = vertical ? a.x : a.y;
        s -= WallT / 2f;
        e += WallT / 2f;

        int n = 0;
        float cur = s;
        foreach (var g in gaps.OrderBy(g => g.x))
        {
            float gs = g.x - g.y / 2f, ge = g.x + g.y / 2f;
            if (gs > cur + 0.01f) Seg(cur, gs, 0f, WallH);
            Seg(gs, ge, DoorH, WallH - DoorH);
            cur = ge;
        }
        if (e > cur + 0.01f) Seg(cur, e, 0f, WallH);

        void Seg(float from, float to, float y0, float h)
        {
            float len = to - from, mid = (from + to) / 2f;
            Vector3 pos = vertical ? new Vector3(c, y0 + h / 2f, mid) : new Vector3(mid, y0 + h / 2f, c);
            Vector3 size = vertical ? new Vector3(WallT, h, len) : new Vector3(len, h, WallT);
            Box(name + "_" + (n++), parent, pos, size, m);
        }
    }

    static Vector2 G(float center, float width = 2.2f) { return new Vector2(center, width); }

    // ── 常用道具（每個都有 ModelSlot）──

    static GameObject Prop(Transform p, string name, string slotId, Vector3 pos, Vector3 size, Material m, float yaw = 0f)
    {
        var go = Box(name, p, pos, size, m, true, new Vector3(0, yaw, 0));
        return Slot(go, slotId);
    }

    static GameObject Bench(Transform p, Vector3 pos, Vector2 sizeXZ, float yaw = 0f)
    {
        var root = Empty("LabBench", p, pos, yaw);
        Box("Base", root.transform, new Vector3(0, 0.4f, 0), new Vector3(sizeXZ.x - 0.2f, 0.8f, sizeXZ.y - 0.2f), Metal);
        Box("Top", root.transform, new Vector3(0, 0.84f, 0), new Vector3(sizeXZ.x, 0.08f, sizeXZ.y), BenchMat);
        // 桌上擺一些儀器
        Prim(PrimitiveType.Cylinder, "Flask", root.transform, new Vector3(-sizeXZ.x * 0.25f, 1.0f, 0), new Vector3(0.12f, 0.1f, 0.12f), Mat("Glass", new Color(0.6f, 0.9f, 0.8f), 0.8f), false);
        Box("Scope", root.transform, new Vector3(sizeXZ.x * 0.2f, 1.0f, 0.1f), new Vector3(0.18f, 0.3f, 0.22f), DarkMat, false);
        return Slot(root, "LabBench", "實驗桌（含桌上儀器）");
    }

    static GameObject Cabinet(Transform p, Vector3 pos, Vector3 size, float yaw = 0f)
    {
        return Prop(p, "Cabinet", "Cabinet", pos + Vector3.up * size.y / 2f, size, Metal, yaw);
    }

    static GameObject Debris(Transform p, System.Random rng, Vector3 center, float spread, int count)
    {
        var root = Empty("Debris", p, center);
        for (int i = 0; i < count; i++)
        {
            var s = new Vector3(0.2f + (float)rng.NextDouble() * 0.7f, 0.1f + (float)rng.NextDouble() * 0.4f, 0.2f + (float)rng.NextDouble() * 0.7f);
            var pos = new Vector3(((float)rng.NextDouble() - 0.5f) * spread, s.y / 2f, ((float)rng.NextDouble() - 0.5f) * spread);
            var rot = new Vector3(((float)rng.NextDouble() - 0.5f) * 40f, (float)rng.NextDouble() * 360f, ((float)rng.NextDouble() - 0.5f) * 40f);
            Box("Rubble" + i, root.transform, pos, s, DebrisMat, true, rot);
        }
        return Slot(root, "Debris", "爆炸碎片 / 瓦礫");
    }

    static void SmokeFx(Transform p, Vector3 pos)
    {
        var go = Empty("Smoke", p, pos);
        go.transform.localEulerAngles = new Vector3(-90, 0, 0);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 7f;
        main.startSpeed = 0.35f;
        main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission;
        em.rateOverTime = 6f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.8f;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = SmokeMat();
    }

    static void SetupRender(Color ambient, Color fog, float density)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fog;
        RenderSettings.fogDensity = density;
        RenderSettings.skybox = null;
    }

    static Scene NewScene(string name)
    {
        container = name;
        return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    static void SaveScene(Scene s, string name)
    {
        EditorSceneManager.SaveScene(s, "Assets/Scenes/" + name + ".unity");
    }

    static GameObject Inst(GameObject prefab, Transform parent, Vector3 pos, float yaw = 0f)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = new Vector3(0, yaw, 0);
        return go;
    }

    static GameObject SavePrefab(GameObject go, string path)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    // ═════════════════════════════ Prefab ═════════════════════════════

    static void MakePrefabs()
    {
        container = "Prefab";
        playerPrefab = MakePlayer();
        guardPrefab = MakeGuard();
        guidePrefab = MakeGuide();
        photoPiecePrefab = MakePhotoPiece();
        frameVfPrefab = MakeViewfinderFrame();
        computerPrefab = MakeComputer();
        portalPrefab = MakePortal();
    }

    static GameObject MakePlayer()
    {
        var root = new GameObject("Player");
        root.tag = "Player";
        var cc = root.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.25f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.3f; cc.slopeLimit = 50f;

        var camGo = new GameObject("Camera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(root.transform, false);
        camGo.transform.localPosition = new Vector3(0, 1.65f, 0);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 120f;
        cam.fieldOfView = 75f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.AddComponent<AudioListener>();

        // 頭燈
        var lampGo = new GameObject("Headlamp");
        lampGo.transform.SetParent(camGo.transform, false);
        var lamp = lampGo.AddComponent<Light>();
        lamp.type = LightType.Spot;
        lamp.spotAngle = 75f;
        lamp.range = 14f;
        lamp.intensity = 1.1f;
        lamp.color = new Color(1f, 0.96f, 0.88f);
        lamp.shadows = LightShadows.None;

        var look = root.AddComponent<PlayerLook>();
        look.cameraRoot = camGo.transform;
        root.AddComponent<PlayerMovement>();
        var inter = root.AddComponent<PlayerInteraction>();
        inter.rayOrigin = camGo.transform;
        root.AddComponent<PlayerCheckpoint>();

        return SavePrefab(root, "Assets/Prefabs/Characters/Player.prefab");
    }

    static GameObject MakeGuard()
    {
        var root = new GameObject("Guard");
        var col = root.AddComponent<CapsuleCollider>();
        col.height = 1.8f; col.radius = 0.4f; col.center = new Vector3(0, 0.9f, 0);

        var agent = root.AddComponent<UnityEngine.AI.NavMeshAgent>();
        agent.radius = 0.3f; agent.height = 1.8f; agent.baseOffset = 0f;
        agent.enabled = false; // 等 RuntimeNavMesh 烘焙完成後由 GuardAI 啟用

        var vis = Empty("Visuals", root.transform);
        Prim(PrimitiveType.Capsule, "Body", vis.transform, new Vector3(0, 0.85f, 0), new Vector3(0.75f, 0.8f, 0.75f), GuardMat, false);
        Prim(PrimitiveType.Sphere, "Head", vis.transform, new Vector3(0, 1.65f, 0), new Vector3(0.38f, 0.38f, 0.38f), SkinMat, false);
        Box("Visor", vis.transform, new Vector3(0, 1.68f, 0.17f), new Vector3(0.3f, 0.07f, 0.12f), VisorMat, false);
        Box("Badge", vis.transform, new Vector3(0.18f, 1.2f, 0.34f), new Vector3(0.1f, 0.1f, 0.03f), WarnMat, false);
        SetModel(Slot(vis, "Guard", "守衛（目前用 guard.glb；沒有動畫）"), "M_Guard");

        var eye = Empty("Eye", root.transform, new Vector3(0, 1.62f, 0.12f));

        var ai = root.AddComponent<GuardAI>();
        ai.eye = eye.transform;
        root.AddComponent<GuardCatch>();

        return SavePrefab(root, "Assets/Prefabs/Characters/Guard.prefab");
    }

    static GameObject MakeGuide()
    {
        var root = new GameObject("Guide");
        var col = root.AddComponent<CapsuleCollider>();
        col.height = 1.8f; col.radius = 0.45f; col.center = new Vector3(0, 0.9f, 0);

        var vis = Empty("Visuals", root.transform);
        Prim(PrimitiveType.Capsule, "Body", vis.transform, new Vector3(0, 0.85f, 0), new Vector3(0.7f, 0.8f, 0.7f), GuideMat, false);
        Prim(PrimitiveType.Sphere, "Head", vis.transform, new Vector3(0, 1.65f, 0), new Vector3(0.36f, 0.36f, 0.36f), SkinMat, false);
        Prim(PrimitiveType.Cylinder, "Halo", vis.transform, new Vector3(0, 2.05f, 0), new Vector3(0.45f, 0.01f, 0.45f), PortalMat, false);
        SetModel(Slot(vis, "Guide", "引導人（目前用 player 模型，手臂由 RelaxedPose 放下）"), "M_Guide");

        PointLight("GuideGlow", root.transform, new Vector3(0, 1.4f, 0.4f), new Color(0.4f, 0.9f, 1f), 1.2f, 4f);
        root.AddComponent<GuideNPC>();
        return SavePrefab(root, "Assets/Prefabs/Characters/Guide.prefab");
    }

    static GameObject MakePhotoPiece()
    {
        var root = new GameObject("PhotoPiece");
        var bc = root.AddComponent<BoxCollider>();
        bc.size = new Vector3(0.5f, 0.35f, 0.5f);

        var vis = Empty("Visuals", root.transform);
        Box("Photo", vis.transform, Vector3.zero, new Vector3(0.28f, 0.02f, 0.36f), PhotoPieceMat, false);
        Slot(vis, "PhotoPiece", "照片碎片（發光薄片）");
        vis.AddComponent<PickupSpin>();
        PointLight("Glow", root.transform, new Vector3(0, 0.3f, 0), new Color(1f, 0.85f, 0.3f), 0.9f, 3.5f);

        root.AddComponent<PhotoPickup>();
        return SavePrefab(root, "Assets/Prefabs/Items/PhotoPiece.prefab");
    }

    static GameObject MakeComputer()
    {
        var root = new GameObject("ControlPanel");
        var bc = root.AddComponent<BoxCollider>();
        bc.size = new Vector3(2.4f, 1.7f, 1.0f);
        bc.center = new Vector3(0, 0.85f, 0);

        var vis = Empty("Visuals", root.transform);
        Box("Desk", vis.transform, new Vector3(0, 0.45f, 0), new Vector3(2.4f, 0.9f, 1.0f), Metal, false);
        Box("Keyboard", vis.transform, new Vector3(0, 0.93f, -0.25f), new Vector3(0.9f, 0.04f, 0.3f), DarkMat, false);
        Box("MonitorBody", vis.transform, new Vector3(0, 1.4f, 0.1f), new Vector3(1.6f, 0.9f, 0.1f), DarkMat, false, new Vector3(-8, 0, 0));
        var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
        screen.name = "Screen";
        screen.transform.SetParent(vis.transform, false);
        screen.transform.localPosition = new Vector3(0, 1.4f, 0.04f);
        screen.transform.localEulerAngles = new Vector3(-8, 0, 0);
        screen.transform.localScale = new Vector3(1.5f, 0.8f, 1f);
        screen.GetComponent<Renderer>().sharedMaterial = ScreenMat;
        UnityEngine.Object.DestroyImmediate(screen.GetComponent<Collider>());
        Box("SideMonitorL", vis.transform, new Vector3(-1.5f, 1.2f, 0.2f), new Vector3(0.7f, 0.5f, 0.08f), DarkMat, false, new Vector3(0, 25, 0));
        Box("SideMonitorR", vis.transform, new Vector3(1.5f, 1.2f, 0.2f), new Vector3(0.7f, 0.5f, 0.08f), DarkMat, false, new Vector3(0, -25, 0));
        Slot(vis, "ControlPanel", "中央控制電腦（桌 + 三螢幕）。螢幕朝 -Z");

        PointLight("ScreenGlow", root.transform, new Vector3(0, 1.4f, -0.8f), new Color(0.2f, 0.8f, 1f), 1.4f, 5f);
        root.AddComponent<ControlPanelInteraction>();
        return SavePrefab(root, "Assets/Prefabs/Interactive/ControlPanel.prefab");
    }

    static GameObject MakePortal()
    {
        var root = new GameObject("TimePortal");
        var vis = Empty("Visuals", root.transform);
        Prim(PrimitiveType.Cylinder, "Disc", vis.transform, new Vector3(0, 0.02f, 0), new Vector3(2.6f, 0.02f, 2.6f), PortalMat, false);
        Prim(PrimitiveType.Cylinder, "Ring", vis.transform, new Vector3(0, 0.05f, 0), new Vector3(3.0f, 0.01f, 3.0f), PortalMat, false);
        Slot(vis, "TimePortal", "時空穿越裝置 / 逃脫終點（發光圓盤）");
        PointLight("Glow", root.transform, new Vector3(0, 1.0f, 0), new Color(0.4f, 0.8f, 1f), 1.6f, 7f);
        return SavePrefab(root, "Assets/Prefabs/Interactive/TimePortal.prefab");
    }

    // ═════════════════════════════ 共用：玩家與管理器 ═════════════════════════════

    static GameObject Player(Transform start)
    {
        var p = Inst(playerPrefab, null, start.position, start.eulerAngles.y);
        p.transform.SetParent(null);
        return p;
    }

    static void AddManagers(string levelName, SceneKind kind, Transform playerStart, out LevelManager lm, out GameObject level)
    {
        level = new GameObject(levelName);
        lm = level.AddComponent<LevelManager>();
        lm.kind = kind;
        lm.playerStart = playerStart;
    }

    // ═════════════════════════════ 場景：MainMenu ═════════════════════════════

    static void BuildMainMenu()
    {
        var sc = NewScene("MainMenu");
        SetupRender(new Color(0.05f, 0.05f, 0.07f), new Color(0.03f, 0.03f, 0.05f), 0.035f);

        var env = Empty("Environment", null);
        Box("Floor", env.transform, new Vector3(0, -0.1f, 6), new Vector3(24, 0.2f, 30), FloorModern);
        Box("BackWall", env.transform, new Vector3(0, 2.5f, 16), new Vector3(24, 5, 0.4f), WallModern);
        for (int i = -2; i <= 2; i++)
            Box("Pillar" + i, env.transform, new Vector3(i * 5f, 2.5f, 12), new Vector3(0.8f, 5f, 0.8f), Metal, false);
        var rng = new System.Random(3);
        Debris(env.transform, rng, new Vector3(-4, 0, 8), 4f, 8);
        Debris(env.transform, rng, new Vector3(5, 0, 6), 4f, 8);
        SmokeFx(env.transform, new Vector3(0, 0.1f, 10));

        // 緩慢旋轉的照片，當作標題畫面的主視覺
        var photo = Box("TitlePhoto", env.transform, new Vector3(0, 1.7f, 7), new Vector3(1.2f, 0.03f, 0.9f), PhotoPieceMat, false, new Vector3(75, 0, 0));
        photo.AddComponent<PickupSpin>().spinSpeed = 25f;
        PointLight("PhotoGlow", env.transform, new Vector3(0, 2.3f, 6), new Color(1f, 0.85f, 0.4f), 1.5f, 8f);

        MakeAlarm(env.transform, new Vector3(-6, 3.5f, 10), Color.red, 3f, 14f, true, 2f);
        MakeAlarm(env.transform, new Vector3(6, 3.5f, 10), Color.red, 3f, 14f, true, 2.5f);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0, 1.7f, 0);
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
        cam.fieldOfView = 60f;
        camGo.AddComponent<AudioListener>();

        var start = Empty("PlayerStart", null);
        AddManagers("LevelManager", SceneKind.MainMenu, start.transform, out var lm, out _);

        SaveScene(sc, "MainMenu");
    }

    static void MakeDocument(Transform parent, Vector3 pos, string title, string body)
    {
        var doc = Box("Document", parent, pos, new Vector3(0.3f, 0.015f, 0.4f), PaperMat, true, new Vector3(0, 25, 0));
        var d = doc.AddComponent<DocumentInteraction>();
        d.title = title;
        d.body = body;
        Slot(doc, "Document", "可閱讀的文件");
    }

    static ProximityZone MakeZone(string name, Transform parent, Vector3 center, Vector3 size)
    {
        var go = Empty(name, parent, center);
        var z = go.AddComponent<ProximityZone>();
        var bc = go.GetComponent<BoxCollider>();
        bc.size = size;
        bc.isTrigger = true;
        return z;
    }

    // ═════════════════════════════ 模型插槽清單 ═════════════════════════════

    static void WriteSlotList()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 模型插槽清單（自動產生）");
        sb.AppendLine();
        sb.AppendLine("目前所有 3D 物件都是基本幾何體「替代品」。找到正式模型後，**不用重做場景**：");
        sb.AppendLine();
        sb.AppendLine("1. 把模型匯入 `Assets/Models/` 對應的子資料夾（Environment / Characters / Items / Interactive）。");
        sb.AppendLine("2. 做成 Prefab（或直接用模型的 Prefab）。");
        sb.AppendLine("3. 選取場景裡（或 Prefab 裡）掛有 **ModelSlot** 的物件，把 Prefab 拖到 `Model Prefab` 欄位，必要時調整位置 / 旋轉 / 縮放。");
        sb.AppendLine("   執行時會自動隱藏替代品並放入正式模型；替代品的 Collider 會保留，所以碰撞、互動、守衛 NavMesh 都不用重做。");
        sb.AppendLine("4. 角色（Guard / Guide）建議在模型 Prefab 內放好 Animator，之後再把 Animator 參數接到 GuardAI。");
        sb.AppendLine();
        sb.AppendLine("> 想一次換掉整間房間：在 `EnvShell_*` 這個插槽放整間模型即可（會隱藏該場景 Environment 底下的所有替代品，只保留碰撞）。");
        sb.AppendLine();
        sb.AppendLine("| 位置（場景 / Prefab） | Slot ID | 物件名稱 | 說明 |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var s in slotLog.GroupBy(x => x[0] + "|" + x[1]).Select(g => g.First()))
            sb.AppendLine("| " + s[0] + " | `" + s[1] + "` | " + s[2] + " | " + s[3] + " |");

        File.WriteAllText("Assets/Models/ModelSlots.md", sb.ToString(), new UTF8Encoding(false));
    }
}

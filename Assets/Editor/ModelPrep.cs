using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 把匯入的原始模型整理成「可直接使用」的 Prefab（正確比例、腳底為原點、材質接好）。
/// 輸出到 Assets/Prefabs/Models/，之後 SceneBuilder 會把它們放進各個 ModelSlot。
/// 選單：Tools/D-Day/0. Prepare Model Prefabs
/// </summary>
public static class ModelPrep
{
    const string Out = "Assets/Prefabs/Models";
    const string Models = "Assets/Models";
    static readonly StringBuilder log = new StringBuilder();

    [MenuItem("Tools/D-Day/0. Prepare Model Prefabs")]
    public static void Run()
    {
        log.Clear();
        Directory.CreateDirectory(Out);
        FixTextureImportSettings();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Safe("Lab", PrepLab);
        Safe("Guard", PrepGuard);
        Safe("Guide", PrepGuide);
        Safe("Doors", PrepDoors);
        Safe("Bench", PrepBench);
        Safe("StolLab", PrepStolLab);
        Safe("SciFiTables", PrepSciFiTables);
        Safe("CeilingTile", PrepCeilingTile);
        Safe("SciFi", PrepSciFi);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        File.WriteAllText("modelprep_report.txt", log.ToString());
        Debug.Log("[ModelPrep] done");
    }

    public static void RunBatch() { Run(); EditorApplication.Exit(0); }

    static void Safe(string name, System.Action a)
    {
        try { a(); log.AppendLine("OK   " + name); }
        catch (System.Exception e) { log.AppendLine("FAIL " + name + ": " + e); Debug.LogError("[ModelPrep] " + name + ": " + e); }
    }

    // ───────────── 共用 ─────────────

    static void FixTextureImportSettings()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Models }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter; // 嵌在 fbx/glb 裡的貼圖不是 TextureImporter
            if (imp == null) continue;
            string n = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            bool normal = n.EndsWith("_normal");
            bool changed = false;
            if (normal && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; changed = true; }
            if (imp.maxTextureSize > 2048) { imp.maxTextureSize = 2048; changed = true; } // 大圖縮到 2K，省記憶體
            if (changed) imp.SaveAndReimport();
        }
    }

    static GameObject Load(string path)
    {
        var g = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (g == null) throw new FileNotFoundException(path);
        return g;
    }

    static Bounds BoundsOf(GameObject go, System.Predicate<Renderer> filter = null)
    {
        Bounds b = default; bool first = true;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (filter != null && !filter(r)) continue;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        return b;
    }


    /// <summary>依模型實際範圍在 root 上加一個 BoxCollider（桌子、長椅原本沒有碰撞，玩家會直接穿過去）。</summary>
    static void AddBoxCollider(GameObject root, GameObject inst)
    {
        var b = BoundsOf(inst);
        var col = root.AddComponent<BoxCollider>();
        col.center = root.transform.InverseTransformPoint(b.center);
        col.size = b.size;
    }

    static Vector3 Center(Bounds b) { return b.center; }

    static GameObject Save(GameObject root, string file)
    {
        var p = PrefabUtility.SaveAsPrefabAsset(root, Out + "/" + file + ".prefab");
        Object.DestroyImmediate(root);
        return p;
    }

    static Material StdMat(string path, string albedo, string normal = null, string metallic = null, float smooth = 0.4f)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        if (albedo != null) m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(albedo);
        if (normal != null) { m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal)); m.EnableKeyword("_NORMALMAP"); }
        if (metallic != null) { m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(metallic)); m.EnableKeyword("_METALLICGLOSSMAP"); }
        m.SetFloat("_Glossiness", smooth);
        m.SetFloat("_GlossMapScale", 1f);   // 有 Metallic 貼圖時，平滑度取自貼圖 Alpha
        EditorUtility.SetDirty(m);
        return m;
    }

    // ───────────── 各模型 ─────────────

    static void PrepLab()
    {
        const string fbx = Models + "/Environment/UndergroundLab/UndergroundLab.fbx";
        var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
        if (!imp.addCollider) { imp.addCollider = true; imp.SaveAndReimport(); }
        var src = Load(fbx);

        var matDir = Models + "/Environment/UndergroundLab/Materials";
        Directory.CreateDirectory(matDir);
        // 現代（爆炸後）：灰白色、積灰、霧面
        var modern = StdMat(matDir + "/Lab_Modern.mat", null, smooth: 0.25f); modern.color = new Color(0.74f, 0.74f, 0.75f); modern.SetFloat("_Metallic", 0.1f);
        // 過去（爆炸前）：深色鋼藍、乾淨、有金屬光澤（搭配藍白色燈光）
        var past = StdMat(matDir + "/Lab_Past.mat", null, smooth: 0.7f); past.color = new Color(0.16f, 0.2f, 0.27f); past.SetFloat("_Metallic", 0.55f);
        var glass = StdMat(matDir + "/Lab_Glass.mat", null, smooth: 0.9f);
        glass.color = new Color(0.7f, 0.85f, 0.95f, 0.25f);
        glass.SetFloat("_Mode", 3); glass.SetInt("_SrcBlend", 5); glass.SetInt("_DstBlend", 10); glass.SetInt("_ZWrite", 0);
        glass.DisableKeyword("_ALPHATEST_ON"); glass.EnableKeyword("_ALPHABLEND_ON"); glass.DisableKeyword("_ALPHAPREMULTIPLY_ON"); glass.renderQueue = 3000;

        foreach (var variant in new[] { ("M_Lab_Modern", modern), ("M_Lab_Past", past) })
        {
            var root = new GameObject(variant.Item1);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
            var b = BoundsOf(inst);
            // 讓整體的最小 XZ 角落在原點，大廳地板（原本 y=-3）落在 y=0
            inst.transform.localPosition = new Vector3(-b.min.x, 3.0f, -b.min.z);
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name : "";
                    mats[i] = (n == "glass" || n.StartsWith("Material #")) ? glass : variant.Item2;
                }
                r.sharedMaterials = mats;
            }
            Save(root, variant.Item1);
        }
    }

    static void PrepGuard()
    {
        var src = Load(Models + "/Characters/Guard/Guard.glb");
        var root = new GameObject("M_Guard");
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
        var body = inst.GetComponentsInChildren<Renderer>().First(r => r.name.StartsWith("Avatar"));
        float scale = 1.75f / body.bounds.size.y;
        inst.transform.localScale = Vector3.one * scale;
        var bb = BoundsOf(inst, r => r.name.StartsWith("Avatar"));
        inst.transform.localPosition = -new Vector3(bb.center.x, bb.min.y, bb.center.z);
        Save(root, "M_Guard");
    }

    static void PrepGuide()
    {
        var src = Load(Models + "/Characters/Player/PlayerAvatar.fbx");
        var mat = StdMat(Models + "/Characters/Player/Wolf3D_Avatar.mat",
                         Models + "/Characters/Player/Textures/Wolf3D_Avatar_DIFFUSE.jpeg",
                         Models + "/Characters/Player/Textures/Wolf3D_Avatar_NORMAL.jpeg", smooth: 0.25f);
        var root = new GameObject("M_Guide");
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
        foreach (var r in inst.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
        float scale = 1.72f / BoundsOf(inst).size.y;
        inst.transform.localScale = Vector3.one * scale;
        var b = BoundsOf(inst);
        inst.transform.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z);
        root.AddComponent<RelaxedPose>();
        Save(root, "M_Guide");
    }

    static void PrepDoors()
    {
        // Door2：單純的門板 + 門框（OBJ 單位是公分）。以「門軸側底部」為原點，門往 +X 展開。
        var src2 = Load(Models + "/Interactive/Door2/Door2.obj");
        var mat2 = StdMat(Models + "/Interactive/Door2/Door2.mat",
                          Models + "/Interactive/Door2/Textures/Untitled_2_Mat_1_BaseColor.png",
                          Models + "/Interactive/Door2/Textures/Untitled_2_Mat_1_Normal.png",
                          Models + "/Interactive/Door2/Textures/Untitled_2_Mat_1_Metallic.png", 0.5f);
        var root2 = new GameObject("M_Door2");
        var inst2 = (GameObject)PrefabUtility.InstantiatePrefab(src2, root2.transform);
        foreach (var r in inst2.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat2;
        float s2 = 2.35f / BoundsOf(inst2).size.y;
        inst2.transform.localScale = Vector3.one * s2;
        var b2 = BoundsOf(inst2);
        inst2.transform.localPosition = -new Vector3(b2.min.x, b2.min.y, b2.center.z);
        Save(root2, "M_Door2");

        // Door1：一面牆 + 一扇門 + 地板，整組當作「門牆」使用。縮放到門高約 2.2 公尺。
        var src1 = Load(Models + "/Interactive/Door1/Door1.fbx");
        var root1 = new GameObject("M_Door1");
        var inst1 = (GameObject)PrefabUtility.InstantiatePrefab(src1, root1.transform);
        var leaf = inst1.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("Plane_00") && r.bounds.size.y > 0.015f).ToList();
        float doorH = leaf.Count > 0 ? leaf.Max(r => r.bounds.size.y) : 0.022f;
        float s1 = 2.2f / Mathf.Max(0.001f, doorH);
        inst1.transform.localScale = Vector3.one * s1;
        var b1 = BoundsOf(inst1);
        inst1.transform.localPosition = -new Vector3(b1.center.x, b1.min.y, b1.center.z);
        Save(root1, "M_Door1");
    }

    static void PrepBench()
    {
        var src = Load(Models + "/Items/Table1_ConcreteBench/ConcreteBench.fbx");
        var tex = Models + "/Items/Table1_ConcreteBench/Textures/";
        string[] sets = { "concrete bench2_Material", "concrete bench2_Material.001", "concrete bench2_Material.002", "concrete bench2_Material.003",
                          "concrete bench2_Material.004", "concrete bench2_Material.005", "concrete bench2_Material.006" };
        var mats = new Material[sets.Length];
        for (int i = 0; i < sets.Length; i++)
            mats[i] = StdMat(Models + "/Items/Table1_ConcreteBench/Bench_" + i + ".mat", tex + sets[i] + "_BaseColor.png", tex + sets[i] + "_Normal.png", tex + sets[i] + "_Metallic.png", 0.4f);

        var root = new GameObject("M_Bench");
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
        var rs = inst.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < rs.Length; i++) rs[i].sharedMaterial = mats[Mathf.Min(i, mats.Length - 1)];
        inst.transform.localScale = Vector3.one * 1.8f;
        var b = BoundsOf(inst);
        inst.transform.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z);
        AddBoxCollider(root, inst);
        Save(root, "M_Bench");
    }


    // table2：Stol Lab 實驗桌（.blend 轉成 .glb；七個零件：桌體 + 六個抽屜，各自有 Unity 格式的貼圖）
    static void PrepStolLab()
    {
        var src = Load(Models + "/Items/Table2_StolLab/StolLab.glb");
        string dir = Models + "/Items/Table2_StolLab/";
        var root = new GameObject("M_StolLab");
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
        foreach (var r in inst.GetComponentsInChildren<Renderer>())
        {
            string key = r.name == "Stol" ? "Wood_1" : r.name;          // 抽屜 1~6、桌體用木紋
            string set = dir + "Textures/Stol_Lab1_" + key;
            string normal = File.Exists(set + "_Normal.png") ? set + "_Normal.png" : null;
            r.sharedMaterial = StdMat(dir + "StolLab_" + key + ".mat", set + "_AlbedoTransparency.png", normal, set + "_MetallicSmoothness.png", 1f);
        }
        float h = BoundsOf(inst).size.y;
        inst.transform.localScale = Vector3.one * (0.95f / Mathf.Max(0.01f, h));   // 原始模型只有 0.6 高，放大成約 0.95 公尺的實驗桌
        var b = BoundsOf(inst);
        inst.transform.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z);
        AddBoxCollider(root, inst);
        Save(root, "M_StolLab");
    }

    // table3：Sci-Fi Table（.blend 轉成 .glb；兩個零件：長桌 B 與一塊平板）
    static void PrepSciFiTables()
    {
        var src = Load(Models + "/Items/Table3_SciFiTable/SciFiTable.glb");
        foreach (var part in new[] { ("Table B", "M_SciFiTableB", 0.6f), ("Sci-Fi Table", "M_SciFiPlate", 0.6f) })
        {
            var root = new GameObject(part.Item2);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            // 只留下這個零件
            string Norm(string n) { return n.Replace(" ", "").Replace("_", "").Replace("-", "").ToLowerInvariant(); }   // glTFast 可能把空白 / 連字號改成底線
            var keep = inst.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => Norm(t.name) == Norm(part.Item1));
            if (keep == null) throw new System.Exception("找不到零件 " + part.Item1);
            // 先挑出要刪的物件（連同它們的父節點鏈上沒有 keep 的最上層節點），再一起刪，避免刪到一半又存取已刪除的物件
            var doomed = new List<GameObject>();
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                if (t == inst.transform || t == keep || t.IsChildOf(keep) || keep.IsChildOf(t)) continue;
                if (t.parent == inst.transform || (t.parent != null && (t.parent == keep.parent || keep.IsChildOf(t.parent)) && t.parent != keep)) doomed.Add(t.gameObject);
            }
            foreach (var g in doomed.Distinct()) if (g != null) Object.DestroyImmediate(g);
            inst.transform.localScale = Vector3.one * part.Item3;
            var b = BoundsOf(inst);
            inst.transform.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z);
            // 碰撞：用整體盒子（桌子不需要精細碰撞）
            var bb = BoundsOf(inst);
            var col = root.AddComponent<BoxCollider>();
            col.center = root.transform.InverseTransformPoint(bb.center);
            col.size = bb.size;
            Save(root, part.Item2);
        }
    }

    static void PrepCeilingTile()
    {
        var src = Load(Models + "/Environment/CeilingTiles/CeilingTiles.glb");
        var root = new GameObject("M_CeilingTile");
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
        var b = BoundsOf(inst);
        inst.transform.localPosition = -Center(b);
        Save(root, "M_CeilingTile");
    }

    // ───────────── SciFi 組件包：把合併的網格拆成單件 ─────────────

    static void PrepSciFi()
    {
        var src = Load(Models + "/Environment/SciFiPack/SciFiPack.fbx");
        var meshDir = Models + "/Environment/SciFiPack/Pieces";
        Directory.CreateDirectory(meshDir);
        var tmp = (GameObject)PrefabUtility.InstantiatePrefab(src);

        var kinds = new Dictionary<string, (string prefix, bool centerPivot)>
        {
            { "FloorCeilings_9", ("SciFi_Floor", true) }, { "Walls_6", ("SciFi_Wall", false) }, { "Crates", ("SciFi_Crate", false) },
            { "Doorways", ("SciFi_Doorway", false) }, { "Doors", ("SciFi_Door", false) }, { "Ceiling_Straight", ("SciFi_CeilingTile", true) },
        };
        foreach (var mf in tmp.GetComponentsInChildren<MeshFilter>())
        {
            if (!kinds.TryGetValue(mf.name, out var kind)) continue;
            var mr = mf.GetComponent<MeshRenderer>();
            var parts = SplitMesh(mf.sharedMesh, tmp.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix);
            int idx = 0;
            foreach (var part in parts.OrderBy(p => p.bounds.center.x).ThenBy(p => p.bounds.center.z))
            {
                var size = part.bounds.size;
                if (size.magnitude < 0.2f) continue; // 排除太小的雜件
                Vector3 pivot = kind.centerPivot ? part.bounds.center : new Vector3(part.bounds.center.x, part.bounds.min.y, part.bounds.center.z);
                part.mesh.SetVertices(part.mesh.vertices.Select(v => v - pivot).ToList());
                part.mesh.RecalculateBounds();
                string meshPath = meshDir + "/" + kind.prefix + idx + ".asset";
                AssetDatabase.CreateAsset(part.mesh, meshPath);

                var go = new GameObject(kind.prefix + idx);
                go.AddComponent<MeshFilter>().sharedMesh = part.mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
                go.AddComponent<MeshCollider>().sharedMesh = part.mesh;
                Save(go, kind.prefix + idx);
                log.AppendLine($"     {kind.prefix}{idx}: size=({size.x:F2},{size.y:F2},{size.z:F2})");
                idx++;
            }
        }
        Object.DestroyImmediate(tmp);
    }

    class Part { public Mesh mesh; public Bounds bounds; }

    static List<Part> SplitMesh(Mesh src, Matrix4x4 m)
    {
        var verts = src.vertices; var norms = src.normals; var uvs = src.uv; var tris = src.triangles;
        var weld = new Dictionary<Vector3Int, int>(); var map = new int[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            var k = new Vector3Int(Mathf.RoundToInt(verts[i].x * 200), Mathf.RoundToInt(verts[i].y * 200), Mathf.RoundToInt(verts[i].z * 200));
            if (!weld.TryGetValue(k, out var id)) { id = weld.Count; weld[k] = id; }
            map[i] = id;
        }
        var parent = Enumerable.Range(0, weld.Count).ToArray();
        int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
        for (int i = 0; i < tris.Length; i += 3)
        {
            int a = Find(map[tris[i]]), b = Find(map[tris[i + 1]]), c = Find(map[tris[i + 2]]);
            parent[b] = a; parent[Find(c)] = a;
        }
        var groups = new Dictionary<int, List<int>>();
        for (int i = 0; i < tris.Length; i += 3)
        {
            int r = Find(map[tris[i]]);
            if (!groups.TryGetValue(r, out var l)) groups[r] = l = new List<int>();
            l.Add(tris[i]); l.Add(tris[i + 1]); l.Add(tris[i + 2]);
        }
        var result = new List<Part>();
        foreach (var g in groups.Values)
        {
            var remap = new Dictionary<int, int>(); var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            foreach (var idx in g)
            {
                if (!remap.TryGetValue(idx, out var ni))
                {
                    ni = v.Count; remap[idx] = ni;
                    v.Add(m.MultiplyPoint3x4(verts[idx]));
                    if (norms.Length > idx) n.Add(m.MultiplyVector(norms[idx]).normalized);
                    if (uvs.Length > idx) uv.Add(uvs[idx]);
                }
                t.Add(ni);
            }
            var mesh = new Mesh { indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(v); if (n.Count == v.Count) mesh.SetNormals(n); if (uv.Count == v.Count) mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            if (n.Count != v.Count) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            result.Add(new Part { mesh = mesh, bounds = mesh.bounds });
        }
        return result;
    }
}

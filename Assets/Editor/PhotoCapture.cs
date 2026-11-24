using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 拍攝遊戲裡的「照片」：
///   Photo_HiddenRoom = 隱藏密室的實景（第二關四塊碎片拼出來的內容，所以照片本身就是密室的樣子）
///   Photo_Stage1     = 第一關兩半照片拼起來的畫面（過去實驗室的大廳）
/// 輸出到 Assets/Resources/Photos/。需要圖形裝置（命令列不要加 -nographics）。
/// 選單：Tools/D-Day/4. Capture Photos
/// </summary>
public static class PhotoCapture
{
    [MenuItem("Tools/D-Day/4. Capture Photos")]
    public static void Run()
    {
        Directory.CreateDirectory("Assets/Resources/Photos");

        // 隱藏密室（PastLab 裡長出來的房間）：先讓房間出現，從門口附近往控制台看
        EditorSceneManager.OpenScene("Assets/Scenes/PastLab.unity");
        HideScenePlayer();
        var room = Object.FindFirstObjectByType<HiddenRoom>();
        room.body.SetActive(true);
        room.door.panel.localPosition += room.door.openOffset;
        Shoot("Assets/Resources/Photos/Photo_HiddenRoom.png", 1024, 1024, room.transform.TransformPoint(new Vector3(-1.4f, 2.0f, 1.4f)), new Vector3(8f, room.transform.eulerAngles.y + 16f, 0f), 62f);

        // 過去實驗室：大廳全景（2:1）
        EditorSceneManager.OpenScene("Assets/Scenes/PastLab.unity");
        HideScenePlayer(true);
        var start = GameObject.Find("PlayerStart").transform.position;
        Shoot("Assets/Resources/Photos/Photo_Stage1.png", 1024, 512, start + new Vector3(5f, 1.7f, 1f), new Vector3(4f, 270f, 0f), 60f);

        AssetDatabase.Refresh();
        foreach (var p in new[] { "Assets/Resources/Photos/Photo_HiddenRoom.png", "Assets/Resources/Photos/Photo_Stage1.png" })
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(p);
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 1024;
            imp.SaveAndReimport();
        }
        Debug.Log("[Photo] captured");
    }

    public static void RunBatch() { Run(); EditorApplication.Exit(0); }

    /// <summary>開發用：把候選角度拍到指定資料夾，挑一個最好看的。</summary>
    public static void PreviewBatch()
    {
        string dir = null;
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-previewDir") dir = args[i + 1];
        Directory.CreateDirectory(dir);
        EditorSceneManager.OpenScene("Assets/Scenes/PastLab.unity");
        HideScenePlayer(true);
        var start = GameObject.Find("PlayerStart").transform.position;
        var cands = new (string n, Vector3 off, Vector3 euler)[]
        {
            ("a_west_low", new Vector3(5f, 1.7f, 1f), new Vector3(4f, 270f, 0f)),
            ("b_east_low", new Vector3(-3f, 1.7f, 0f), new Vector3(4f, 90f, 0f)),
            ("c_north_low", new Vector3(0f, 1.7f, -2f), new Vector3(6f, 0f, 0f)),
            ("d_west_high", new Vector3(8f, 3.5f, 0f), new Vector3(12f, 262f, 0f)),
            ("e_south_low", new Vector3(0f, 1.7f, 4f), new Vector3(2f, 180f, 0f)),
            ("f_ne_high", new Vector3(-6f, 3.5f, -2f), new Vector3(14f, 55f, 0f)),
        };
        foreach (var c in cands) Shoot(Path.Combine(dir, c.n + ".png"), 1024, 512, start + c.off, c.euler, 60f);
        EditorApplication.Exit(0);
    }

    static void HideScenePlayer(bool hideAlarms = false)
    {
        if (hideAlarms) foreach (var a in Object.FindObjectsByType<AlarmLight>(FindObjectsSortMode.None)) a.gameObject.SetActive(false);  // 第四關的紅色警報燈不要拍進去
        var p = GameObject.FindWithTag("Player");
        if (p != null) p.SetActive(false);   // 拍照時不要把玩家的頭燈拍進去
    }

    static void Shoot(string path, int w, int h, Vector3 pos, Vector3 euler, float fov)
    {
        var go = new GameObject("PhotoCam");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.Euler(euler);
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.05f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.05f, 0.07f);
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        cam.targetTexture = null;
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(go);
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>專案基本設定與輸入資產匯出。選單：Tools/D-Day/…</summary>
public static class ProjectSetup
{
    [MenuItem("Tools/D-Day/1. Apply Project Settings")]
    public static void Apply()
    {
        PlayerSettings.companyName = "1151VR";
        PlayerSettings.productName = "D-Day";
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.runInBackground = true; // 手把斷線 / 視窗失焦時遊戲不要當掉

        // Input System：使用「兩者皆可」(2)，這樣之後匯入的素材包即使用舊版 Input 也不會報錯。
        var all = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (all != null && all.Length > 0)
        {
            var so = new SerializedObject(all[0]);
            var p = so.FindProperty("activeInputHandler");
            if (p != null && p.intValue != 2)
            {
                p.intValue = 2;
                so.ApplyModifiedProperties();
                Debug.Log("[D-Day] activeInputHandler = Both（需重新啟動 Unity 才會生效）");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[D-Day] Project settings applied.");
    }

    [MenuItem("Tools/D-Day/2. Export GameInput.inputactions")]
    public static void ExportInputActions()
    {
        Directory.CreateDirectory("Assets/Input");
        var asset = GameInput.Build();
        File.WriteAllText("Assets/Input/GameInput.inputactions", asset.ToJson());
        Object.DestroyImmediate(asset);
        AssetDatabase.ImportAsset("Assets/Input/GameInput.inputactions");
        Debug.Log("[D-Day] Exported Assets/Input/GameInput.inputactions");
    }

    /// <summary>命令列用：Unity -batchmode -executeMethod ProjectSetup.RunAllBatch -quit</summary>
    public static void RunAllBatch()
    {
        try
        {
            Apply();
            ExportInputActions();
            SceneBuilder.BuildAll();
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    public static void ApplyBatch()
    {
        try
        {
            Apply();
            ExportInputActions();
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}

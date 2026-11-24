using UnityEngine;

/// <summary>
/// 內建字型的 TextMesh 預設會「穿牆顯示」（Shader 的 ZTest 是 Always），
/// 相框編號 / 自毀顯示器如果穿牆會洩漏位置。這裡在載入場景前把字型的 Shader 換成會被遮住的版本。
/// </summary>
public static class Text3DDepthFix
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Fix()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var shader = Shader.Find("DDay/Text3D");
        if (font != null && shader != null && font.material != null && font.material.shader != shader)
            font.material.shader = shader;
    }
}

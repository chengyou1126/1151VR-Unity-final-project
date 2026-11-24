using UnityEngine;

/// <summary>在牆上 / 物件上顯示 Resources 裡的照片（例如密室南牆那張「你剛剛走進來的照片」）。</summary>
public class PhotoDisplay : MonoBehaviour
{
    public string resourcePath = "Photos/Photo_HiddenRoom";
    public Renderer target;

    void Start()
    {
        var tex = Resources.Load<Texture2D>(resourcePath);
        if (tex == null || target == null) return;
        target.material.mainTexture = tex;
    }
}

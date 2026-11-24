using UnityEngine;

/// <summary>
/// 程式畫出來的替代照片（左上太陽、右上紅三角形、下方大樓、一條對角線），
/// 內容刻意不對稱，旋轉 / 交換錯誤時一眼就看得出來。
/// 找到正式照片後，把 Texture 指定給 PhotoPuzzleManager.customPhoto 就會取代。
/// </summary>
public static class PlaceholderPhoto
{
    public static Texture2D Generate(int cols, int rows)
    {
        int W = 256 * cols, H = 256 * rows;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var px = new Color32[W * H];
        float aspect = W / (float)H;

        Color top = new Color(0.12f, 0.32f, 0.45f);
        Color bottom = new Color(0.04f, 0.08f, 0.16f);
        Color sun = new Color(1f, 0.82f, 0.25f);
        Color tri = new Color(0.9f, 0.18f, 0.15f);
        Color build = new Color(0.02f, 0.03f, 0.05f);
        Color line = new Color(0.95f, 0.95f, 0.95f);

        // 大樓：x 區間與高度
        float[] bx = { 0.00f, 0.14f, 0.26f, 0.42f, 0.55f, 0.70f, 0.84f };
        float[] bh = { 0.30f, 0.46f, 0.22f, 0.55f, 0.34f, 0.40f, 0.26f };

        for (int y = 0; y < H; y++)
        {
            float v = y / (float)(H - 1);
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)(W - 1);
                Color c = Color.Lerp(bottom, top, v);

                // 太陽
                float dx = (u - 0.2f) * aspect, dy = v - 0.78f;
                if (dx * dx + dy * dy < 0.011f) c = sun;

                // 紅色三角形（右上）
                float tx = (u - 0.78f) * aspect, ty = v - 0.62f;
                if (ty > 0f && ty < 0.26f && Mathf.Abs(tx) < (0.26f - ty) * 0.6f) c = tri;

                // 大樓
                for (int i = 0; i < bx.Length; i++)
                {
                    float x0 = bx[i], x1 = i + 1 < bx.Length ? bx[i + 1] - 0.015f : 1f;
                    if (u >= x0 && u <= x1 && v < bh[i] * 0.9f) { c = build; break; }
                }

                // 對角線
                if (Mathf.Abs(v - u * rows / (float)cols) < 0.012f) c = line;

                // 邊框
                if (u < 0.008f || u > 0.992f || v < 0.008f || v > 0.992f) c = line;

                px[y * W + x] = c;
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.name = "PlaceholderPhoto";
        return tex;
    }
}

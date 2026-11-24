using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 用程式建立 uGUI 的小工具（所有介面都是執行時建立，不需要手動拉 Canvas）。
/// 字型使用作業系統的中文字型（微軟正黑體…），所以中文不用另外匯入字型資產。
/// 之後若要改用 TextMeshPro，只要替換這個類別的 Label / MakeButton 即可。
/// </summary>
public static class UIFactory
{
    static Font _font, _mono;
    static Sprite _heart, _circle, _ring;

    public static Font Font
    {
        get
        {
            if (_font == null)
                _font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft JhengHei UI", "Microsoft JhengHei", "Microsoft YaHei UI", "PingFang TC", "Noto Sans CJK TC", "Arial" }, 32);
            return _font;
        }
    }

    public static Font Mono
    {
        get
        {
            if (_mono == null)
                _mono = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "Menlo", "Lucida Console" }, 24);
            return _mono;
        }
    }

    public static readonly Color Cyan = new Color(0.35f, 0.85f, 1f);
    public static readonly Color Green = new Color(0.35f, 1f, 0.55f);
    public static readonly Color Red = new Color(1f, 0.3f, 0.3f);

    // ───────────── RectTransform 小幫手 ─────────────

    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    public static void Stretch(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    public static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, Vector2? pivot = null)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot ?? (anchorMin + anchorMax) * 0.5f;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    // ───────────── 基本元件 ─────────────

    public static Image Panel(Transform parent, string name, Color color)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    public static Text Label(Transform parent, string name, string text, int size, Color color,
                             TextAnchor anchor = TextAnchor.UpperLeft, bool mono = false)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = mono ? Mono : Font;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.text = text;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    public static Button MakeButton(Transform parent, string name, string label, UnityAction onClick,
                                    int fontSize = 34, float width = 460f, float height = 76f)
    {
        var rt = NewRect(name, parent);
        rt.sizeDelta = new Vector2(width, height);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = Color.white;

        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var cb = b.colors;
        cb.normalColor = new Color(0.13f, 0.17f, 0.22f, 1f);
        cb.highlightedColor = new Color(0.22f, 0.5f, 0.7f, 1f);
        cb.selectedColor = new Color(0.22f, 0.55f, 0.78f, 1f);
        cb.pressedColor = new Color(0.35f, 0.7f, 0.9f, 1f);
        cb.disabledColor = new Color(0.1f, 0.1f, 0.12f, 0.6f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.05f;
        b.colors = cb;

        var t = Label(rt, "Label", label, fontSize, Color.white, TextAnchor.MiddleCenter);
        Stretch(t.rectTransform, 12, 4, 12, 4);
        if (onClick != null) b.onClick.AddListener(onClick);
        return b;
    }

    public static void SetButtonLabel(Button b, string text)
    {
        var t = b.GetComponentInChildren<Text>();
        if (t != null) t.text = text;
    }

    public static void SetButtonFontSize(Button b, int size)
    {
        var t = b.GetComponentInChildren<Text>();
        if (t != null) t.fontSize = size;
    }

    // ───────────── 程式產生的小圖 ─────────────

    public static Sprite HeartSprite
    {
        get
        {
            if (_heart == null)
                _heart = MakeSprite(64, (x, y) =>
                {
                    float nx = (x - 0.5f) * 2.4f, ny = (y - 0.45f) * 2.4f;
                    float a = nx * nx + ny * ny - 1f;
                    return a * a * a - nx * nx * ny * ny * ny <= 0f ? 1f : 0f;
                });
            return _heart;
        }
    }

    public static Sprite CircleSprite
    {
        get
        {
            if (_circle == null)
                _circle = MakeSprite(128, (x, y) =>
                {
                    float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f)) * 2f;
                    return 1f - Mathf.SmoothStep(0.55f, 1f, d);
                });
            return _circle;
        }
    }

    public static Sprite RingSprite
    {
        get
        {
            if (_ring == null)
                _ring = MakeSprite(256, (x, y) =>
                {
                    float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f)) * 2f;
                    float k = (d - 0.9f) / 0.05f;
                    float core = Mathf.Exp(-k * k);
                    float glow = Mathf.Exp(-Mathf.Pow((d - 0.82f) / 0.2f, 2f)) * 0.4f;
                    return Mathf.Clamp01(core + glow) * (d < 1f ? 1f : Mathf.Clamp01(1f - (d - 1f) * 8f));
                });
            return _ring;
        }
    }

    static Sprite MakeSprite(int size, Func<float, float, float> alpha)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = Mathf.Clamp01(alpha(x / (float)(size - 1), y / (float)(size - 1)));
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}

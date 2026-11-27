using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 時空穿越演出（替代版）：照片接縫發光 → 光環從中心擴張 → 環境色由紅轉白藍 → 白光淡出並載入下一關。
/// 之後想做成「光線環繞玩家一圈、經過的地方變成過去場景」，可以在這裡換成 Shader Graph / 後處理版本。
/// </summary>
public static class TimeTravelEffect
{
    public static void Play(Action onComplete)
    {
        GameManager.Instance.StartCoroutine(PlayRoutine(onComplete));
    }

    static IEnumerator PlayRoutine(Action onComplete)
    {
        var ui = UIManager.Instance;
        var gm = GameManager.Instance;
        gm.SetMode(InputMode.Locked);
        AudioManager.Play(Sfx.TimeTravel);
        ui.SetPrompt(null);

        var layer = UIFactory.NewRect("TimeTravelFX", ui.Canvas.transform);
        UIFactory.Stretch(layer);

        // 接縫
        var glow = UIFactory.Panel(layer, "SeamGlow", new Color(0.6f, 0.9f, 1f, 0f));
        var seam = UIFactory.Panel(layer, "Seam", new Color(1f, 1f, 1f, 0f));
        UIFactory.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 700));
        UIFactory.Place(seam.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8, 640));

        // 光環
        var ring = UIFactory.Panel(layer, "Ring", new Color(0.8f, 0.95f, 1f, 0f));
        ring.sprite = UIFactory.RingSprite;
        UIFactory.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100, 100));

        var white = UIFactory.Panel(layer, "White", new Color(1f, 1f, 1f, 0f));
        UIFactory.Stretch(white.rectTransform);

        Color ambient0 = RenderSettings.ambientLight;
        Color fog0 = RenderSettings.fogColor;
        Color ambient1 = new Color(0.55f, 0.65f, 0.8f);
        Color fog1 = new Color(0.4f, 0.5f, 0.65f);

        // 1) 接縫發光
        for (float t = 0; t < 1f; t += Time.deltaTime / 1.4f)
        {
            float k = Mathf.SmoothStep(0, 1, t);
            seam.color = new Color(1, 1, 1, k);
            glow.color = new Color(0.6f, 0.9f, 1f, k * 0.5f);
            seam.rectTransform.sizeDelta = new Vector2(8 + 10 * k, 640);
            yield return null;
        }

        // 2) 光環擴張
        float maxSize = 4200f;
        for (float t = 0; t < 1f; t += Time.deltaTime / 2.4f)
        {
            float k = t * t;
            ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(100f, maxSize, k);
            ring.color = new Color(0.8f, 0.95f, 1f, Mathf.Clamp01(1.4f - t));
            RenderSettings.ambientLight = Color.Lerp(ambient0, ambient1, t);
            RenderSettings.fogColor = Color.Lerp(fog0, fog1, t);
            seam.color = new Color(1, 1, 1, 1f - t);
            glow.color = new Color(0.6f, 0.9f, 1f, 0.5f * (1f - t));
            yield return null;
        }

        // 3) 白光
        for (float t = 0; t < 1f; t += Time.deltaTime / 0.7f)
        {
            white.color = new Color(1, 1, 1, t);
            yield return null;
        }
        white.color = Color.white;

        onComplete?.Invoke();
        yield return new WaitForSecondsRealtime(0.1f);
        UnityEngine.Object.Destroy(layer.gameObject);
    }
}

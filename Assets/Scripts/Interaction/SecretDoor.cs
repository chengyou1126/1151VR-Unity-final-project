using System.Collections;
using UnityEngine;

/// <summary>滑動式門（隱藏密室的入口 / 出口）。Open() / Close() 會把門板沿 openOffset 滑開或滑回。</summary>
public class SecretDoor : MonoBehaviour
{
    public Transform panel;
    public Vector3 openOffset = new Vector3(0f, 3.2f, 0f);
    public float duration = 1.6f;

    public bool IsOpen { get; private set; }

    Vector3 closedPos;
    bool cached;
    Coroutine slide;

    void Cache()
    {
        if (cached) return;
        if (panel == null) panel = transform;
        closedPos = panel.localPosition;
        cached = true;
    }

    void Awake() { Cache(); }

    public void Open()
    {
        Cache();
        if (IsOpen) return;
        IsOpen = true;
        Slide(closedPos + openOffset);
    }

    public void Close()
    {
        Cache();
        if (!IsOpen) return;
        IsOpen = false;
        Slide(closedPos);
    }

    public void OpenInstant()
    {
        Cache();
        IsOpen = true;
        if (slide != null) StopCoroutine(slide);
        panel.localPosition = closedPos + openOffset;
    }

    public void CloseInstant()
    {
        Cache();
        IsOpen = false;
        if (slide != null) StopCoroutine(slide);
        panel.localPosition = closedPos;
    }

    void Slide(Vector3 to)
    {
        if (slide != null) StopCoroutine(slide);
        if (!isActiveAndEnabled) { panel.localPosition = to; return; }
        slide = StartCoroutine(SlideRoutine(to));
    }

    IEnumerator SlideRoutine(Vector3 to)
    {
        AudioManager.Play(Sfx.Door);
        Vector3 from = panel.localPosition;
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            panel.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        panel.localPosition = to;
        slide = null;
    }
}

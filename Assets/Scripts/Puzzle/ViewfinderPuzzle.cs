using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// ViewFinder 式的視角謎題。
/// 四個相框分別放在不同的位置、不同的遠近與高度；碎片都放好後，只有從某個視角看過去，
/// 四塊碎片在畫面上才會剛好排成「2x2 的完整照片」。判定刻意放寬：
/// 位置、大小不需要完全吻合，大約站在那個點附近（約 ±1.5 公尺）、看向那個方向（約 ±15 度）就算。
/// 成功後四塊碎片飛向中央合併，照片裡的房間（隱藏密室）就在原地長出來，可以直接走進去。
/// </summary>
public class ViewfinderPuzzle : MonoBehaviour
{
    public ViewfinderFrame[] frames;       // 0=左上 1=右上 2=左下 3=右下
    public HiddenRoom room;
    [Tooltip("判定寬鬆度：越大越寬鬆（0.3 約為每塊碎片大小的 30% 誤差）")]
    [Range(0.1f, 0.8f)] public float tolerance = 0.32f;
    public float holdSeconds = 0.5f;

    [Header("開發用：建置時求解出的「正確視角」（不會顯示給玩家；測試腳本與 Gizmo 使用）")]
    public Vector3 solutionPosition;
    public float solutionYaw;

    public bool Opened { get; private set; }
    public float Score { get; private set; }
    /// <summary>最近一次 Evaluate 回傳 0 的原因（除錯用）。</summary>
    public string LastFailReason { get; private set; }
    public event Action OnOpened;
    /// <summary>碎片合併、房間開始長出來的那一刻（用來收起這一關的守衛等東西）。</summary>
    public event Action OnMerged;

    float hold;
    float hintCooldown;
    Camera cam;

    public int PlacedCount
    {
        get { int n = 0; foreach (var f in frames) if (f != null && f.Placed) n++; return n; }
    }
    public bool AllPlaced => PlacedCount >= frames.Length;

    void Update()
    {
        if (Opened) return;
        var gm = GameManager.Instance;
        if (gm.State != GameState.Playing || gm.Mode != InputMode.Gameplay) return;

        if (!AllPlaced) { Score = 0f; return; }
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        float prev = Score;
        Score = Evaluate(cam);
        foreach (var f in frames) f.SetAlignment(Score);

        hintCooldown -= Time.deltaTime;
        if (prev < 0.12f && Score >= 0.12f && hintCooldown <= 0f)
        {
            UIManager.Instance.Toast("四塊碎片好像快要對上了……", 2f);
            hintCooldown = 6f;
        }

        if (Score >= 0.4f)
        {
            hold += Time.deltaTime;
            if (hold >= holdSeconds) StartCoroutine(OpenRoutine());
        }
        else hold = 0f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(solutionPosition + Vector3.up * 1.65f, 0.5f);
        Gizmos.DrawRay(solutionPosition + Vector3.up * 1.65f, Quaternion.Euler(0, solutionYaw, 0) * Vector3.forward * 6f);
    }

    /// <summary>目前這個視角下，四塊碎片排成 2x2 的吻合程度（0~1）。</summary>
    public float Evaluate(Camera c)
    {
        var pos = new Vector2[4];
        var size = new float[4];
        Vector3 cp = c.transform.position;
        float aspect = c.aspect;

        for (int i = 0; i < 4; i++)
        {
            var f = frames[i];
            Vector3 center = f.Center;
            Vector3 vp = c.WorldToViewportPoint(center);
            if (vp.z < 0.5f) return Fail("frame " + i + " behind camera");
            if (vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.05f) return Fail("frame " + i + " off screen vp=" + vp);

            // 要從正面（照片面）那一側看過去
            if (Vector3.Dot(f.Normal, (cp - center).normalized) < 0.5f) return Fail("frame " + i + " seen from the side");

            // 不能被牆壁 / 貨箱等東西擋住（相框本身與支架不算）
            if (IsBlocked(cp, center, f))
                return Fail("frame " + i + " blocked");

            Vector3 half = f.transform.right * (f.Width * 0.5f);
            Vector3 a = c.WorldToViewportPoint(center - half), b = c.WorldToViewportPoint(center + half);
            size[i] = Vector2.Distance(new Vector2(a.x * aspect, a.y), new Vector2(b.x * aspect, b.y));
            pos[i] = new Vector2(vp.x * aspect, vp.y);
        }

        float s = (size[0] + size[1] + size[2] + size[3]) * 0.25f;
        if (s < 0.02f) return Fail("too small");
        float minS = Mathf.Min(Mathf.Min(size[0], size[1]), Mathf.Min(size[2], size[3]));
        float maxS = Mathf.Max(Mathf.Max(size[0], size[1]), Mathf.Max(size[2], size[3]));
        if (maxS / Mathf.Max(0.0001f, minS) > 1.9f) return Fail("size ratio " + (maxS / minS).ToString("F2"));   // 大小差太多 = 視角差太遠

        var offs = new[] { new Vector2(-0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f) };
        Vector2 m = Vector2.zero;
        for (int i = 0; i < 4; i++) m += pos[i] - offs[i] * s;
        m *= 0.25f;
        float err = 0f;
        for (int i = 0; i < 4; i++) err += Vector2.Distance(pos[i], m + offs[i] * s);
        err = err * 0.25f / s;
        LastFailReason = "layout error " + err.ToString("F3");
        return Mathf.Clamp01(1f - err / tolerance);
    }

    /// <summary>視線被擋住了嗎？沿途遇到「這個相框自己」就算看得到；其他相框與支架不算遮擋。</summary>
    bool IsBlocked(Vector3 from, Vector3 to, ViewfinderFrame self)
    {
        Vector3 d = to - from;
        var hits = Physics.RaycastAll(from, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in hits)
        {
            var t = h.transform;
            if (t.IsChildOf(self.transform)) return false;
            if (t.GetComponentInParent<ViewfinderFrame>() != null || t.name.StartsWith("Pole")) continue;
            if (t.IsChildOf(PlayerCheckpoint.Instance != null ? PlayerCheckpoint.Instance.transform : transform)) continue;
            return true;
        }
        return false;
    }

    float Fail(string why)
    {
        LastFailReason = why;
        return 0f;
    }

    IEnumerator OpenRoutine()
    {
        Opened = true;
        var gm = GameManager.Instance;
        var ui = UIManager.Instance;
        gm.Data.photoPuzzleSolved = true;
        gm.Data.portalOpen = true;
        gm.SetMode(InputMode.Locked);
        AudioManager.Play(Sfx.TimeTravel);
        ui.Flash(new Color(0.8f, 0.95f, 1f, 0.55f), 0.9f);
        ui.SetPrompt(null);

        // 四塊碎片飛向中央（房間要長出來的地方）
        foreach (var f in frames)
        {
            if (f.pole != null) f.pole.SetActive(false);
            if (f.poleBase != null) f.poleBase.SetActive(false);
        }
        Vector3 target = room.mergePoint.position;
        var from = new Vector3[4]; var scale = new Vector3[4];
        for (int i = 0; i < 4; i++) { from[i] = frames[i].transform.position; scale[i] = frames[i].transform.localScale; }
        for (float t = 0; t < 1f; t += Time.deltaTime / 1.6f)
        {
            float k = Mathf.SmoothStep(0, 1, t);
            for (int i = 0; i < 4; i++)
            {
                frames[i].transform.position = Vector3.Lerp(from[i], target, k);
                frames[i].transform.localScale = Vector3.Lerp(scale[i], scale[i] * 0.3f, k);
                frames[i].SetAlignment(1f);
            }
            yield return null;
        }
        foreach (var f in frames) f.gameObject.SetActive(false);

        ui.Flash(new Color(0.8f, 0.95f, 1f, 0.8f), 0.7f);
        OnMerged?.Invoke();
        yield return room.RevealRoutine();
        yield return new WaitForSeconds(0.6f);
        gm.SetMode(InputMode.Gameplay);
        OnOpened?.Invoke();
    }
}

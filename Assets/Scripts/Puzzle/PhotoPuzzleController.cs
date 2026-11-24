using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 照片拼圖介面（手把操作）：
///   方向鍵 / 左類比搖桿：移動游標　　A：選取 / 與游標處的碎片交換
///   LB / RB：逆時針 / 順時針旋轉　　B：取消選取（沒有選取時 = 關閉介面）
/// 進入時切換到 Puzzle Input Action Map，所以 RB 不會同時觸發角色奔跑。
/// </summary>
public class PhotoPuzzleController : MonoBehaviour
{
    const float Cell = 230f;

    GameObject root;
    RectTransform grid;
    Text hint, title;
    RawImage preview;

    int cols, rows, cursor, selected = -1;
    int[] piece;      // 每個格子目前放的碎片編號
    int[] rot;        // 每個格子目前的旋轉（四分之一圈，順時針）
    Image[] border;
    RectTransform[] cellRt;
    RawImage[] cellImg;
    Texture photo;
    Action onSolved;
    bool busy;
    float openedAt;
    readonly DiscreteNav nav = new DiscreteNav();

    public bool IsOpen => root != null && root.activeSelf;

    public void Build(Transform canvasRoot)
    {
        var bg = UIFactory.Panel(canvasRoot, "PhotoPuzzlePanel", new Color(0.02f, 0.03f, 0.05f, 0.97f));
        UIFactory.Stretch(bg.rectTransform);
        root = bg.gameObject;

        title = UIFactory.Label(root.transform, "Title", "拼接照片", 54, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(1200, 90));

        grid = UIFactory.NewRect("Grid", root.transform);
        UIFactory.Place(grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-120, 10), new Vector2(Cell * 2, Cell * 2));

        var pv = UIFactory.NewRect("Preview", root.transform);
        UIFactory.Place(pv, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(560, 120), new Vector2(300, 300));
        preview = pv.gameObject.AddComponent<RawImage>();
        var pvLabel = UIFactory.Label(root.transform, "PreviewLabel", "目標照片", 28, new Color(0.7f, 0.85f, 1f), TextAnchor.MiddleCenter);
        UIFactory.Place(pvLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(560, 300), new Vector2(300, 40));

        hint = UIFactory.Label(root.transform, "Hint", "", 30, new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleCenter);
        UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(1700, 120));

        root.SetActive(false);
    }

    public void Open(int cols, int rows, Texture photo, Action onSolved)
    {
        this.cols = cols; this.rows = rows; this.photo = photo; this.onSolved = onSolved;
        busy = false; selected = -1; cursor = 0;
        BuildCells();
        Scramble();
        Refresh();
        preview.texture = photo;
        preview.rectTransform.sizeDelta = new Vector2(300f, 300f * rows / cols);

        root.SetActive(true);
        GameManager.Instance.SetMode(InputMode.Puzzle);
        openedAt = Time.unscaledTime;
        AudioManager.Play(Sfx.Panel);
    }

    void BuildCells()
    {
        foreach (Transform c in grid) Destroy(c.gameObject);
        int n = cols * rows;
        piece = new int[n]; rot = new int[n];
        border = new Image[n]; cellRt = new RectTransform[n]; cellImg = new RawImage[n];
        grid.sizeDelta = new Vector2(Cell * cols, Cell * rows);

        for (int i = 0; i < n; i++)
        {
            int c = i % cols, r = i / cols;
            var b = UIFactory.NewRect("Cell" + i, grid);
            UIFactory.Place(b, new Vector2(0, 1), new Vector2(0, 1), new Vector2(c * Cell + Cell / 2, -(r * Cell + Cell / 2)), new Vector2(Cell, Cell), pivot: new Vector2(0.5f, 0.5f));
            border[i] = b.gameObject.AddComponent<Image>();

            var inner = UIFactory.NewRect("Img", b);
            UIFactory.Stretch(inner, 8, 8, 8, 8);
            var ri = inner.gameObject.AddComponent<RawImage>();
            ri.texture = photo;
            ri.raycastTarget = false;
            cellRt[i] = inner; cellImg[i] = ri;
        }
    }

    void Scramble()
    {
        int n = cols * rows;
        var rng = new System.Random();
        do
        {
            for (int i = 0; i < n; i++) { piece[i] = i; rot[i] = rng.Next(4); }
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (piece[i], piece[j]) = (piece[j], piece[i]);
            }
        } while (IsSolved());
    }

    bool IsSolved()
    {
        for (int i = 0; i < piece.Length; i++)
            if (piece[i] != i || rot[i] != 0) return false;
        return true;
    }

    void Refresh()
    {
        for (int i = 0; i < piece.Length; i++)
        {
            int pc = piece[i];
            int pc_c = pc % cols, pc_r = pc / cols;
            // 圖片左上為第 0 列；UV 的 v 由下往上
            cellImg[i].uvRect = new Rect(pc_c / (float)cols, (rows - 1 - pc_r) / (float)rows, 1f / cols, 1f / rows);
            cellRt[i].localEulerAngles = new Vector3(0, 0, -90f * rot[i]);

            Color bc = new Color(0.25f, 0.3f, 0.38f);
            if (i == selected) bc = new Color(1f, 0.85f, 0.2f);
            else if (i == cursor) bc = Color.white;
            border[i].color = bc;
        }
        hint.text = "方向鍵 / 左搖桿：移動　　" + GameInput.ConfirmLabel + "：選取 / 交換　　LB / RB：旋轉　　" + GameInput.CancelLabel + "：取消 / 關閉\n"
                  + (selected >= 0 ? "再選一格就會和它交換位置" : "把碎片排回正確的位置，並轉到正確的方向");
    }

    void Update()
    {
        if (!IsOpen || busy) return;
        if (Time.unscaledTime - openedAt < 0.25f) return; // 避免開啟時的同一次按鍵被吃掉

        var dir = nav.Poll(GameInput.PzNavigate.ReadValue<Vector2>());
        if (dir != Vector2Int.zero)
        {
            int c = cursor % cols, r = cursor / cols;
            c = Mathf.Clamp(c + dir.x, 0, cols - 1);
            r = Mathf.Clamp(r - dir.y, 0, rows - 1);
            cursor = r * cols + c;
            AudioManager.Play(Sfx.Click, 0.4f);
            Refresh();
        }

        if (GameInput.PzSelect.WasPressedThisFrame())
        {
            if (selected < 0) selected = cursor;
            else if (selected == cursor) selected = -1;
            else
            {
                (piece[selected], piece[cursor]) = (piece[cursor], piece[selected]);
                (rot[selected], rot[cursor]) = (rot[cursor], rot[selected]);
                selected = -1;
            }
            AudioManager.Play(Sfx.Click);
            Refresh();
            CheckSolved();
        }

        if (GameInput.PzRotateLeft.WasPressedThisFrame()) Rotate(-1);
        if (GameInput.PzRotateRight.WasPressedThisFrame()) Rotate(+1);

        if (GameInput.PzCancel.WasPressedThisFrame())
        {
            if (selected >= 0) { selected = -1; Refresh(); }
            else Close();
        }
    }

    void Rotate(int d)
    {
        int i = selected >= 0 ? selected : cursor;
        rot[i] = (rot[i] + d + 4) % 4;
        AudioManager.Play(Sfx.Click, 0.6f);
        Refresh();
        CheckSolved();
    }

    void CheckSolved()
    {
        if (IsSolved()) StartCoroutine(SolvedRoutine());
    }

    IEnumerator SolvedRoutine()
    {
        busy = true;
        hint.text = "照片拼接完成！";
        hint.color = new Color(0.4f, 1f, 0.6f);
        for (int i = 0; i < border.Length; i++) border[i].color = new Color(0.4f, 1f, 0.6f);
        AudioManager.Play(Sfx.Success);
        yield return new WaitForSecondsRealtime(1.4f);
        hint.color = new Color(0.85f, 0.9f, 1f);
        Close();
        onSolved?.Invoke();
    }

    public void Close()
    {
        root.SetActive(false);
        if (GameManager.Instance.State == GameState.Playing)
            GameManager.Instance.SetMode(InputMode.Gameplay);
    }
}

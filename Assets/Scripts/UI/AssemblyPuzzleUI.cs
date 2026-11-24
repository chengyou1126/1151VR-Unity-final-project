using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 中央控制電腦的組合語言解謎介面（全程手把操作）：
///   左搖桿 / 方向鍵：切換選項　A：選擇答案（游標自動移到「確認」）　A（在確認上）：提交　B：離開電腦
///   LB / RB：縮小 / 放大文字　右搖桿：上下左右捲動　按下右搖桿：切換捲動目標（程式碼 ⇄ 題目）
/// 程式碼區與題目區都是可捲動的視窗，所以長程式（第五題）放大後也看得到全部。
/// 答錯 → 生命 -1，可重新作答；答對 → 修復一個系統模組並解鎖下一題。
/// </summary>
public class AssemblyPuzzleUI : MonoBehaviour
{
    const float MinZoom = 0.5f, MaxZoom = 2.6f, ZoomStep = 1.15f;

    GameObject root;
    Text headerText, codeText, titleText, introText, questionText, logText, subText, paneLabel, codeTitle;
    Image[] hearts;
    Image[] moduleBoxes;
    Text[] moduleTexts;
    Button[] optionButtons;
    Image[] optionMarkers;
    Text[] optionTexts;
    LayoutElement[] optionLayouts;
    Button confirmButton, shutdownButton;
    LayoutElement confirmLayout, shutdownLayout;

    // 捲動區
    ScrollRect codeScroll, qScroll;
    Image codePaneBg, qPaneBg, codeBar, qBar;
    bool scrollTargetIsCode = true;
    GameObject lastSelected;

    // 縮放
    float zoom = 1f;
    int codeBase = 30, introBase = 26, questionBase = 32, optionBase = 30;
    float optionHeightBase = 82f;

    AssemblyPuzzleManager mgr;
    Action onShutdown;
    int picked = -1;
    bool busy;
    float openedAt;

    public bool IsOpen => root != null && root.activeSelf;
    public float Zoom => zoom;
    public float CodeScrollY => codeScroll.content.anchoredPosition.y;
    public float CodeContentHeight => codeScroll.content.rect.height;
    public float CodeViewHeight => codeScroll.viewport.rect.height;

    /// <summary>直接指定縮放比例（測試 / 教學用）。</summary>
    public void SetZoom(float z)
    {
        zoom = Mathf.Clamp(z, MinZoom, MaxZoom);
        ApplyZoom();
    }

    /// <summary>直接捲動到指定位置（像素；0 = 最上面）。</summary>
    public void ScrollTo(bool code, float y)
    {
        var sr = code ? codeScroll : qScroll;
        Canvas.ForceUpdateCanvases();
        var pos = sr.content.anchoredPosition;
        pos.y = Mathf.Clamp(y, 0f, Mathf.Max(0f, sr.content.rect.height - sr.viewport.rect.height));
        sr.content.anchoredPosition = pos;
    }

    static readonly Color Dim = new Color(0.12f, 0.16f, 0.2f);
    static readonly Color PaneIdle = new Color(0.02f, 0.06f, 0.05f, 1f);
    static readonly Color PaneActive = new Color(0.04f, 0.12f, 0.1f, 1f);

    // ───────────── 建立介面 ─────────────

    public void Build(Transform canvasRoot)
    {
        var bg = UIFactory.Panel(canvasRoot, "AssemblyPuzzlePanel", new Color(0.015f, 0.03f, 0.045f, 1f));
        UIFactory.Stretch(bg.rectTransform);
        root = bg.gameObject;

        headerText = UIFactory.Label(root.transform, "Header", "CENTRAL CONTROL SYSTEM   //   SYSTEM STATUS: ERROR", 30, UIFactory.Cyan, TextAnchor.MiddleLeft, true);
        UIFactory.Place(headerText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -20), new Vector2(1000, 50), new Vector2(0, 1));

        hearts = new Image[GameManager.MaxLives];
        for (int i = 0; i < hearts.Length; i++)
        {
            var h = UIFactory.NewRect("Heart" + i, root.transform);
            UIFactory.Place(h, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40 - (hearts.Length - 1 - i) * 50, -22), new Vector2(40, 40), new Vector2(1, 1));
            hearts[i] = h.gameObject.AddComponent<Image>();
            hearts[i].sprite = UIFactory.HeartSprite;
            hearts[i].raycastTarget = false;
        }

        BuildCodePane();
        BuildQuestionPane();

        // 左下：系統訊息
        var logBg = UIFactory.Panel(root.transform, "LogPanel", new Color(0.03f, 0.05f, 0.08f, 1f));
        UIFactory.Place(logBg.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 30), new Vector2(880, 150), new Vector2(0, 0));
        logText = UIFactory.Label(logBg.transform, "Log", "", 28, Color.white, TextAnchor.UpperLeft, true);
        UIFactory.Stretch(logText.rectTransform, 20, 12, 20, 10);
        logText.resizeTextForBestFit = true;
        logText.resizeTextMinSize = 18;
        logText.resizeTextMaxSize = 28;
        logText.verticalOverflow = VerticalWrapMode.Truncate;

        root.SetActive(false);
        GameManager.Instance.OnStateChanged += s => { if (s != GameState.Playing && root.activeSelf) root.SetActive(false); };
        GameManager.Instance.OnLivesChanged += _ => RefreshHearts();
    }

    ScrollRect MakeScroll(Transform parent, string name, bool horizontal, out RectTransform content)
    {
        var rt = UIFactory.NewRect(name, parent);
        UIFactory.Stretch(rt);
        var sr = rt.gameObject.AddComponent<ScrollRect>();

        var vp = UIFactory.NewRect("Viewport", rt);
        UIFactory.Stretch(vp);
        vp.gameObject.AddComponent<RectMask2D>();
        var hit = vp.gameObject.AddComponent<Image>();   // 讓滑鼠滾輪 / 拖曳有東西可以點到
        hit.color = new Color(0, 0, 0, 0);

        content = UIFactory.NewRect("Content", vp);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = horizontal ? new Vector2(0, 1) : new Vector2(1, 1);
        content.pivot = new Vector2(0, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        sr.viewport = vp;
        sr.content = content;
        sr.horizontal = horizontal;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.inertia = false;
        sr.scrollSensitivity = 40f;
        return sr;
    }

    Image MakeBar(Transform parent)
    {
        var bar = UIFactory.Panel(parent, "ScrollBar", new Color(0.4f, 0.8f, 1f, 0.55f));
        bar.raycastTarget = false;
        bar.rectTransform.anchorMin = new Vector2(1, 1);
        bar.rectTransform.anchorMax = new Vector2(1, 1);
        bar.rectTransform.pivot = new Vector2(1, 1);
        bar.rectTransform.sizeDelta = new Vector2(8, 40);
        bar.gameObject.SetActive(false);
        return bar;
    }

    void BuildCodePane()
    {
        codePaneBg = UIFactory.Panel(root.transform, "CodePanel", PaneIdle);
        UIFactory.Place(codePaneBg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -80), new Vector2(880, 800), new Vector2(0, 1));

        codeTitle = UIFactory.Label(codePaneBg.transform, "CodeTitle", "// PROGRAM LISTING", 24, new Color(0.3f, 0.6f, 0.4f), TextAnchor.UpperLeft, true);
        UIFactory.Place(codeTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -10), new Vector2(840, 32), new Vector2(0, 1));

        var holder = UIFactory.NewRect("ScrollHolder", codePaneBg.transform);
        UIFactory.Stretch(holder, 16, 48, 16, 12);
        codeScroll = MakeScroll(holder, "CodeScroll", true, out var content);

        codeText = UIFactory.Label(content, "Code", "", 30, new Color(0.55f, 1f, 0.7f), TextAnchor.UpperLeft, true);
        codeText.horizontalOverflow = HorizontalWrapMode.Overflow;
        codeText.verticalOverflow = VerticalWrapMode.Overflow;
        // content 跟著文字大小（文字大小由下面的 LayoutGroup 控制）
        var csf2 = content.gameObject.AddComponent<ContentSizeFitter>();
        csf2.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf2.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var lg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        lg.childControlWidth = true; lg.childControlHeight = true;
        lg.childForceExpandWidth = false; lg.childForceExpandHeight = false;

        codeBar = MakeBar(codePaneBg.transform);
    }

    void BuildQuestionPane()
    {
        // 上方固定：系統模組狀態列
        var strip = UIFactory.NewRect("ModuleStrip", root.transform);
        UIFactory.Place(strip, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero, new Vector2(0, 1));
        strip.offsetMin = new Vector2(960, -158);
        strip.offsetMax = new Vector2(-40, -80);
        var h2 = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
        h2.spacing = 8;
        h2.childControlWidth = true; h2.childControlHeight = true;
        h2.childForceExpandWidth = true; h2.childForceExpandHeight = false;
        int mc = AssemblyQuestions.ModuleNames.Length;
        moduleBoxes = new Image[mc];
        moduleTexts = new Text[mc];
        for (int i = 0; i < mc; i++)
        {
            var box = UIFactory.Panel(strip, "Module" + i, Dim);
            box.gameObject.AddComponent<LayoutElement>().preferredHeight = 78;
            moduleBoxes[i] = box;
            var t = UIFactory.Label(box.transform, "T", "", 20, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Stretch(t.rectTransform, 4, 4, 4, 4);
            moduleTexts[i] = t;
        }

        // 下方：可捲動的題目區
        qPaneBg = UIFactory.Panel(root.transform, "QuestionPane", PaneIdle);
        UIFactory.Place(qPaneBg.rectTransform, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero, new Vector2(0, 0));
        qPaneBg.rectTransform.offsetMin = new Vector2(960, 70);
        qPaneBg.rectTransform.offsetMax = new Vector2(-40, -168);

        qScroll = MakeScroll(qPaneBg.transform, "QScroll", false, out var content);
        var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.UpperLeft;
        v.spacing = 12;
        v.padding = new RectOffset(6, 18, 6, 12);
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        var fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        titleText = AddText(content, "Title", 34, UIFactory.Cyan, false, 44);
        introText = AddText(content, "Intro", 26, new Color(1f, 0.85f, 0.4f), true, 0);
        questionText = AddText(content, "Question", 32, Color.white, false, 0);

        optionButtons = new Button[4];
        optionMarkers = new Image[4];
        optionTexts = new Text[4];
        optionLayouts = new LayoutElement[4];
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            var b = UIFactory.MakeButton(content, "Option" + i, "", () => OnOption(idx), 30, 100, 80);
            optionButtons[i] = b;
            optionLayouts[i] = b.gameObject.AddComponent<LayoutElement>();
            optionTexts[i] = b.GetComponentInChildren<Text>();
            optionTexts[i].alignment = TextAnchor.MiddleLeft;
            UIFactory.Stretch(optionTexts[i].rectTransform, 40, 4, 12, 4);

            var m = UIFactory.Panel(b.transform, "Marker", UIFactory.Green);
            m.raycastTarget = false;
            UIFactory.Place(m.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(10, 0), new Vector2(12, -16), new Vector2(0.5f, 0.5f));
            optionMarkers[i] = m;
        }

        confirmButton = UIFactory.MakeButton(content, "Confirm", "確認答案  (CONFIRM)", OnConfirm, 32, 100, 70);
        confirmLayout = confirmButton.gameObject.AddComponent<LayoutElement>();
        confirmLayout.preferredHeight = 70;
        confirmButton.GetComponent<Image>().color = new Color(0.7f, 1f, 0.8f);

        shutdownButton = UIFactory.MakeButton(content, "Shutdown", "啟動緊急停止程序  (EMERGENCY SHUTDOWN)", OnShutdown, 36, 100, 110);
        shutdownLayout = shutdownButton.gameObject.AddComponent<LayoutElement>();
        shutdownLayout.preferredHeight = 110;
        shutdownButton.GetComponent<Image>().color = new Color(1f, 0.55f, 0.5f);

        qBar = MakeBar(qPaneBg.transform);

        // 底部固定提示列
        subText = UIFactory.Label(root.transform, "Hint", "", 22, new Color(0.6f, 0.75f, 0.9f), TextAnchor.MiddleLeft);
        UIFactory.Place(subText.rectTransform, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, Vector2.zero, new Vector2(0, 0));
        subText.rectTransform.offsetMin = new Vector2(960, 12);
        subText.rectTransform.offsetMax = new Vector2(-40, 66);

        // 左上方：目前捲動目標 / 縮放比例
        paneLabel = UIFactory.Label(root.transform, "PaneLabel", "", 22, UIFactory.Cyan, TextAnchor.MiddleRight, true);
        UIFactory.Place(paneLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(560, -90), new Vector2(340, 30), new Vector2(0, 1));
    }

    Text AddText(Transform parent, string name, int size, Color color, bool mono, float minHeight)
    {
        var t = UIFactory.Label(parent, name, "", size, color, TextAnchor.UpperLeft, mono);
        var le = t.gameObject.AddComponent<LayoutElement>();
        if (minHeight > 0) le.minHeight = minHeight;
        return t;
    }

    // ───────────── 開啟 / 關閉 ─────────────

    public void Open(AssemblyPuzzleManager manager, Action onShutdownPressed)
    {
        mgr = manager;
        onShutdown = onShutdownPressed;
        busy = false;
        root.SetActive(true);
        GameManager.Instance.SetMode(InputMode.UI);
        openedAt = Time.unscaledTime;
        AudioManager.Play(Sfx.Panel);
        Log("系統待命。請依序修復五個系統模組。", UIFactory.Cyan);
        Refresh();
    }

    public void Close()
    {
        root.SetActive(false);
        UIManager.Instance.ClearSelection();
        if (GameManager.Instance.State == GameState.Playing) GameManager.Instance.SetMode(InputMode.Gameplay);
    }

    void Update()
    {
        if (!IsOpen) return;

        // 縮放與捲動在作答中（busy）也可以用；離開電腦只在非 busy 時
        HandleZoom();
        HandleScroll();
        FollowSelection();
        UpdateBars();

        if (busy || Time.unscaledTime - openedAt < 0.3f) return;
        if (GameInput.UICancel.WasPressedThisFrame()) Close();
    }

    // ───────────── 縮放 / 捲動 ─────────────

    void HandleZoom()
    {
        int dir = 0;
        if (GameInput.UIZoomIn.WasPressedThisFrame()) dir = 1;
        else if (GameInput.UIZoomOut.WasPressedThisFrame()) dir = -1;
        // 滑鼠：Ctrl + 滾輪
        var mouse = UnityEngine.InputSystem.Mouse.current;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (mouse != null && kb != null && kb.ctrlKey.isPressed)
        {
            float wheel = mouse.scroll.ReadValue().y;
            if (wheel > 0.5f) dir = 1; else if (wheel < -0.5f) dir = -1;
        }
        if (dir == 0) return;

        zoom = Mathf.Clamp(zoom * (dir > 0 ? ZoomStep : 1f / ZoomStep), MinZoom, MaxZoom);
        ApplyZoom();
        AudioManager.Play(Sfx.Click, 0.3f);
    }

    void ApplyZoom()
    {
        codeText.fontSize = Mathf.Max(8, Mathf.RoundToInt(codeBase * zoom));
        introText.fontSize = Mathf.Max(8, Mathf.RoundToInt(introBase * zoom));
        questionText.fontSize = Mathf.Max(8, Mathf.RoundToInt(questionBase * zoom));
        titleText.fontSize = Mathf.Max(10, Mathf.RoundToInt(34 * zoom));
        for (int i = 0; i < 4; i++)
        {
            optionTexts[i].fontSize = Mathf.Max(8, Mathf.RoundToInt(optionBase * zoom));
            optionLayouts[i].preferredHeight = Mathf.Max(40f, optionHeightBase * zoom);
        }
        confirmLayout.preferredHeight = Mathf.Max(44f, 70f * zoom);
        shutdownLayout.preferredHeight = Mathf.Max(60f, 110f * zoom);
        UIFactory.SetButtonFontSize(confirmButton, Mathf.RoundToInt(32 * zoom));
        UIFactory.SetButtonFontSize(shutdownButton, Mathf.RoundToInt(36 * zoom));
        UpdatePaneLabel();
    }

    void HandleScroll()
    {
        if (GameInput.UIScrollTarget.WasPressedThisFrame())
        {
            scrollTargetIsCode = !scrollTargetIsCode;
            AudioManager.Play(Sfx.Click, 0.3f);
            UpdatePaneLabel();
        }

        Vector2 v = GameInput.UIScroll.ReadValue<Vector2>();
        if (v.magnitude < 0.2f) return;

        var sr = scrollTargetIsCode ? codeScroll : qScroll;
        float contentH = sr.content.rect.height, viewH = sr.viewport.rect.height;
        float contentW = sr.content.rect.width, viewW = sr.viewport.rect.width;
        float speed = 900f * Time.unscaledDeltaTime;   // 像素/秒

        var pos = sr.content.anchoredPosition;
        pos.y -= v.y * speed;                           // 搖桿往上 = 往上看（內容往下移）
        if (sr.horizontal) pos.x -= v.x * speed;
        pos.y = Mathf.Clamp(pos.y, 0f, Mathf.Max(0f, contentH - viewH));
        pos.x = Mathf.Clamp(pos.x, -Mathf.Max(0f, contentW - viewW), 0f);
        sr.content.anchoredPosition = pos;
    }

    /// <summary>選到的選項捲到可見範圍內；選到第一個選項時回到最上面，才看得到題目。</summary>
    void FollowSelection()
    {
        var es = EventSystem.current;
        if (es == null) return;
        var sel = es.currentSelectedGameObject;
        if (sel == lastSelected || sel == null) return;
        lastSelected = sel;
        if (!sel.transform.IsChildOf(qScroll.content)) return;

        var item = (RectTransform)sel.transform;
        if (sel == optionButtons[0].gameObject) { qScroll.content.anchoredPosition = Vector2.zero; return; }

        var corners = new Vector3[4];
        item.GetWorldCorners(corners);
        var vp = qScroll.viewport;
        float bottom = vp.InverseTransformPoint(corners[0]).y, top = vp.InverseTransformPoint(corners[1]).y;
        var r = vp.rect;
        var pos = qScroll.content.anchoredPosition;
        if (bottom < r.yMin) pos.y += (r.yMin - bottom) + 12f;
        else if (top > r.yMax) pos.y -= (top - r.yMax) + 12f;
        pos.y = Mathf.Clamp(pos.y, 0f, Mathf.Max(0f, qScroll.content.rect.height - r.height));
        qScroll.content.anchoredPosition = pos;
    }

    void UpdateBars()
    {
        UpdateBar(codeBar, codeScroll, codePaneBg.rectTransform.rect.height - 60f, -48f);
        UpdateBar(qBar, qScroll, qPaneBg.rectTransform.rect.height - 8f, -4f);
    }

    static void UpdateBar(Image bar, ScrollRect sr, float trackH, float topOffset)
    {
        float contentH = sr.content.rect.height, viewH = sr.viewport.rect.height;
        if (contentH <= viewH + 1f) { if (bar.gameObject.activeSelf) bar.gameObject.SetActive(false); return; }
        if (!bar.gameObject.activeSelf) bar.gameObject.SetActive(true);
        float ratio = Mathf.Clamp01(viewH / contentH);
        float h = Mathf.Max(30f, trackH * ratio);
        float t = Mathf.Clamp01(sr.content.anchoredPosition.y / (contentH - viewH));
        bar.rectTransform.sizeDelta = new Vector2(8, h);
        bar.rectTransform.anchoredPosition = new Vector2(-4, topOffset - t * (trackH - h));
    }

    void UpdatePaneLabel()
    {
        codePaneBg.color = scrollTargetIsCode ? PaneActive : PaneIdle;
        qPaneBg.color = scrollTargetIsCode ? PaneIdle : PaneActive;
        paneLabel.text = "ZOOM " + Mathf.RoundToInt(zoom * 100f) + "%   SCROLL: " + (scrollTargetIsCode ? "CODE" : "QUESTION");
        string scroll = GameInput.UsingGamepad ? "右搖桿" : "PageUp/Down";
        string toggle = GameInput.UsingGamepad ? "按下右搖桿" : "Tab";
        string zoomKeys = GameInput.UsingGamepad ? "LB/RB" : "- / =";
        subText.text = GameInput.ConfirmLabel + "：選擇/確認   " + GameInput.CancelLabel + "：離開   " + zoomKeys + "：縮放   " + scroll + "：捲動（" + toggle + " 切換程式碼/題目）";
    }

    // ───────────── 顯示 ─────────────

    void RefreshHearts()
    {
        if (hearts == null) return;
        int lives = GameManager.Instance.Data.lives;
        for (int i = 0; i < hearts.Length; i++)
            hearts[i].color = i < lives ? new Color(1f, 0.25f, 0.3f) : new Color(0.25f, 0.25f, 0.28f, 0.8f);
    }

    void RefreshModules()
    {
        for (int i = 0; i < moduleBoxes.Length; i++)
        {
            bool ok = mgr.IsModuleRestored(i);
            moduleBoxes[i].color = ok ? new Color(0.1f, 0.45f, 0.22f) : new Color(0.5f, 0.1f, 0.1f);
            moduleTexts[i].text = AssemblyQuestions.ModuleNames[i] + "\n" + (ok ? "RESTORED" : AssemblyQuestions.ModuleInitialState[i]);
        }
    }

    void Refresh()
    {
        RefreshHearts();
        RefreshModules();
        picked = -1;
        qScroll.content.anchoredPosition = Vector2.zero;
        codeScroll.content.anchoredPosition = Vector2.zero;
        scrollTargetIsCode = true;

        if (mgr.AllDone)
        {
            ShowRestored();
            return;
        }

        var s = mgr.Current;
        int lines = 1; foreach (char ch in s.code) if (ch == '\n') lines++;

        titleText.text = s.header + "     " + Stars(s.difficulty) + "   [" + AssemblyQuestions.ModuleNames[s.module] + "]";
        introText.text = s.intro;
        questionText.text = s.question;
        codeText.text = s.code;

        // 預設縮放：長程式預設縮小以便一眼看到大部分，之後可自行放大再捲動
        codeBase = 30; introBase = 26; questionBase = 32; optionBase = s.bigOptions ? 26 : 30;
        optionHeightBase = s.bigOptions ? 116f : 82f;
        zoom = lines > 24 ? 0.7f : 1f;

        introText.gameObject.SetActive(true);
        questionText.gameObject.SetActive(true);
        shutdownButton.gameObject.SetActive(false);
        confirmButton.gameObject.SetActive(true);

        for (int i = 0; i < 4; i++)
        {
            optionButtons[i].gameObject.SetActive(true);
            optionButtons[i].interactable = true;
            optionTexts[i].text = s.options[i];
            optionMarkers[i].gameObject.SetActive(false);
        }
        confirmButton.interactable = true;
        ApplyZoom();

        UIManager.Instance.Focus(optionButtons[0].gameObject);
    }

    void ShowRestored()
    {
        titleText.text = "SYSTEM RESTORED";
        introText.text = "ALL MODULES OPERATIONAL\nEMERGENCY SHUTDOWN: READY";
        questionText.text = "所有系統模組恢復正常。\n緊急停止功能已解鎖。\n請確認是否立即執行緊急停止程序。";
        codeText.text = "> SYSTEM RESTORED\n> ALL MODULES OPERATIONAL\n> EMERGENCY SHUTDOWN: READY\n>\n> AWAITING COMMAND_";
        for (int i = 0; i < 4; i++) optionButtons[i].gameObject.SetActive(false);
        confirmButton.gameObject.SetActive(false);
        shutdownButton.gameObject.SetActive(true);
        zoom = 1f; codeBase = 30; introBase = 26; questionBase = 32;
        ApplyZoom();
        Log("所有系統模組已修復。", UIFactory.Green);
        UIManager.Instance.Focus(shutdownButton.gameObject);
    }

    static string Stars(int n)
    {
        return new string('★', n) + new string('☆', 5 - n);
    }

    void Log(string text, Color color)
    {
        logText.color = color;
        logText.text = text;
    }

    // ───────────── 作答 ─────────────

    void OnOption(int index)
    {
        if (busy) return;
        picked = index;
        for (int i = 0; i < 4; i++) optionMarkers[i].gameObject.SetActive(i == picked);
        AudioManager.Play(Sfx.Click);
        Log("已選擇 " + (char)('A' + index) + "。移到「確認答案」按 " + GameInput.ConfirmLabel + " 提交。", UIFactory.Cyan);
        UIManager.Instance.Focus(confirmButton.gameObject); // 選完自動移到確認鈕
    }

    void OnConfirm()
    {
        if (busy) return;
        if (picked < 0)
        {
            Log("請先選擇一個答案。", new Color(1f, 0.85f, 0.4f));
            AudioManager.Play(Sfx.Error, 0.5f);
            return;
        }

        var step = mgr.Current;
        var result = mgr.Submit(picked);

        switch (result)
        {
            case AnswerResult.Correct:
                StartCoroutine(CorrectRoutine(step));
                break;

            case AnswerResult.Wrong:
                AudioManager.Play(Sfx.Error);
                UIManager.Instance.Flash(new Color(1f, 0f, 0f, 0.35f), 0.5f);
                Log(step.failText + "\n（生命 -1）", UIFactory.Red);
                RefreshHearts();
                picked = -1;
                for (int i = 0; i < 4; i++) optionMarkers[i].gameObject.SetActive(false);
                UIManager.Instance.Focus(optionButtons[0].gameObject);
                break;

            case AnswerResult.GameOver:
                RefreshHearts();
                break;
        }
    }

    IEnumerator CorrectRoutine(AssemblyStep step)
    {
        busy = true;
        AudioManager.Play(Sfx.Success);
        Log(step.successText, UIFactory.Green);
        RefreshModules();
        for (int i = 0; i < 4; i++) { optionButtons[i].interactable = false; }
        confirmButton.interactable = false;
        UIManager.Instance.ClearSelection();

        yield return new WaitForSecondsRealtime(2.4f);

        busy = false;
        string keep = logText.text;
        Refresh();
        if (!mgr.AllDone) Log(keep, UIFactory.Green);
    }

    void OnShutdown()
    {
        if (busy) return;
        busy = true;
        Close();
        busy = false;
        onShutdown?.Invoke();
    }
}

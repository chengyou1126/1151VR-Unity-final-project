using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 常駐 UI：HUD、對話框、選單（主選單 / 暫停 / Game Over / Game Clear / 手把斷線）、任務與照片資訊面板。
/// 所有介面都由程式建立（見 UIFactory），全程支援手把導覽（EventSystem + InputSystemUIInputModule）。
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    public Canvas Canvas { get; private set; }
    public RectTransform Root { get; private set; }
    public PhotoPuzzleController PhotoPuzzle { get; private set; }
    public AssemblyPuzzleUI Assembly { get; private set; }

    // HUD
    GameObject hudRoot;
    Image[] hearts;
    Text chapterText, missionText, photoText, promptText, toastText;
    Image promptBg, flashImg, fadeImg;

    // 面板
    GameObject mainMenuPanel, howToPanel, pausePanel, gameOverPanel, clearPanel, controllerPanel,
               missionPanel, photoPanel, dialoguePanel;
    Text controllerText, clearLivesText, missionPanelText, photoPanelText, speakerText, dialogueText, dialogueHint;
    Button mainStartButton, pauseResumeButton, gameOverRestartButton, clearMenuButton, controllerButton,
           howToBackButton, photoPuzzleButton;
    Action howToOnClose;

    string mission = "";
    Coroutine toastCo, flashCo, dialogueCo;
    GameObject selectionBeforeOverlay;

    // ───────────── 初始化 ─────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        BuildEventSystem();
        BuildCanvas();
        BuildHud();
        BuildDialogue();
        BuildMenus();
        BuildInfoPanels();

        var photo = new GameObject("PhotoPuzzle").AddComponent<PhotoPuzzleController>();
        photo.transform.SetParent(transform, false);
        photo.Build(Root);
        PhotoPuzzle = photo;

        var asm = new GameObject("AssemblyPuzzle").AddComponent<AssemblyPuzzleUI>();
        asm.transform.SetParent(transform, false);
        asm.Build(Root);
        Assembly = asm;

        BuildOverlays(); // 淡入淡出與閃紅必須在最上層

        var gm = GameManager.Instance;
        gm.OnLivesChanged += _ => RefreshLives();
        gm.OnStateChanged += OnStateChanged;
        RefreshLives();
        SetHudVisible(false);
    }

    void BuildEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.transform.SetParent(transform, false);
        go.AddComponent<EventSystem>();
        var module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }

    void BuildCanvas()
    {
        var go = new GameObject("UI_Canvas");
        go.transform.SetParent(transform, false);
        Canvas = go.AddComponent<Canvas>();
        Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Canvas.sortingOrder = 100;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2048, 1152);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        // Keep at least 1920 x 1080 layout units inside the screen margins.
        Root = UIFactory.NewRect("UIContent", Canvas.transform);
        UIFactory.Stretch(Root, 64, 36, 64, 36);
        go.AddComponent<GraphicRaycaster>();
    }

    void BuildHud()
    {
        var rt = UIFactory.NewRect("HUD", Root);
        UIFactory.Stretch(rt);
        hudRoot = rt.gameObject;

        // 五條命
        hearts = new Image[GameManager.MaxLives];
        for (int i = 0; i < hearts.Length; i++)
        {
            var h = UIFactory.NewRect("Heart" + i, hudRoot.transform);
            UIFactory.Place(h, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60 + i * 56, -56), new Vector2(46, 46));
            hearts[i] = h.gameObject.AddComponent<Image>();
            hearts[i].sprite = UIFactory.HeartSprite;
            hearts[i].raycastTarget = false;
        }

        missionText = UIFactory.Label(hudRoot.transform, "Mission", "", 30, Color.white);
        UIFactory.Place(missionText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -118), new Vector2(760, 130), new Vector2(0, 1));
        AddOutline(missionText);

        photoText = UIFactory.Label(hudRoot.transform, "PhotoProgress", "", 28, new Color(1f, 0.9f, 0.45f));
        UIFactory.Place(photoText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -250), new Vector2(760, 44), new Vector2(0, 1));
        AddOutline(photoText);

        chapterText = UIFactory.Label(hudRoot.transform, "Chapter", "", 34, UIFactory.Cyan, TextAnchor.UpperRight);
        UIFactory.Place(chapterText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -36), new Vector2(520, 50), new Vector2(1, 1));
        AddOutline(chapterText);

        // 準星
        var cross = UIFactory.Panel(hudRoot.transform, "Crosshair", new Color(1, 1, 1, 0.75f));
        cross.sprite = UIFactory.CircleSprite;
        cross.raycastTarget = false;
        UIFactory.Place(cross.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8, 8));

        // 互動提示
        promptBg = UIFactory.Panel(hudRoot.transform, "PromptBg", new Color(0, 0, 0, 0.55f));
        promptBg.raycastTarget = false;
        UIFactory.Place(promptBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(760, 70));
        promptText = UIFactory.Label(promptBg.transform, "PromptText", "", 36, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Stretch(promptText.rectTransform);
        promptBg.gameObject.SetActive(false);

        // Toast
        toastText = UIFactory.Label(Root, "Toast", "", 40, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Place(toastText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -200), new Vector2(1500, 120));
        AddOutline(toastText);
        toastText.gameObject.SetActive(false);
    }

    static void AddOutline(Text t)
    {
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0, 0, 0, 0.85f);
        o.effectDistance = new Vector2(2, -2);
    }

    void BuildDialogue()
    {
        var bg = UIFactory.Panel(Root, "Dialogue", new Color(0.03f, 0.05f, 0.08f, 0.92f));
        UIFactory.Place(bg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(1500, 330));
        dialoguePanel = bg.gameObject;

        speakerText = UIFactory.Label(dialoguePanel.transform, "Speaker", "", 36, UIFactory.Cyan);
        UIFactory.Place(speakerText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -22), new Vector2(900, 50), new Vector2(0, 1));

        dialogueText = UIFactory.Label(dialoguePanel.transform, "Text", "", 36, Color.white);
        UIFactory.Place(dialogueText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -80), new Vector2(1428, 185), new Vector2(0, 1));

        dialogueHint = UIFactory.Label(dialoguePanel.transform, "Hint", "", 28, new Color(0.7f, 0.8f, 0.9f), TextAnchor.LowerRight);
        UIFactory.Place(dialogueHint.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 14), new Vector2(400, 40), new Vector2(1, 0));

        dialoguePanel.SetActive(false);
    }

    GameObject MakeMenu(string name, string title, string subtitle, out RectTransform column, float width = 560f)
    {
        var bg = UIFactory.Panel(Root, name, new Color(0.02f, 0.03f, 0.05f, 0.9f));
        UIFactory.Stretch(bg.rectTransform);

        var t = UIFactory.Label(bg.transform, "Title", title, 96, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -190), new Vector2(1600, 140));
        AddOutline(t);

        var s = UIFactory.Label(bg.transform, "Subtitle", subtitle, 36, UIFactory.Cyan, TextAnchor.MiddleCenter);
        UIFactory.Place(s.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -310), new Vector2(1600, 60));

        column = UIFactory.NewRect("Buttons", bg.transform);
        UIFactory.Place(column, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(width, 560));
        var v = column.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.UpperCenter;
        v.spacing = 22;
        v.childControlWidth = true;
        v.childControlHeight = false;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        bg.gameObject.SetActive(false);
        return bg.gameObject;
    }

    void BuildMenus()
    {
        RectTransform col;

        // 主選單
        mainMenuPanel = MakeMenu("MainMenu", "感染日  D-DAY", "阻止實驗室事故，拯救世界", out col);
        mainStartButton = UIFactory.MakeButton(col, "Start", "Start Game", () => { AudioManager.Play(Sfx.Click); mainMenuPanel.SetActive(false); GameManager.Instance.StartNewGame(); });
        UIFactory.MakeButton(col, "HowTo", "How to Play", () => { AudioManager.Play(Sfx.Click); ShowControlsHelp(null); });
        UIFactory.MakeButton(col, "Quit", "Quit Game", () =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
        var mmHint = UIFactory.Label(mainMenuPanel.transform, "Hint", "左類比搖桿 / 方向鍵：選擇　　A：確認", 30, new Color(0.7f, 0.8f, 0.9f), TextAnchor.MiddleCenter);
        UIFactory.Place(mmHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 80), new Vector2(1400, 50));

        // 暫停
        pausePanel = MakeMenu("Pause", "PAUSED", "遊戲暫停", out col);
        pauseResumeButton = UIFactory.MakeButton(col, "Resume", "Resume", () => GameManager.Instance.Resume());
        UIFactory.MakeButton(col, "Menu", "Main Menu", () => { pausePanel.SetActive(false); GameManager.Instance.ReturnToMainMenu(); });

        // Game Over
        gameOverPanel = MakeMenu("GameOver", "GAME OVER", "生命值已歸零", out col);
        gameOverRestartButton = UIFactory.MakeButton(col, "Restart", "Restart Game", () => { gameOverPanel.SetActive(false); GameManager.Instance.StartNewGame(); });
        UIFactory.MakeButton(col, "Menu", "Main Menu", () => { gameOverPanel.SetActive(false); GameManager.Instance.ReturnToMainMenu(); });
        gameOverPanel.GetComponent<Image>().color = new Color(0.12f, 0.01f, 0.01f, 0.93f);

        // Game Clear
        clearPanel = UIFactory.Panel(Root, "GameClear", new Color(0.025f, 0.055f, 0.09f, 0.91f)).gameObject;
        UIFactory.Place(clearPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, 32), new Vector2(740, 310));
        var resultLayout = clearPanel.AddComponent<VerticalLayoutGroup>();
        resultLayout.childAlignment = TextAnchor.MiddleCenter;
        resultLayout.spacing = 8;
        resultLayout.childControlWidth = resultLayout.childControlHeight = false;
        resultLayout.childForceExpandWidth = resultLayout.childForceExpandHeight = false;
        var title = UIFactory.Label(clearPanel.transform, "Title", "任務完成", 58, Color.white, TextAnchor.MiddleCenter);
        title.rectTransform.sizeDelta = new Vector2(700, 82);
        var subtitle = UIFactory.Label(clearPanel.transform, "Subtitle", "成功阻止實驗室事故", 32, UIFactory.Cyan, TextAnchor.MiddleCenter);
        subtitle.rectTransform.sizeDelta = new Vector2(700, 48);
        clearLivesText = UIFactory.Label(clearPanel.transform, "Lives", "", 30, Color.white, TextAnchor.MiddleCenter);
        clearLivesText.rectTransform.sizeDelta = new Vector2(700, 46);
        clearMenuButton = UIFactory.MakeButton(clearPanel.transform, "Menu", "返回主選單", () => { clearPanel.SetActive(false); GameManager.Instance.ReturnToMainMenu(); }, 30, 320, 64);
        clearPanel.SetActive(false);
        // 手把斷線
        controllerPanel = MakeMenu("ControllerOverlay", "Controller Disconnected", "", out col, 760f);
        controllerPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.93f);
        controllerText = controllerPanel.transform.Find("Subtitle").GetComponent<Text>();
        controllerButton = UIFactory.MakeButton(col, "Continue", "Continue", () => ControllerManager.Instance.Acknowledge(), 34, 760f);

        // How to Play
        howToPanel = UIFactory.Panel(Root, "HowToPlay", new Color(0.02f, 0.03f, 0.05f, 0.95f)).gameObject;
        UIFactory.Stretch(howToPanel.GetComponent<RectTransform>());
        var ht = UIFactory.Label(howToPanel.transform, "Title", "How to Play  操作說明", 64, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Place(ht.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -56), new Vector2(1400, 84), new Vector2(0.5f, 1f));
        string[] keys = { "左類比搖桿", "右類比搖桿", "RB", "LB", "B", "A", "X", "Y", "Start", "方向鍵 / 左搖桿" };
        string[] actions = { "移動", "視角", "奔跑（按住）", "跳躍", "蹲下（按住）；介面中 = 返回", "互動 / 確認", "任務資訊", "照片收集", "暫停", "切換選項" };
        var top = new Vector2(0.5f, 1f);
        for (int i = 0; i < keys.Length; i++)
        {
            float y = -194f - 56f * i;
            var key = UIFactory.Label(howToPanel.transform, "Key" + i, keys[i], 34, UIFactory.Cyan, TextAnchor.MiddleRight);
            UIFactory.Place(key.rectTransform, top, top, new Vector2(-40, y), new Vector2(520, 54), new Vector2(1f, 0.5f));
            var action = UIFactory.Label(howToPanel.transform, "Action" + i, actions[i], 34, Color.white, TextAnchor.MiddleLeft);
            UIFactory.Place(action.rectTransform, top, top, new Vector2(40, y), new Vector2(700, 54), new Vector2(0f, 0.5f));
        }
        var kb = UIFactory.Label(howToPanel.transform, "Keyboard",
            "鍵盤滑鼠：WASD 移動 · 滑鼠視角 · Shift 奔跑 · Space 跳躍 · Ctrl/C 蹲下\nE 互動 · Enter 確認 · Backspace 返回 · M 任務 · Tab 照片 · Esc 暫停",
            26, new Color(0.7f, 0.8f, 0.9f), TextAnchor.MiddleCenter);
        UIFactory.Place(kb.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 225), new Vector2(1600, 124), new Vector2(0.5f, 0.5f));
        howToBackButton = UIFactory.MakeButton(howToPanel.transform, "Back", "OK", CloseHowTo, 34, 360f, 76f);
        UIFactory.Place(howToBackButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 64), new Vector2(360, 76));
        howToPanel.SetActive(false);
    }

    void BuildInfoPanels()
    {
        // 任務資訊（X）
        missionPanel = UIFactory.Panel(Root, "MissionPanel", new Color(0.02f, 0.04f, 0.07f, 0.94f)).gameObject;
        UIFactory.Place(missionPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 640));
        var mt = UIFactory.Label(missionPanel.transform, "Title", "任務資訊", 54, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Place(mt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(1000, 80));
        missionPanelText = UIFactory.Label(missionPanel.transform, "Body", "", 38, Color.white);
        UIFactory.Place(missionPanelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1060, 440));
        var mh = UIFactory.Label(missionPanel.transform, "Hint", "", 28, new Color(0.7f, 0.8f, 0.9f), TextAnchor.MiddleCenter);
        UIFactory.Place(mh.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 36), new Vector2(1000, 40));
        mh.name = "Hint";
        missionPanel.SetActive(false);

        // 照片收集（Y）
        photoPanel = UIFactory.Panel(Root, "PhotoPanel", new Color(0.02f, 0.04f, 0.07f, 0.94f)).gameObject;
        UIFactory.Place(photoPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1120, 820));
        var pt = UIFactory.Label(photoPanel.transform, "Title", "Photo Collection", 54, Color.white, TextAnchor.MiddleCenter);
        UIFactory.Place(pt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(900, 80));
        photoPanelText = UIFactory.Label(photoPanel.transform, "Body", "", 40, Color.white, TextAnchor.UpperLeft, true);
        UIFactory.Place(photoPanelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 12), new Vector2(1000, 508));
        photoPuzzleButton = UIFactory.MakeButton(photoPanel.transform, "Puzzle", "拼接照片", () =>
        {
            ClosePanel(photoPanel);
            if (PhotoPuzzleManager.Instance != null) PhotoPuzzleManager.Instance.OpenPuzzle();
        }, 34, 440f, 76f);
        UIFactory.Place(photoPuzzleButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 100), new Vector2(440, 76));
        var ph = UIFactory.Label(photoPanel.transform, "Hint", "", 28, new Color(0.7f, 0.8f, 0.9f), TextAnchor.MiddleCenter);
        UIFactory.Place(ph.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 36), new Vector2(900, 40));
        photoPanel.SetActive(false);
    }

    void BuildOverlays()
    {
        flashImg = UIFactory.Panel(Canvas.transform, "DamageFlash", new Color(1, 0, 0, 0));
        UIFactory.Stretch(flashImg.rectTransform);
        flashImg.raycastTarget = false;
        flashImg.gameObject.SetActive(false);

        fadeImg = UIFactory.Panel(Canvas.transform, "Fade", new Color(0, 0, 0, 1));
        UIFactory.Stretch(fadeImg.rectTransform);
        fadeImg.raycastTarget = false;
    }

    // ───────────── 每幀 ─────────────

    void Update()
    {
        var gm = GameManager.Instance;

        if (GameInput.Pause.WasPressedThisFrame())
        {
            if (gm.State == GameState.Playing && gm.Mode == InputMode.Gameplay) gm.Pause();
            else if (gm.State == GameState.Paused && !controllerPanel.activeSelf) gm.Resume();
        }

        if (gm.State == GameState.Playing && gm.Mode == InputMode.Gameplay && !SceneLoader.IsLoading)
        {
            if (GameInput.OpenMission.WasPressedThisFrame()) OpenMissionPanel();
            else if (GameInput.OpenPhoto.WasPressedThisFrame()) OpenPhotoPanel();
        }

        if (gm.Mode == InputMode.UI && GameInput.UICancel.WasPressedThisFrame())
        {
            if (controllerPanel.activeSelf) return;
            if (missionPanel.activeSelf) ClosePanel(missionPanel);
            else if (photoPanel.activeSelf) ClosePanel(photoPanel);
            else if (howToPanel.activeSelf) CloseHowTo();
            else if (gm.State == GameState.Paused) gm.Resume();
        }
    }

    void OnStateChanged(GameState s)
    {
        pausePanel.SetActive(s == GameState.Paused);
        gameOverPanel.SetActive(s == GameState.GameOver);
        clearPanel.SetActive(s == GameState.GameClear);
        if (s != GameState.MainMenu) mainMenuPanel.SetActive(false);

        if (s == GameState.Paused) Focus(pauseResumeButton.gameObject);
        else if (s == GameState.GameOver)
        {
            CloseAllInfo();
            Focus(gameOverRestartButton.gameObject);
        }
        else if (s == GameState.GameClear)
        {
            CloseAllInfo();
            clearLivesText.text = "剩餘生命　" + GameManager.Instance.Data.lives + " / " + GameManager.MaxLives;
            Focus(clearMenuButton.gameObject);
        }
        else ClearSelection();

        if (s != GameState.Playing) SetPrompt(null);
    }

    void CloseAllInfo()
    {
        missionPanel.SetActive(false);
        photoPanel.SetActive(false);
        dialoguePanel.SetActive(false);
        howToPanel.SetActive(false);
        if (dialogueCo != null) { StopCoroutine(dialogueCo); dialogueCo = null; }
    }

    // ───────────── HUD API ─────────────

    public void SetHudVisible(bool on) { hudRoot.SetActive(on); }

    public void RefreshLives()
    {
        if (hearts == null || GameManager.Instance == null) return;
        int lives = GameManager.Instance.Data.lives;
        for (int i = 0; i < hearts.Length; i++)
            hearts[i].color = i < lives ? new Color(1f, 0.25f, 0.3f) : new Color(0.25f, 0.25f, 0.28f, 0.8f);
    }

    public void SetChapter(int stage)
    {
        chapterText.text = stage > 0 ? "CHAPTER " + stage.ToString("00") + " / 04" : "";
    }

    public void SetMission(string text)
    {
        mission = text;
        missionText.text = string.IsNullOrEmpty(text) ? "" : "目前任務：\n" + text;
    }

    public void SetPhotoProgress(int current, int total)
    {
        photoText.text = total > 0 ? "Photo Pieces: " + current + " / " + total : "";
    }

    public void SetPrompt(string text)
    {
        bool show = !string.IsNullOrEmpty(text);
        if (promptBg.gameObject.activeSelf != show) promptBg.gameObject.SetActive(show);
        if (show) promptText.text = text;
    }

    public void Toast(string text, float seconds = 2.5f)
    {
        if (toastCo != null) StopCoroutine(toastCo);
        toastCo = StartCoroutine(ToastRoutine(text, seconds));
    }

    IEnumerator ToastRoutine(string text, float seconds)
    {
        toastText.text = text;
        toastText.gameObject.SetActive(true);
        toastText.color = Color.white;
        yield return new WaitForSecondsRealtime(seconds);
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.4f)
        {
            toastText.color = new Color(1, 1, 1, 1f - t);
            yield return null;
        }
        toastText.gameObject.SetActive(false);
    }

    /// <summary>換場景時清掉殘留的暫時性畫面（閃紅/閃綠、對話框、提示、Toast）。</summary>
    public void ResetTransient()
    {
        if (flashCo != null) { StopCoroutine(flashCo); flashCo = null; }
        if (toastCo != null) { StopCoroutine(toastCo); toastCo = null; }
        if (dialogueCo != null) { StopCoroutine(dialogueCo); dialogueCo = null; }
        flashImg.gameObject.SetActive(false);
        toastText.gameObject.SetActive(false);
        dialoguePanel.SetActive(false);
        SetPrompt(null);
    }

    // ───────────── 淡入淡出 / 閃紅 ─────────────

    public IEnumerator FadeRoutine(float target, float duration, Color color)
    {
        fadeImg.gameObject.SetActive(true);
        fadeImg.transform.SetAsLastSibling();
        float start = fadeImg.color.a;
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration))
        {
            fadeImg.color = new Color(color.r, color.g, color.b, Mathf.Lerp(start, target, t));
            yield return null;
        }
        fadeImg.color = new Color(color.r, color.g, color.b, target);
        if (target <= 0.001f) fadeImg.gameObject.SetActive(false);
    }

    public void Flash(Color color, float duration)
    {
        if (flashCo != null) StopCoroutine(flashCo);
        flashCo = StartCoroutine(FlashRoutine(color, duration));
    }

    IEnumerator FlashRoutine(Color color, float duration)
    {
        flashImg.gameObject.SetActive(true);
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / duration)
        {
            flashImg.color = new Color(color.r, color.g, color.b, color.a * (1f - t));
            yield return null;
        }
        flashImg.gameObject.SetActive(false);
    }

    // ───────────── 對話 ─────────────

    public void Say(string speaker, string[] lines, Action done = null)
    {
        if (dialogueCo != null) StopCoroutine(dialogueCo);
        dialogueCo = StartCoroutine(DialogueRoutine(speaker, lines, done, 0f));
    }

    public IEnumerator SayRoutine(string speaker, string[] lines, float autoAdvanceSeconds = 0f)
    {
        bool finished = false;
        if (dialogueCo != null) StopCoroutine(dialogueCo);
        dialogueCo = StartCoroutine(DialogueRoutine(speaker, lines, () => finished = true, autoAdvanceSeconds));
        while (!finished) yield return null;
    }

    IEnumerator DialogueRoutine(string speaker, string[] lines, Action done, float autoAdvanceSeconds)
    {
        var gm = GameManager.Instance;
        var prevMode = gm.Mode;
        ClearSelection();
        SetPrompt(null);
        dialoguePanel.SetActive(true);
        speakerText.text = speaker;
        gm.SetMode(InputMode.UI);
        yield return new WaitForSecondsRealtime(0.15f);

        foreach (var line in lines)
        {
            dialogueHint.text = "";
            float shown = 0f;
            while (shown < line.Length)
            {
                shown += 50f * Time.unscaledDeltaTime;
                dialogueText.text = line.Substring(0, Mathf.Min(line.Length, (int)shown));
                if (GameInput.UISubmit.WasPressedThisFrame()) break;
                yield return null;
            }
            dialogueText.text = line;
            dialogueHint.text = "[" + GameInput.ConfirmLabel + "] 繼續";
            yield return null;
            float until = autoAdvanceSeconds > 0f ? Time.unscaledTime + autoAdvanceSeconds : float.PositiveInfinity;
            while (!GameInput.UISubmit.WasPressedThisFrame() && Time.unscaledTime < until) yield return null;
        }

        dialoguePanel.SetActive(false);
        if (gm.State == GameState.Playing) gm.SetMode(prevMode == InputMode.UI ? InputMode.Gameplay : prevMode);
        dialogueCo = null;
        done?.Invoke();
    }

    // ───────────── 選單 / 面板 ─────────────

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        GameManager.Instance.SetMode(InputMode.UI);
        Focus(mainStartButton.gameObject);
    }

    public void ShowControlsHelp(Action onClose)
    {
        howToOnClose = onClose;
        howToPanel.SetActive(true);
        howToPanel.transform.SetAsLastSibling();
        if (GameManager.Instance.State == GameState.Playing) GameManager.Instance.SetMode(InputMode.UI);
        Focus(howToBackButton.gameObject);
    }

    void CloseHowTo()
    {
        howToPanel.SetActive(false);
        ClearSelection();
        var gm = GameManager.Instance;
        if (gm.State == GameState.MainMenu) Focus(mainStartButton.gameObject);
        else if (gm.State == GameState.Playing) gm.SetMode(InputMode.Gameplay);
        var cb = howToOnClose;
        howToOnClose = null;
        cb?.Invoke();
    }

    void OpenMissionPanel()
    {
        var d = GameManager.Instance.Data;
        missionPanelText.text = "目前任務\n" + (string.IsNullOrEmpty(mission) ? "—" : mission)
                              + "\n\n生命：" + d.lives + " / " + GameManager.MaxLives
                              + "\n章節：" + d.stage + " / 4";
        missionPanel.transform.Find("Hint").GetComponent<Text>().text = "[" + GameInput.CancelLabel + "] 返回";
        missionPanel.SetActive(true);
        GameManager.Instance.SetMode(InputMode.UI);
        ClearSelection();
        AudioManager.Play(Sfx.Panel);
    }

    void OpenPhotoPanel()
    {
        var d = GameManager.Instance.Data;
        var pm = PhotoPuzzleManager.Instance;
        int total = pm != null ? pm.TotalPieces : (d.stage == 1 ? 2 : 4);

        var sb = new System.Text.StringBuilder();
        bool frames = d.stage != 1;   // 第二、四關：碎片要放進相框
        for (int i = 0; i < total; i++)
        {
            sb.Append(d.photoPieces[i] ? "[X] " : "[ ] ").Append("Photo Piece ").Append((i + 1).ToString("00"));
            if (frames) sb.Append(d.framesPlaced[i] ? "   -> frame placed" : (d.photoPieces[i] ? "   -> not placed" : ""));
            sb.Append('\n');
        }
        int got = 0;
        for (int i = 0; i < total; i++) if (d.photoPieces[i]) got++;
        sb.Append("\nCollected: ").Append(got).Append(" / ").Append(total);
        if (frames)
        {
            int placed = 0; foreach (var p in d.framesPlaced) if (p) placed++;
            sb.Append("\nFrames: ").Append(placed).Append(" / 4");
            if (d.portalOpen) sb.Append("\n隱藏密室已經出現");
            else if (placed >= 4) sb.Append("\n換個視角，讓四塊碎片拼起來");
        }
        else if (d.photoPuzzleSolved) sb.Append("\n照片已拼接完成");
        photoPanelText.text = sb.ToString();

        bool canPuzzle = d.stage == 1 && pm != null && pm.AllCollected && !pm.Solved;
        photoPuzzleButton.gameObject.SetActive(canPuzzle);
        photoPanel.transform.Find("Hint").GetComponent<Text>().text = "[" + GameInput.CancelLabel + "] 返回";

        photoPanel.SetActive(true);
        GameManager.Instance.SetMode(InputMode.UI);
        if (canPuzzle) Focus(photoPuzzleButton.gameObject); else ClearSelection();
        AudioManager.Play(Sfx.Panel);
    }

    void ClosePanel(GameObject panel)
    {
        panel.SetActive(false);
        ClearSelection();
        if (GameManager.Instance.State == GameState.Playing) GameManager.Instance.SetMode(InputMode.Gameplay);
    }

    // ───────────── 手把斷線 ─────────────

    public void ShowControllerOverlay(bool reconnected)
    {
        controllerPanel.SetActive(true);
        controllerPanel.transform.SetAsLastSibling();
        fadeImg.transform.SetAsLastSibling();
        controllerPanel.transform.Find("Title").GetComponent<Text>().text = reconnected ? "Controller Reconnected" : "Controller Disconnected";
        controllerText.text = reconnected ? "手把已重新連線" : "請重新連接藍牙手把";
        UIFactory.SetButtonLabel(controllerButton, reconnected ? "Continue" : "Continue Without Controller");
        if (EventSystem.current != null) selectionBeforeOverlay = EventSystem.current.currentSelectedGameObject;
        Focus(controllerButton.gameObject);
    }

    public void HideControllerOverlay()
    {
        controllerPanel.SetActive(false);
        var back = selectionBeforeOverlay;
        selectionBeforeOverlay = null;
        if (back != null && back.activeInHierarchy) Focus(back);
        else if (GameManager.Instance.State == GameState.Paused) Focus(pauseResumeButton.gameObject);
        else ClearSelection();
    }

    // ───────────── 手把焦點 ─────────────

    public void Focus(GameObject go)
    {
        StartCoroutine(FocusRoutine(go));
    }

    IEnumerator FocusRoutine(GameObject go)
    {
        yield return null; // 等一幀，避免同一次按鍵立刻觸發按鈕
        if (go != null && go.activeInHierarchy && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(go);
        }
    }

    public void ClearSelection()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
}

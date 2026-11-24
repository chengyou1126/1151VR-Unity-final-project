using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 輸入定義（單一來源）。以 Xbox 手把為預設配置，另外附上鍵盤滑鼠備援方便沒有手把時測試。
/// 編輯器選單 Tools/D-Day/Export GameInput.inputactions 可以把這份定義輸出成 .inputactions 資產。
/// </summary>
public static class GameInput
{
    static InputActionAsset _asset;
    public static InputActionAsset Asset => _asset != null ? _asset : (_asset = Build());

    // Player
    public static InputAction Move, Look, Sprint, Jump, Crouch, Interact, OpenMission, OpenPhoto, Pause;
    // UI
    public static InputAction UINavigate, UISubmit, UICancel, UIPoint, UIScroll, UIZoomIn, UIZoomOut, UIScrollTarget;
    // Puzzle
    public static InputAction PzNavigate, PzSelect, PzCancel, PzRotateLeft, PzRotateRight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _asset = null; }

    /// <summary>目前有連線手把時提示用 A，否則提示鍵盤 E。</summary>
    public static bool UsingGamepad => Gamepad.all.Count > 0;
    public static string InteractLabel => Gamepad.all.Count > 0 ? "A" : "E";
    public static string ConfirmLabel => Gamepad.all.Count > 0 ? "A" : "Enter";
    public static string CancelLabel => Gamepad.all.Count > 0 ? "B" : "Backspace";
    public static string PhotoLabel => Gamepad.all.Count > 0 ? "Y" : "Tab";

    public static InputActionAsset Build()
    {
        var a = ScriptableObject.CreateInstance<InputActionAsset>();
        a.name = "GameInput";

        // ── Player ──
        var p = a.AddActionMap("Player");
        Move = p.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
        Move.AddBinding("<Gamepad>/leftStick");
        AddKeys(Move, "w", "s", "a", "d");

        Look = p.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
        Look.AddBinding("<Gamepad>/rightStick");
        Look.AddBinding("<Mouse>/delta");

        Sprint = p.AddAction("Sprint", InputActionType.Button);
        Sprint.AddBinding("<Gamepad>/rightShoulder");
        Sprint.AddBinding("<Keyboard>/leftShift");

        Jump = p.AddAction("Jump", InputActionType.Button);
        Jump.AddBinding("<Gamepad>/leftShoulder");
        Jump.AddBinding("<Keyboard>/space");

        // 蹲下：按住（B 在遊戲中沒有其他用途；介面裡的 B 是「返回」，兩者在不同的輸入模式）
        Crouch = p.AddAction("Crouch", InputActionType.Button);
        Crouch.AddBinding("<Gamepad>/buttonEast");
        Crouch.AddBinding("<Keyboard>/leftCtrl");
        Crouch.AddBinding("<Keyboard>/c");

        Interact = p.AddAction("Interact", InputActionType.Button);
        Interact.AddBinding("<Gamepad>/buttonSouth");
        Interact.AddBinding("<Keyboard>/e");

        OpenMission = p.AddAction("OpenMission", InputActionType.Button);
        OpenMission.AddBinding("<Gamepad>/buttonWest");
        OpenMission.AddBinding("<Keyboard>/m");

        OpenPhoto = p.AddAction("OpenPhoto", InputActionType.Button);
        OpenPhoto.AddBinding("<Gamepad>/buttonNorth");
        OpenPhoto.AddBinding("<Keyboard>/tab");

        Pause = p.AddAction("Pause", InputActionType.Button);
        Pause.AddBinding("<Gamepad>/start");
        Pause.AddBinding("<Keyboard>/escape");

        // ── UI ──
        var u = a.AddActionMap("UI");
        UINavigate = u.AddAction("Navigate", InputActionType.Value, expectedControlLayout: "Vector2");
        UINavigate.AddBinding("<Gamepad>/dpad");
        UINavigate.AddBinding("<Gamepad>/leftStick");
        AddArrows(UINavigate);

        UISubmit = u.AddAction("Submit", InputActionType.Button);
        UISubmit.AddBinding("<Gamepad>/buttonSouth");
        UISubmit.AddBinding("<Keyboard>/enter");
        UISubmit.AddBinding("<Keyboard>/space");

        UICancel = u.AddAction("Cancel", InputActionType.Button);
        UICancel.AddBinding("<Gamepad>/buttonEast");
        UICancel.AddBinding("<Keyboard>/backspace");
        UICancel.AddBinding("<Keyboard>/escape");

        UIPoint = u.AddAction("Point", InputActionType.PassThrough, expectedControlLayout: "Vector2");
        UIPoint.AddBinding("<Mouse>/position");

        // 題目畫面的捲動 / 縮放：右搖桿捲動、LB 縮小、RB 放大（鍵盤：PageUp/PageDown 與方向，- / = 縮放）
        UIScroll = u.AddAction("Scroll", InputActionType.Value, expectedControlLayout: "Vector2");
        UIScroll.AddBinding("<Gamepad>/rightStick");
        UIScroll.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/pageUp").With("Down", "<Keyboard>/pageDown")
            .With("Left", "<Keyboard>/home").With("Right", "<Keyboard>/end");

        UIZoomOut = u.AddAction("ZoomOut", InputActionType.Button);
        UIZoomOut.AddBinding("<Gamepad>/leftShoulder");
        UIZoomOut.AddBinding("<Keyboard>/minus");

        UIZoomIn = u.AddAction("ZoomIn", InputActionType.Button);
        UIZoomIn.AddBinding("<Gamepad>/rightShoulder");
        UIZoomIn.AddBinding("<Keyboard>/equals");

        // 切換「右搖桿要捲動哪一塊」：程式碼 / 題目（按下右搖桿）
        UIScrollTarget = u.AddAction("ScrollTarget", InputActionType.Button);
        UIScrollTarget.AddBinding("<Gamepad>/rightStickPress");
        UIScrollTarget.AddBinding("<Keyboard>/tab");

        // ── Puzzle（照片拼圖）──
        var z = a.AddActionMap("Puzzle");
        PzNavigate = z.AddAction("Navigate", InputActionType.Value, expectedControlLayout: "Vector2");
        PzNavigate.AddBinding("<Gamepad>/dpad");
        PzNavigate.AddBinding("<Gamepad>/leftStick");
        AddArrows(PzNavigate);

        PzSelect = z.AddAction("Select", InputActionType.Button);
        PzSelect.AddBinding("<Gamepad>/buttonSouth");
        PzSelect.AddBinding("<Keyboard>/enter");
        PzSelect.AddBinding("<Keyboard>/space");

        PzCancel = z.AddAction("Cancel", InputActionType.Button);
        PzCancel.AddBinding("<Gamepad>/buttonEast");
        PzCancel.AddBinding("<Keyboard>/backspace");
        PzCancel.AddBinding("<Keyboard>/escape");

        PzRotateLeft = z.AddAction("RotateLeft", InputActionType.Button);
        PzRotateLeft.AddBinding("<Gamepad>/leftShoulder");
        PzRotateLeft.AddBinding("<Keyboard>/q");

        PzRotateRight = z.AddAction("RotateRight", InputActionType.Button);
        PzRotateRight.AddBinding("<Gamepad>/rightShoulder");
        PzRotateRight.AddBinding("<Keyboard>/e");

        return a;
    }

    static void AddKeys(InputAction act, string up, string down, string left, string right)
    {
        act.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/" + up)
            .With("Down", "<Keyboard>/" + down)
            .With("Left", "<Keyboard>/" + left)
            .With("Right", "<Keyboard>/" + right);
    }

    static void AddArrows(InputAction act)
    {
        AddKeys(act, "upArrow", "downArrow", "leftArrow", "rightArrow");
    }

    public static void SetMode(InputMode mode)
    {
        var a = Asset;
        foreach (var map in a.actionMaps) map.Disable();
        switch (mode)
        {
            case InputMode.Gameplay: a.FindActionMap("Player").Enable(); break;
            case InputMode.UI: a.FindActionMap("UI").Enable(); break;
            case InputMode.Puzzle: a.FindActionMap("Puzzle").Enable(); break;
        }
        Pause.Enable(); // 暫停鍵（Start）在任何模式都要能讀
    }
}

/// <summary>把搖桿/方向鍵的連續輸入轉成「一次一格」的離散方向，供拼圖等自製介面使用。</summary>
public class DiscreteNav
{
    Vector2Int last;
    float nextTime;

    public Vector2Int Poll(Vector2 v, float threshold = 0.6f, float firstDelay = 0.35f, float repeat = 0.16f)
    {
        Vector2Int dir = Vector2Int.zero;
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
        {
            if (v.x > threshold) dir = Vector2Int.right;
            else if (v.x < -threshold) dir = Vector2Int.left;
        }
        else
        {
            if (v.y > threshold) dir = Vector2Int.up;
            else if (v.y < -threshold) dir = Vector2Int.down;
        }

        float now = Time.unscaledTime;
        if (dir == Vector2Int.zero) { last = dir; return dir; }
        if (dir != last) { last = dir; nextTime = now + firstDelay; return dir; }
        if (now >= nextTime) { nextTime = now + repeat; return dir; }
        return Vector2Int.zero;
    }
}

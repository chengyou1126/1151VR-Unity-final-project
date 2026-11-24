using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 手把連線管理：手把中途斷線 → 暫停並顯示 Controller Disconnected；重新連線後可繼續。
/// 從未連過手把（純鍵盤測試）不會觸發斷線暫停。
/// </summary>
public class ControllerManager : MonoBehaviour
{
    public static ControllerManager Instance { get; private set; }

    bool hadGamepad;
    public bool Disconnected { get; private set; }

    void Awake()
    {
        Instance = this;
        hadGamepad = Gamepad.all.Count > 0;
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    void OnDestroy()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (!(device is Gamepad)) return;

        switch (change)
        {
            case InputDeviceChange.Added:
            case InputDeviceChange.Reconnected:
                hadGamepad = true;
                if (Disconnected) UIManager.Instance.ShowControllerOverlay(true);
                break;

            case InputDeviceChange.Removed:
            case InputDeviceChange.Disconnected:
                if (hadGamepad && Gamepad.all.Count == 0) HandleLost();
                break;
        }
    }

    void HandleLost()
    {
        var gm = GameManager.Instance;
        if (gm.State == GameState.MainMenu) return;

        Disconnected = true;
        if (gm.State == GameState.Playing && gm.Mode == InputMode.Gameplay) gm.Pause();
        UIManager.Instance.ShowControllerOverlay(false);
    }

    /// <summary>玩家在斷線提示上按「繼續」。</summary>
    public void Acknowledge()
    {
        Disconnected = false;
        UIManager.Instance.HideControllerOverlay();
    }
}

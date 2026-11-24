using UnityEngine;

/// <summary>密室牆上的自毀系統狀態顯示器。Arm() 啟動警報、Disarm() 解除。</summary>
public class SelfDestructController : MonoBehaviour
{
    public TextMesh display;
    public AlarmLight[] alarms;

    public bool Armed { get; private set; }

    void Update()
    {
        if (display == null) return;
        if (Armed)
        {
            bool blink = Mathf.Repeat(Time.time, 1f) < 0.65f;
            display.color = new Color(1f, 0.15f, 0.1f);
            display.text = "SELF-DESTRUCT\n" + (blink ? "!! ARMED !!" : "");
        }
        else
        {
            display.color = new Color(0.2f, 1f, 0.45f);
            display.text = "SELF-DESTRUCT\nDISARMED";
        }
    }

    public void Arm()
    {
        Armed = true;
        SetAlarms(true);
    }

    public void Disarm()
    {
        Armed = false;
        GameManager.Instance.Data.selfDestructDisarmed = true;
        SetAlarms(false);
    }

    void SetAlarms(bool on)
    {
        if (alarms == null) return;
        foreach (var a in alarms) if (a != null) a.SetRunning(on);
    }
}

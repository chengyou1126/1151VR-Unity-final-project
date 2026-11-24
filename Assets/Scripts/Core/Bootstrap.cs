using UnityEngine;

/// <summary>
/// 不論從哪個場景按 Play，都會自動建立常駐系統（GameManager / AudioManager / UIManager / ControllerManager）。
/// 這樣每個場景都能單獨測試，不需要手動放 Manager。
/// </summary>
public static class Bootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (GameManager.Instance != null) return;

        var go = new GameObject("[DDay Systems]");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<GameManager>();
        go.AddComponent<AudioManager>();
        go.AddComponent<UIManager>();
        go.AddComponent<ControllerManager>();
    }
}

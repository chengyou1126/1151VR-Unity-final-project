using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoader
{
    public static bool IsLoading { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { IsLoading = false; }

    public static void Load(string sceneName, Color? fadeColor = null)
    {
        if (IsLoading) return;
        GameManager.Instance.StartCoroutine(Routine(sceneName, fadeColor ?? Color.black));
    }

    static IEnumerator Routine(string sceneName, Color color)
    {
        IsLoading = true;
        GameManager.Instance.SetMode(InputMode.Locked);
        UIManager.Instance.SetPrompt(null);
        yield return UIManager.Instance.FadeRoutine(1f, 0.5f, color);

        var op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;

        IsLoading = false;
        // 淡入由新場景的 LevelManager.BeginLevel() 負責
    }
}

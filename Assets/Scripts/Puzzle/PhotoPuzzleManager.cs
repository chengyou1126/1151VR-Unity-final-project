using System;
using UnityEngine;

/// <summary>
/// 照片蒐集邏輯。
/// 第一關：2 張（2x1，玩家本來有 1 張），撿到後開啟拼接介面（PhotoPuzzleController）。
/// 第二關：4 張碎片（2x2），要分別放進四個相框，再找視角對齊（ViewfinderPuzzle），不使用拼接介面。
/// 照片內容：Resources/Photos/Photo_Stage1、Photo_HiddenRoom（由編輯器工具拍攝實景產生）；
/// 找不到時使用程式畫的替代照片。
/// </summary>
public class PhotoPuzzleManager : MonoBehaviour
{
    public static PhotoPuzzleManager Instance { get; private set; }

    [Tooltip("正式照片。留空 = 使用 Resources/Photos 內的實景照片，再沒有就用程式畫的替代照片")]
    public Texture2D customPhoto;

    public event Action<int> OnPieceCollected;
    public event Action OnAllCollected;
    public event Action OnSolved;

    Texture2D loaded;
    Texture2D generated;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    RunData Data => GameManager.Instance.Data;

    public int TotalPieces => Data.stage == 1 ? 2 : 4;
    public int Cols => 2;
    public int Rows => Data.stage == 1 ? 1 : 2;

    public Texture2D Photo
    {
        get
        {
            if (customPhoto != null) return customPhoto;
            if (loaded == null) loaded = Resources.Load<Texture2D>(Data.stage == 1 ? "Photos/Photo_Stage1" : "Photos/Photo_HiddenRoom");
            if (loaded != null) return loaded;
            if (generated == null) generated = PlaceholderPhoto.Generate(Cols, Rows);
            return generated;
        }
    }

    public int CollectedCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < TotalPieces; i++) if (Data.photoPieces[i]) n++;
            return n;
        }
    }

    public bool AllCollected => CollectedCount >= TotalPieces;
    public bool Solved => Data.photoPuzzleSolved;

    public void Collect(int id)
    {
        if (id < 0 || id >= Data.photoPieces.Length || Data.photoPieces[id]) return;
        Data.photoPieces[id] = true;
        UIManager.Instance.SetPhotoProgress(CollectedCount, TotalPieces);
        OnPieceCollected?.Invoke(id);
        if (AllCollected) OnAllCollected?.Invoke();
    }

    /// <summary>第一關：開啟兩半照片的拼接介面。</summary>
    public void OpenPuzzle()
    {
        if (Data.stage != 1 || !AllCollected || Solved) return;
        UIManager.Instance.PhotoPuzzle.Open(Cols, Rows, Photo, () =>
        {
            Data.photoPuzzleSolved = true;
            AudioManager.Play(Sfx.Success);
            OnSolved?.Invoke();
        });
    }
}

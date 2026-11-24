using System;
using UnityEngine;

/// <summary>
/// 第二關的相框（參考 ViewFinder）。每個相框只能放入編號相同的照片碎片。
/// 碎片放進去之後，相框會顯示那一塊照片；四個相框都放好後，
/// 要從某個「視角」看過去，四塊碎片才會對齊成完整的照片（見 ViewfinderPuzzle）。
/// </summary>
public class ViewfinderFrame : MonoBehaviour, IInteractable
{
    public int index;                    // 0~3：左上、右上、左下、右下
    public Renderer picture;
    public Light glow;
    public Material pictureMaterial;     // Unlit/Texture 材質（放入照片後使用）
    public GameObject pole;              // 支架（合併時會先收起來）
    public GameObject poleBase;          // 支架底座（同上）
    public Renderer[] borders;

    public bool Placed { get; private set; }
    public event Action<ViewfinderFrame> OnPlaced;

    public float Width => transform.lossyScale.x;
    public Vector3 Center => picture != null ? picture.transform.position : transform.position;
    /// <summary>照片面朝向（Quad 的正面朝 -Z）。</summary>
    public Vector3 Normal => -transform.forward;

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock block;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        if (glow != null) glow.enabled = false;
    }

    void Start()
    {
        if (GameManager.Instance.Data.framesPlaced[index]) Place(false);
    }

    public string GetInteractionText()
    {
        if (Placed) return null;
        string n = (index + 1).ToString("00");
        return GameManager.Instance.Data.photoPieces[index] ? "放入照片碎片 " + n : "相框 " + n + "（缺少照片碎片 " + n + "）";
    }

    public void Interact()
    {
        if (Placed) return;
        string n = (index + 1).ToString("00");
        if (!GameManager.Instance.Data.photoPieces[index])
        {
            UIManager.Instance.Toast("你還沒找到照片碎片 " + n, 2.2f);
            AudioManager.Play(Sfx.Error, 0.5f);
            return;
        }
        Place(true);
    }

    public void Place(bool notify)
    {
        Placed = true;
        GameManager.Instance.Data.framesPlaced[index] = true;

        int col = index % 2, row = index / 2;
        var mat = new Material(pictureMaterial) { mainTexture = PhotoPuzzleManager.Instance.Photo };
        mat.mainTextureScale = new Vector2(0.5f, 0.5f);
        mat.mainTextureOffset = new Vector2(col * 0.5f, (1 - row) * 0.5f);
        picture.sharedMaterial = mat;
        if (glow != null) glow.enabled = true;

        if (notify)
        {
            AudioManager.Play(Sfx.Success);
            OnPlaced?.Invoke(this);
        }
    }

    /// <summary>對齊程度 0~1：越接近正確視角，邊框與光越亮。</summary>
    public void SetAlignment(float k)
    {
        if (glow != null) { glow.enabled = Placed; glow.intensity = Mathf.Lerp(1.2f, 6f, k); }
        if (borders == null) return;
        var c = Color.Lerp(new Color(0.15f, 0.3f, 0.4f), new Color(0.5f, 1.8f, 2.2f), k);
        foreach (var r in borders)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(EmissionId, c);
            r.SetPropertyBlock(block);
        }
    }
}

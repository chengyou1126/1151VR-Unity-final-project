using UnityEngine;

/// <summary>
/// 「模型插槽」：目前這個物件用基本幾何體當替代品。
/// 找到正式模型後，把 Prefab 拖進 Model Prefab 欄位，執行時（或按右鍵 Apply Model）就會隱藏替代品並放入正式模型。
/// 替代品的 Collider 會保留，所以碰撞、互動、NavMesh 都不用重做。
///
/// 也可以直接在 Prefab 內把替代品的 MeshRenderer 換掉——兩種方式都可以，看你們拿到的模型型態。
/// </summary>
[DisallowMultipleComponent]
public class ModelSlot : MonoBehaviour
{
    [Tooltip("這個插槽的名稱，方便對照（例如 Guard / LabTable / PhotoFrame）")]
    public string slotId;

    [Tooltip("正式模型 Prefab。留空 = 繼續使用替代品")]
    public GameObject modelPrefab;

    public Vector3 localPosition = Vector3.zero;
    public Vector3 localEuler = Vector3.zero;
    public Vector3 localScale = Vector3.one;

    [Tooltip("放入正式模型時，是否移除模型自帶的 Collider（保留替代品的碰撞）")]
    public bool stripModelColliders = true;

    GameObject instance;

    void Awake()
    {
        Apply();
    }

    [ContextMenu("Apply Model")]
    public void Apply()
    {
        if (modelPrefab == null) return;
        if (instance != null) Destroy(instance);

        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = false;

        instance = Instantiate(modelPrefab, transform);
        instance.name = "Model_" + slotId;
        instance.transform.localPosition = localPosition;
        instance.transform.localEulerAngles = localEuler;
        instance.transform.localScale = localScale;

        if (stripModelColliders)
            foreach (var c in instance.GetComponentsInChildren<Collider>(true)) Destroy(c);
    }
}

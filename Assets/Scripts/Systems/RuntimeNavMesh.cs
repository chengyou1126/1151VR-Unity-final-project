using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 載入場景時加入導航網格。優先使用編輯器預先烘焙好的資料（bakedData，載入快）；
/// 沒有的話才會用 Environment 底下所有 Collider 即時烘焙。
/// 之後換成正式模型也不用手動重烘（模型有 Collider 即可）。
/// </summary>
[DefaultExecutionOrder(-200)]
public class RuntimeNavMesh : MonoBehaviour
{
    public static bool Ready { get; private set; }

    [Tooltip("要烘焙的根物件（地板、牆壁、家具都放在它底下）")]
    public Transform environmentRoot;
    [Tooltip("編輯器預先烘焙的資料（SceneBuilder 會指定）。留空 = 執行時烘焙")]
    public NavMeshData bakedData;

    NavMeshDataInstance instance;

    void Awake()
    {
        Ready = false;
        if (environmentRoot == null) environmentRoot = transform;

        var data = bakedData != null ? bakedData : DDayNav.Build(environmentRoot);
        instance = NavMesh.AddNavMeshData(data);
        Ready = true;
    }

    void OnDestroy()
    {
        Ready = false;
        if (instance.valid) NavMesh.RemoveNavMeshData(instance);
    }
}

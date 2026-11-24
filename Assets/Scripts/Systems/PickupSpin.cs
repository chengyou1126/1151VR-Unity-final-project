using UnityEngine;

/// <summary>讓照片碎片之類的物件緩慢旋轉、上下浮動，比較容易被注意到。</summary>
public class PickupSpin : MonoBehaviour
{
    public float spinSpeed = 70f;
    public float bobHeight = 0.04f;
    public float bobSpeed = 2f;

    Vector3 basePos;

    void Start()
    {
        basePos = transform.localPosition;
    }

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        transform.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }
}

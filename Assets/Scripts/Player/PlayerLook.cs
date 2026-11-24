using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook : MonoBehaviour
{
    public Transform cameraRoot;
    public float stickSpeed = 140f;     // 右類比搖桿：度/秒
    public float mouseSensitivity = 0.1f; // 滑鼠備援：度/像素
    public float maxPitch = 80f;

    float pitch;

    void Update()
    {
        if (GameManager.Instance.Mode != InputMode.Gameplay) return;

        var look = GameInput.Look;
        Vector2 v = look.ReadValue<Vector2>();
        bool mouse = look.activeControl != null && look.activeControl.device is Mouse;

        Vector2 d;
        if (mouse)
        {
            d = v * mouseSensitivity;
        }
        else
        {
            // 搖桿加一點曲線，小幅度更好瞄準
            float mag = Mathf.Pow(Mathf.Clamp01(v.magnitude), 1.6f);
            d = (v.sqrMagnitude > 0f ? v.normalized : Vector2.zero) * mag * stickSpeed * Time.deltaTime;
        }

        transform.Rotate(0f, d.x, 0f);
        pitch = Mathf.Clamp(pitch - d.y, -maxPitch, maxPitch);
        if (cameraRoot != null) cameraRoot.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }

    public void SetRotation(float yaw)
    {
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        pitch = 0f;
        if (cameraRoot != null) cameraRoot.localEulerAngles = Vector3.zero;
    }
}

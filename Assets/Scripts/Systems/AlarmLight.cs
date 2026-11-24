using UnityEngine;

/// <summary>警報燈 / 閃爍燈。running=false 時關閉。</summary>
public class AlarmLight : MonoBehaviour
{
    public Light lightSource;
    public Renderer glowRenderer;       // 選填：燈罩，會一起發光
    public Color color = Color.red;
    public float maxIntensity = 3f;
    public float minIntensity = 0.1f;
    public float speed = 3f;
    public bool flicker;                // true = 不規則閃爍（壞掉的燈）
    public bool running = true;

    MaterialPropertyBlock block;
    float seed;

    void Awake()
    {
        if (lightSource == null) lightSource = GetComponentInChildren<Light>();
        block = new MaterialPropertyBlock();
        seed = Random.value * 100f;
    }

    void Update()
    {
        float k;
        if (!running) k = 0f;
        else if (flicker) k = Mathf.PerlinNoise(Time.time * speed, seed) > 0.35f ? 1f : 0.05f;
        else k = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;

        if (lightSource != null)
        {
            lightSource.color = color;
            lightSource.intensity = running ? Mathf.Lerp(minIntensity, maxIntensity, k) : 0f;
        }

        if (glowRenderer != null)
        {
            glowRenderer.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", color * (running ? Mathf.Lerp(0.2f, 2.5f, k) : 0f));
            glowRenderer.SetPropertyBlock(block);
        }
    }

    public void SetRunning(bool on)
    {
        running = on;
    }
}

using UnityEngine;

// Owns the main camera's "feel": positional shake, zoom-punch and a gentle idle
// sway. This is a code-only stand-in for Cinemachine — for a fixed 2D screen it
// delivers the same impulse/zoom impact with zero package wiring and no risk of
// a broken build. Uses UNSCALED time so impacts still read during a hit-stop.
[DefaultExecutionOrder(60)]
public class CameraRig : MonoBehaviour
{
    public static CameraRig Instance { get; private set; }

    Camera cam;
    Vector3 basePos;
    float baseSize;
    float shake;        // current shake magnitude
    float shakeDecay;   // units/sec
    float zoomPunch;    // >0 = momentarily zoomed in

    void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        basePos = transform.position;
        baseSize = cam != null ? cam.orthographicSize : 6f;
    }

    // Lazily guarantees a rig exists on the main camera, so Juice.Shake() works
    // even if GameBootstrap somehow didn't add one.
    public static CameraRig Ensure()
    {
        if (Instance != null) return Instance;
        var c = Camera.main;
        if (c == null) return null;
        Instance = c.GetComponent<CameraRig>();
        if (Instance == null) Instance = c.gameObject.AddComponent<CameraRig>();
        return Instance;
    }

    public void AddShake(float intensity, float duration)
    {
        shake = Mathf.Max(shake, intensity);
        shakeDecay = Mathf.Max(shakeDecay, intensity / Mathf.Max(0.01f, duration));
    }

    public void Punch(float amount) => zoomPunch = Mathf.Max(zoomPunch, amount);

    void LateUpdate()
    {
        if (cam == null) return;
        float dt = Time.unscaledDeltaTime;

        Vector3 offset = Vector3.zero;
        if (shake > 0f)
        {
            offset = (Vector3)(Random.insideUnitCircle * shake);
            shake = Mathf.Max(0f, shake - shakeDecay * dt);
        }

        float sway = Mathf.Sin(Time.unscaledTime * 0.55f) * 0.05f;
        transform.position = basePos + offset + new Vector3(sway, sway * 0.4f, 0f);

        zoomPunch = Mathf.Lerp(zoomPunch, 0f, dt * 6f);
        cam.orthographicSize = baseSize - zoomPunch;   // positive punch zooms in
    }
}

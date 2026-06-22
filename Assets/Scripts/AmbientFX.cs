using UnityEngine;

// Tiny ambient animators that bring the static neo-brutalist scenery to life.
// All are added by GameBootstrap in code (multiple MonoBehaviours per file is
// fine because they're attached via AddComponent, not the Inspector menu).

// Slowly drifts horizontally and wraps around — used for clouds.
public class Drift : MonoBehaviour
{
    public float speed = 0.4f;
    public float wrapMin = -13f, wrapMax = 13f;
    void Update()
    {
        var p = transform.position;
        p.x += speed * Time.deltaTime;
        if (p.x > wrapMax) p.x = wrapMin;
        else if (p.x < wrapMin) p.x = wrapMax;
        transform.position = p;
    }
}

// Softly fades a sprite's alpha in and out — used for building windows.
public class Twinkle : MonoBehaviour
{
    public float speed = 2f;
    SpriteRenderer sr;
    float seed, baseA;
    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        seed = Random.value * 10f;
        baseA = sr != null ? sr.color.a : 1f;
    }
    void Update()
    {
        if (sr == null) return;
        float a = baseA * (0.55f + 0.45f * Mathf.Sin(Time.time * speed + seed));
        var c = sr.color; c.a = a; sr.color = c;
    }
}

// Gentle vertical bob around the start position.
public class Bob : MonoBehaviour
{
    public float amplitude = 0.12f, speed = 1.5f;
    Vector3 basePos; float seed;
    void Start() { basePos = transform.localPosition; seed = Random.value * 6f; }
    void Update()
    {
        var p = basePos; p.y += Mathf.Sin(Time.time * speed + seed) * amplitude;
        transform.localPosition = p;
    }
}

// Gentle breathing scale — used for the sun and bonus markers.
public class Pulse : MonoBehaviour
{
    public float amount = 0.06f, speed = 1.2f;
    Vector3 baseScale; float seed;
    void Start() { baseScale = transform.localScale; seed = Random.value * 6f; }
    void Update()
    {
        transform.localScale = baseScale * (1f + Mathf.Sin(Time.time * speed + seed) * amount);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

// ─────────────────────────────────────────────────────────────────────────────
// Central "game feel" toolkit. Self-bootstrapping singleton: ANY script can call
//   Juice.Shake(...)  Juice.Pop(...)  Juice.Burst(...)
//   Juice.Text(...)   Juice.HitStop(...)  Juice.SlowMo(...)
// Pop() and Text() are powered by DOTween; the public API is unchanged so every
// existing call site keeps working. Shake/Zoom/HitStop/SlowMo/Burst stay custom.
// ─────────────────────────────────────────────────────────────────────────────
public class Juice : MonoBehaviour
{
    static Juice _i;
    static Juice I
    {
        get
        {
            if (_i == null)
            {
                var go = new GameObject("~Juice");
                _i = go.AddComponent<Juice>();
            }
            return _i;
        }
    }

    static Font _font;
    static Font UIFont => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

    // ── Camera ────────────────────────────────────────────────────────────────
    public static void Shake(float intensity, float duration = 0.25f)
        => CameraRig.Ensure()?.AddShake(intensity, duration);

    public static void ZoomPunch(float amount = 0.05f)
        => CameraRig.Ensure()?.Punch(amount);

    // ── Hit stop (freeze frame) ───────────────────────────────────────────────
    public static void HitStop(float seconds = 0.06f) => I.StartCoroutine(I.HitStopCo(seconds));
    IEnumerator HitStopCo(float s)
    {
        if (Time.timeScale == 0f) yield break;           // already frozen — skip
        float prev = Time.timeScale;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(s);
        if (Time.timeScale == 0f) Time.timeScale = prev;  // restore (unless a SlowMo took over)
    }

    // ── Slow motion (used for the victory beat) ───────────────────────────────
    public static void SlowMo(float scale, float seconds) => I.StartCoroutine(I.SlowMoCo(scale, seconds));
    IEnumerator SlowMoCo(float scale, float seconds)
    {
        Time.timeScale = scale;
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        while (Time.timeScale < 1f)
        {
            Time.timeScale = Mathf.Min(1f, Time.timeScale + Time.unscaledDeltaTime * 2f);
            yield return null;
        }
        Time.timeScale = 1f;
    }

    // ── Scale pop (one-shot overshoot) — DOTween ──────────────────────────────
    // Squash-up then settle back to the captured base scale (sign preserved, so
    // flipped sprites stay flipped). Any in-flight pop on the same target is
    // completed first to avoid scale drift.
    public static void Pop(Transform t, float strength = 0.3f, float dur = 0.22f)
    {
        if (t == null) return;
        DOTween.Kill(t, true);                 // finish/clear previous pop → restores base
        Vector3 b = t.localScale;
        DOTween.Sequence().SetTarget(t).SetUpdate(true)
            .Append(t.DOScale(b * (1f + strength), dur * 0.45f).SetEase(Ease.OutQuad))
            .Append(t.DOScale(b, dur * 0.55f).SetEase(Ease.InOutQuad));
    }

    // ── Sprite colour flash ───────────────────────────────────────────────────
    public static void Flash(SpriteRenderer sr, Color flash, float dur = 0.12f)
    {
        if (sr != null) I.StartCoroutine(I.FlashCo(sr, flash, dur));
    }
    IEnumerator FlashCo(SpriteRenderer sr, Color flash, float dur)
    {
        Color original = sr.color;
        sr.color = flash;
        float e = 0f;
        while (e < dur)
        {
            if (sr == null) yield break;
            e += Time.deltaTime;
            sr.color = Color.Lerp(flash, original, e / dur);
            yield return null;
        }
        if (sr != null) sr.color = original;
    }

    // ── Floating world text (score numbers, combos) — DOTween ─────────────────
    // Pop-in overshoot + steady rise + fade-out on the back half, driven by a
    // single DOTween Sequence that self-destroys the object when it ends.
    public static void Text(Vector3 worldPos, string msg, Color color, float size = 1f,
                            float rise = 1.3f, float life = 0.9f)
    {
        var go = new GameObject("~FloatText");
        go.transform.position = worldPos + (Vector3)(Random.insideUnitCircle * 0.15f);
        var tm = go.AddComponent<TextMesh>();
        tm.text = msg;
        tm.font = UIFont;
        tm.fontSize = 48;
        tm.characterSize = 0.12f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = color;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = UIFont.material;     // ensure it actually renders at runtime
        mr.sortingOrder = 60;

        var tr = go.transform;
        float startY = tr.position.y;
        tr.localScale = Vector3.one * size * 0.3f;

        var seq = DOTween.Sequence().SetTarget(tr).SetUpdate(true);
        // pop-in then settle (scale)
        seq.Append(tr.DOScale(size * 1.12f, life * 0.18f).SetEase(Ease.OutBack));
        seq.Append(tr.DOScale(size, life * 0.82f).SetEase(Ease.OutQuad));
        // steady rise across the whole life (starts at t=0, runs in parallel)
        seq.Insert(0f, tr.DOMoveY(startY + rise, life).SetEase(Ease.OutQuad));
        // fade alpha out on the last 40%
        seq.Insert(life * 0.6f,
            DOTween.To(() => tm.color.a, a => { var c = tm.color; c.a = a; tm.color = c; },
                       0f, life * 0.4f));
        seq.OnComplete(() => { if (go != null) Destroy(go); });
    }

    // ── Particle burst (sprite-based: bulletproof in any pipeline / WebGL) ─────
    public static void Burst(Vector3 pos, Color color, int count = 10, float speed = 4f,
                             float size = 0.16f, float life = 0.5f, float gravity = 9f)
        => I.StartCoroutine(I.BurstCo(pos, color, count, speed, size, life, gravity));
    IEnumerator BurstCo(Vector3 pos, Color color, int count, float speed, float size, float life, float gravity)
    {
        var trs = new List<Transform>(count);
        var srs = new List<SpriteRenderer>(count);
        var vel = new List<Vector2>(count);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("~p");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size * Random.Range(0.7f, 1.3f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GameAssets.Dot;
            sr.color = color;
            sr.sortingOrder = 50;
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float sp = speed * Random.Range(0.45f, 1f);
            vel.Add(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp);
            trs.Add(go.transform); srs.Add(sr);
        }
        float e = 0f;
        while (e < life)
        {
            float dt = Time.deltaTime;
            e += dt;
            float fade = 1f - e / life;
            for (int i = 0; i < trs.Count; i++)
            {
                if (trs[i] == null) continue;
                Vector2 v = vel[i];
                v.y -= gravity * dt;
                v *= (1f - 2.2f * dt);
                vel[i] = v;
                trs[i].position += (Vector3)(v * dt);
                var c = color; c.a = fade; srs[i].color = c;
            }
            yield return null;
        }
        for (int i = 0; i < trs.Count; i++) if (trs[i] != null) Destroy(trs[i].gameObject);
    }
}

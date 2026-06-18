using UnityEngine;

// Generates all sound effects and background music procedurally at runtime,
// so the game has audio without importing any files. Call the static helpers
// (AudioManager.Instance?.PlayCollect(), etc.) from gameplay scripts.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private AudioSource sfx;
    private AudioSource music;

    private AudioClip collectClip, goodClip, badClip, stickClip, placeClip;

    const int SampleRate = 44100;
    enum Wave { Sine, Square, Triangle, Noise }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.volume = 0.5f;

        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.loop = true;
        music.volume = 0.12f;

        BuildClips();
    }

    void Start()
    {
        music.clip = BuildMusic();
        music.Play();
    }

    void BuildClips()
    {
        // pickup: short high blip
        collectClip = Tone("collect", new[] { (880f, 0.07f) }, Wave.Square, 0.4f);
        // correct delivery: rising happy arpeggio
        goodClip = Tone("good", new[] { (523f, 0.08f), (659f, 0.08f), (784f, 0.12f) }, Wave.Triangle, 0.5f);
        // wrong delivery: low descending buzz
        badClip = Tone("bad", new[] { (200f, 0.12f), (150f, 0.16f) }, Wave.Square, 0.45f);
        // stuck: low noisy thud
        stickClip = Tone("stick", new[] { (110f, 0.28f) }, Wave.Noise, 0.5f);
        // place collector: soft mid blip
        placeClip = Tone("place", new[] { (440f, 0.06f), (660f, 0.06f) }, Wave.Sine, 0.4f);
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public void PlayCollect()     => sfx.PlayOneShot(collectClip);
    public void PlayDeliverGood() => sfx.PlayOneShot(goodClip);
    public void PlayDeliverBad()  => sfx.PlayOneShot(badClip);
    public void PlayStick()       => sfx.PlayOneShot(stickClip);
    public void PlayPlace()       => sfx.PlayOneShot(placeClip);

    // ── Procedural generation ─────────────────────────────────────────────────

    AudioClip Tone(string name, (float freq, float dur)[] notes, Wave wave, float volume)
    {
        float total = 0f;
        foreach (var n in notes) total += n.dur;
        int samples = Mathf.CeilToInt(total * SampleRate);
        var data = new float[samples];

        int idx = 0;
        foreach (var n in notes)
        {
            int len = Mathf.CeilToInt(n.dur * SampleRate);
            for (int i = 0; i < len && idx < samples; i++, idx++)
            {
                float t = i / (float)SampleRate;
                float env = Envelope(i, len);                  // pluck envelope
                data[idx] = Sample(wave, n.freq, t) * env * volume;
            }
        }

        var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // A gentle, non-annoying looping melody (C-major pentatonic).
    AudioClip BuildMusic()
    {
        float[] mel = { 523, 587, 659, 784, 659, 587, 523, 440,
                        523, 659, 784, 880, 784, 659, 587, 523 };
        float noteDur = 0.32f;
        int noteLen = Mathf.CeilToInt(noteDur * SampleRate);
        var data = new float[noteLen * mel.Length];

        for (int n = 0; n < mel.Length; n++)
            for (int i = 0; i < noteLen; i++)
            {
                float t = i / (float)SampleRate;
                float env = MusicEnvelope(i, noteLen);
                // soft tone: sine + a touch of triangle for body
                float s = 0.7f * Sample(Wave.Sine, mel[n], t)
                        + 0.3f * Sample(Wave.Triangle, mel[n] / 2f, t);
                data[n * noteLen + i] = s * env * 0.5f;
            }

        var clip = AudioClip.Create("music", data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    float Sample(Wave wave, float freq, float t)
    {
        float phase = freq * t;
        switch (wave)
        {
            case Wave.Sine:     return Mathf.Sin(phase * 2f * Mathf.PI);
            case Wave.Square:   return Mathf.Sin(phase * 2f * Mathf.PI) >= 0 ? 1f : -1f;
            case Wave.Triangle: return Mathf.PingPong(phase, 1f) * 2f - 1f;
            case Wave.Noise:    return Random.value * 2f - 1f;
            default:            return 0f;
        }
    }

    // Quick attack, exponential-ish decay → plucky SFX
    float Envelope(int i, int len)
    {
        float t = i / (float)len;
        float attack = 0.02f;
        if (t < attack) return t / attack;
        return Mathf.Pow(1f - (t - attack) / (1f - attack), 2f);
    }

    // Softer envelope for sustained music notes
    float MusicEnvelope(int i, int len)
    {
        float t = i / (float)len;
        float attack = 0.08f, release = 0.25f;
        if (t < attack) return t / attack;
        if (t > 1f - release) return (1f - t) / release;
        return 1f;
    }
}

using UnityEngine;

// Generates all sprites at runtime so the prototype looks good without any
// imported art. Trash icons are drawn in white/grey so TrashItem can tint
// them by type colour; bins and the player are composed from coloured shapes
// in GameBootstrap.
public static class GameAssets
{
    private static Sprite _square, _circle, _dot, _vignette;

    // Limpa o cache no início de CADA Play. Sem isso, com "Domain Reload"
    // desligado (Unity 6), os sprites guardados aqui sobrevivem entre execuções
    // apontando para texturas já destruídas → o cenário não renderiza ("tela
    // branca" mostrando só a HUD). Roda antes de qualquer Awake.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticCache()
    {
        _square = null; _circle = null; _dot = null; _vignette = null;
    }

    public static Sprite Square => _square ??= MakeSquare(32);
    public static Sprite Circle => _circle ??= MakeCircle(64);
    public static Sprite Dot    => _dot ??= MakeFilledCircle(24);          // particle bit
    public static Sprite Vignette => _vignette ??= MakeVignette(256);      // screen edges

    // ── Primitives ────────────────────────────────────────────────────────────

    public static Sprite MakeSquare(int size)
    {
        var tex = NewTex(size);
        var px = Fill(size * size, Color.white);
        return Bake(tex, px, size);
    }

    public static Sprite MakeCircle(int size)
    {
        var tex = NewTex(size);
        var px = Fill(size * size, Color.clear);
        float r = size / 2f;
        float ring = Mathf.Max(2f, size * 0.10f);   // thick black outline (neo-brutalism)
        Vector2 c = new Vector2(r, r);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), c);
                if (d <= r - 1)
                    px[y * size + x] = (d >= r - 1 - ring) ? Color.black : Color.white;
            }
        return Bake(tex, px, size);
    }

    // Solid filled circle, no outline — used as a particle "bit".
    public static Sprite MakeFilledCircle(int size)
    {
        var tex = NewTex(size);
        var px = Fill(size * size, Color.clear);
        float r = size / 2f;
        Vector2 c = new Vector2(r, r);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (Vector2.Distance(new Vector2(x + .5f, y + .5f), c) <= r - 1)
                    px[y * size + x] = Color.white;
        return Bake(tex, px, size);
    }

    // Radial darkening (transparent centre → dark corners) for a cheap vignette.
    public static Sprite MakeVignette(int size)
    {
        var tex = NewTex(size);
        tex.filterMode = FilterMode.Bilinear;   // smooth, no banding
        var px = new Color[size * size];
        Vector2 c = new Vector2(size / 2f, size / 2f);
        float maxD = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / maxD;
                float a = Mathf.Clamp01((d - 0.55f) / 0.45f);
                px[y * size + x] = new Color(0f, 0f, 0f, a * a * 0.7f);
            }
        return Bake(tex, px, size);
    }

    // Hollow ring (white, tint later) used to show a collector's action radius.
    public static Sprite MakeRing(int size, float thickness)
    {
        var tex = NewTex(size);
        var px = Fill(size * size, Color.clear);
        float r = size / 2f;
        Vector2 c = new Vector2(r, r);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), c);
                if (d <= r - 1 && d >= r - 1 - thickness) px[y * size + x] = Color.white;
            }
        return Bake(tex, px, size);
    }

    // Rounded rectangle filling the whole texture, with a thick dark outline.
    public static Sprite MakeRoundedRect(int w, int h, int radius, Color fill, Color outline, int thickness = 4)
    {
        var tex = NewTex(w, h);
        var px = Fill(w * h, Color.clear);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (InsideRounded(x, y, w, h, radius))
                {
                    bool edge = !InsideRounded(x, y, w, h, radius, thickness);
                    px[y * w + x] = edge ? outline : fill;
                }
            }
        return Bake(tex, px, w, h);
    }

    // ── Vertical gradient (for sky / panels) ───────────────────────────────────

    public static Sprite VerticalGradient(int w, int h, Color top, Color bottom)
    {
        var tex = NewTex(w, h);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = y / (float)(h - 1);
            Color c = Color.Lerp(bottom, top, t);
            for (int x = 0; x < w; x++) px[y * w + x] = c;
        }
        return Bake(tex, px, w, h);
    }

    // ── Trash icons (white/grey, tinted later by TrashItem) ─────────────────────

    public static Sprite TrashSprite(TrashItem.TrashType type) => type switch
    {
        TrashItem.TrashType.Glass   => MakeGlass(),
        TrashItem.TrashType.Plastic => MakeBottle(),
        TrashItem.TrashType.Paper   => MakePaper(),
        TrashItem.TrashType.Metal   => MakeCan(),
        _ => MakeSquare(64)
    };

    // Glass bottle: round body + long thin neck + cork, with a reflection
    // stripe so it clearly reads as "glass" (distinct from the plastic bottle).
    static Sprite MakeGlass()
    {
        int w = 44, h = 64; var tex = NewTex(w, h); var px = Fill(w * h, Color.clear);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool body     = y >= 2  && y < 34 && x > 8  && x < 36;  // round body
                bool shoulder = y >= 34 && y < 42 && x > 12 && x < 32;  // shoulders
                bool neck     = y >= 42 && y < 58 && x > 17 && x < 27;  // long thin neck
                bool cap      = y >= 58 && y < 62 && x > 16 && x < 28;  // cork / cap
                if (body || shoulder || neck || cap)
                {
                    bool edge = (body     && (x <= 9  || x >= 35 || y <= 2)) ||
                                (shoulder && (x <= 13 || x >= 31)) ||
                                (neck     && (x <= 18 || x >= 26)) ||
                                (cap      && (x <= 17 || x >= 27 || y >= 61));
                    px[y * w + x] = edge ? Dark : (cap ? Grey : White);
                }
            }
        // vertical reflection highlight on the body (glass shine)
        for (int y = 6; y < 32; y++)
            if (px[y * w + 14].a > 0f) px[y * w + 14] = Grey;
        return Bake(tex, px, w, h);
    }

    static Sprite MakeBottle()
    {
        int w = 48, h = 64; var tex = NewTex(w, h); var px = Fill(w * h, Color.clear);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool body = y < 46 && x > 12 && x < 36;          // wide body
                bool neck = y >= 46 && y < 56 && x > 18 && x < 30; // neck
                bool cap  = y >= 56 && x > 17 && x < 31;           // cap
                if (body || neck || cap)
                {
                    bool edge = x <= 13 || x >= 35 || (neck && (x <= 19 || x >= 29));
                    px[y * w + x] = edge ? Dark : (cap ? Grey : White);
                }
            }
        return Bake(tex, px, w, h);
    }

    static Sprite MakePaper()
    {
        int s = 56; var tex = NewTex(s); var px = Fill(s * s, Color.clear);
        for (int y = 6; y < 50; y++)
            for (int x = 8; x < 48; x++)
            {
                // folded top-right corner
                if (y > 38 && x > 38 && (x - 38) + (y - 38) > 11) continue;
                bool edge = x == 8 || x == 47 || y == 6 ||
                            (y > 38 && x > 38 && Mathf.Abs((x - 38) + (y - 38) - 11) < 2);
                px[y * s + x] = edge ? Dark : White;
            }
        // text lines
        for (int line = 0; line < 4; line++)
            for (int x = 14; x < 40; x++) px[(16 + line * 7) * s + x] = Grey;
        return Bake(tex, px, s);
    }

    static Sprite MakeCan()
    {
        int w = 44, h = 60; var tex = NewTex(w, h); var px = Fill(w * h, Color.clear);
        for (int y = 4; y < 56; y++)
            for (int x = 8; x < 36; x++)
            {
                bool edge = x == 8 || x == 35 || y == 4 || y == 55;
                px[y * w + x] = edge ? Dark : White;
            }
        // label band
        for (int y = 22; y < 38; y++)
            for (int x = 8; x < 36; x++) px[y * w + x] = (x == 8 || x == 35) ? Dark : Grey;
        return Bake(tex, px, w, h);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    static readonly Color White = Color.white;
    static readonly Color Grey  = new Color(0.75f, 0.75f, 0.75f);
    static readonly Color Dark  = Color.black;   // pure black outlines

    static bool InsideRounded(int x, int y, int w, int h, int r, int inset = 0)
    {
        int xi = x, yi = y;
        int left = inset + r, right = w - 1 - inset - r;
        int bottom = inset + r, top = h - 1 - inset - r;
        if (xi < inset || xi > w - 1 - inset || yi < inset || yi > h - 1 - inset) return false;
        int cx = Mathf.Clamp(xi, left, right);
        int cy = Mathf.Clamp(yi, bottom, top);
        return (xi - cx) * (xi - cx) + (yi - cy) * (yi - cy) <= r * r;
    }

    static Texture2D NewTex(int s) => NewTex(s, s);
    static Texture2D NewTex(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        return t;
    }
    static Color[] Fill(int n, Color c) { var a = new Color[n]; for (int i = 0; i < n; i++) a[i] = c; return a; }
    static Sprite Bake(Texture2D tex, Color[] px, int s) => Bake(tex, px, s, s);
    static Sprite Bake(Texture2D tex, Color[] px, int w, int h)
    {
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Mathf.Max(w, h));
    }
}

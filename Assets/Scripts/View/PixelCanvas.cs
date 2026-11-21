using UnityEngine;

namespace PixelSpace
{
    /// <summary>Пиксельная картинка: прозрачные пиксели имеют a = 0.</summary>
    public sealed class PixelSprite
    {
        public readonly int w, h;
        public readonly Color32[] px; // строка 0 — верх

        public PixelSprite(int w, int h) { this.w = w; this.h = h; px = new Color32[w * h]; }

        public static PixelSprite FromRows(string[] rows, params (char ch, string col)[] pal)
        {
            var s = new PixelSprite(rows[0].Length, rows.Length);
            for (int y = 0; y < s.h; y++)
                for (int x = 0; x < s.w; x++)
                {
                    char ch = rows[y][x];
                    if (ch == '.') continue;
                    foreach (var (c, col) in pal)
                        if (c == ch) { s.px[y * s.w + x] = Px.Hex(col); break; }
                }
            return s;
        }

        /// <summary>Texture2D для UI (иконки HUD).</summary>
        public Texture2D ToTexture()
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var buf = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    buf[(h - 1 - y) * w + x] = px[y * w + x];
            t.SetPixels32(buf);
            t.Apply(false);
            return t;
        }
    }

    /// <summary>
    /// Кадр низкого разрешения (аналог canvas 2D из веб-версии). Координаты: x вправо, y вниз.
    /// Рисуется в Color32[], затем заливается в Texture2D с фильтрацией Point.
    /// </summary>
    public sealed class PixelCanvas
    {
        public int W, H;
        public Texture2D Tex;
        Color32[] buf;

        public void Resize(int w, int h)
        {
            if (w == W && h == H && Tex != null) return;
            W = w; H = h;
            if (Tex != null) Object.Destroy(Tex);
            Tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            buf = new Color32[W * H];
        }

        /// <summary>Общая прозрачность всего, что рисуется (для плавного перехода карта ↔ система).</summary>
        public float GlobalAlpha = 1;

        public void Clear(Color32 c)
        {
            c.a = 255;
            if (GlobalAlpha < 1) { Rect(0, 0, W, H, c); return; }
            for (int i = 0; i < buf.Length; i++) buf[i] = c;
        }

        public void Apply()
        {
            Tex.SetPixels32(buf);
            Tex.Apply(false);
        }

        public void Pset(int x, int y, Color32 c, float alpha = 1)
        {
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;
            float a = alpha * GlobalAlpha * c.a / 255f;
            if (a <= 0) return;
            int i = (H - 1 - y) * W + x;
            if (a >= 1) { c.a = 255; buf[i] = c; return; }
            var d = buf[i];
            buf[i] = new Color32((byte)(d.r + (c.r - d.r) * a), (byte)(d.g + (c.g - d.g) * a), (byte)(d.b + (c.b - d.b) * a), 255);
        }

        /// <summary>Аддитивное смешивание: наложения светлеют вплоть до белого (свечение плотных скоплений).</summary>
        public void Add(int x, int y, Color32 c, float alpha)
        {
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;
            float a = alpha * GlobalAlpha;
            if (a <= 0) return;
            int i = (H - 1 - y) * W + x;
            var d = buf[i];
            buf[i] = new Color32((byte)Mathf.Min(255, d.r + c.r * a), (byte)Mathf.Min(255, d.g + c.g * a), (byte)Mathf.Min(255, d.b + c.b * a), 255);
        }

        public void Rect(int x, int y, int w, int h, Color32 c, float alpha = 1)
        {
            // отсечение по кадру: при сильном приближении фигуры бывают в тысячи пикселей
            int x0 = Mathf.Max(x, 0), y0 = Mathf.Max(y, 0), x1 = Mathf.Min(x + w, W), y1 = Mathf.Min(y + h, H);
            for (int yy = y0; yy < y1; yy++)
                for (int xx = x0; xx < x1; xx++)
                    Pset(xx, yy, c, alpha);
        }

        /// <summary>Пиксельный круг (pcirc).</summary>
        public void Circle(int x, int y, int r, Color32 c, float alpha = 1)
        {
            int dy0 = Mathf.Max(-r, -y), dy1 = Mathf.Min(r, H - 1 - y);
            for (int dy = dy0; dy <= dy1; dy++)
            {
                int w = Mathf.FloorToInt(Mathf.Sqrt(r * r - dy * dy));
                Rect(x - w, y + dy, w * 2 + 1, 1, c, alpha);
            }
        }

        /// <summary>Линия в 1 пиксель; прозрачность плавно меняется от a0 в начале до a1 в конце.</summary>
        public void Line(float x0, float y0, float x1, float y1, Color32 c, float a0, float a1)
        {
            // отсекаем по кадру с запасом, чтобы при сильном приближении не шагать по тысячам пикселей вне экрана
            float dx = x1 - x0, dy = y1 - y0;
            float t0 = 0, t1 = 1;
            if (!Clip(-dx, x0 + 1, ref t0, ref t1) || !Clip(dx, W - x0, ref t0, ref t1) ||
                !Clip(-dy, y0 + 1, ref t0, ref t1) || !Clip(dy, H - y0, ref t0, ref t1)) return;
            int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * (t1 - t0)));
            int px = int.MinValue, py = int.MinValue;
            for (int i = 0; i <= n; i++)
            {
                float t = Mathf.Lerp(t0, t1, (float)i / n);
                int X = Px.R(x0 + dx * t), Y = Px.R(y0 + dy * t);
                if (X == px && Y == py) continue;
                px = X; py = Y;
                Pset(X, Y, c, Mathf.Lerp(a0, a1, t));
            }
        }

        // отсечение Лианга — Барски для одной границы
        static bool Clip(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Abs(p) < 1e-6f) return q >= 0;
            float r = q / p;
            if (p < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
            return true;
        }

        /// <summary>Пунктирное кольцо из n точек.</summary>
        public void Ring(int x, int y, float r, Color32 c, int n, float rot = 0, float alpha = 1)
        {
            for (int i = 0; i < n; i++)
            {
                float a = (float)i / n * Px.TAU + rot;
                Pset(Px.R(x + Mathf.Cos(a) * r), Px.R(y + Mathf.Sin(a) * r), c, alpha);
            }
        }

        public void Blit(PixelSprite s, int x, int y)
        {
            for (int yy = 0; yy < s.h; yy++)
                for (int xx = 0; xx < s.w; xx++)
                {
                    var c = s.px[yy * s.w + xx];
                    if (c.a > 0) Pset(x + xx, y + yy, c);
                }
        }

        /// <summary>Спрайт с центром в (x,y), повёрнутый на угол a и масштабированный (ближайший сосед).</summary>
        public void BlitRot(PixelSprite s, int x, int y, float a, float scale = 1)
        {
            float cs = Mathf.Cos(a) / scale, sn = Mathf.Sin(a) / scale;
            int hw = s.w >> 1, hh = s.h >> 1;
            int R = Mathf.CeilToInt(Mathf.Sqrt(s.w * s.w + s.h * s.h) / 2 * scale) + 1;
            for (int Y = Mathf.Max(y - R, 0); Y <= Mathf.Min(y + R, H - 1); Y++)
                for (int X = Mathf.Max(x - R, 0); X <= Mathf.Min(x + R, W - 1); X++)
                {
                    float dx = X + .5f - x, dy = Y + .5f - y;
                    int sx = Mathf.FloorToInt(dx * cs + dy * sn) + hw;
                    int sy = Mathf.FloorToInt(-dx * sn + dy * cs) + hh;
                    if ((uint)sx >= (uint)s.w || (uint)sy >= (uint)s.h) continue;
                    var c = s.px[sy * s.w + sx];
                    if (c.a > 0) Pset(X, Y, c);
                }
        }
    }
}

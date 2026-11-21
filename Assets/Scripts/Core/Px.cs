using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelSpace
{
    /// <summary>Детерминированный ГСЧ (mulberry32), как в веб-версии.</summary>
    public sealed class Rng
    {
        uint a;
        public Rng(uint seed) { a = seed; }

        /// <summary>Число в [0,1).</summary>
        public double Next()
        {
            unchecked
            {
                a += 0x6D2B79F5;
                uint t = (a ^ (a >> 15)) * (1u | a);
                t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        public float F() => (float)Next();
        public int Int(int n) => (int)Math.Floor(Next() * n);
    }

    /// <summary>Шум, утилиты и генерация имён.</summary>
    public static class Px
    {
        public const float TAU = 6.283f;

        public static double Hash(int x, int y, int s)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + s * 1442695041;
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                return (uint)(h ^ (int)((uint)h >> 16)) / 4294967296.0;
            }
        }

        public static float VNoise(float x, float y, int s)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi, u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            float a = (float)Hash(xi, yi, s), b = (float)Hash(xi + 1, yi, s), c = (float)Hash(xi, yi + 1, s), d = (float)Hash(xi + 1, yi + 1, s);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        public static float Fbm(float x, float y, int s) =>
            VNoise(x, y, s) * .5f + VNoise(x * 2, y * 2, s + 1) * .3f + VNoise(x * 4, y * 4, s + 2) * .2f;

        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;

        /// <summary>Округление как Math.round в JS (половина — вверх).</summary>
        public static int R(float v) => (int)Math.Floor(v + .5f);

        public static float Dist(float ax, float ay, float bx, float by) => Mathf.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
        public static float Dist(Star a, Star b) => Dist(a.x, a.y, b.x, b.y);
        public static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);

        static readonly string[] SYL = { "ка", "ро", "ми", "зе", "та", "лу", "вон", "ар", "ис", "ке", "на", "по", "дра", "ге", "ос", "ул", "фе", "ри", "бо", "сэ", "кс", "ти", "ма", "лор", "зу", "ван", "эль", "ох", "кри", "ды" };

        public static string GenName(Rng r, int n = 0)
        {
            int k = n > 0 ? n : 2 + r.Int(2);
            string s = "";
            for (int i = 0; i < k; i++) s += SYL[r.Int(SYL.Length)];
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        public static List<T> Shuffle<T>(List<T> a, Rng r)
        {
            for (int i = a.Count - 1; i > 0; i--)
            {
                int j = r.Int(i + 1);
                (a[i], a[j]) = (a[j], a[i]);
            }
            return a;
        }

        static readonly Dictionary<string, Color32> hexCache = new Dictionary<string, Color32>();

        public static Color32 Hex(string h)
        {
            if (hexCache.TryGetValue(h, out var c)) return c;
            int n = Convert.ToInt32(h.Substring(1, 6), 16);
            byte al = h.Length >= 9 ? Convert.ToByte(h.Substring(7, 2), 16) : (byte)255;
            return hexCache[h] = new Color32((byte)(n >> 16 & 255), (byte)(n >> 8 & 255), (byte)(n & 255), al);
        }

        public static Color HexC(string h) => Hex(h);

        public static string Rep(string s, int n)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < n; i++) sb.Append(s);
            return sb.ToString();
        }
    }
}

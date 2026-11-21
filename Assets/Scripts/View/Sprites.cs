using System.Collections.Generic;
using UnityEngine;

namespace PixelSpace
{
    /// <summary>Спрайты кораблей и процедурные планеты.</summary>
    public static class Sprites
    {
        public static readonly PixelSprite Ship = PixelSprite.FromRows(new[]
        {
            "...hh......",
            "...hdh.....",
            "..hddhhh...",
            ".ehddddwhh.",
            "eeccccccwwh",
            ".ehddddwhh.",
            "..hddhhh...",
            "...hdh.....",
            "...hh......",
        }, ('h', "#cfd8e6"), ('d', "#7a8aa8"), ('c', "#ffcf4a"), ('w', "#6fe3ff"), ('e', "#ff7a2a"));

        public static readonly PixelSprite Pirate = PixelSprite.FromRows(new[]
        {
            "r...r....",
            "rr.rrr...",
            ".rrrrrrr.",
            "rkrrryyrr",
            ".rrrrrrr.",
            "rr.rrr...",
            "r...r....",
        }, ('r', "#d8344a"), ('k', "#2a0000"), ('y', "#ffe066"));

        static readonly string[] EnRows =
        {
            "xx.......",
            ".xxx.....",
            "..xxxxx..",
            ".kxxyyxxx",
            "..xxxxx..",
            ".xxx.....",
            "xx.......",
        };

        static readonly Dictionary<int, PixelSprite> enSpr = new Dictionary<int, PixelSprite>();

        public static PixelSprite Empire(int i)
        {
            if (!enSpr.TryGetValue(i, out var s))
                enSpr[i] = s = PixelSprite.FromRows(EnRows, ('x', Game.G.emp[i].col), ('k', "#111111"), ('y', "#ffffff"));
            return s;
        }

        public static void ResetCaches() { enSpr.Clear(); clsSpr.Clear(); }

        // ---------- боевые корабли по классам ----------
        // x — корпус, d — тёмная обшивка, y — окна, a — акценты, e — огонь двигателей. Нос — вправо.

        static readonly string[][] CLASS_ROWS =
        {
            new[]
            {
                "x.......",
                "dx......",
                ".dxxa...",
                "edxxxyyx",
                ".dxxa...",
                "dx......",
                "x.......",
            },
            new[]
            {
                "..dd.......",
                ".dxxxa.....",
                "edxxxxxxx..",
                "edxxyyxxxxa",
                "edxxxxxxx..",
                ".dxxxa.....",
                "..dd.......",
            },
            new[]
            {
                "..ddddd......",
                ".dxxxxxaa....",
                "..dd.........",
                "edddxxxxx....",
                "edxxxxxxxxxa.",
                "edxxyyyxxxxxa",
                "edxxxxxxxxxa.",
                "edddxxxxx....",
                "..dd.........",
                ".dxxxxxaa....",
                "..ddddd......",
            },
            new[]
            {
                "xx.........",
                "dxx........",
                ".dxx.......",
                "..dxxx.....",
                "eddxxxxyx..",
                "eddxxxxyyxa",
                "eddxxxxyx..",
                "..dxxx.....",
                ".dxx.......",
                "dxx........",
                "xx.........",
            },
            null, // дрон — маленький корабль игрока
            new[]
            {
                "..ddddddd....",
                ".dxaxaxaxd...",
                "edxxxxxxxxdd.",
                "edxaxaxaxxxyx",
                "edxxxxxxxxdd.",
                ".dxaxaxaxd...",
                "..ddddddd....",
            },
            new[]
            {
                ".....ddddd.....",
                "...ddxxxxxdd...",
                "..dxxaxxxaxxd..",
                ".dxxxxxxxxxxxd.",
                ".dxaxxdddxxaxd.",
                "dxxxxdyyydxxxxd",
                "dxxxdyyyyydxxxa",
                "dxxxdyyyyydxxxa",
                "dxxxdyyyyydxxxa",
                "dxxxxdyyydxxxxd",
                ".dxaxxdddxxaxd.",
                ".dxxxxxxxxxxxd.",
                "..dxxaxxxaxxd..",
                "...ddxxxxxdd...",
                ".....ddddd.....",
            },
            new[]
            {
                "...d...",
                "..dxd..",
                ".dxaxd.",
                "edxyyxa",
                ".dxaxd.",
                "..dxd..",
                "...d...",
            },
        };

        // во сколько раз увеличивать спрайт класса (размер в пикселях уже нарисован по классу; база — крупнее)
        static readonly float[] CLASS_SCALE = { 1, 1, 1, 1, 1, 1, 1.5f, 1 };

        static readonly Dictionary<(int, int, string), PixelSprite> clsSpr = new Dictionary<(int, int, string), PixelSprite>();

        static string HexOf(Color32 c) => "#" + c.r.ToString("x2") + c.g.ToString("x2") + c.b.ToString("x2");
        static string Dark(string hex, float k) { var c = Px.Hex(hex); return HexOf(new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), 255)); }

        /// <summary>Спрайт боевого корабля по классу и стороне: свои — серебро с цветом империи игрока,
        /// империи — в своих цветах, пираты — серые с красными окнами. scale — дополнительное увеличение.</summary>
        public static PixelSprite Combat(BShip e, out float scale)
        {
            scale = 1;
            var rows = e.cls < CLASS_ROWS.Length ? CLASS_ROWS[e.cls] : null;
            if (rows == null || e.drone) return Ship;
            scale = CLASS_SCALE[e.cls];
            int who = e.pirate ? -3 : e.ei >= 0 ? e.ei : -1;
            string my = HexOf(Game.EmpCol);
            var key = (who, e.cls, who == -1 ? my : "");
            if (clsSpr.TryGetValue(key, out var s)) return s;
            (char, string)[] pal;
            if (e.pirate) pal = new[] { ('x', "#8a8f98"), ('d', "#4a4e56"), ('y', "#ff3040"), ('a', "#c0242f"), ('e', "#ff7a2a") };
            else if (e.ei >= 0)
            {
                string col = Game.G.emp[e.ei].col;
                pal = new[] { ('x', col), ('d', Dark(col, .55f)), ('y', "#ffffff"), ('a', "#1a1a22"), ('e', "#ff7a2a") };
            }
            else pal = new[] { ('x', "#cfd8e6"), ('d', "#7a8aa8"), ('y', "#6fe3ff"), ('a', my), ('e', "#ff7a2a") };
            clsSpr[key] = s = PixelSprite.FromRows(rows, pal);
            return s;
        }


        // ---------- иконки HUD ----------
        public static readonly PixelSprite IcoCoin = PixelSprite.FromRows(new[]
        {
            "..yyy..", ".yYYYy.", "yYyYYYy", "yYyYYYy", "yYYYYYy", ".yYYYy.", "..yyy..",
        }, ('y', "#b07a12"), ('Y', "#ffcf4a"));

        public static readonly PixelSprite IcoBolt = PixelSprite.FromRows(new[]
        {
            "....yy.", "...yy..", "..yy...", ".yyyyy.", "...yy..", "..yy...", ".yy....",
        }, ('y', "#ffe066"));

        public static readonly PixelSprite IcoHeart = PixelSprite.FromRows(new[]
        {
            ".rr.rr.", "rRrrrrr", "rrrrrrr", "rrrrrrr", ".rrrrr.", "..rrr..", "...r...",
        }, ('r', "#ff5a6a"), ('R', "#ffd0d6"));

        public static readonly PixelSprite IcoGem = PixelSprite.FromRows(new[]
        {
            ".bbbbb.", "bWbbbBb", "bbbbbbb", ".bbbbb.", "..bbb..", "...b...",
        }, ('b', "#6fe3ff"), ('W', "#ffffff"), ('B', "#2a8ab0"));

        // ---------- иконки предметов (8×8): a — тень, b — основной цвет, c — блик, w — белый ----------

        static readonly Dictionary<string, string[]> ItemRows = new Dictionary<string, string[]>
        {
            ["gem"] = new[] { "..aaaa..", ".abbbba.", "abccbbba", "abcbbbba", ".abbbba.", "..abba..", "...aa...", "........" },
            ["relic"] = new[] { "..aaaa..", "...bb...", "..bccb..", ".bbccbb.", ".bbbbbb.", "..bbbb..", ".aaaaaa.", "aaaaaaaa" },
            ["crystal"] = new[] { "...a....", "..aba.a.", "..bcb.ab", ".abcbabc", ".abcbabb", "abbbbbba", "aaaaaaaa", "........" },
            ["map"] = new[] { "aaaaaaa.", "abbbbbba", "abcbbcba", "abbcbbba", "abbbcbba", "abcbbbba", "abbbbbba", ".aaaaaaa" },
            ["idol"] = new[] { "..aaaa..", ".abccba.", ".abwwba.", "..abba..", ".abbbba.", "abbbbbba", "..abba..", ".aa..aa." },
            ["battery"] = new[] { "...aa...", ".aaaaaa.", ".awwwwa.", ".abbbba.", ".abccba.", ".abbbba.", ".abbbba.", ".aaaaaa." },
            ["ingot"] = new[] { "........", "........", "..aaaaa.", ".abcccba", "abbbbba.", "aaaaaa..", "........", "........" },
            ["spice"] = new[] { "...cc...", "..cbbc..", ".abbbba.", "abbcbbba", "abbbbcba", "abbbbbba", ".abbbba.", "..aaaa.." },
            ["kit"] = new[] { ".b......", ".bccc...", ".bcc....", ".b......", "aaaaaaaa", "abbbbbba", "abwwwwba", "aaaaaaaa" },
            ["atmo"] = new[] { "........", "..cccc..", ".cbbbbc.", "cbbbbbbc", "abbbbbba", ".aaaaaa.", "..b..b..", ".b..b..." },
            ["heat"] = new[] { "...b....", "..bb..b.", "..bcb.bb", ".bccbbcb", ".bcwccb.", "abcwwcba", ".abccba.", "..aaaa.." },
            ["cool"] = new[] { "...c....", ".c.b.c..", "..cbc...", "cbbwbbc.", "..cbc...", ".c.b.c..", "...c....", "........" },
            ["up_eng"] = new[] { "..aaaa..", ".abbbba.", ".abccba.", ".abbbba.", "..abba..", "..cbbc..", "...cc...", "....c..." },
            ["up_las"] = new[] { "........", "aa......", "abaaaaaa", "abbccccw", "abaaaaaa", "aa......", "........", "........" },
            ["up_arm"] = new[] { "aaaaaaaa", "abbbbbba", "abccbbba", "abcbbbba", ".abbbba.", ".abbbba.", "..abba..", "...aa..." },
            ["up_cargo"] = new[] { "........", "aaaaaaaa", "abbbbbba", "abaaaaba", "abbbbbba", "abaaaaba", "abbbbbba", "aaaaaaaa" },
            ["gas"] = new[] { "........", "..cccc..", ".cbbbbc.", "abbaabba", "abbbbbba", "aabbaaba", ".abbbba.", "..aaaa.." },
            ["drone"] = new[] { "...aa...", "..abba..", ".abccba.", "aabwwbaa", "aabwwbaa", ".abccba.", "..abba..", "...aa..." },
            ["station"] = new[] { "...cc...", "...bb...", ".aabbaa.", "abbccbba", "abbccbba", ".aabbaa.", "...bb...", "...aa..." },
            ["repair"] = new[] { ".cb..cb.", ".bb..bb.", ".cbbbbb.", "..abba..", "...cb...", "...cb...", "...cb...", "...aa..." }, // гаечный ключ
            ["seal"] = new[] { "..aaaa..", ".abbbba.", "abcbbcba", "abbccbba", "abbccbba", "abcbbcba", ".abbbba.", "..aaaa.." },
            ["larva"] = new[] { "...aa...", "..abba..", ".abcbba.", ".abbbba.", ".abcbba.", ".abbbba.", "..abba..", "...aa..." },
            ["hammer"] = new[] { ".aaaaaa.", ".abccba.", ".abbbba.", ".aaaaaa.", "...ab...", "...ab...", "...ab...", "...aa..." },
            ["stone"] = new[] { "........", "...aa...", "..abba..", ".abcbba.", "abccbbba", "abbbbbba", ".aaaaaa.", "........" },
            ["wheel"] = new[] { "..aaaa..", ".ab.b.a.", "a.bcb..a", "abcwcba.", "a..bcb.a", ".a.b.ba.", "..aaaa..", "........" },
            ["cup"] = new[] { "aaaaaaaa", "abccbbba", ".abbbba.", "..abba..", "...bb...", "...bb...", "..abba..", ".aaaaaa." },
            ["ember"] = new[] { "....c...", "...cb...", "...bbc..", "..cbwb..", "..bwwbc.", ".abwwba.", "..abba..", ".aaaaaa." },
            // оружие
            ["w_blaster"] = new[] { "........", "......c.", ".aabbbcw", "abbbbcww", ".aabbbcw", "......c.", "........", "........" },
            ["w_laser"] = new[] { "........", "aa......", "abaaaaaa", "abwwwwww", "abaaaaaa", "aa......", "........", "........" },
            ["w_bomb"] = new[] { ".....c..", "....w...", "..aaba..", ".abbbba.", ".abcbba.", ".abbbba.", "..aaaa..", "........" },
            ["w_bolt"] = new[] { "....bb..", "...bb...", "..bc....", ".bccccb.", "....cb..", "...bb...", "..bb....", ".b......" },
            ["w_missile"] = new[] { "......cw", ".....bbc", "....bbb.", "...bbb..", ".abbb...", "aaab....", ".aa.....", "a.a....." },
            ["w_auto"] = new[] { "...b....", ".aabaa..", ".a...a..", "bb.c.bb.", ".a...a..", ".aabaa..", "...b....", "........" },
            ["w_breaker"] = new[] { ".aaaaaa.", "abbcbbba", "abbcbbba", "abccbbba", "abbbcbba", ".abbcba.", "..abba..", "...aa..." },
        };

        static readonly Dictionary<string, Texture2D> iconCache = new Dictionary<string, Texture2D>();

        /// <summary>Иконка предмета или улучшения, окрашенная в цвет col.</summary>
        public static Texture2D Icon(string key, string col)
        {
            string ck = key + col;
            if (iconCache.TryGetValue(ck, out var t)) return t;
            var b = Px.Hex(col);
            var a = new Color32((byte)(b.r * .5f), (byte)(b.g * .5f), (byte)(b.b * .5f), 255);
            var c = (Color32)Color.Lerp(b, Color.white, .55f);
            var rows = ItemRows.TryGetValue(key, out var r) ? r : ItemRows["gem"];
            var s = new PixelSprite(rows[0].Length, rows.Length);
            for (int y = 0; y < s.h; y++)
                for (int x = 0; x < s.w; x++)
                    switch (rows[y][x])
                    {
                        case 'a': s.px[y * s.w + x] = a; break;
                        case 'b': s.px[y * s.w + x] = b; break;
                        case 'c': s.px[y * s.w + x] = c; break;
                        case 'w': s.px[y * s.w + x] = new Color32(255, 255, 255, 255); break;
                    }
            return iconCache[ck] = s.ToTexture();
        }

        // ---------- планеты ----------
        static readonly Dictionary<string, PixelSprite> planetCache = new Dictionary<string, PixelSprite>();

        static readonly Color32 cWaterDeep = Px.Hex("#2b66c4"), cWater = Px.Hex("#2a4f8a"), cGrass = Px.Hex("#3f9c3f"), cLush = Px.Hex("#7cc456"), cCloud = Px.Hex("#ffffff");

        /// <summary>Процедурная планета; облик зависит от пригодности (меняется при терраформинге).</summary>
        public static PixelSprite PlanetSprite(Planet p)
        {
            int hab = Game.Habit(p);
            string key = p.seed + "_" + p.type + "_" + hab + "_" + p.atmo + "_" + p.size;
            if (planetCache.TryGetValue(key, out var cached)) return cached;

            var T = Defs.Types[p.type];
            int R = p.size, D = R * 2 + 1;
            var s = new PixelSprite(D, D);
            var pal = (T.Gas && p.seed % 2 == 1) ? Defs.GasAltPal : T.Pal;
            bool terr = !T.Gas && p.type != "ocean" && p.type != "garden";

            for (int y = 0; y < D; y++)
                for (int x = 0; x < D; x++)
                {
                    float dx = x - R, dy = y - R, d = Px.Len(dx, dy) / (R + .5f);
                    if (d > 1) continue;
                    float n = T.Gas
                        ? .5f + .33f * Mathf.Sin(dy / R * 7 + Px.Fbm(x * .15f, y * .6f, p.seed) * 4) + .2f * (Px.Fbm(x * .3f, y * .3f, p.seed + 5) - .5f)
                        : Px.Fbm(x * 3f / R + 3, y * 3f / R + 3, p.seed);
                    float l = 1 - Px.Len(dx + R * .45f, dy + R * .45f) / (R * 2.1f);
                    Color32? col = null;
                    if (terr)
                    {
                        if (hab >= 1 && n < .42f) col = hab >= 2 ? cWaterDeep : cWater;
                        else if (hab >= 2 && n < .58f) col = cGrass;
                        else if (hab >= 3 && n < .66f) col = cLush;
                    }
                    var c = col ?? pal[Mathf.FloorToInt(Px.Clamp(n * .9f + l * .5f - .15f, 0, .999f) * pal.Length)];
                    if (!T.Gas && p.atmo >= 2 && Px.Fbm(x * 5f / R, y * 5f / R, p.seed + 9) > .69f) c = cCloud;
                    float sh = l < .12f ? .4f : l < .28f ? .68f : 1;
                    if (d > .92f) sh *= .8f;
                    s.px[y * D + x] = new Color32((byte)(c.r * sh), (byte)(c.g * sh), (byte)(c.b * sh), 255);
                }
            return planetCache[key] = s;
        }
    }
}

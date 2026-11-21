using System.Collections.Generic;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    /// <summary>
    /// Малые империи: галактика поделена на клетки, и как только клетка оказывается в радиусе
    /// 5 прыжков максимального двигателя от корабля, в ней (по seed) может возникнуть империя
    /// на 3–30 систем, чаще всего 3–7. Созданные клетки запоминаются в сохранении.
    /// </summary>
    public static class EmpireGen
    {
        const float CELL = 600;
        public const float GEN_R = 310 * 5;    // радиус «оживления» галактики вокруг корабля
        const float EMPIRE_CHANCE = .5f;       // доля клеток со своей империей
        const float HOME_FREE = 450;           // у Родины игрока чужих систем нет

        static readonly string[] Prefix =
        {
            "Королевство", "Федерация", "Ханство", "Протекторат", "Синдикат", "Лига", "Дом", "Орден",
            "Улей", "Директорат", "Республика", "Конгломерат", "Альянс", "Каганат", "Теократия", "Клан",
        };

        static HashSet<long> done;
        static GalaxyGen.StarIndex index;
        static GameState forGame;
        static readonly List<Star> near = new List<Star>();

        static long Key(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;

        /// <summary>Создать недостающие империи вокруг точки pos.</summary>
        public static void Update(Vector2 pos)
        {
            if (G == null) return;
            if (forGame != G)
            {
                // новая партия или загрузка — пересобираем вспомогательные структуры
                forGame = G;
                done = new HashSet<long>(G.genCells);
                index = new GalaxyGen.StarIndex(G.stars);
            }
            int x0 = Mathf.FloorToInt((pos.x - GEN_R) / CELL), x1 = Mathf.FloorToInt((pos.x + GEN_R) / CELL);
            int y0 = Mathf.FloorToInt((pos.y - GEN_R) / CELL), y1 = Mathf.FloorToInt((pos.y + GEN_R) / CELL);
            for (int cx = x0; cx <= x1; cx++)
                for (int cy = y0; cy <= y1; cy++)
                {
                    long k = Key(cx, cy);
                    if (done.Contains(k)) continue;
                    float mx = (cx + .5f) * CELL, my = (cy + .5f) * CELL;
                    if (Px.Dist(mx, my, pos.x, pos.y) > GEN_R + CELL * .71f) continue;
                    done.Add(k);
                    G.genCells.Add(k);
                    TrySpawn(cx, cy);
                }
        }

        static void TrySpawn(int cx, int cy)
        {
            int seed = G.seed;
            if (Px.Hash(cx, cy, seed ^ 0x5bd1) > EMPIRE_CHANCE) return;

            // столица — звезда рядом со случайной точкой клетки
            float px = (cx + (float)Px.Hash(cx, cy, seed + 1)) * CELL, py = (cy + (float)Px.Hash(cx, cy, seed + 2)) * CELL;
            var home = G.stars[G.home];
            index.Near(px, py, CELL * .6f, near);
            Star cap = null;
            float bd = float.MaxValue;
            foreach (var s in near)
            {
                if (!Free(s, home)) continue;
                float d = Px.Dist(s.x, s.y, px, py);
                if (d < bd) { bd = d; cap = s; }
            }
            if (cap == null) return;

            // размер: чаще 3–7, иногда до 15, изредка до 30
            float u = (float)Px.Hash(cx, cy, seed + 3), v = (float)Px.Hash(cx, cy, seed + 4);
            int size = u < .7f ? 3 + (int)(v * 5) : u < .92f ? 8 + (int)(v * 8) : 16 + (int)(v * 15);
            size = Mathf.Max(3, Mathf.RoundToInt(size * G.empScale)); // модификатор из настроек галактики

            var r = new Rng((uint)(seed ^ (cx * 73856093) ^ (cy * 19349663)));
            // территория растёт от столицы — вдоль рукава и в гущу звёзд
            var land = GalaxyGen.Grow(cap, size, s => Free(s, home), index, GalaxyGen.Rot(seed), r);
            if (land.Count < 3) return;

            int ei = G.emp.Count;
            float hue = r.F();
            var col = Color.HSVToRGB(hue, .45f + r.F() * .35f, .95f);
            int rel = r.F() < .15f ? -40 - r.Int(30) : -20 + r.Int(50); // каждая седьмая — враждебна
            G.emp.Add(new Empire
            {
                name = Prefix[r.Int(Prefix.Length)] + " " + Px.GenName(r), col = "#" + ColorUtility.ToHtmlStringRGB(col),
                rel = rel, capital = cap.id, major = false,
            });
            foreach (var s in land)
            {
                s.owner = s.initOwner = ei;
                s.empHome = s == cap;
                GalaxyGen.ApplyEmpire(s);
            }
        }

        // свободна для новой империи: ничья, не ядро, ещё не посещена (не появится «из ниоткуда» у вас на глазах), не у Родины
        static bool Free(Star s, Star home) =>
            !s.core && s.owner == Owner.NONE && !s.visited && Px.Dist(s, home) > HOME_FREE;
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace PixelSpace
{
    /// <summary>
    /// Процедурная галактика: ядро в центре, вокруг него большое кольцо, от кольца по касательной уходят
    /// спиральные рукава. Звёзды разбросаны по плотности этой «картинки». Всё выводится из seed, поэтому
    /// в сохранение пишутся только изменённые звёзды, а планеты создаются при первом обращении.
    /// </summary>
    public static class GalaxyGen
    {
        public const float R = 6800;          // радиус диска
        public const float CORE_EMPTY = 240;  // пустота вокруг ядра: долететь можно только двигателем ур.4 (310)
        public const float RING = 800;        // радиус центрального круга (от его края начинаются рукава)
        const float RING_W = 450;             // ширина размытого края центрального круга
        const int ARMS = 4;
        const float PITCH = .41f;             // tg угла закрутки: рукава от круга до края делают ~3/4 оборота
        const int TARGET = 40000;             // верхняя граница числа звёзд
        // число попыток фиксировано: при «бесконечных» попытках даже редкие области забиваются до предела и рисунок пропадает
        const int TRIES = 260000;
        const float MIN_D = 42;               // минимальное расстояние между звёздами
        const int MIN_GROUP = 25;             // меньшие оторванные кучки звёзд между рукавами убираем, большие соединяем мостом
        const float STRAY_KEEP = 180;         // …но одиночки не дальше этого от рукава оставляем: к ним долетит двигатель получше
        // редкие звёзды вокруг рукавов: ореол в HALO_W раз шире рукава, плотность HALO — во много раз ниже рукава
        const float HALO = .06f, HALO_W = 3f;
        const float JUMP = 95;                // дальность прыжка двигателя ур.1 — по ней проверяем связность
        const float BRIDGE_MAX = 900;         // дальше этого изолированную группу не соединяем, а убираем

        /// <summary>Поворот рукавов для данного seed.</summary>
        public static float Rot(int seed) => (float)Px.Hash(seed, 17, 3) * Px.TAU;

        // плотность сплошного центрального круга: у края DISK_EDGE, к центру растёт, но не больше чем в 1.5 раза
        const float DISK_EDGE = .35f, DISK_CENTER = DISK_EDGE * 1.5f;
        const float ARM_IN0 = .55f, ARM_IN1 = 1.7f; // рукава проявляются от 0.55 до 1.7 радиуса круга
        // заполнение промежутков: полностью при q ≤ GAP_FILL0, нет при q ≥ GAP_FILL1
        const float GAP_FILL0 = 1.3f, GAP_FILL1 = 1.9f, GAP_LEVEL = .7f;

        /// <summary>Плотность звёзд в точке (0…~1): сплошной центральный круг, рукава и редкое поле между ними.</summary>
        public static float Density(float x, float y, float rot)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            if (r < CORE_EMPTY || r > R) return 0;
            float disk;
            if (r <= RING) disk = Mathf.Lerp(DISK_CENTER, DISK_EDGE, (r - CORE_EMPTY) / (RING - CORE_EMPTY));
            else { float dr = (r - RING) / RING_W; disk = DISK_EDGE * Mathf.Exp(-dr * dr); } // мягкий край круга
            float arm = 0, halo = 0;
            if (r > RING * ARM_IN0)
            {
                float a = Mathf.Atan2(y, x);
                // рукава очень толстые у круга и быстро сужаются к краю (корень — основная часть сужения в начале)
                // но у самого круга не шире половины радиуса: иначе рукава сливаются в сплошной диск и круга не видно
                float t = Mathf.Sqrt(Mathf.Clamp01((r - RING) / (R - RING)));
                float w = Mathf.Min(Mathf.Lerp(1500, 520, t), r * .45f);
                float fade = Mathf.Clamp01((R - r) / (R * .18f)); // и мягко тают у края диска
                // начала рукавов постепенно растворяются в центральном круге — без резкой границы
                float u = Mathf.Clamp01((r - RING * ARM_IN0) / (RING * (ARM_IN1 - ARM_IN0)));
                fade *= u * u * (3 - 2 * u);
                for (int k = 0; k < ARMS; k++)
                {
                    // логарифмическая спираль, выходящая из кольца по касательной
                    float phi = rot + k * Px.TAU / ARMS + Mathf.Log(r / RING) / PITCH;
                    float d = Mathf.DeltaAngle(a * Mathf.Rad2Deg, phi * Mathf.Rad2Deg) * Mathf.Deg2Rad * r / w;
                    arm = Mathf.Max(arm, Mathf.Exp(-d * d) * fade);
                    float hd = d / HALO_W;
                    halo = Mathf.Max(halo, Mathf.Exp(-hd * hd) * fade);
                }
                // Там, где рукава почти смыкаются, промежуток между ними заполняем — граница стирается.
                // q — половина расстояния между соседними рукавами в их ширинах: чем меньше, тем теснее
                float q = Mathf.PI * r / ARMS / w;
                float g = Mathf.Clamp01((q - GAP_FILL0) / (GAP_FILL1 - GAP_FILL0));
                arm = Mathf.Max(arm, (1 - g * g * (3 - 2 * g)) * GAP_LEVEL * fade);
            }
            float field = .015f * (1 - r / R);
            return Mathf.Max(Mathf.Max(disk, arm), HALO * halo) + field;
        }

        // ---------- сетка для быстрого поиска соседей ----------

        sealed class Grid
        {
            readonly float cell;
            readonly Dictionary<long, List<int>> map = new Dictionary<long, List<int>>();
            readonly List<Vector2> pts;
            public Grid(float cell, List<Vector2> pts) { this.cell = cell; this.pts = pts; }
            long Key(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;
            public void Add(int i)
            {
                var p = pts[i];
                long k = Key(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell));
                if (!map.TryGetValue(k, out var l)) map[k] = l = new List<int>();
                l.Add(i);
            }
            public void Near(Vector2 p, float rad, List<int> outList)
            {
                outList.Clear();
                int x0 = Mathf.FloorToInt((p.x - rad) / cell), x1 = Mathf.FloorToInt((p.x + rad) / cell);
                int y0 = Mathf.FloorToInt((p.y - rad) / cell), y1 = Mathf.FloorToInt((p.y + rad) / cell);
                float r2 = rad * rad;
                for (int cx = x0; cx <= x1; cx++)
                    for (int cy = y0; cy <= y1; cy++)
                        if (map.TryGetValue(Key(cx, cy), out var l))
                            foreach (var i in l)
                                if ((pts[i] - p).sqrMagnitude <= r2) outList.Add(i);
            }
        }

        // ---------- построение ----------

        /// <param name="empScale">Модификатор размера империй из настроек новой галактики.</param>
        public static void Build(int seed, float empScale, out List<Star> stars, out int home, out List<Empire> emp)
        {
            var r = new Rng((uint)seed);
            float rot = Rot(seed);

            // 1. точки по плотности (отбор с отказами) с минимальным расстоянием
            var pts = new List<Vector2>();
            var grid = new Grid(MIN_D, pts);
            var tmp = new List<int>();
            for (int tries = 0; pts.Count < TARGET && tries < TRIES; tries++)
            {
                float rad = R * Mathf.Sqrt(r.F()), ang = r.F() * Px.TAU;
                var p = new Vector2(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad);
                if (r.F() * 1.15f > Density(p.x, p.y, rot)) continue;
                grid.Near(p, MIN_D, tmp);
                if (tmp.Count > 0) continue;
                pts.Add(p);
                grid.Add(pts.Count - 1);
            }

            // 2. к ядру должна вести звезда в пределах 300 (дальность ур.4 — 310)
            int nearest = 0;
            for (int i = 1; i < pts.Count; i++) if (pts[i].sqrMagnitude < pts[nearest].sqrMagnitude) nearest = i;
            if (pts[nearest].magnitude > 300)
            {
                var dir = pts[nearest].normalized;
                for (float d = pts[nearest].magnitude - 80; d > 280; d -= 80) { pts.Add(dir * d); grid.Add(pts.Count - 1); }
                pts.Add(dir * 280); grid.Add(pts.Count - 1);
            }

            // 3. связность при дальности ур.1: соединяем группы цепочками звёзд, далёкие крохи убираем
            var jumpGrid = new Grid(JUMP, pts);
            for (int i = 0; i < pts.Count; i++) jumpGrid.Add(i);
            var comp = Components(pts, jumpGrid, out int compCount);
            var size = new int[compCount];
            foreach (var c in comp) size[c]++;
            int main = 0;
            for (int c = 1; c < compCount; c++) if (size[c] > size[main]) main = c;

            var alive = new bool[pts.Count];
            for (int i = 0; i < pts.Count; i++) alive[i] = comp[i] == main;
            var members = new List<int>[compCount];
            for (int i = 0; i < pts.Count; i++) (members[comp[i]] ??= new List<int>()).Add(i);
            // от больших групп к маленьким: большие соединяем первыми, к ним могут прицепиться остальные
            var order = new List<int>();
            for (int c = 0; c < compCount; c++) if (c != main) order.Add(c);
            order.Sort((a, b) => size[b].CompareTo(size[a]));
            var mainGrid = new Grid(200, pts);
            for (int i = 0; i < pts.Count; i++) if (alive[i]) mainGrid.Add(i);
            foreach (int c in order)
            {
                if (size[c] < MIN_GROUP)
                {
                    // крошечная кучка: остаётся, если она недалеко от рукава (без моста), иначе исчезает
                    bool keep = false;
                    foreach (var i in members[c]) { mainGrid.Near(pts[i], STRAY_KEEP, tmp); if (tmp.Count > 0) { keep = true; break; } }
                    if (keep) foreach (var i in members[c]) alive[i] = true;
                    continue;
                }
                int bestA = -1, bestB = -1;
                float bd = float.MaxValue;
                var mem = members[c];
                int step = Mathf.Max(1, mem.Count / 40); // у больших групп проверяем не каждую звезду
                for (int k = 0; k < mem.Count; k += step)
                {
                    mainGrid.Near(pts[mem[k]], BRIDGE_MAX, tmp);
                    foreach (var j in tmp)
                    {
                        float d = (pts[j] - pts[mem[k]]).sqrMagnitude;
                        if (d < bd) { bd = d; bestA = mem[k]; bestB = j; }
                    }
                }
                if (bestA < 0) continue; // слишком далеко — группа исчезнет
                // мост: промежуточные звёзды каждые ~80
                var a = pts[bestA]; var b = pts[bestB];
                int n = Mathf.CeilToInt(Vector2.Distance(a, b) / 80f);
                for (int s = 1; s < n; s++)
                {
                    pts.Add(Vector2.Lerp(a, b, (float)s / n));
                    System.Array.Resize(ref alive, pts.Count);
                    alive[pts.Count - 1] = true;
                    mainGrid.Add(pts.Count - 1);
                }
                foreach (var i in mem) { alive[i] = true; mainGrid.Add(i); }
            }

            // 4. звёзды: ядро — id 0, затем живые точки
            stars = new List<Star> { new Star { id = 0, x = 0, y = 0, name = "Ядро Галактики", col = "#fff4d0", core = true, seed = 1 } };
            for (int i = 0; i < pts.Count; i++)
            {
                if (!alive[i]) continue;
                stars.Add(new Star
                {
                    id = stars.Count, x = pts[i].x, y = pts[i].y, name = Px.GenName(r),
                    col = Defs.StarCols[r.Int(Defs.StarCols.Length)], price = 9 + r.Int(14), seed = 1 + r.Int(1 << 30),
                });
            }

            // 5. родина — на рукаве ближе к краю
            var cand = new List<Star>();
            foreach (var s in stars) { float d = Px.Len(s.x, s.y); if (d > R * .6f && d < R * .82f) cand.Add(s); }
            if (cand.Count == 0) cand.Add(stars[stars.Count - 1]);
            var hs = cand[r.Int(cand.Count)];
            home = hs.id;
            hs.owner = hs.initOwner = Owner.PLAYER;
            hs.visited = true;

            // 6. четыре большие империи — по всей галактике, скорее далеко друг от друга (от Родины не зависят).
            //    Первая столица — случайная звезда, следующие — случайные с перевесом в пользу дальних
            //    от уже выбранных. Остальную галактику населяют малые империи (EmpireGen).
            emp = new List<Empire>();
            var rels = Px.Shuffle(new List<int> { -60, 5, 15, 25 }, r);
            var homes = new List<Star>();
            var cands = new List<Star>();
            foreach (var s in stars)
                if (!s.core && Px.Len(s.x, s.y) > RING * .8f && Px.Dist(s, hs) > MAJOR_R + 450) cands.Add(s);
            if (cands.Count > 0)
            {
                homes.Add(cands[r.Int(cands.Count)]);
                while (homes.Count < 4)
                {
                    // взвешенный случайный выбор: вес растёт с квадратом расстояния до ближайшей столицы.
                    // Дальние точки вероятнее, но империи не обязательно оказываются на самых концах рукавов
                    var pool = new List<(Star s, float w)>();
                    float total = 0;
                    for (int k = 0; k < 400; k++)
                    {
                        var c = cands[r.Int(cands.Count)];
                        float d = float.MaxValue;
                        foreach (var h in homes) d = Mathf.Min(d, Px.Dist(h, c));
                        if (d < MAJOR_APART) continue; // но не вплотную друг к другу
                        pool.Add((c, d * d));
                        total += d * d;
                    }
                    if (pool.Count == 0) break;
                    float pick = r.F() * total;
                    var chosen = pool[pool.Count - 1].s;
                    foreach (var (c, wgt) in pool) { pick -= wgt; if (pick <= 0) { chosen = c; break; } }
                    homes.Add(chosen);
                }
            }
            for (int ei = 0; ei < homes.Count; ei++)
                emp.Add(new Empire { name = Defs.EmpPrefix[ei] + " " + Px.GenName(r), col = Defs.EmpCols[ei], rel = rels[ei], capital = homes[ei].id, major = true });
            // территории растут от столицы — вдоль рукава и в гущу звёзд; не ближе 450 к Родине
            var index = new StarIndex(stars);
            for (int ei = 0; ei < homes.Count; ei++)
            {
                int e = ei;
                foreach (var s in Grow(homes[ei], Mathf.Max(3, Mathf.RoundToInt(MAJOR_SIZE * empScale)), s => !s.core && s.owner == Owner.NONE && Px.Dist(s, hs) >= 450, index, rot, r))
                {
                    s.owner = s.initOwner = e;
                    s.empHome = s == homes[e];
                }
            }

            // 7. звёздные мосты к ядру — в самом конце списка и со своим генератором случайных чисел,
            //    чтобы остальная галактика (и номера звёзд в старых сохранениях) не изменились
            Bridges(seed, stars, index, rot);
        }

        // ---------- притяжение ядра и звёздные мосты ----------

        public const float PULL_START = 1900; // отсюда (чуть дальше, чем сходятся рукава) дальность прыжка начинает падать
        public const float PULL_FULL = CORE_EMPTY + 10; // у края пустоты вокруг ядра она минимальна
        public const float PULL_MIN = .22f;   // ур.4 — 310 → 68: по обычным звёздам ещё пройти можно, через пустоту — только по мосту
        public const int BRIDGES = 4;
        const float BRIDGE_SECTOR = .35f;     // мост начинается у самой близкой к ядру обычной звезды в секторе ± этого угла
        const float BRIDGE_STEP = .93f;       // шаг моста — доля дальности двигателя ур.4: ур.3 не хватит даже с бонусами
        const float BRIDGE_CLEAR = 20;        // звезду моста не ставим вплотную к обычной
        public const float MAX_RANGE = 310;   // дальность двигателя ур.4

        /// <summary>Множитель дальности прыжка на расстоянии r от ядра: 1 снаружи, PULL_MIN у ядра (плавно, квадратично).</summary>
        public static float Pull(float r)
        {
            float t = Mathf.Clamp01((r - PULL_FULL) / (PULL_START - PULL_FULL));
            return PULL_MIN + (1 - PULL_MIN) * t * t;
        }

        static void Bridges(int seed, List<Star> stars, StarIndex index, float rot)
        {
            var r = new Rng((uint)(seed * 7919 + 1234567));
            var near = new List<Star>();
            int natural = stars.Count;
            var added = new List<Star>();
            bool Clear(float x, float y)
            {
                index.Near(x, y, BRIDGE_CLEAR, near);
                foreach (var o in near) if (!o.core) return false;
                foreach (var o in added) if (Px.Dist(o.x, o.y, x, y) < BRIDGE_CLEAR) return false;
                return true;
            }
            for (int b = 0; b < BRIDGES; b++)
            {
                // начало — самая близкая к ядру обычная звезда в своём секторе: мост только пересекает пустоту вокруг ядра
                float a = rot + b * Px.TAU / BRIDGES + Px.TAU / (BRIDGES * 2) + (r.F() - .5f) * .4f;
                index.Near(0, 0, CORE_EMPTY + 250, near);
                Star cur = null;
                foreach (var s in near)
                {
                    if (s.core || s.id >= natural) continue;
                    float da = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, Mathf.Atan2(s.y, s.x) * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    if (da > BRIDGE_SECTOR) continue;
                    if (cur == null || Px.Len(s.x, s.y) < Px.Len(cur.x, cur.y)) cur = s;
                }
                if (cur == null) continue;
                float twist = (r.F() < .5f ? -1 : 1) * (.15f + r.F() * .2f); // мост слегка закручен
                float x = cur.x, y = cur.y;
                for (int guard = 0; guard < 200; guard++)
                {
                    float d = Px.Len(x, y);
                    float step = MAX_RANGE * Pull(d) * BRIDGE_STEP;
                    if (d <= step) break; // от этой звезды уже долетаешь до ядра
                    float dir = Mathf.Atan2(-y, -x) + twist * Mathf.Clamp01(d / CORE_EMPTY);
                    float nx = 0, ny = 0;
                    bool ok = false;
                    // не вплотную к обычным звёздам: пробуем немного свернуть
                    for (int k = 0; k < 9 && !ok; k++)
                    {
                        float da = (k + 1) / 2 * .14f * (k % 2 == 0 ? 1 : -1);
                        nx = x + Mathf.Cos(dir + da) * step; ny = y + Mathf.Sin(dir + da) * step;
                        ok = Px.Len(nx, ny) < d && Clear(nx, ny);
                    }
                    if (!ok) { nx = x + Mathf.Cos(dir) * step; ny = y + Mathf.Sin(dir) * step; }
                    var s = new Star
                    {
                        id = stars.Count, x = nx, y = ny, name = Px.GenName(r),
                        col = Defs.StarCols[r.Int(Defs.StarCols.Length)], price = 9 + r.Int(14), seed = 1 + r.Int(1 << 30),
                    };
                    stars.Add(s);
                    added.Add(s);
                    x = nx; y = ny;
                }
            }
        }

        // ---------- рост территории империи ----------

        const float GROW_STEP = 110;     // соседние системы, на которые может перекинуться территория
        const float DENS_R = 100;        // радиус подсчёта плотности вокруг звезды

        /// <summary>
        /// Территория империи как случайный рост от столицы: каждый шаг присоединяет одну соседнюю систему.
        /// Шансы выше у систем по направлению рукава (а не поперёк), в гуще звёзд и ближе к столице —
        /// получаются вытянутые вдоль рукавов и неровные формы вместо кругов.
        /// </summary>
        public static List<Star> Grow(Star cap, int size, System.Func<Star, bool> free, StarIndex index, float rot, Rng r)
        {
            var taken = new List<Star> { cap };
            var inSet = new HashSet<int> { cap.id };
            var frontier = new List<(Star s, float w)>();
            var near = new List<Star>();
            var dens = new Dictionary<int, int>();
            float reach = 150 + 80 * Mathf.Sqrt(size); // насколько далеко от столицы территория ещё охотно растёт

            int Density(Star s)
            {
                if (dens.TryGetValue(s.id, out int n)) return n;
                index.Near(s.x, s.y, DENS_R, near);
                return dens[s.id] = near.Count;
            }

            void Expand(Star from)
            {
                // направление рукава в точке from: касательная к логарифмической спирали (вне центрального круга)
                float fr = Px.Len(from.x, from.y);
                Vector2 tangent = Vector2.zero;
                if (fr > RING * 1.6f)
                {
                    var er = new Vector2(from.x, from.y) / fr;
                    var et = new Vector2(-er.y, er.x);
                    tangent = (er * PITCH + et).normalized;
                }
                index.Near(from.x, from.y, GROW_STEP, near);
                var found = new List<Star>(near);
                foreach (var s in found)
                {
                    if (inSet.Contains(s.id) || !free(s)) continue;
                    inSet.Add(s.id);
                    float dirW = 1;
                    if (tangent != Vector2.zero)
                    {
                        var dir = new Vector2(s.x - from.x, s.y - from.y).normalized;
                        float a = Mathf.Abs(Vector2.Dot(dir, tangent));
                        dirW = .15f + 2.5f * a * a * a;          // вдоль рукава — сильно охотнее, чем поперёк
                    }
                    float densW = 1 + Density(s);                 // гуще звёзды — охотнее
                    float dc = Px.Dist(s, cap) / reach;
                    float distW = Mathf.Exp(-dc * dc);            // далеко от столицы — неохотно
                    frontier.Add((s, dirW * densW * distW + 1e-6f));
                }
            }

            Expand(cap);
            while (taken.Count < size && frontier.Count > 0)
            {
                float total = 0;
                foreach (var f in frontier) total += f.w;
                float pick = r.F() * total;
                int idx = frontier.Count - 1;
                for (int i = 0; i < frontier.Count; i++) { pick -= frontier[i].w; if (pick <= 0) { idx = i; break; } }
                var s = frontier[idx].s;
                frontier.RemoveAt(idx);
                if (!free(s)) continue;
                taken.Add(s);
                Expand(s);
            }
            return taken;
        }


        const float MAJOR_R = 900;   // примерный радиус территории большой империи
        const float MAJOR_APART = 2 * MAJOR_R; // столицы не ближе — территории не сливаются
        const int MAJOR_SIZE = 150;  // систем у большой империи при модификаторе ×1

        /// <summary>Превратить уже созданные планеты звезды в колонии её (новой) империи.</summary>
        public static void ApplyEmpire(Star s)
        {
            if (s.Loaded) Convert(s, s.planets, new Rng((uint)(s.seed * 31 + 7)));
        }

        static void Convert(Star s, List<Planet> list, Rng r)
        {
            if (!Owner.IsEmp(s.initOwner) || list.Count == 0) return;
            int ei = s.initOwner, k = 0;
            foreach (var p in list)
                if (!Defs.Types[p.type].Gas && k < 3 && (s.empHome || r.Next() < .7))
                {
                    p.owner = ei; p.atmo = Mathf.Max(p.atmo, 2); p.temp = 0; k++;
                }
            if (k == 0)
            {
                var p = list[0];
                p.type = "rock"; p.size = 7; p.owner = ei; p.atmo = 2; p.temp = 0;
            }
            // изредка у империи есть газовая станция на газовом гиганте
            foreach (var p in list)
                if (Defs.Types[p.type].Gas && r.Next() < EMP_STATION_CHANCE) p.owner = ei;

            // постройки: на главной планете столицы — пять, у остальных 1–3, и чем ближе к столице, тем больше
            float near = Closeness(s);
            bool capDone = false;
            foreach (var p in list)
            {
                if (p.owner != ei) continue;
                if (s.empHome && !capDone && !Defs.Types[p.type].Gas)
                {
                    p.capital = true; p.colonies = Colony.MAX_HOME; capDone = true;
                    continue;
                }
                p.colonies = 1;
                if (Px.Hash(p.seed, 11, s.id) < .12 + .7 * near) p.colonies++;
                if (Px.Hash(p.seed, 12, s.id) < .04 + .5 * near) p.colonies++;
            }
        }

        /// <summary>1 — сама столица, 0 — далёкая окраина империи.</summary>
        static float Closeness(Star s)
        {
            if (s.empHome) return 1;
            var g = Game.G;
            if (g == null || g.emp == null || g.stars == null || s.initOwner >= g.emp.Count) return .4f;
            var e = g.emp[s.initOwner];
            if (e.capital < 0 || e.capital >= g.stars.Count) return .4f;
            float R = e.major ? 1100 : 400; // примерный радиус территории
            return Mathf.Clamp01(1 - Px.Dist(s, g.stars[e.capital]) / R);
        }

        const double EMP_STATION_CHANCE = .07;

        /// <summary>Сетка звёзд для поиска соседей во время игры.</summary>
        public sealed class StarIndex
        {
            const float CELL = 200;
            readonly Dictionary<long, List<Star>> map = new Dictionary<long, List<Star>>();
            static long Key(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;

            public StarIndex(List<Star> stars)
            {
                foreach (var s in stars)
                {
                    long k = Key(Mathf.FloorToInt(s.x / CELL), Mathf.FloorToInt(s.y / CELL));
                    if (!map.TryGetValue(k, out var l)) map[k] = l = new List<Star>();
                    l.Add(s);
                }
            }

            public void Near(float x, float y, float rad, List<Star> outList)
            {
                outList.Clear();
                int x0 = Mathf.FloorToInt((x - rad) / CELL), x1 = Mathf.FloorToInt((x + rad) / CELL);
                int y0 = Mathf.FloorToInt((y - rad) / CELL), y1 = Mathf.FloorToInt((y + rad) / CELL);
                for (int cx = x0; cx <= x1; cx++)
                    for (int cy = y0; cy <= y1; cy++)
                        if (map.TryGetValue(Key(cx, cy), out var l))
                            foreach (var s in l)
                                if (Px.Dist(s.x, s.y, x, y) <= rad) outList.Add(s);
            }
        }

        static int[] Components(List<Vector2> pts, Grid g, out int count)
        {
            var comp = new int[pts.Count];
            for (int i = 0; i < comp.Length; i++) comp[i] = -1;
            var stack = new Stack<int>();
            var near = new List<int>();
            count = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                if (comp[i] >= 0) continue;
                comp[i] = count;
                stack.Push(i);
                while (stack.Count > 0)
                {
                    int v = stack.Pop();
                    g.Near(pts[v], JUMP, near);
                    foreach (var j in near) if (comp[j] < 0) { comp[j] = count; stack.Push(j); }
                }
                count++;
            }
            return comp;
        }

        // ---------- планеты: создаются при первом обращении к звезде ----------

        static string PickType(Rng r)
        {
            double x = r.Next();
            foreach (var (t, w) in Defs.TW)
                if ((x -= w) < 0) return t;
            return "rock";
        }

        static Planet MkPlanet(Rng r, int i, string sname, string type)
        {
            var T = Defs.Types[type];
            int orbit = 55 + i * 34 + r.Int(10);
            int temp = T.T;
            if (type == "desert") temp += r.Int(2);
            if (type == "ice") temp -= r.Int(2);
            return new Planet
            {
                name = sname + " " + Defs.Roman[i], type = type, orbit = orbit,
                size = T.Gas ? 11 + r.Int(5) : 5 + r.Int(5),
                temp = temp, atmo = T.A, ang = r.F() * Px.TAU, spd = 2.4f / orbit,
                seed = 1 + r.Int(1000000), owner = Owner.NONE, art = r.Next() < .3,
            };
        }

        public static List<Planet> Planets(Star s)
        {
            var list = new List<Planet>();
            if (s.core) return list;
            var r = new Rng((uint)s.seed);

            if (s.initOwner == Owner.PLAYER) // родина игрока
            {
                var p0 = MkPlanet(r, 0, s.name, "desert"); p0.temp = 1; p0.atmo = 1; p0.size = 7; p0.art = false;
                var p1 = MkPlanet(r, 1, s.name, "garden"); p1.owner = Owner.PLAYER; p1.home = true; p1.colonies = 1; p1.size = 8; p1.name = "Родина";
                var p2 = MkPlanet(r, 2, s.name, "rock"); p2.size = 6;
                var p3 = MkPlanet(r, 3, s.name, "gas"); p3.size = 13;
                list.AddRange(new[] { p0, p1, p2, p3 });
                return list;
            }

            int n = 1 + r.Int(5);
            for (int i = 0; i < n; i++) list.Add(MkPlanet(r, i, s.name, PickType(r)));

            Convert(s, list, r); // колонии империи
            return list;
        }
    }
}

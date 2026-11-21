using System;
using System.Collections.Generic;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    /// <summary>Корабль в бою. side 0 — игрок и его защитники, side 1 — враги (пираты, империя).</summary>
    public sealed class BShip
    {
        public int side;
        public bool pirate;
        public int ei = -1;    // империя корабля (спрайт, отношения)
        public int home = -1;  // защитник планеты home; -1 — охрана системы или налётчик
        public float x, y, vx, vy, a, hp, cd, orb;
        public bool Def => home >= 0;
        public bool back;      // бой окончен: защитник возвращается на планету
        public int cls = ShipClass.FRIGATE; // класс корабля: истребитель, фрегат, канонерка, бомбардировщик, дрон
        public bool drone;     // союзный дрон «Роения»
        public bool patrol;    // патрульный дрон системы (куплен и поставлен игроком)
        public bool cargo;     // грузовой корабль конвоя
        public bool wreck;     // подбитый корабль, которого спасают
        public float spawnT;   // пиратская база: таймер выпуска истребителей
        public ShipClass C => ShipClass.All[cls];
    }

    public sealed class BShot
    {
        public const int NORMAL = 0, BOMB = 1, MISSILE = 2, BREAKER = 3, AUTO = 4, HEAVY = 5; // HEAVY — снаряд бомбардировщика
        public float x, y, vx, vy, life, d;
        public float sd;    // урон по щиту планеты (выстрелы игрока)
        public int side;
        public int kind;    // вид снаряда: обычный, бомба, ракета, пробиватель, автобластер
        public bool player; // выстрел корабля игрока
    }

    /// <summary>
    /// Классы боевых кораблей: размер и соотношение характеристик. Истребители быстрые и хрупкие, фрегаты — середина,
    /// канонерки медленные и крепкие и бьют веером, бомбардировщики стреляют редко, медленно и очень больно
    /// (по щитам планет — особенно).
    /// </summary>
    public sealed class ShipClass
    {
        public const int FIGHTER = 0, FRIGATE = 1, GUNSHIP = 2, BOMBER = 3, DRONE = 4, FREIGHTER = 5, BASE = 6, PATROL = 7;
        public string N;
        public float Hp, Speed, CdMin, CdMax, Dmg, ShotSpeed, Scale, Hit, Spread;
        public int Shots, Credits;

        public static readonly ShipClass[] All =
        {
            new ShipClass { N = "Истребитель", Hp = 20, Speed = 2.3f, CdMin = .8f, CdMax = 1.4f, Dmg = 6, ShotSpeed = 2.8f, Scale = .75f, Hit = 4.5f, Shots = 1, Credits = 25 },
            new ShipClass { N = "Фрегат", Hp = 40, Speed = 1.7f, CdMin = 1.3f, CdMax = 2.7f, Dmg = 10, ShotSpeed = 2.4f, Scale = 1, Hit = 6, Shots = 1, Credits = 50 },
            new ShipClass { N = "Канонерка", Hp = 120, Speed = 1.05f, CdMin = 2.2f, CdMax = 3.2f, Dmg = 8, ShotSpeed = 2.2f, Scale = 1.5f, Hit = 9, Shots = 3, Spread = .22f, Credits = 120 },
            new ShipClass { N = "Бомбардировщик", Hp = 60, Speed = 1.3f, CdMin = 3, CdMax = 4, Dmg = 24, ShotSpeed = 1.7f, Scale = 1.25f, Hit = 7.5f, Shots = 1, Credits = 80 },
            new ShipClass { N = "Дрон", Hp = 20, Speed = 2.4f, CdMin = .7f, CdMax = 1.1f, Dmg = 7, ShotSpeed = 2.8f, Scale = .55f, Hit = 4, Shots = 1, Credits = 0 },
            new ShipClass { N = "Грузовик", Hp = 70, Speed = .35f, CdMin = 99, CdMax = 99, Dmg = 0, ShotSpeed = 1, Scale = 1.3f, Hit = 8, Shots = 0, Credits = 0 },
            new ShipClass { N = "Пиратская база", Hp = 700, Speed = 0, CdMin = 1.4f, CdMax = 2f, Dmg = 9, ShotSpeed = 2.3f, Scale = 2.6f, Hit = 18, Shots = 4, Spread = .35f, Credits = 400 },
            new ShipClass { N = "Патрульный дрон", Hp = 60, Speed = 1.9f, CdMin = .9f, CdMax = 1.5f, Dmg = 7, ShotSpeed = 2.8f, Scale = .8f, Hit = 5, Shots = 1, Credits = 0 },
        };

        /// <summary>Случайный класс: у пиратов больше истребителей и нет канонерок.</summary>
        public static int Roll(bool pirate, float r)
        {
            if (pirate) return r < .5f ? FIGHTER : r < .85f ? FRIGATE : BOMBER;
            return r < .35f ? FIGHTER : r < .7f ? FRIGATE : r < .85f ? GUNSHIP : BOMBER;
        }
    }

    /// <summary>Мгновенный эффект оружия: луч лазера, молния, взрыв бомбы.</summary>
    public sealed class BFx
    {
        public const int BEAM = 0, BOLT = 1, BLAST = 2;
        public int kind;
        public float life, max, r;
        public readonly List<Vector2> pts = new List<Vector2>();
    }

    /// <summary>Сражение в одной системе. Нападение (raid) идёт и без игрока; бой с охраной — только при нём.</summary>
    public sealed class Battle
    {
        public readonly int star;
        public Raid raid;
        public readonly List<BShip> ships = new List<BShip>();
        public readonly List<BShot> shots = new List<BShot>();
        public readonly List<BFx> fx = new List<BFx>();
        public Vector2 convoyTo; // куда идёт конвой
        public readonly Dictionary<int, float> flash = new Dictionary<int, float>(); // планета → вспышка щита
        public bool done;
        public bool playerHere;
        public bool after;     // нападение отбито: защитники возвращаются на планеты
        public bool joined;    // игрок вступил в чужой бой (стрелял по нападающим)
        public int helpKills;  // сколько нападающих сбил игрок
        public bool shieldLost, buildingLost; // при нападении на игрока пробит щит / потеряна постройка (для испытаний)
        public bool patrolOut; // патрульные дроны системы уже вступили в этот бой
        public bool chosen;    // в чужой войне игрок уже выбрал сторону (окно при входе в систему)
        public bool withAtt;   // игрок на стороне нападающих: его враги — обороняющиеся (сторона 0)

        public Battle(int star) { this.star = star; }
        public Star S => G.stars[star];

        /// <summary>Жива ли охрана системы — пока она есть, по планетам врага стрелять бесполезно.</summary>
        public bool Guards
        {
            get
            {
                if (raid != null) return false;
                foreach (var e in ships) if (e.side == 1 && !e.Def && e.hp > 0) return true;
                return false;
            }
        }

        public int Foes { get { int n = 0; foreach (var e in ships) if (e.side == 1 && e.hp > 0) n++; return n; } }
    }

    /// <summary>
    /// Боевые действия. У планеты столько щитов, сколько построек; щиты ломаются по очереди
    /// от 3, 5, 10, 20 и 50 выстрелов лазера первого уровня. После первого попадания в каждый щит
    /// с планеты вылетает столько же защитников. Пробиты все щиты — планета сдаётся (пираты же
    /// разрушают по постройке за щит). Сдались все планеты — система переходит к захватчику.
    /// </summary>
    public static class Battles
    {
        public static readonly int[] HITS = { 3, 5, 10, 20, 50 };
        public const float SHOT = 10;            // урон выстрела лазера 1 уровня
        public const float PLAYER_HIT = 7;       // урон по кораблю игрока
        // налётчики бьют по щитам слабее лазера игрока и реже — колония держится, пока вы летите на помощь
        public static float SIEGE = .25f, SIEGE_CD = 2.5f;
        public static float WAR_SIEGE = .35f; // множитель осады, когда империя нападает на империю
        const float SHOT_SPEED = 2.4f, SHOT_LIFE = 2.6f;

        public static readonly List<Battle> all = new List<Battle>();
        static readonly Rng rng = new Rng((uint)Environment.TickCount);
        static float Rnd() => rng.F();
        static GameState forGame;

        // связь с интерфейсом и сценой
        public static Action<string> Toast;
        public static Action<int, int> Capitulated;                 // система врага сдалась игроку (звезда, империя)
        public static Action<int, float, float, int, Color32[]> Fx;  // взрыв: звезда, x, y, частиц, цвета
        public static Action<float, float> PlayerHit;                // попадание в корабль игрока

        static readonly Color32 cW = Px.Hex("#ffffff"), cGold = Px.Hex("#ffcf4a"), cOr = Px.Hex("#ff7a2a"),
            cCyan = Px.Hex("#6fe3ff"), cShield = Px.Hex("#7fe8ff"), cBeam = Px.Hex("#ff6af0"), cBolt = Px.Hex("#b8d8ff"), cPir = Px.Hex("#d8344a"), cMy = Px.Hex("#cfd8e6");

        // ---------- щиты ----------

        public static int ShieldsOf(Planet p) => p.shields > 0 ? p.shields : Mathf.Max(1, Colony.Buildings(p));
        public static int ShieldsLeft(Planet p) => p.surrendered ? 0 : Mathf.Max(0, ShieldsOf(p) - p.broken);
        public static float ShieldHp(int level) => HITS[Mathf.Clamp(level, 0, HITS.Length - 1)] * SHOT;
        /// <summary>Доля прочности текущего щита (1 — целый).</summary>
        public static float ShieldFrac(Planet p) => 1 - Mathf.Clamp01(p.shieldDmg / ShieldHp(p.broken));
        /// <summary>У планеты есть оборона: колония игрока или планета империи.</summary>
        public static bool Defended(Planet p) => p.owner == Owner.PLAYER ? Colony.Buildings(p) > 0 : Owner.IsEmp(p.owner);
        public static bool Hostile(int ei) => ei >= 0 && ei < G.emp.Count && G.emp[ei].rel < -30;

        public static void ResetShield(Planet p)
        {
            p.shields = 0; p.broken = 0; p.shieldDmg = 0; p.defOut = false; p.surrendered = false;
        }

        static void Rel(int ei, int d)
        {
            if (ei < 0 || ei >= G.emp.Count) return;
            var e = G.emp[ei];
            bool was = e.rel < -30;
            e.rel = Mathf.Clamp(e.rel + d, -100, 100);
            if (e.fallen) return;
            if (!was && e.rel < -30) Notes.Add(e.name + " объявляет вам войну", Notes.ATTACK);
            else if (was && e.rel >= -30) Notes.Add(e.name + " прекращает войну с вами", Notes.INFO);
        }

        static string Att(Raid r) => r.att == Raid.PIRATES ? "Пираты" : G.emp[r.att].name;

        // ---------- сражения ----------

        static void Sync()
        {
            if (forGame == G) return;
            forGame = G;
            all.Clear();
            // после загрузки нападения продолжаются: налётчики снова у края системы
            foreach (var r in G.raids)
            {
                if (r.star < 0 || r.star >= G.stars.Count) continue;
                var b = Get(r.star);
                b.raid = r;
                SpawnRaiders(b, r.n >= 0 ? r.n : DefaultRaid(r.att));
                // защитники не сохраняются — при следующем попадании вылетят снова
                foreach (var p in b.S.planets) p.defOut = false;
            }
            G.raids.RemoveAll(r => Of(r.star)?.raid != r);
        }

        public static Battle Of(int star)
        {
            Sync();
            foreach (var b in all) if (b.star == star && !b.done) return b;
            return null;
        }

        public static Battle Get(int star)
        {
            var b = Of(star);
            if (b == null) { b = new Battle(star); all.Add(b); }
            return b;
        }

        public static Raid RaidAt(int star) => Of(star)?.raid;

        /// <summary>Сторона врагов игрока в этом бою: обычно нападающие (1), а если он помогает нападающим — обороняющиеся (0).</summary>
        public static int Foe(Battle b) => b != null && b.withAtt ? 0 : 1;
        /// <summary>Сторона самого игрока (его выстрелов).</summary>
        public static int Mine(Battle b) => 1 - Foe(b);

        /// <summary>Перед сохранением: сколько налётчиков ещё живо.</summary>
        public static void Store()
        {
            foreach (var b in all) if (b.raid != null) b.raid.n = b.Foes;
        }

        public static int DefaultRaid(int att) => att == Raid.PIRATES ? 2 + P.lv.arm : 3 + P.lv.las;

        /// <summary>Корабль класса cls; у пиратов прочность на четверть ниже.</summary>
        static BShip Mk(int side, bool pirate, int ei, float x, float y, int cls, bool weak = false) => new BShip
        {
            side = side, pirate = pirate, ei = ei, x = x, y = y, cls = cls,
            hp = ShipClass.All[cls].Hp * (pirate || weak ? .75f : 1),
            cd = 1.5f + Rnd() * 2, orb = Rnd() < .5f ? 1 : -1,
        };

        /// <summary>Конвой: грузовики империи идут от from к to.</summary>
        public static void SpawnConvoy(Battle b, Vector2 from, Vector2 to, int ei, int n)
        {
            b.convoyTo = to;
            for (int k = 0; k < n; k++)
            {
                var e = Mk(0, false, ei, from.x + (k - 1) * 14, from.y + 12, ShipClass.FREIGHTER);
                e.cargo = true;
                b.ships.Add(e);
            }
        }

        /// <summary>Пираты (не налёт, а засада): нападают на всех, кто рядом.</summary>
        public static void SpawnPirates(Battle b, Vector2 at, int n)
        {
            for (int k = 0; k < n; k++)
            {
                float a = Rnd() * Px.TAU, d = 20 + Rnd() * 40;
                b.ships.Add(Mk(1, true, -1, at.x + Mathf.Cos(a) * d, at.y + Mathf.Sin(a) * d, ShipClass.Roll(true, Rnd())));
            }
        }

        /// <summary>Подбитый корабль империи: стоит на месте, его нужно прикрыть, пока берут на буксир.</summary>
        public static void SpawnWreck(Battle b, Vector2 at, int ei)
        {
            var e = Mk(0, false, ei, at.x, at.y, ShipClass.FRIGATE);
            e.wreck = true; e.hp = 60;
            b.ships.Add(e);
        }

        /// <summary>Пиратская база: стоит на месте, крепкая, бьёт веером и выпускает истребители.</summary>
        public static void SpawnBase(Battle b, Vector2 at)
        {
            var e = Mk(1, true, -1, at.x, at.y, ShipClass.BASE);
            e.hp = ShipClass.All[ShipClass.BASE].Hp;
            b.ships.Add(e);
        }

        /// <summary>«Роение»: союзные дроны вокруг корабля игрока.</summary>
        public static void SpawnDrones(Battle b, Vector2 at, int n)
        {
            for (int k = 0; k < n; k++)
            {
                float a = (float)k / n * Px.TAU;
                var e = Mk(0, false, -1, at.x + Mathf.Cos(a) * 14, at.y + Mathf.Sin(a) * 14, ShipClass.DRONE);
                e.drone = true; e.cd = .3f + Rnd() * .5f;
                b.ships.Add(e);
            }
        }

        /// <summary>Охрана вражеской системы нападает на игрока, как только он прилетел.</summary>
        public static void SpawnGuards(Battle b, Vector2 at, int ei, int n)
        {
            for (int k = 0; k < n; k++)
            {
                float a = Rnd() * Px.TAU, d = 170 + Rnd() * 60;
                b.ships.Add(Mk(1, false, ei, at.x + Mathf.Cos(a) * d, at.y + Mathf.Sin(a) * d, ShipClass.Roll(false, Rnd())));
            }
        }

        static void SpawnRaiders(Battle b, int n)
        {
            var r = b.raid;
            var s = b.S;
            float a0 = Rnd() * Px.TAU, R = SysR(s) + 130;
            for (int k = 0; k < n; k++)
            {
                float a = a0 + (Rnd() - .5f) * .6f, d = R + Rnd() * 40;
                bool pir = r.att == Raid.PIRATES;
                b.ships.Add(Mk(1, pir, pir ? -1 : r.att, Mathf.Cos(a) * d, Mathf.Sin(a) * d, ShipClass.Roll(pir, Rnd())));
            }
        }

        // «цена» защитника в очках силы: истребитель, фрегат, канонерка, бомбардировщик
        static readonly float[] DEF_COST = { 1, 1.5f, 4, 2.5f, 1, 99, 99 };

        static void SpawnDefenders(Battle b, int pi, int n)
        {
            var p = b.S.planets[pi];
            var pp = PPos(p);
            bool mine = p.owner == Owner.PLAYER;
            // при нападении защитники на стороне 0 (вместе с игроком), при штурме игрока — на стороне 1
            int side = b.raid != null ? 0 : 1;
            if (mine) n += Faith.ExtraDefenders; // Путь Пламени: на каждый щит — защитник больше
            else if (Owner.IsEmp(p.owner))
            {
                // поручения: святыня (+1 всегда) и спасённый экипаж или посылка (+N при следующей обороне)
                var pe = G.emp[p.owner];
                n += pe.defBonus + pe.defOnce;
                pe.defOnce = 0;
            }
            // защитники разных классов. Сила прежняя: n «мест» по 1,2 очка (как раньше — истребители с фрегатами),
            // а тяжёлые корабли занимают больше очков — их меньше
            var classes = new List<int>();
            float budget = n * 1.2f;
            while (budget >= 1)
            {
                int cl = ShipClass.Roll(false, Rnd());
                float cost = DEF_COST[cl];
                if (cost > budget) { cl = budget >= DEF_COST[ShipClass.FRIGATE] && Rnd() < .5f ? ShipClass.FRIGATE : ShipClass.FIGHTER; cost = DEF_COST[cl]; }
                classes.Add(cl);
                budget -= cost;
            }
            n = classes.Count;
            for (int k = 0; k < n; k++)
            {
                float a = (float)k / n * Px.TAU + Rnd() * .3f;
                var e = Mk(side, false, mine ? -1 : p.owner, pp.x + Mathf.Cos(a) * (p.size + 4), pp.y + Mathf.Sin(a) * (p.size + 4), classes[k]);
                e.home = pi;
                e.vx = Mathf.Cos(a) * 1.2f; e.vy = Mathf.Sin(a) * 1.2f;
                e.cd = .6f + Rnd() * 1.2f;
                b.ships.Add(e);
            }
        }

        /// <summary>Начать нападение на колонию игрока: бой идёт сразу, даже если игрока там нет.</summary>
        public static void StartRaid(int star, int planet, int att, int n, int def = Owner.PLAYER)
        {
            var r = new Raid { star = star, planet = planet, att = att, n = n, def = def };
            if (def == Owner.PLAYER && att >= 0 && att < G.emp.Count) { G.emp[att].met = true; G.emp[att].attackedAt = G.time; } // напавшую империю вы теперь знаете
            G.raids.Add(r);
            var b = Get(star);
            b.raid = r;
            SpawnRaiders(b, n);
        }

        public static void AddPlayerShot(int star, float x, float y, float vx, float vy, float d) =>
            AddPlayerShot(star, x, y, vx, vy, d, d, BShot.NORMAL, .9f);

        public static void AddPlayerShot(int star, float x, float y, float vx, float vy, float d, float sd, int kind, float life) =>
            Get(star).shots.Add(new BShot { x = x, y = y, vx = vx, vy = vy, life = life, d = d, sd = sd, kind = kind, side = Mine(Get(star)), player = true });

        public const float BOMB_R = 30;
        public const float DEF_LEASH = 60; // насколько защитник колонии игрока отходит от края своей планеты

        static BFx AddFx(Battle b, int kind, float life)
        {
            var f = new BFx { kind = kind, life = life, max = life };
            b.fx.Add(f);
            return f;
        }

        /// <summary>Луч лазера: пробивает все вражеские корабли на линии, обрывается о первую планету.</summary>
        /// <summary>Длина луча до первой планеты на пути (или len); planet — её индекс или -1.</summary>
        public static float BeamEnd(Star s, float x, float y, float a, float len, out int planet)
        {
            float c = Mathf.Cos(a), sn = Mathf.Sin(a), end = len;
            planet = -1;
            for (int pi = 0; pi < s.planets.Count; pi++)
            {
                var pp = PPos(s.planets[pi]);
                float vx = pp.x - x, vy = pp.y - y, t = vx * c + vy * sn, r = s.planets[pi].size + 2;
                float perp2 = vx * vx + vy * vy - t * t;
                if (t <= 0 || perp2 >= r * r) continue;
                float th = Mathf.Max(0, t - Mathf.Sqrt(r * r - perp2));
                if (th < end) { end = th; planet = pi; }
            }
            return end;
        }

        public const float BEAM_W = 3; // полуширина луча для попадания: узкий — им трудно целиться

        /// <summary>Один «тик» непрерывного луча: урон всем вражеским кораблям на линии и щиту планеты, о которую он обрывается.</summary>
        public static void Beam(int star, float x, float y, float a, float len, float d, float sd)
        {
            var b = Get(star);
            float c = Mathf.Cos(a), sn = Mathf.Sin(a), end = BeamEnd(b.S, x, y, a, len, out int hitP);
            foreach (var e in b.ships)
            {
                if (e.side != Foe(b) || e.hp <= 0) continue;
                float vx = e.x - x, vy = e.y - y, t = vx * c + vy * sn;
                if (t <= 0 || t > end || Mathf.Abs(vx * sn - vy * c) > BEAM_W * e.C.Scale) continue;
                b.joined = true;
                e.hp -= d;
                Fx?.Invoke(b.star, e.x, e.y, 2, new[] { cW, cBeam });
                if (e.hp <= 0) Kill(b, e, true);
            }
            if (hitP >= 0 && !b.done && CanHit(b, hitP, Mine(b))) HitPlanet(b, hitP, sd, true);
        }

        public const int BOLT_JUMPS = 5;
        public const float BOLT_JUMP = 70;

        /// <summary>Молния: бьёт врага у прицела и перескакивает по цепочке на соседей, слабея с каждым прыжком.</summary>
        public static void Bolt(int star, float x, float y, Vector2 aim, float range, float d)
        {
            var b = Get(star);
            var s = b.S;
            BShip first = null;
            float bd = float.MaxValue;
            foreach (var e in b.ships)
            {
                if (e.side != Foe(b) || e.hp <= 0 || Px.Dist(e.x, e.y, x, y) > range) continue;
                float da = Px.Dist(e.x, e.y, aim.x, aim.y);
                if (da < bd) { bd = da; first = e; }
            }
            var f = AddFx(b, BFx.BOLT, .25f);
            f.pts.Add(new Vector2(x, y));
            if (first == null)
            {
                // кораблей рядом нет — разряд в щит планеты у прицела или в пустоту
                int best = -1; bd = float.MaxValue;
                for (int pi = 0; pi < s.planets.Count; pi++)
                {
                    if (!CanHit(b, pi, Mine(b))) continue;
                    var pp = PPos(s.planets[pi]);
                    if (Px.Dist(pp.x, pp.y, x, y) > range + s.planets[pi].size) continue;
                    float da = Px.Dist(pp.x, pp.y, aim.x, aim.y);
                    if (da < bd) { bd = da; best = pi; }
                }
                if (best >= 0)
                {
                    var pp = PPos(s.planets[best]);
                    float dx = x - pp.x, dy = y - pp.y, l = Mathf.Max(1, Px.Len(dx, dy)), r = s.planets[best].size + 3;
                    f.pts.Add(new Vector2(pp.x + dx / l * r, pp.y + dy / l * r));
                    HitPlanet(b, best, d, true);
                }
                else
                {
                    float dx = aim.x - x, dy = aim.y - y, l = Mathf.Max(1, Px.Len(dx, dy)), r = Mathf.Min(l, 50);
                    f.pts.Add(new Vector2(x + dx / l * r, y + dy / l * r));
                }
                return;
            }
            var hit = new HashSet<BShip>();
            var cur = first;
            float dmg = d;
            for (int k = 0; k < BOLT_JUMPS && cur != null; k++)
            {
                hit.Add(cur);
                f.pts.Add(new Vector2(cur.x, cur.y));
                b.joined = true;
                cur.hp -= dmg;
                Fx?.Invoke(b.star, cur.x, cur.y, 5, new[] { cW, cBolt });
                if (cur.hp <= 0) Kill(b, cur, true);
                dmg *= .8f;
                BShip next = null; float nd = BOLT_JUMP;
                foreach (var e in b.ships)
                {
                    if (e.side != Foe(b) || e.hp <= 0 || hit.Contains(e)) continue;
                    float dd = Px.Dist(e.x, e.y, cur.x, cur.y);
                    if (dd < nd) { nd = dd; next = e; }
                }
                cur = next;
            }
        }

        /// <summary>Взрыв бомбы: урон всем вражеским кораблям в радиусе, слабее к краю.</summary>
        static void Explode(Battle b, BShot sh)
        {
            foreach (var e in b.ships)
            {
                if (e.side == sh.side || e.hp <= 0) continue;
                float dd = Px.Dist(e.x, e.y, sh.x, sh.y);
                if (dd >= BOMB_R) continue;
                if (sh.player) b.joined = true;
                e.hp -= sh.d * (1 - .5f * dd / BOMB_R);
                if (e.hp <= 0) Kill(b, e, sh.player);
            }
            Fx?.Invoke(b.star, sh.x, sh.y, 30, new[] { cW, cGold, cOr, cOr });
            var f = AddFx(b, BFx.BLAST, .3f);
            f.r = BOMB_R;
            f.pts.Add(new Vector2(sh.x, sh.y));
        }

        /// <summary>Самонаведение ракеты: плавно доворачивает на ближайший вражеский корабль.</summary>
        static void Home(Battle b, BShot sh, float f)
        {
            BShip t = null; float bd = 240;
            foreach (var e in b.ships)
            {
                if (e.side == sh.side || e.hp <= 0) continue;
                float dd = Px.Dist(e.x, e.y, sh.x, sh.y);
                if (dd < bd) { bd = dd; t = e; }
            }
            if (t == null) return;
            float sp = Px.Len(sh.vx, sh.vy), cur = Mathf.Atan2(sh.vy, sh.vx), want = Mathf.Atan2(t.y - sh.y, t.x - sh.x);
            float diff = Mathf.DeltaAngle(cur * Mathf.Rad2Deg, want * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            cur += Mathf.Clamp(diff, -.08f * f, .08f * f);
            sh.vx = Mathf.Cos(cur) * sp; sh.vy = Mathf.Sin(cur) * sp;
        }

        /// <summary>Игрок улетел: бой с охраной прекращается, несдавшиеся планеты восстанавливают щиты.</summary>
        public static void Leave(int star)
        {
            if (star < 0 || star >= G.stars.Count) return;
            var b = Of(star);
            if (b != null && b.raid != null) return;
            if (b != null) { b.done = true; all.Remove(b); }
            var s = G.stars[star];
            if (s.Loaded)
                foreach (var p in s.planets) if (!p.surrendered && p.owner != Owner.PLAYER) ResetShield(p);
        }

        /// <summary>
        /// Шаг всех сражений. cur — звезда, где сейчас корабль игрока (-1 — он не в системе),
        /// hittable — по нему можно попасть.
        /// </summary>
        public static void Tick(float dt, int cur, ShipState player, bool hittable)
        {
            Sync();
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var b = all[i];
                if (b.raid == null && b.star != P.cur) { Leave(b.star); continue; }
                b.playerHere = b.star == cur;
                if (b.raid == null && !b.playerHere) continue; // бой с охраной ждёт игрока
                Step(b, dt, b.playerHere ? player : null, hittable);
            }
            all.RemoveAll(b => b.done);
        }

        // цель для ИИ: корабль, игрок или планета
        struct Tg { public float x, y, vx, vy; public bool ok, planet; public float size; }

        static Tg Nearest(Battle b, BShip e, ShipState pl, float fromX, float fromY, float maxD)
        {
            var t = new Tg();
            float bd = maxD;
            foreach (var o in b.ships)
            {
                if (o.side == e.side || o.hp <= 0) continue;
                float d = Px.Dist(o.x, o.y, fromX, fromY);
                if (d < bd) { bd = d; t = new Tg { x = o.x, y = o.y, vx = o.vx, vy = o.vy, ok = true }; }
            }
            if (e.side == Foe(b) && pl != null && VsPlayer(b))
            {
                float d = Px.Dist(pl.x, pl.y, fromX, fromY);
                if (d < bd) t = new Tg { x = pl.x, y = pl.y, vx = pl.vx, vy = pl.vy, ok = true };
            }
            return t;
        }

        static readonly List<BShip> spawnLater = new List<BShip>();

        /// <summary>k-й патрульный дрон системы вступает в бой — с того места, где он сейчас облетает систему.</summary>
        public static void SpawnPatrol(Battle b, int k)
        {
            var at = Patrols.Pos(b.S, k, G.time, out float ang);
            var e = Mk(0, false, -1, at.x, at.y, ShipClass.PATROL);
            e.patrol = true;
            e.a = ang;
            e.vx = Mathf.Cos(ang) * .5f; e.vy = Mathf.Sin(ang) * .5f;
            e.cd = .5f + Rnd();
            b.ships.Add(e);
        }

        static void Step(Battle b, float dt, ShipState pl, bool hittable)
        {
            float f = dt * 60;
            var s = b.S;
            // враги в системе — патрульные дроны идут в бой
            if (!b.patrolOut && b.Foes > 0)
            {
                b.patrolOut = true;
                int pn = Patrols.Count(b.star);
                for (int k = 0; k < pn; k++) SpawnPatrol(b, k);
            }

            foreach (var e in b.ships)
            {
                if (e.back) { Return(b, e, f); continue; }
                if (e.drone && !Faith.Active(Faith.SWARM)) { e.hp = 0; continue; } // «Роение» кончилось
                if (e.wreck) { e.vx = e.vy = 0; e.a += .2f * dt; continue; } // подбитый корабль медленно вращается
                if (e.cargo)
                {
                    // грузовик идёт к краю системы; дошёл — конвой засчитан
                    float cx = b.convoyTo.x - e.x, cy = b.convoyTo.y - e.y, cd = Mathf.Max(.01f, Px.Len(cx, cy));
                    if (cd < 10) { e.hp = 0; Quests.OnCargoArrived(b); continue; }
                    e.vx = cx / cd * e.C.Speed; e.vy = cy / cd * e.C.Speed;
                    e.x += e.vx * f; e.y += e.vy * f;
                    e.a = Mathf.Atan2(cy, cx);
                    continue;
                }
                if (e.cls == ShipClass.BASE)
                {
                    // база выпускает истребители, пока их меньше четырёх
                    e.spawnT += dt;
                    if (e.spawnT >= 20)
                    {
                        e.spawnT = 0;
                        int alive = 0;
                        foreach (var o in b.ships) if (o.side == 1 && o.cls != ShipClass.BASE && o.hp > 0) alive++;
                        if (alive < 4) spawnLater.Add(Mk(1, true, -1, e.x + 20, e.y, ShipClass.FIGHTER));
                    }
                }
                Tg t;
                float near = 50, far = 90;
                if (e.drone)
                {
                    // дрон бьёт ближайшего врага, а если врагов нет — кружит у корабля игрока
                    t = Nearest(b, e, pl, e.x, e.y, 260);
                    if (!t.ok && pl != null) { t = new Tg { x = pl.x, y = pl.y, ok = true, planet = true }; near = 16; far = 30; }
                }
                else if (e.patrol)
                {
                    // патрульный дрон гонится за любым врагом в системе, а когда их нет — кружит по системе
                    t = Nearest(b, e, pl, e.x, e.y, 1e9f);
                    if (!t.ok) { float r0 = SysR(s) * .5f; t = new Tg { ok = true, planet = true }; near = r0 - 10; far = r0 + 10; }
                }
                else if (e.Def)
                {
                    var hp = PPos(s.planets[e.home]);
                    t = Nearest(b, e, pl, hp.x, hp.y, 230);
                    if (!t.ok)
                    {
                        // врагов рядом нет — кружат у своей планеты
                        float r0 = s.planets[e.home].size + 22;
                        t = new Tg { x = hp.x, y = hp.y, ok = true, planet = true };
                        near = r0 - 6; far = r0 + 6;
                    }
                }
                else if (b.raid != null)
                {
                    t = Nearest(b, e, pl, e.x, e.y, 140);
                    if (!t.ok && b.raid.planet < s.planets.Count)
                    {
                        var p = s.planets[b.raid.planet];
                        var pp = PPos(p);
                        t = new Tg { x = pp.x, y = pp.y, ok = true, planet = true, size = p.size };
                        near = p.size + 30; far = p.size + 60;
                    }
                }
                else t = Nearest(b, e, pl, e.x, e.y, 1e9f); // охрана преследует игрока

                if (!t.ok) { e.vx *= .95f; e.vy *= .95f; continue; }
                float dx = t.x - e.x, dy = t.y - e.y, dd = Px.Len(dx, dy);
                if (dd == 0) dd = 1;
                float ux = dx / dd, uy = dy / dd, tx = 0, ty = 0;
                if (dd > far) { tx = ux; ty = uy; }
                else if (dd < near) { tx = -ux; ty = -uy; }
                tx += -uy * e.orb * .6f; ty += ux * e.orb * .6f;
                if (e.Def && e.side == 0)
                {
                    // защитники колоний игрока держатся у своей планеты: стреляют, но не гонятся далеко
                    var hp = PPos(s.planets[e.home]);
                    float hx = hp.x - e.x, hy = hp.y - e.y, hd = Mathf.Max(.01f, Px.Len(hx, hy));
                    float over = hd - (s.planets[e.home].size + DEF_LEASH);
                    if (over > 0)
                    {
                        float k = Mathf.Clamp01(over / 20); // чем дальше за поводком, тем сильнее тянет назад
                        tx = Mathf.Lerp(tx, hx / hd * 1.5f, k); ty = Mathf.Lerp(ty, hy / hd * 1.5f, k);
                    }
                }
                e.vx += tx * .05f * f; e.vy += ty * .05f * f;
                float fr = Mathf.Pow(.97f, f);
                e.vx *= fr; e.vy *= fr;
                float sp = Px.Len(e.vx, e.vy), mx = e.C.Speed + (e.Def ? .2f : 0);
                if (sp > mx) { e.vx *= mx / sp; e.vy *= mx / sp; }
                e.x += e.vx * f; e.y += e.vy * f;
                e.a = Mathf.Atan2(dy, dx);
                e.cd -= dt;
                // защитник у своей планеты не стреляет по пустому месту
                bool shoot = !((e.Def || e.drone || e.patrol) && t.planet) && !(e.side == 1 && Faith.Serene && b.star == P.cur);
                if (shoot && e.cd <= 0 && dd < 220)
                {
                    var C = e.C;
                    float cd = C.CdMin + Rnd() * (C.CdMax - C.CdMin);
                    e.cd = e.Def ? 1f + Rnd() * .8f : t.planet ? Mathf.Max(cd, SIEGE_CD + Rnd() * 1.5f) : cd;
                    float lt = dd / C.ShotSpeed * .6f;
                    float a0 = Mathf.Atan2(t.y + t.vy * lt - e.y, t.x + t.vx * lt - e.x) + (Rnd() - .5f) * .15f;
                    for (int k = 0; k < C.Shots; k++)
                    {
                        // канонерка бьёт веером
                        float a = a0 + (C.Shots > 1 ? (k - (C.Shots - 1) / 2f) * C.Spread : 0);
                        b.shots.Add(new BShot { x = e.x, y = e.y, vx = Mathf.Cos(a) * C.ShotSpeed, vy = Mathf.Sin(a) * C.ShotSpeed, life = SHOT_LIFE * SHOT_SPEED / C.ShotSpeed, d = C.Dmg, side = e.side, kind = e.cls == ShipClass.BOMBER ? BShot.HEAVY : BShot.NORMAL });
                    }
                }
            }

            for (int si = 0; si < b.shots.Count; si++)
            {
                if (b.done) break;
                var sh = b.shots[si];
                if (sh.kind == BShot.MISSILE) Home(b, sh, f);
                sh.x += sh.vx * f; sh.y += sh.vy * f; sh.life -= dt;
                foreach (var e in b.ships)
                {
                    if (e.side == sh.side || e.hp <= 0 || Px.Dist(e.x, e.y, sh.x, sh.y) >= e.C.Hit + (sh.kind == BShot.BOMB ? 2 : 0)) continue;
                    sh.life = 0;
                    if (sh.kind == BShot.BOMB) { Explode(b, sh); break; }
                    if (sh.player) b.joined = true;
                    e.hp -= sh.d;
                    Fx?.Invoke(b.star, sh.x, sh.y, 4, new[] { cW, cCyan });
                    if (e.hp <= 0) Kill(b, e, sh.player);
                    break;
                }
                if (sh.life <= 0) continue;
                if (sh.side == Foe(b) && !sh.player && pl != null && hittable && Px.Dist(pl.x, pl.y, sh.x, sh.y) < 5)
                {
                    sh.life = 0;
                    PlayerHit?.Invoke(sh.x, sh.y);
                    continue;
                }
                // автобластер и союзные корабли (дроны) не трогают планеты — нечаянно войну не начнут
                if (sh.kind == BShot.AUTO || (sh.side == 0 && !sh.player)) continue;
                for (int pi = 0; pi < s.planets.Count; pi++)
                {
                    if (!CanHit(b, pi, sh.side)) continue;
                    var p = s.planets[pi];
                    var pp = PPos(p);
                    if (Px.Dist(pp.x, pp.y, sh.x, sh.y) >= p.size + 4) continue;
                    sh.life = 0;
                    if (sh.kind == BShot.BOMB) Explode(b, sh);
                    // войны империй идут медленнее, чтобы игрок успел прилететь на помощь
                    HitPlanet(b, pi, sh.player ? sh.sd : sh.d * SIEGE * (b.raid != null && b.raid.def != Owner.PLAYER ? WAR_SIEGE : 1), sh.player);
                    break;
                }
            }
            if (spawnLater.Count > 0) { b.ships.AddRange(spawnLater); spawnLater.Clear(); }
            foreach (var fx in b.fx) fx.life -= dt;
            b.fx.RemoveAll(fx => fx.life <= 0);
            b.ships.RemoveAll(e => e.hp <= 0);
            b.shots.RemoveAll(sh => sh.life <= 0);
            var keys = new List<int>(b.flash.Keys);
            foreach (var k in keys) { b.flash[k] -= dt; if (b.flash[k] <= 0) b.flash.Remove(k); }

            if (!b.done && b.raid != null && b.Foes == 0) Repelled(b);
            else if (b.after && b.ships.Count == 0 && b.shots.Count == 0) b.done = true;
        }

        /// <summary>Враги в этом бою воюют и с игроком: охрана, нападение на него, враждебная империя,
        /// или игрок сам вмешался в чужую войну.</summary>
        public static bool VsPlayer(Battle b) =>
            b.withAtt || b.raid == null || b.raid.def == Owner.PLAYER || b.raid.att == Raid.PIRATES || b.joined || (Owner.IsEmp(b.raid.att) && Hostile(b.raid.att));

        /// <summary>По планете pi может стрелять сторона side: налётчики — по атакуемой колонии,
        /// игрок — по планетам враждебной империи, когда охрана перебита.</summary>
        public static bool CanHit(Battle b, int pi, int side)
        {
            var p = b.S.planets[pi];
            if (p.surrendered || !Defended(p)) return false;
            if (side == 1) return b.raid != null && b.raid.planet == pi && p.owner == b.raid.def;
            return b.raid == null && Owner.IsEmp(p.owner) && Hostile(p.owner) && !b.Guards;
        }

        static void Kill(Battle b, BShip e, bool byPlayer)
        {
            Fx?.Invoke(b.star, e.x, e.y, e.cls == ShipClass.BASE ? 90 : 26, new[] { cW, cGold, cOr, e.pirate ? cPir : e.ei >= 0 ? Px.Hex(G.emp[e.ei].col) : cMy });
            Quests.OnKill(b, e, byPlayer);
            if (e.patrol) Patrols.Lost(b.star); // сбитый патрульный дрон потерян
            if (!byPlayer) return;
            Faith.OnKill();
            if (b.raid != null && b.raid.def != Owner.PLAYER) b.helpKills++;
            int cr = e.C.Credits;
            if (e.pirate) P.credits += Mathf.RoundToInt(cr * .6f);
            else { P.credits += cr; Rel(e.ei, Faith.KillRel); }
        }

        static float blockedToastAt;

        static void HitPlanet(Battle b, int pi, float dmg, bool byPlayer = false)
        {
            var s = b.S;
            var p = s.planets[pi];
            if (p.shields == 0) p.shields = Mathf.Max(1, Colony.Buildings(p));
            b.flash[pi] = .25f;
            if (!p.defOut)
            {
                p.defOut = true;
                if (p.broken == 0 && b.raid == null) Rel(p.owner, -5); // игрок напал на планету
                SpawnDefenders(b, pi, HITS[Mathf.Min(p.broken, HITS.Length - 1)]);
                if (b.raid == null) return; // первое попадание только поднимает защитников
            }
            // штурм игрока: пока в системе есть охрана или защитники, щит неуязвим
            // (и когда игрок штурмует вместе с нападающей империей — пока живы защитники планет)
            if (b.raid == null && b.Foes > 0 || byPlayer && b.withAtt && b.ships.Exists(e => e.side == 0 && e.Def && e.hp > 0))
            {
                if (G.time >= blockedToastAt) { blockedToastAt = G.time + 6; Toast?.Invoke("Щит неуязвим, пока живы защитники"); }
                return;
            }
            p.shieldDmg += dmg;
            if (p.shieldDmg < ShieldHp(p.broken)) return;

            p.broken++;
            p.shieldDmg = 0;
            p.defOut = false;
            var pp = PPos(p);
            Fx?.Invoke(b.star, pp.x, pp.y, 40, new[] { cW, cShield, cCyan });
            bool pirates = b.raid != null && b.raid.att == Raid.PIRATES;
            if (b.raid != null && b.raid.def == Owner.PLAYER) b.shieldLost = true;
            if (pirates)
            {
                // пираты разрушают по постройке за каждый щит
                if (p.colonies > 0) p.colonies--; else if (p.depots > 0) p.depots--;
                b.buildingLost = true;
            }
            if (p.broken >= p.shields) { Fallen(b, pi); return; }
            if (pirates) Notes.Add("Пираты разрушили постройку на «" + p.name + "» (" + s.name + ") · щитов осталось: " + ShieldsLeft(p), Notes.LOSS);
            else if (b.raid != null && b.raid.def == Owner.PLAYER) Toast?.Invoke("Пробит щит колонии «" + p.name + "» · щитов осталось: " + ShieldsLeft(p));
        }

        static void Fallen(Battle b, int pi)
        {
            var s = b.S;
            var p = s.planets[pi];
            b.ships.RemoveAll(e => e.home == pi); // защитники планеты складывают оружие
            if (b.raid == null)
            {
                int ei = p.owner;
                p.surrendered = true;
                Rel(ei, -10);
                Notes.Add("Планета «" + p.name + "» в системе " + s.name + " сдалась вам", Notes.WIN);
                if (!s.planets.Exists(q => q.owner == ei && !q.surrendered)) Conquer(b, ei);
            }
            else if (b.raid.att == Raid.PIRATES)
            {
                LosePlanet(s, pi, Owner.NONE);
                if (!s.planets.Exists(q => q.owner == Owner.PLAYER) && s.owner == Owner.PLAYER)
                {
                    s.owner = Owner.NONE;
                    DropRoutes(s);
                }
                Notes.Add("Пираты уничтожили колонию «" + p.name + "» в системе " + s.name, Notes.LOSS);
                EndRaid(b);
            }
            else
            {
                int ei = b.raid.att, def = b.raid.def;
                p.surrendered = true;
                if (def == Owner.PLAYER) b.buildingLost = true;
                if (def == Owner.PLAYER)
                {
                    Rel(ei, 2);
                    Notes.Add("Колония «" + p.name + "» в системе " + s.name + " сдалась: " + G.emp[ei].name, Notes.LOSS);
                }
                int next = NextTarget(s, def);
                if (next >= 0) b.raid.planet = next;
                else SystemLost(b, ei);
            }
        }

        /// <summary>Следующая планета обороняющегося (игрока или империи def) в системе, которая ещё держится.</summary>
        public static int NextTarget(Star s, int def = Owner.PLAYER)
        {
            for (int i = 0; i < s.planets.Count; i++)
            {
                var p = s.planets[i];
                if (p.owner != def || p.surrendered) continue;
                if (def == Owner.PLAYER && (p.home || Colony.Buildings(p) == 0)) continue;
                return i;
            }
            return -1;
        }

        /// <summary>Все планеты империи в системе сдались — система переходит игроку.</summary>
        static void Conquer(Battle b, int ei)
        {
            var s = b.S;
            foreach (var p in s.planets)
            {
                if (p.owner != ei) continue;
                p.owner = Owner.PLAYER;
                if (Colony.Buildings(p) == 0) p.colonies = 1;
                p.store.Clear(); p.ether = 0;
                Shop.FillCaptured(p); // склады захваченной планеты частично заполнены
                ResetShield(p);
            }
            s.owner = Owner.PLAYER;
            Rel(ei, -15);
            bool given = Quests.OnConquer(s, ei); // «Передача системы»: захваченное уходит заказчику
            b.ships.RemoveAll(e => e.side == 1); // уцелевшие корабли империи уходят
            foreach (var sh in b.shots) if (sh.side == 1) sh.life = 0;
            Notes.Add(G.emp[ei].name + " капитулирует в системе " + s.name + (given ? ": система передана по поручению" : ": система ваша"), Notes.WIN);
            Faith.Event("conquer");
            G.emp[ei].met = true;
            if (Notes.CheckFallen(ei)) Notes.Add(G.emp[ei].name + ": захвачена последняя система — держава пала", Notes.WIN);
            Capitulated?.Invoke(s.id, ei);
        }

        /// <summary>Сдались все колонии игрока — система переходит к империи-захватчику.</summary>
        static void SystemLost(Battle b, int ei)
        {
            var s = b.S;
            int def = b.raid.def;
            if (def != Owner.PLAYER)
            {
                // одна империя захватила систему другой
                foreach (var p in s.planets) if (p.owner == def) { p.owner = ei; ResetShield(p); }
                if (s.owner == def) s.owner = ei;
                Diplo.Add(ei, def, -10);
                var d = G.emp[def];
                if (b.withAtt && b.helpKills > 0 && Owner.IsEmp(ei))
                {
                    // игрок помог захватить систему: нападающие благодарят
                    var a = G.emp[ei];
                    int reward = Mathf.RoundToInt((HELP_REWARD + HELP_PER_KILL * b.helpKills) * Faith.HelpRewardMul);
                    P.credits += reward;
                    Diplo.GiveDiscount(ei, Diplo.DISCOUNT, Diplo.DISCOUNT_TIME);
                    a.met = true;
                    Rel(ei, 5);
                    Notes.Add(a.name + " благодарит за помощь в захвате системы " + s.name + ": +" + reward + " кр. и скидка 20% в магазинах на 20 минут", Notes.WIN);
                }
                if (d.rel >= Diplo.ALLY) Notes.Add("Ваш союзник потерял систему: " + s.name + " (" + d.name + ") захватила " + G.emp[ei].name, Notes.INFO);
                if (Notes.CheckFallen(def) && d.met) Notes.Add(d.name + ": захвачена последняя система — держава пала под натиском " + G.emp[ei].name, Notes.INFO);
                EndRaid(b);
                return;
            }
            foreach (var p in s.planets)
                if (p.owner == Owner.PLAYER && !p.home) { LosePlanet(s, s.planets.IndexOf(p), ei); }
            if (!s.planets.Exists(q => q.owner == Owner.PLAYER)) s.owner = ei;
            DropRoutes(s);
            Rel(ei, 5); // враг понял, что с вами нужно быть осторожнее
            Notes.Add("Система " + s.name + " сдалась: теперь она принадлежит " + G.emp[ei].name, Notes.LOSS);
            EndRaid(b);
        }

        static void LosePlanet(Star s, int pi, int newOwner)
        {
            var p = s.planets[pi];
            p.owner = newOwner;
            if (newOwner == Owner.NONE) { p.colonies = 0; p.depots = 0; }
            else if (Colony.Buildings(p) == 0) p.colonies = 1;
            p.store.Clear(); p.ether = 0;
            ResetShield(p);
            G.routes.RemoveAll(r => r.star == s.id && (r.a == pi || r.b == pi));
            G.hubs.RemoveAll(h => h.star == s.id && h.planet == pi);
        }

        /// <summary>Пути системы, которая больше не ваша, перестают существовать.</summary>
        static void DropRoutes(Star s)
        {
            if (s.planets.Exists(q => q.owner == Owner.PLAYER)) return;
            G.routes.RemoveAll(r => r.star == s.id || (!r.InSystem && (r.a == s.id || r.b == s.id)));
            G.hubs.RemoveAll(h => h.star == s.id);
        }

        public const int HELP_REWARD = 150, HELP_PER_KILL = 40;

        static void Repelled(Battle b)
        {
            var s = b.S;
            string who = Att(b.raid);
            int def = b.raid.def;
            if (def != Owner.PLAYER)
            {
                var d = G.emp[def];
                if (b.helpKills > 0 && !b.withAtt)
                {
                    if (b.playerHere) { Faith.Event("defend"); Faith.OnVictory(b.star); }
                    // помогли отбиться: награда, скидка 20% на 20 минут, отношения лучше
                    int reward = Mathf.RoundToInt((HELP_REWARD + HELP_PER_KILL * b.helpKills) * Faith.HelpRewardMul);
                    P.credits += reward;
                    d.discountUntil = G.time + Diplo.DISCOUNT_TIME;
                    d.met = true;
                    Rel(def, 5);
                    Notes.Add(d.name + " благодарит за помощь в отражении нападения (" + who + ", система " + s.name + "): +" + reward + " кр. и скидка 20% в магазинах на 20 минут", Notes.WIN);
                }
                else if (d.rel >= Diplo.ALLY) Notes.Add("Союзник отбился: нападение (" + who + ") на систему " + s.name + " (" + d.name + ") отражено", Notes.INFO);
            }
            if (def == Owner.PLAYER)
            {
                Faith.Event(b.playerHere ? "defend" : "alone");
                if (!b.shieldLost) Faith.Event("noshield");
                if (!b.buildingLost) Faith.Event("nolosses");
                if (b.playerHere) Faith.OnVictory(b.star);
            }
            if (def != Owner.PLAYER) { }
            else if (b.playerHere) { P.credits += 150; Notes.Add("Колония в системе " + s.name + " спасена от нападения (" + who + ")! +150 кр.", Notes.WIN); }
            else Notes.Add("Система " + s.name + " отбила нападение: " + who, Notes.WIN);
            // защитники не исчезают сразу, а улетают на свои планеты (видно, только если игрок рядом)
            var stay = b.playerHere ? b.ships.FindAll(e => (e.Def && e.side == 0 || e.drone) && e.hp > 0) : null;
            EndRaid(b);
            if (stay == null || stay.Count == 0) return;
            foreach (var e in stay) if (!e.drone) e.back = true; // дроны остаются с игроком до конца «Роения»
            b.ships.AddRange(stay);
            b.done = false;
            b.after = true;
        }

        /// <summary>Защитник летит к центру своей планеты и исчезает, коснувшись её.</summary>
        static void Return(Battle b, BShip e, float f)
        {
            var p = b.S.planets[e.home];
            var pp = PPos(p);
            float dx = pp.x - e.x, dy = pp.y - e.y, d = Mathf.Max(.01f, Px.Len(dx, dy));
            if (d < p.size * .6f) { e.hp = 0; return; }
            float want = Mathf.Min(1f, .25f + d * .015f); // неспешно, чтобы было видно возвращение
            e.vx += (dx / d * want - e.vx) * .08f * f;
            e.vy += (dy / d * want - e.vy) * .08f * f;
            e.x += e.vx * f; e.y += e.vy * f;
            e.a = Mathf.Atan2(e.vy, e.vx);
        }

        static void EndRaid(Battle b)
        {
            G.raids.Remove(b.raid);
            foreach (var p in b.S.planets) if (p.owner == Owner.PLAYER || p.owner == b.raid.def) ResetShield(p);
            b.ships.Clear();
            b.shots.Clear();
            b.raid = null;
            b.done = true;
        }
    }
}

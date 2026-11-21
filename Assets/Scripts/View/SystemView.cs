using System.Collections.Generic;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    public sealed class ShipState { public float x, y, vx, vy, a, cd, inv; }

    public sealed class Particle { public float x, y, vx, vy, life, max; public Color32 c; }

    /// <summary>Сцена звёздной системы: полёт, бой, планеты.</summary>
    public sealed class SystemView
    {
        readonly GameManager gm;
        public readonly ShipState ship = new ShipState();
        readonly List<Particle> pt = new List<Particle>();
        int lastFoes;       // сколько врагов было в прошлом кадре — чтобы заметить конец боя
        public int near = -1;

        // виртуальный джойстик: палец в любом месте экрана
        public bool joyOn;
        public int joyId;
        public Vector2 joy0, joy;
        public bool fire;

        static readonly Color32 cBg = Px.Hex("#04020a"), cOrbit = Px.Hex("#8c82ff"), cLights = Px.Hex("#ffe680"),
            cRaid = Px.Hex("#ff3040"), cGrey = Px.Hex("#8a86a8"), cPB = Px.Hex("#9ff4ff"), cEB = Px.Hex("#ff6070"), cWhite = Px.Hex("#ffffff"),
            cGold = Px.Hex("#ffcf4a"), cOrange = Px.Hex("#ff7a2a"), cCyan = Px.Hex("#6fe3ff"), cRed = Px.Hex("#ff5a6a"),
            cPir = Px.Hex("#d8344a"), cSunHi = Px.Hex("#fffbe8");

        public SystemView(GameManager gm)
        {
            this.gm = gm;
            Battles.Fx = (st, x, y, n, cols) => { if (st == P.cur) Boom(x, y, n, cols); };
            Battles.PlayerHit = (x, y) =>
            {
                P.hull -= Battles.PLAYER_HIT * Faith.HullDamageMul;
                Faith.OnHurt();
                Boom(x, y, 6, cRed, cWhite);
                if (P.hull <= 0 && !gm.Dead) gm.Die();
            };
        }

        /// <summary>Сражение в текущей системе (или null).</summary>
        public static Battle B => Battles.Of(P.cur);

        static readonly Color32 cShield = Px.Hex("#7fe8ff"), cShieldHi = Px.Hex("#d8fbff");
        readonly List<(int star, int planet)> qMarks = new List<(int, int)>();
        static readonly BShip patrolLook = new BShip { cls = ShipClass.PATROL, patrol = true }; // образец для спрайта патрульного дрона

        // автопилот входа: от звезды по спирали к ближайшей своей колонии или к самой пригодной планете
        const float AUTO_TIME = 2.6f, AUTO_TURNS = 1.25f;
        float autoT = -1; // прошедшее время автопилота; < 0 — выключен
        int autoTarget = -1;
        public bool AutoPilot => autoT >= 0;

        /// <summary>Куда лететь при входе: ближайшая (по орбите) колония игрока, иначе планета с наибольшей
        /// пригодностью (при равенстве — ближайшая), иначе — первая.</summary>
        static int EntryTarget(Star s)
        {
            int best = -1;
            for (int i = 0; i < s.planets.Count; i++)
                if (s.planets[i].owner == Owner.PLAYER && (best < 0 || s.planets[i].orbit < s.planets[best].orbit)) best = i;
            if (best >= 0) return best;
            for (int i = 0; i < s.planets.Count; i++)
                if (best < 0 || Habit(s.planets[i]) > Habit(s.planets[best])) best = i; // планеты идут по возрастанию орбиты
            return best;
        }

        // ---------- масштаб системы ----------
        // zoom = 1 — исходный масштаб (камера на корабле); Fit() — вся система в кадре (камера на звезде).
        public const float SZ_DEF = 1, SZ_MAX = 2;
        const float ROUTE_SHIPS_ZOOM = .7f; // с какого приближения видны грузовые корабли
        const float OVER_EXIT = 1.4f; // во сколько раз «пережать» Fit щипком, чтобы выйти на карту
        public float zoom = SZ_DEF;
        public bool autoZoom;         // во время спирали входа камера сама приближается от Fit к SZ_DEF
        int lastW = 200, lastH = 300;
        public void SetViewport(int w, int h) { lastW = w; lastH = h; }

        // Самое дальнее отдаление: система занимает ~65% меньшей стороны экрана, чтобы крайние
        // планеты не прятались под HUD, нижней панелью и кнопкой огня.
        const float FIT_SHARE = .65f;
        public float Fit() => Mathf.Min(.6f, Mathf.Min(lastW, lastH) / 2f * FIT_SHARE / (SysR(Cur) + 14));

        /// <summary>Центр камеры: при отдалении плавно смещается от корабля к звезде.</summary>
        public Vector2 CamCenter(float z)
        {
            float fit = Fit(), w = Mathf.Clamp01((z - fit) / (SZ_DEF - fit));
            return new Vector2(ship.x * w, ship.y * w);
        }

        public void SetZoom(float z)
        {
            autoZoom = false;
            zoomTarget = -1;
            float fit = Fit();
            pinchK = 0;
            if (z < fit)
            {
                float over = fit / z;
                pinchK = Mathf.Clamp01((over - 1) / (OVER_EXIT - 1));
                if (over >= OVER_EXIT) { gm.ExitToGalaxy(GalaxyView.ZMAX); return; }
            }
            zoom = Mathf.Clamp(z, fit, SZ_MAX);
        }

        /// <summary>Шаг колеса/кнопки: отдаление на уровне «вся система» уводит на карту.</summary>
        public void ZoomStep(float k)
        {
            float fit = Fit(), from = zoomTarget > 0 ? zoomTarget : zoom;
            if (k < 1 && from <= fit + 1e-3f && zoom <= fit * 1.02f) { gm.ExitToGalaxy(GalaxyView.ZMAX); return; }
            autoZoom = false;
            pinchK = 0;
            zoomTarget = Mathf.Clamp(from * k, fit, SZ_MAX);
        }

        // Колесо задаёт цель, а масштаб плавно к ней подтягивается — шаги мелкие и без рывков.
        public const float WHEEL_STEP = 1.1f;
        float zoomTarget = -1;

        void UpdateZoom(float dt)
        {
            if (zoomTarget <= 0) return;
            float lz = Mathf.Lerp(Mathf.Log(zoom), Mathf.Log(zoomTarget), 1 - Mathf.Exp(-dt * 12));
            zoom = Mathf.Exp(lz);
            if (Mathf.Abs(zoom / zoomTarget - 1) < .003f) { zoom = zoomTarget; zoomTarget = -1; }
        }

        // ---------- мышь: прицел, клик по планете ----------

        public Vector2? aimPx;   // курсор в пикселях кадра; null — целимся автоматически (касания)
        public int goTo = -1;    // планета, к которой корабль летит сам
        public int hover = -1;   // планета под курсором — подсвечивается
        float viewOX, viewOY, viewZ = 1; // преобразование мир → экран последнего кадра

        public Vector2 ScreenToWorld(Vector2 px) => new Vector2((px.x - viewOX) / viewZ, (px.y - viewOY) / viewZ);

        /// <summary>Планета под точкой экрана (с небольшим запасом вокруг диска) или -1.</summary>
        public int PlanetAtPx(Vector2 px)
        {
            var s = Cur;
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < s.planets.Count; i++)
            {
                var pp = PPos(s.planets[i]);
                float d = Px.Dist(viewOX + pp.x * viewZ, viewOY + pp.y * viewZ, px.x, px.y);
                if (d <= Mathf.Max(3, s.planets[i].size * viewZ) + 2 && d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>Курсор над диском звезды в центре системы?</summary>
        public bool SunAtPx(Vector2 px)
        {
            float r = (Cur.core ? 26 : 12) * viewZ;
            return Px.Dist(viewOX, viewOY, px.x, px.y) <= Mathf.Max(3, r) + 2;
        }

        public void GoTo(int i)
        {
            goTo = i;
            if (AutoPilot) { autoT = -1; ship.inv = 1; autoZoom = false; }
        }

        /// <summary>Тяга к выбранной планете с торможением у её края. Возвращает угол носа на цель.</summary>
        float SteerToPlanet(Star s, ref float ax, ref float ay)
        {
            var p = s.planets[goTo];
            var pp = PPos(p);
            float dx = pp.x - ship.x, dy = pp.y - ship.y, d = Mathf.Max(.01f, Px.Len(dx, dy));
            float stop = p.size + 12;
            if (d < stop + 3 && Px.Len(ship.vx, ship.vy) < .4f) { goTo = -1; return float.NaN; } // прилетели
            float want = Mathf.Min(2 + P.lv.eng * .25f, Mathf.Max(0, d - stop) * .06f);
            ax = (dx / d * want - ship.vx) * 2;
            ay = (dy / d * want - ship.vy) * 2;
            float l = Px.Len(ax, ay);
            if (l > 1) { ax /= l; ay /= l; }
            return Mathf.Atan2(dy, dx);
        }

        public void Reset(Star s)
        {
            goTo = -1;
            ship.x = 0; ship.y = 0; ship.vx = 0; ship.vy = 0; ship.a = 0; ship.inv = 0;
            autoT = 0;
            autoTarget = EntryTarget(s);
            zoom = SZ_DEF; autoZoom = false; zoomTarget = -1;
            pt.Clear();
            lastFoes = 0; near = -1; beamOn = false;
            ReleaseInput();
        }

        /// <summary>Точка спирали при прогрессе e∈[0,1]; в конце — у края целевой планеты, со стороны звезды.</summary>
        Vector2 SpiralPos(Star s, float e)
        {
            float endAng = 0, endR = 60;
            if (autoTarget >= 0 && autoTarget < s.planets.Count)
            {
                var p = s.planets[autoTarget];
                endAng = p.ang;
                endR = Mathf.Max(20, p.orbit - p.size - 9);
            }
            float r = e * endR, a = endAng - (1 - e) * AUTO_TURNS * Px.TAU;
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        void UpdateAutoPilot(Star s, float dt, float f)
        {
            autoT += dt;
            float u = Mathf.Clamp01(autoT / AUTO_TIME), e = u * u * (3 - 2 * u);
            var np = SpiralPos(s, e);
            float dx = np.x - ship.x, dy = np.y - ship.y;
            if (dx * dx + dy * dy > 1e-4f) ship.a = Mathf.Atan2(dy, dx);
            // скорость сохраняем, чтобы при перехвате управления корабль летел дальше по инерции
            if (f > 0) { ship.vx = dx / f; ship.vy = dy / f; }
            ship.x = np.x; ship.y = np.y;
            if (autoZoom)
            {
                float fit = Fit();
                zoom = Mathf.Exp(Mathf.Lerp(Mathf.Log(fit), Mathf.Log(SZ_DEF), e));
                if (u >= 1) autoZoom = false;
            }
            if (Random.value < .8f)
                Part(ship.x - Mathf.Cos(ship.a) * 6, ship.y - Mathf.Sin(ship.a) * 6,
                    -Mathf.Cos(ship.a) * .8f + (Random.value - .5f) * .4f, -Mathf.Sin(ship.a) * .8f + (Random.value - .5f) * .4f,
                    .35f, Random.value < .5f ? cGold : cOrange);
            if (u >= 1) { autoT = -1; ship.inv = 1; }
        }

        public void ReleaseInput()
        {
            joyOn = false; joy = Vector2.zero; fire = false;
            touches.Clear(); pinching = false; pinchK = 0;
        }

        // ---------- ввод: один палец — джойстик, два — масштаб ----------

        readonly Dictionary<int, Vector2> touches = new Dictionary<int, Vector2>();
        bool pinching;
        float pinchD0, pinchZ0;
        float pinchK; // насколько «пережат» масштаб «вся система» (0..1), для подсказки

        float TwoDist()
        {
            Vector2 a = default, b = default;
            int k = 0;
            foreach (var v in touches.Values) { if (k == 0) a = v; else if (k == 1) b = v; k++; }
            return Mathf.Max(1, (a - b).magnitude);
        }

        /// <summary>Подхватить пальцы, оставшиеся после перехода с карты: щипок продолжается без нового касания.</summary>
        public void Adopt(Dictionary<int, Vector2> active)
        {
            touches.Clear();
            foreach (var kv in active) touches[kv.Key] = kv.Value;
            if (touches.Count >= 2) { pinching = true; pinchD0 = TwoDist(); pinchZ0 = zoom; pinchK = 0; }
        }

        public void PointerDown(int id, Vector2 p)
        {
            touches[id] = p;
            if (touches.Count >= 2)
            {
                joyOn = false; joy = Vector2.zero;
                pinching = true; pinchD0 = TwoDist(); pinchZ0 = zoom; pinchK = 0;
                return;
            }
            if (!joyOn) { joyOn = true; joyId = id; joy0 = p; joy = Vector2.zero; }
        }

        public void PointerMove(int id, Vector2 p)
        {
            if (touches.ContainsKey(id)) touches[id] = p;
            if (pinching && touches.Count >= 2)
            {
                SetZoom(pinchZ0 * TwoDist() / pinchD0);
                return;
            }
            if (joyOn && id == joyId)
            {
                var d = (p - joy0) / 20f;
                joy = d.magnitude > 1 ? d.normalized : d;
            }
        }

        public void PointerUp(int id)
        {
            touches.Remove(id);
            if (pinching && touches.Count < 2) { pinching = false; pinchK = 0; }
            if (joyOn && id == joyId) { joyOn = false; joy = Vector2.zero; }
        }

        void Part(float x, float y, float vx, float vy, float life, Color32 c) =>
            pt.Add(new Particle { x = x, y = y, vx = vx, vy = vy, life = life, max = life, c = c });

        public void Boom(float x, float y, int n, params Color32[] cols)
        {
            for (int i = 0; i < n; i++)
            {
                float a = Random.value * Px.TAU, s = Random.value * 1.8f;
                Part(x, y, Mathf.Cos(a) * s, Mathf.Sin(a) * s, .4f + Random.value * .7f, cols[i % cols.Length]);
            }
        }

        public void Update(float dt, Vector2 keys, bool keyFire)
        {
            var s = Cur;
            float f = dt * 60;
            foreach (var p in s.planets) p.ang += p.spd * dt * .6f;
            UpdateZoom(dt);

            if (!gm.Dead && AutoPilot)
            {
                // игрок может перехватить управление джойстиком или клавишами
                if (Px.Len(joy.x + keys.x, joy.y + keys.y) > .3f) { autoT = -1; ship.inv = 1; autoZoom = false; }
                else UpdateAutoPilot(s, dt, f);
            }

            if (!gm.Dead && !AutoPilot)
            {
                float ax = joy.x + keys.x, ay = joy.y + keys.y;
                float faceA = float.NaN;
                if (Px.Len(ax, ay) > .3f) goTo = -1; // ручное управление отменяет полёт к планете
                else if (goTo >= 0) faceA = SteerToPlanet(s, ref ax, ref ay);
                float al = Px.Len(ax, ay);
                if (al > 1) { ax /= al; ay /= al; }
                ship.vx += ax * .09f * f; ship.vy += ay * .09f * f;
                float fr = Mathf.Pow(.965f, f);
                ship.vx *= fr; ship.vy *= fr;
                float sp = Px.Len(ship.vx, ship.vy), mx = 2 + P.lv.eng * .25f;
                if (sp > mx) { ship.vx *= mx / sp; ship.vy *= mx / sp; }
                ship.x += ship.vx * f; ship.y += ship.vy * f;
                if (al > .1f)
                {
                    float ta = Mathf.Atan2(ay, ax); // направление тяги
                    // при полёте к планете нос смотрит на цель, даже когда двигатель тормозит
                    ship.a = float.IsNaN(faceA) ? ta : faceA;
                    // выхлоп — против тяги, а не против носа: с мышью корабль может лететь боком
                    if (Random.value < .8f)
                        Part(ship.x - Mathf.Cos(ta) * 6, ship.y - Mathf.Sin(ta) * 6,
                            -Mathf.Cos(ta) * .8f + (Random.value - .5f) * .4f, -Mathf.Sin(ta) * .8f + (Random.value - .5f) * .4f,
                            .35f, Random.value < .5f ? cGold : cOrange);
                }
                if (al <= .1f && aimPx.HasValue)
                {
                    // мышь: пока летим по инерции — нос по ходу движения, когда почти стоим — на курсор
                    float spd = Px.Len(ship.vx, ship.vy);
                    if (spd > .35f) ship.a = Mathf.Atan2(ship.vy, ship.vx);
                    else
                    {
                        var w = ScreenToWorld(aimPx.Value);
                        ship.a = Mathf.Atan2(w.y - ship.y, w.x - ship.x);
                    }
                }
                // мягкая граница системы
                float lim = SysR(s) + 170, dc = Px.Len(ship.x, ship.y);
                if (dc > lim) { ship.vx -= ship.x / dc * .15f * f; ship.vy -= ship.y / dc * .15f * f; }
                ship.inv -= dt; ship.cd -= dt;

                beamOn = false;
                if (fire || keyFire)
                {
                    var aim = AimPoint();
                    Weapons.Fire(P.cur, ship, aim);
                    if (Weapons.Sel == Weapons.LASER && P.energy >= Weapons.Cost(Weapons.Get(Weapons.LASER)))
                    {
                        // луч рисуется каждый кадр, пока зажат огонь; урон идёт тиками
                        float a = Mathf.Atan2(aim.y - ship.y, aim.x - ship.x);
                        beamA = new Vector2(ship.x + Mathf.Cos(a) * 5, ship.y + Mathf.Sin(a) * 5);
                        float l = Battles.BeamEnd(s, beamA.x, beamA.y, a, Weapons.BEAM_LEN, out int bp);
                        beamB = beamA + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * l;
                        beamOn = true;
                        if (bp >= 0 && Random.value < .6f) Part(beamB.x, beamB.y, (Random.value - .5f) * 1.2f, (Random.value - .5f) * 1.2f, .25f, Random.value < .5f ? cLaser : cWhite);
                    }
                }
                Weapons.Passive(P.cur, ship);
            }
            Weapons.Tick(dt);
            if (Faith.AbilityActive && !gm.Dead && Random.value < .5f)
            {
                // способность веры горит — вокруг корабля искры её цвета
                float a = Random.value * Px.TAU;
                var fc = Px.Hex(Faith.Mine.Col);
                Part(ship.x + Mathf.Cos(a) * 5, ship.y + Mathf.Sin(a) * 5, Mathf.Cos(a) * .3f, Mathf.Sin(a) * .3f - .2f, .4f, Random.value < .5f ? fc : cWhite);
            }

            // след ракет
            var bm = B;
            if (bm != null)
                foreach (var sh in bm.shots)
                    if (sh.kind == BShot.MISSILE && Random.value < .7f)
                        Part(sh.x - sh.vx * 1.5f, sh.y - sh.vy * 1.5f, (Random.value - .5f) * .3f, (Random.value - .5f) * .3f, .3f, Random.value < .5f ? cGold : cOrange);

            near = -1;
            for (int i = 0; i < s.planets.Count; i++)
            {
                var pp = PPos(s.planets[i]);
                if (Px.Dist(pp.x, pp.y, ship.x, ship.y) < s.planets[i].size + 16) near = i;
            }

            foreach (var p in pt)
            {
                p.x += p.vx * f; p.y += p.vy * f; p.vx *= .96f; p.vy *= .96f; p.life -= dt;
            }
            pt.RemoveAll(p => p.life <= 0);

            // охрана перебита: дальше можно штурмовать планеты враждебной империи
            var b = B;
            int foes = b != null ? b.Foes : 0;
            if (lastFoes > 0 && foes == 0 && b != null && b.raid == null && !b.after)
            {
                bool targets = false;
                for (int i = 0; i < s.planets.Count; i++) if (Battles.CanHit(b, i, 0)) targets = true;
                gm.Toast(targets ? "Охрана уничтожена — можно атаковать планеты" : "Угроза устранена");
                Faith.OnVictory(P.cur); // «Вечное возвращение»
                Quests.OnVictory(P.cur); // «Карательный рейд», «Дуэль»
                Save();
            }
            lastFoes = foes;
        }

        /// <summary>Куда стрелять: мышь — курсор; касания — ближайший враг или по носу корабля.</summary>
        Vector2 AimPoint()
        {
            if (aimPx.HasValue) return ScreenToWorld(aimPx.Value);
            BShip tg = null;
            float bd = 160;
            var bt = B;
            if (bt != null)
                foreach (var e in bt.ships)
                {
                    if (e.side != Battles.Foe(bt) || e.hp <= 0) continue;
                    float d = Px.Dist(e.x, e.y, ship.x, ship.y);
                    if (d < bd) { bd = d; tg = e; }
                }
            if (tg != null) return new Vector2(tg.x, tg.y);
            return new Vector2(ship.x + Mathf.Cos(ship.a) * 100, ship.y + Mathf.Sin(ship.a) * 100);
        }

        bool beamOn;           // лазер включён в этом кадре
        Vector2 beamA, beamB;  // начало и конец луча (мировые координаты)

        static readonly Color32 cLaser = Px.Hex("#ff5a7a"), cLaserHi = Px.Hex("#ffd0da"), cBolt = Px.Hex("#b8d8ff"),
            cBreaker = Px.Hex("#c67bff"), cAuto = Px.Hex("#59ffb0"), cBomb = Px.Hex("#ff9a2e");

        /// <summary>Снаряд: у каждого вида оружия свой вид.</summary>
        static void DrawShot(PixelCanvas c, BShot b, int X, int Y, float tnow)
        {
            if (!b.player && b.side != 0)
            {
                if (b.kind == BShot.HEAVY) { c.Circle(X, Y, 2, cEB); c.Pset(X, Y, cWhite); } // снаряд бомбардировщика
                else c.Rect(X - 1, Y - 1, 2, 2, cEB);
                return;
            }
            switch (b.kind)
            {
                case BShot.BOMB:
                    c.Circle(X, Y, 2, cBomb);
                    if (Mathf.Sin(tnow * 20) > 0) c.Pset(X, Y, cWhite);
                    break;
                case BShot.MISSILE:
                    c.Rect(X - 1, Y - 1, 2, 2, cWhite);
                    c.Pset(Px.R(X - b.vx), Px.R(Y - b.vy), cGold);
                    break;
                case BShot.BREAKER:
                    c.Rect(X - 1, Y - 1, 3, 3, cBreaker, .5f);
                    c.Rect(X - 1, Y - 1, 2, 2, cBreaker);
                    break;
                case BShot.AUTO:
                    c.Rect(X, Y, 1, 1, cAuto);
                    c.Pset(Px.R(X - b.vx * .5f), Px.R(Y - b.vy * .5f), cAuto, .5f);
                    break;
                default:
                    c.Rect(X - 1, Y - 1, 2, 2, cPB);
                    break;
            }
        }

        /// <summary>Лучи, молнии и взрывы.</summary>
        static void DrawFx(PixelCanvas c, BFx f, System.Func<float, int> sx, System.Func<float, int> sy, float z)
        {
            float k = Mathf.Clamp01(f.life / f.max);
            switch (f.kind)
            {
                case BFx.BEAM:
                {
                    Vector2 a = f.pts[0], b = f.pts[1];
                    float dx = b.x - a.x, dy = b.y - a.y, l = Mathf.Max(1, Px.Len(dx, dy)), nx = -dy / l, ny = dx / l;
                    float X0 = sx(a.x), Y0 = sy(a.y), X1 = sx(b.x), Y1 = sy(b.y);
                    c.Line(X0 + nx, Y0 + ny, X1 + nx, Y1 + ny, cLaser, .8f * k, .5f * k);
                    c.Line(X0 - nx, Y0 - ny, X1 - nx, Y1 - ny, cLaser, .8f * k, .5f * k);
                    c.Line(X0, Y0, X1, Y1, cLaserHi, k, .8f * k);
                    break;
                }
                case BFx.BOLT:
                    for (int i = 0; i + 1 < f.pts.Count; i++)
                    {
                        // ломаная с дрожью: каждый кадр новая
                        Vector2 a = f.pts[i], b = f.pts[i + 1];
                        float px = sx(a.x), py = sy(a.y);
                        const int SEG = 4;
                        for (int j = 1; j <= SEG; j++)
                        {
                            float t = (float)j / SEG;
                            float qx = Mathf.Lerp(sx(a.x), sx(b.x), t), qy = Mathf.Lerp(sy(a.y), sy(b.y), t);
                            if (j < SEG) { qx += (Random.value - .5f) * 6; qy += (Random.value - .5f) * 6; }
                            c.Line(px, py, qx, qy, cBolt, k, k);
                            c.Line(px + 1, py, qx + 1, qy, cWhite, .5f * k, .5f * k);
                            px = qx; py = qy;
                        }
                    }
                    break;
                case BFx.BLAST:
                {
                    var p = f.pts[0];
                    float r = f.r * z * (1 - k * .6f);
                    c.Ring(sx(p.x), sy(p.y), r, cBomb, Mathf.Max(16, Px.R(r * 3)), 0, k);
                    c.Circle(sx(p.x), sy(p.y), Px.R(r * .7f), cGold, .25f * k);
                    break;
                }
            }
        }

        // ---------- отрисовка ----------

        void EdgeMark(PixelCanvas c, int X, int Y, Color32 col)
        {
            int W = c.W, H = c.H;
            if (X >= 0 && X < W && Y >= 0 && Y < H) return;
            float dx = X - W / 2f, dy = Y - H / 2f;
            float t = Mathf.Min((W / 2f - 4) / Mathf.Max(Mathf.Abs(dx), 1e-6f), (H / 2f - 4) / Mathf.Max(Mathf.Abs(dy), 1e-6f));
            c.Rect(Px.R(W / 2f + dx * t) - 1, Px.R(H / 2f + dy * t) - 1, 3, 3, col);
        }

        /// <summary>
        /// Грузовые корабли путей между системами: из этой системы — от каждой своей планеты, где есть груз,
        /// к краю системы в сторону звезды назначения; в эту систему — от края к приёмной планете.
        /// </summary>
        void DrawGalaxyRouteShips(PixelCanvas c, Star s, System.Func<float, int> sx, System.Func<float, int> sy)
        {
            float edge = SysR(s) + 60;
            foreach (var r in G.routes)
            {
                if (r.InSystem || r.res.Count == 0 || (r.a != s.id && r.b != s.id)) continue;
                var other = G.stars[r.a == s.id ? r.b : r.a];
                var dir = new Vector2(other.x - s.x, other.y - s.y).normalized;
                var gate = dir * edge;
                if (r.a == s.id)
                {
                    foreach (var p in s.planets)
                    {
                        if (!Routes.Owned(p)) continue;
                        string id = null;
                        foreach (var res in r.res) if (Stacks.Count(p.store, res) > 0) { id = res; break; }
                        if (id == null) continue; // с этой планеты везти нечего
                        Ships(c, PPos(p), gate, Routes.ColorOf(id), sx, sy);
                    }
                }
                else
                {
                    int hub = Routes.HubOf(s);
                    if (hub >= 0) Ships(c, gate, PPos(s.planets[hub]), Routes.ColorOf(r.res[0]), sx, sy);
                }
            }
        }

        static void Ships(PixelCanvas c, Vector2 from, Vector2 to, Color32 col, System.Func<float, int> sx, System.Func<float, int> sy)
        {
            for (int k = 0; k < 2; k++)
            {
                float t = G.time / Routes.PERIOD + k / 2f; t -= Mathf.Floor(t);
                var q = Vector2.Lerp(from, to, t);
                c.Rect(sx(q.x) - 1, sy(q.y) - 1, 2, 2, col);
            }
        }

        public static void DrawSun(PixelCanvas c, int X, int Y, Star s, float tnow, float z = 1)
        {
            float r = (s.core ? 26 : 12) * z;
            float pulse = Mathf.Sin(tnow * 2) * 1.5f * z;
            var col = Px.Hex(s.col);
            c.Circle(X, Y, Mathf.Max(1, Px.R(r * 2.2f + pulse)), col, .06f);
            c.Circle(X, Y, Mathf.Max(1, Px.R(r * 1.5f + pulse)), col, .12f);
            c.Circle(X, Y, Mathf.Max(2, Px.R(r)), col);
            if (r >= 4) c.Circle(X - Px.R(2 * z), Y - Px.R(2 * z), Px.R(r * .6f), cSunHi);
        }

        /// <summary>Показывать ли щиты планеты: враждебная планета, атакуемая колония, бой уже шёл.</summary>
        public static bool ShowShield(Battle bt, int i, Planet p)
        {
            if (!Battles.Defended(p)) return false;
            if (p.surrendered || p.broken > 0 || p.shieldDmg > 0 || p.defOut) return true;
            if (Owner.IsEmp(p.owner) && Battles.Hostile(p.owner)) return true;
            return bt != null && bt.raid != null && bt.raid.planet == i;
        }

        /// <summary>Силовое поле: по кольцу на каждый оставшийся щит, внешнее — текущее, оно тускнеет от попаданий.</summary>
        static void DrawShield(PixelCanvas c, Battle bt, int i, Planet p, int X, int Y, float ps, float tnow)
        {
            if (!ShowShield(bt, i, p)) return;
            if (p.surrendered)
            {
                c.Ring(X, Y, ps + 5, cWhite, 8, tnow * .3f, .55f); // белый флаг: планета сдалась
                return;
            }
            int left = Battles.ShieldsLeft(p);
            if (left <= 0) return;
            float fl = bt != null && bt.flash.TryGetValue(i, out var t) ? t / .25f : 0;
            float outer = ps + 4 + (left - 1) * 2;
            c.Circle(X, Y, Px.R(outer), cShield, .05f + .1f * fl); // купол поля
            for (int k = 0; k < left; k++)
            {
                float r = ps + 4 + k * 2;
                bool cur = k == left - 1;
                float a = cur ? .25f + .55f * Battles.ShieldFrac(p) : .45f;
                if (cur && fl > 0) a = Mathf.Max(a, fl);
                int n = Mathf.Max(16, Px.R(r * 2.6f));
                c.Ring(X, Y, r, fl > 0 && cur ? cShieldHi : cShield, n, tnow * (k % 2 == 0 ? .5f : -.5f), a);
            }
        }

        public void Draw(PixelCanvas c, float tnow, List<BgStar> bgStars)
        {
            lastW = c.W; lastH = c.H;
            DrawAt(c, tnow, bgStars, zoom, CamCenter(zoom), new Vector2(c.W / 2f, c.H / 2f));
        }

        /// <summary>Рисует систему в масштабе z так, что мировая точка cen оказывается в экранной точке anchor.</summary>
        public void DrawAt(PixelCanvas c, float tnow, List<BgStar> bgStars, float z, Vector2 cen, Vector2 anchor)
        {
            var s = Cur;
            int W = c.W, H = c.H;
            lastW = W; lastH = H;
            float ox = anchor.x - cen.x * z, oy = anchor.y - cen.y * z;
            viewOX = ox; viewOY = oy; viewZ = z;
            int SX(float x) => Px.R(ox + x * z);
            int SY(float y) => Px.R(oy + y * z);
            float shipScale = Mathf.Max(z, .5f); // корабли не мельче половины, иначе их не разглядеть
            c.Clear(cBg);

            foreach (var st in bgStars)
                c.Pset((int)(((st.x - cen.x * z * st.f) % W + W) % W), (int)(((st.y - cen.y * z * st.f) % H + H) % H), st.c);

            foreach (var p in s.planets)
            {
                float r = p.orbit * z;
                int n = Mathf.Max(12, Mathf.FloorToInt(r * Px.TAU / 5));
                for (int i = 0; i < n; i++)
                {
                    float a = (float)i / n * Px.TAU;
                    c.Pset(Px.R(ox + Mathf.Cos(a) * r), Px.R(oy + Mathf.Sin(a) * r), cOrbit, .25f);
                }
            }

            DrawSun(c, Px.R(ox), Px.R(oy), s, tnow, z);
            var raid = G.raids.Find(r => r.star == P.cur);
            Quests.Marks(qMarks); // цели поручений в этой системе
            var bt = B;

            for (int i = 0; i < s.planets.Count; i++)
            {
                var p = s.planets[i];
                var pp = PPos(p);
                int X = SX(pp.x), Y = SY(pp.y);
                float ps = p.size * z;
                int m = (int)ps + 30;
                if (X > -m && X < W + m && Y > -m && Y < H + m)
                {
                    if (i == hover)
                    {
                        // белое свечение под планетой и пунктирное кольцо — как у звезды на карте
                        float pulse = .5f + .5f * Mathf.Sin(tnow * 4);
                        c.Circle(X, Y, Px.R(ps) + 4, cWhite, .12f + .06f * pulse);
                        c.Ring(X, Y, ps + 6, cWhite, Mathf.Max(12, Px.R(ps * 2.5f)), tnow * .6f, .75f);
                    }
                    c.BlitRot(Sprites.PlanetSprite(p), X, Y, 0, z);
                    if (p.owner == Owner.PLAYER)
                    {
                        c.Ring(X, Y, ps + 3, EmpCol, 14, tnow * .5f);
                        if (z >= .7f && Mathf.Sin(tnow * 3 + i) > -.3f)
                            for (int k = 0; k < 3; k++)
                                c.Pset(X + Px.R(((float)Px.Hash(k, 1, p.seed) - .5f) * ps * 1.1f), Y + Px.R(((float)Px.Hash(k, 2, p.seed) - .5f) * ps * 1.1f), cLights);
                    }
                    else if (Owner.IsEmp(p.owner)) c.Ring(X, Y, ps + 3, Px.Hex(G.emp[p.owner].col), 14, tnow * .5f);
                    DrawShield(c, bt, i, p, X, Y, ps, tnow);
                    if (raid != null && raid.planet == i && Mathf.Sin(tnow * 8) > 0) c.Ring(X, Y, ps + 6, cRaid, 20);
                    if (i == goTo) c.Ring(X, Y, ps + 8, cGold, 16, -tnow * 2); // цель автополёта
                    if (qMarks.Contains((P.cur, i)))
                    {
                        // цель поручения: оранжевое кольцо и четыре метки, «дышат»
                        float rq = ps + 11 + 2 * Mathf.Sin(tnow * 3);
                        c.Ring(X, Y, rq, GalaxyView.cQuest, Mathf.Max(16, Px.R(rq * 2.5f)), tnow * .7f, .8f);
                        for (int k = 0; k < 4; k++)
                        {
                            float a = k * Mathf.PI / 2 + Mathf.PI / 4;
                            c.Rect(X + Px.R(Mathf.Cos(a) * (rq + 3)) - 1, Y + Px.R(Mathf.Sin(a) * (rq + 3)) - 1, 2, 2, GalaxyView.cQuest);
                        }
                    }
                    if (i == near)
                    {
                        int r = Px.R(ps) + 5;
                        foreach (var (a, b) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                        {
                            c.Rect(X + a * r - (a > 0 ? 1 : 0), Y + b * r, 2, 1, cWhite);
                            c.Rect(X + a * r, Y + b * r - (b > 0 ? 1 : 0), 1, 2, cWhite);
                        }
                    }
                }
                else EdgeMark(c, X, Y, qMarks.Contains((P.cur, i)) ? GalaxyView.cQuest : p.owner == Owner.PLAYER ? EmpCol : Owner.IsEmp(p.owner) ? Px.Hex(G.emp[p.owner].col) : cGrey);
            }

            // грузовые корабли на транспортных путях — видны при большом приближении
            if (z >= ROUTE_SHIPS_ZOOM)
                foreach (var r in G.routes)
                {
                    if (r.star != s.id || r.res.Count == 0 || r.a >= s.planets.Count || r.b >= s.planets.Count) continue;
                    Vector2 pa = PPos(s.planets[r.a]), pb = PPos(s.planets[r.b]);
                    for (int k = 0; k < 3; k++)
                    {
                        float t = G.time / Routes.PERIOD + k / 3f; t -= Mathf.Floor(t);
                        var q = Vector2.Lerp(pa, pb, t);
                        c.Rect(SX(q.x) - 1, SY(q.y) - 1, 2, 2, Routes.ColorOf(r.res[k % r.res.Count]));
                    }
                }
            if (z >= ROUTE_SHIPS_ZOOM) DrawGalaxyRouteShips(c, s, SX, SY);
            foreach (var p in pt) c.Pset(SX(p.x), SY(p.y), p.c, Px.Clamp(p.life / p.max, 0, 1));
            // патрульные дроны в мирное время облетают систему (в бою они — обычные корабли сражения)
            int pn = Patrols.Count(P.cur);
            if (pn > 0 && (bt == null || !bt.patrolOut))
            {
                var spr = Sprites.Combat(patrolLook, out float pcs);
                for (int k = 0; k < pn; k++)
                {
                    var at = Patrols.Pos(s, k, G.time, out float pa);
                    int X = SX(at.x), Y = SY(at.y);
                    if (X < -10 || X > W + 10 || Y < -10 || Y > H + 10) continue;
                    c.BlitRot(spr, X, Y, pa, shipScale * pcs);
                }
            }
            if (bt != null)
            {
                foreach (var b in bt.shots) DrawShot(c, b, SX(b.x), SY(b.y), tnow);
                foreach (var f in bt.fx) DrawFx(c, f, SX, SY, z);
                foreach (var e in bt.ships)
                {
                    int X = SX(e.x), Y = SY(e.y);
                    if (X < -10 || X > W + 10 || Y < -10 || Y > H + 10) { if (e.side == 1 && !e.Def) EdgeMark(c, X, Y, cRaid); continue; }
                    // защитники планет — помельче; свои — в цветах корабля игрока
                    // у каждого класса свой спрайт (свои, империи, пираты — в своих цветах); размер уже нарисован по классу
                    var spr = Sprites.Combat(e, out float cs);
                    if (e.wreck && Mathf.Sin(tnow * 6) < -.6f) continue; // подбитый корабль мигает
                    float es = shipScale * (spr == Sprites.Ship ? e.C.Scale : cs) * (e.Def ? .8f : 1);
                    if (e.back)
                    {
                        // возвращаясь, защитник уменьшается — будто садится на планету
                        var hp = PPos(s.planets[e.home]);
                        es *= Mathf.Clamp(Px.Dist(e.x, e.y, hp.x, hp.y) / (s.planets[e.home].size + 12), .3f, 1);
                    }
                    c.BlitRot(spr, X, Y, e.a, es);
                }
            }

            if (beamOn && !gm.Dead)
            {
                float X0 = SX(beamA.x), Y0 = SY(beamA.y), X1 = SX(beamB.x), Y1 = SY(beamB.y);
                float dx = X1 - X0, dy = Y1 - Y0, l = Mathf.Max(1, Px.Len(dx, dy)), nx = -dy / l, ny = dx / l;
                float fl = .75f + .25f * Mathf.Sin(tnow * 40); // мерцание
                c.Line(X0 + nx, Y0 + ny, X1 + nx, Y1 + ny, cLaser, .7f * fl, .45f * fl);
                c.Line(X0 - nx, Y0 - ny, X1 - nx, Y1 - ny, cLaser, .7f * fl, .45f * fl);
                c.Line(X0, Y0, X1, Y1, cLaserHi, fl, .8f * fl);
            }

            int SXs = SX(ship.x), SYs = SY(ship.y);
            if (!gm.Dead)
            {
                if (SXs < -6 || SXs > W + 6 || SYs < -6 || SYs > H + 6) EdgeMark(c, SXs, SYs, cWhite);
                else c.BlitRot(Sprites.Ship, SXs, SYs, ship.a, shipScale); // неуязвимость после входа не мигает
            }

            // масштаб «пережат» ниже «вся система»: кольцо расходится от звезды — сейчас откроется карта
            if (pinchK > 0)
                c.Ring(Px.R(ox), Px.R(oy), Mathf.Lerp(20, Mathf.Max(W, H) * .6f, pinchK), cGold, 48, tnow, .4f + .6f * pinchK);

            if (joyOn)
            {
                c.Ring(Px.R(joy0.x), Px.R(joy0.y), 20, cWhite, 28, 0, .35f);
                c.Circle(Px.R(joy0.x + joy.x * 20), Px.R(joy0.y + joy.y * 20), 5, cWhite, .35f);
            }
        }
    }
}

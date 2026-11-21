using System.Collections.Generic;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    public struct BgStar { public float x, y, f; public Color32 c; }

    public struct Dust { public float x, y; public Color32 c; }

    /// <summary>Прыжок: из точки (fx, fy) — звезды или места, где корабль сменил курс, — к звезде to.</summary>
    public sealed class Travel { public float fx, fy, t, cost; public int to; }

    /// <summary>Карта галактики: пан, зум, выбор звезды, анимация прыжка.</summary>
    public sealed class GalaxyView
    {
        readonly GameManager gm;
        public float camX, camY, zoom = .35f;
        public int sel = -1;
        public int hover = -1; // звезда под курсором — подсвечивается
        public Travel travel;

        public readonly List<Dust> dust = new List<Dust>();
        public readonly List<BgStar> bgStars = new List<BgStar>();

        // жест пана
        bool panOn, panMoved;
        int panId;
        Vector2 pan0;
        float panCX, panCY;

        static readonly Color32 cAllyRaid = Px.Hex("#ffcf4a"), cAnyRaid = Px.Hex("#c67bff");
        public static readonly Color32 cQuest = Px.Hex("#ffa53a"); // цели поручений
        readonly List<(int star, int planet)> qMarks = new List<(int, int)>();
        readonly HashSet<int> qStars = new HashSet<int>();
        static readonly Color32 cBg = Px.Hex("#03020a"), cCoreGlow = Px.Hex("#ffe9b0"), cRange = Px.Hex("#6effa0"), cWhiteHi = Px.Hex("#fffbe8"),
            cRaid = Px.Hex("#ff3040"), cGold = Px.Hex("#ffcf4a"), cWhite = Px.Hex("#ffffff"),
            cVisited = Px.Hex("#a8a4c8"), cWave = Px.Hex("#bfe6ff"), cGlow = Px.Hex("#9669ff");

        // свечение при отдалении: начинается с масштаба FAR0, полностью — к FAR1; GLOW_A — вклад одной звезды
        const float FAR0 = .2f, FAR1 = .03f, GLOW_A = .45f, GLOW_Z = .02f; // GLOW_Z — масштаб, ниже которого вклад звезды падает
        const float GLOW_CENTER = 2.2f, GLOW_CENTER_R = 2200; // усиление свечения у центра (во сколько раз сверх обычного) и его радиус

        public GalaxyView(GameManager gm) { this.gm = gm; }

        public void InitDerived()
        {
            var r = new Rng((uint)(G.seed + 7));
            dust.Clear();
            var DC = new[] { Px.Hex("#241d48"), Px.Hex("#33285f"), Px.Hex("#1b3350"), Px.Hex("#43295a"), Px.Hex("#2b2252") };
            var bright = Px.Hex("#8c86c8");
            // туманная пыль по той же «картинке», что и звёзды: ядро, кольцо, рукава
            float rot = GalaxyGen.Rot(G.seed);
            for (int tries = 0; dust.Count < 9000 && tries < 200000; tries++)
            {
                float rad = GalaxyGen.R * 1.02f * Mathf.Sqrt(r.F()), ang = r.F() * Px.TAU;
                float x = Mathf.Cos(ang) * rad, y = Mathf.Sin(ang) * rad;
                if (r.F() * 1.15f > Mathf.Max(GalaxyGen.Density(x, y, rot), rad < GalaxyGen.CORE_EMPTY ? .6f : 0)) continue;
                dust.Add(new Dust { x = x, y = y, c = r.Next() < .06 ? bright : DC[r.Int(DC.Length)] });
            }
            bgStars.Clear();
            Color32 c1 = Px.Hex("#c9c4ff"), c2 = Px.Hex("#7d77b8"), c3 = Px.Hex("#46417a");
            for (int i = 0; i < 160; i++)
            {
                float f = .05f + r.F() * .35f;
                bgStars.Add(new BgStar { x = r.F() * 2000, y = r.F() * 2000, f = f, c = f > .3f ? c1 : f > .18f ? c2 : c3 });
            }
        }

        public void CenterOnCurrent()
        {
            camX = Cur.x; camY = Cur.y;
            sel = -1;
            panOn = false;
            pinching = false;
            touches.Clear();
            zoomTarget = -1;
            ResetZoomHint();
        }

        /// <summary>Подхватить пальцы, оставшиеся на экране после перехода из системы: щипок продолжается.</summary>
        public void Adopt(Dictionary<int, Vector2> active)
        {
            touches.Clear();
            foreach (var kv in active) touches[kv.Key] = kv.Value;
            panOn = false;
            gestureDirty = touches.Count > 0; // это продолжение жеста, а не тап по звезде
            if (touches.Count >= 2) BeginPinch();
        }

        void BeginPinch()
        {
            panOn = false; pinching = true; gestureDirty = true;
            pinchD0 = PinchDist(); pinchZ0 = zoom;
            var m = PinchMid();
            pinchWX = camX + (m.x - lastW / 2f) / zoom;
            pinchWY = camY + (m.y - lastH / 2f) / zoom;
        }

        // ---------- прыжки: курс можно сменить прямо в полёте ----------

        bool follow; // камера следует за кораблём, пока игрок сам не сдвинул карту

        /// <summary>Где корабль сейчас: у текущей звезды или на линии прыжка.</summary>
        public Vector2 ShipPos()
        {
            if (travel == null) return new Vector2(Cur.x, Cur.y);
            var b = G.stars[travel.to];
            return new Vector2(travel.fx + (b.x - travel.fx) * travel.t, travel.fy + (b.y - travel.fy) * travel.t);
        }

        float DistFromShip(Star s) { var p = ShipPos(); return Px.Dist(p.x, p.y, s.x, s.y); }

        // при смене курса неизрасходованная часть энергии прежнего прыжка возвращается
        float Refund() => travel == null ? 0 : travel.cost * (1 - travel.t);

        public int TravelCost(Star s) => Faith.FreeJumps ? 0 : Mathf.CeilToInt(DistFromShip(s) * .3f); // «Прозрение» — прыжки без энергии
        public int Distance(Star s) => Px.R(DistFromShip(s));
        public bool InRange(Star s) => DistFromShip(s) <= Range(ShipPos());
        public bool IsDestination(Star s) => travel == null ? s.id == P.cur : s.id == travel.to;
        public bool EnoughEnergy(Star s) => P.energy + Refund() >= TravelCost(s);
        public bool CanFly(Star s) => !IsDestination(s) && InRange(s) && EnoughEnergy(s);

        public void Fly(Star s)
        {
            if (!CanFly(s)) return;
            var from = ShipPos();
            int cost = TravelCost(s);
            P.energy += Refund() - cost;
            travel = new Travel { fx = from.x, fy = from.y, to = s.id, cost = cost };
            follow = true;
        }

        public const float TRAVEL_SPEED = 160; // единиц карты в секунду: прыжок на 95 — 0,6 с, на 310 — 1,9 с
        public const float TRAVEL_MIN = .35f;  // самый короткий прыжок длится не меньше этого

        public void Update(float dt)
        {
            UpdateZoom(dt);
            if (travel == null) return;
            // корабль летит с постоянной скоростью: короткий прыжок быстрее длинного (но не мгновенно)
            var dest = G.stars[travel.to];
            float len = Px.Dist(travel.fx, travel.fy, dest.x, dest.y);
            travel.t += dt / Mathf.Max(TRAVEL_MIN, len / TRAVEL_SPEED);
            if (follow)
            {
                var p = ShipPos();
                camX += (p.x - camX) * .1f;
                camY += (p.y - camY) * .1f;
            }
            if (travel.t >= 1) gm.Arrive(travel.to);
        }

        /// <summary>Звезда под точкой кадра (для наведения мышью) или -1.</summary>
        public int StarAtPx(Vector2 p)
        {
            int best = -1;
            float bd = 8;
            foreach (var s in G.stars)
            {
                float X = (s.x - camX) * zoom + lastW / 2f, Y = (s.y - camY) * zoom + lastH / 2f, d = Px.Dist(X, Y, p.x, p.y);
                if (d < bd) { bd = d; best = s.id; }
            }
            return best;
        }

        // ---------- зум: приближение сверх максимума открывает текущую систему ----------

        // ZMIN — вся галактика в кадре
        public const float ZMIN = .012f, ZMAX = 1.2f, ZDEF = .35f;
        const float CAM_LIM = GalaxyGen.R * 1.05f;
        // при сильном отдалении кольца владельцев и метки посещённых систем сливаются и заслоняют карту империй — прячем их
        const float MARK_ZOOM = .25f, MARK_FADE = .05f; // кольца плавно гаснут в диапазоне MARK_ZOOM ± MARK_FADE

        /// <summary>Насколько видны кольца владельцев: 1 — вблизи, 0 — при сильном отдалении, между ними — плавный переход.</summary>
        static float MarkK(float z) { float t = Mathf.Clamp01((z - (MARK_ZOOM - MARK_FADE)) / (2 * MARK_FADE)); return t * t * (3 - 2 * t); }
        const float OVER_ENTER = 1.4f; // во сколько раз «пережать» максимум щипком
        float over = 1;                 // текущее «пережатие» (для подсказки на карте)
        public void ResetZoomHint() { over = 1; }

        void SetZoom(float z)
        {
            zoomTarget = -1;
            if (z > ZMAX && travel == null)
            {
                over = z / ZMAX;
                if (over >= OVER_ENTER) { over = 1; gm.EnterSystemAnimated(P.cur); return; }
            }
            else over = 1;
            zoom = Mathf.Clamp(z, ZMIN, ZMAX);
        }

        // Колесо задаёт цель, масштаб плавно к ней подтягивается (как в системе).
        public const float WHEEL_STEP = 1.1f;
        float zoomTarget = -1;

        public void ZoomStep(float k)
        {
            float from = zoomTarget > 0 ? zoomTarget : zoom;
            if (k > 1 && from >= ZMAX - 1e-4f && zoom >= ZMAX * .98f)
            {
                if (travel == null) gm.EnterSystemAnimated(P.cur);
                return;
            }
            over = 1;
            zoomTarget = Mathf.Clamp(from * k, ZMIN, ZMAX);
        }

        void UpdateZoom(float dt)
        {
            if (zoomTarget <= 0) return;
            zoom = Mathf.Exp(Mathf.Lerp(Mathf.Log(zoom), Mathf.Log(zoomTarget), 1 - Mathf.Exp(-dt * 12)));
            if (Mathf.Abs(zoom / zoomTarget - 1) < .003f) { zoom = zoomTarget; zoomTarget = -1; }
        }


        // ---------- ввод (координаты в пикселях кадра) ----------

        readonly Dictionary<int, Vector2> touches = new Dictionary<int, Vector2>();
        bool pinching, gestureDirty; // gestureDirty: в жесте был щипок — не считать его тапом
        float pinchD0, pinchZ0, pinchWX, pinchWY;
        int lastW = 200, lastH = 300;

        Vector2 PinchMid()
        {
            Vector2 a = default, b = default;
            int k = 0;
            foreach (var v in touches.Values) { if (k == 0) a = v; else if (k == 1) b = v; k++; }
            return (a + b) / 2;
        }

        float PinchDist()
        {
            Vector2 a = default, b = default;
            int k = 0;
            foreach (var v in touches.Values) { if (k == 0) a = v; else if (k == 1) b = v; k++; }
            return Mathf.Max(1, (a - b).magnitude);
        }

        public void PointerDown(int id, Vector2 p)
        {
            touches[id] = p;
            if (touches.Count >= 2)
            {
                // второй палец — начинаем щипок, пан отменяется
                BeginPinch();
                return;
            }
            if (panOn) return;
            gestureDirty = false;
            panOn = true; panMoved = false; panId = id; pan0 = p; panCX = camX; panCY = camY;
        }

        public void PointerMove(int id, Vector2 p)
        {
            if (touches.ContainsKey(id)) touches[id] = p;
            if (pinching && touches.Count >= 2)
            {
                SetZoom(pinchZ0 * PinchDist() / pinchD0);
                if (gm.mode != GameManager.Mode.Galaxy) return; // вошли в систему
                follow = false;
                // точка галактики под центром щипка остаётся на месте
                var m = PinchMid();
                camX = Mathf.Clamp(pinchWX - (m.x - lastW / 2f) / zoom, -CAM_LIM, CAM_LIM);
                camY = Mathf.Clamp(pinchWY - (m.y - lastH / 2f) / zoom, -CAM_LIM, CAM_LIM);
                return;
            }
            if (!panOn || id != panId) return;
            var d = p - pan0;
            if (d.magnitude > 3) panMoved = true;
            if (panMoved)
            {
                follow = false;
                camX = Mathf.Clamp(panCX - d.x / zoom, -CAM_LIM, CAM_LIM);
                camY = Mathf.Clamp(panCY - d.y / zoom, -CAM_LIM, CAM_LIM);
            }
        }

        public enum Tap { Select, Fly, None }

        /// <param name="tap">Что делает клик без перетаскивания: Select — панель звезды (касания),
        /// Fly — сразу лететь / войти (ЛКМ), None — ничего (ПКМ: информация и так видна при наведении).</param>
        public void PointerUp(int id, Vector2 p, PixelCanvas c, Tap tap = Tap.Select)
        {
            touches.Remove(id);
            if (pinching)
            {
                if (touches.Count < 2) { pinching = false; over = 1; }
                return;
            }
            if (!panOn || id != panId) return;
            panOn = false;
            if (panMoved || gestureDirty) return;
            int best = -1;
            float bd = 12;
            foreach (var s in G.stars)
            {
                float X = (s.x - camX) * zoom + c.W / 2f, Y = (s.y - camY) * zoom + c.H / 2f, d = Px.Dist(X, Y, p.x, p.y);
                if (d < bd) { bd = d; best = s.id; }
            }
            if (tap == Tap.None) return;
            if (tap == Tap.Fly) { if (best >= 0) QuickAction(G.stars[best]); return; }
            sel = best;
            gm.ui.InvalidatePanel();
        }

        /// <summary>ЛКМ по звезде: лететь (в том числе сменить курс в полёте) или войти в текущую систему.</summary>
        void QuickAction(Star s)
        {
            if (travel == null && s.id == P.cur) { gm.EnterSystemAnimated(s.id); return; }
            if (IsDestination(s)) return; // уже летим туда
            if (CanFly(s)) { sel = -1; Fly(s); gm.ui.InvalidatePanel(); return; }
            gm.Toast(!InRange(s) ? (s.core ? "Ядро: долететь можно только по звёздному мосту, с двигателем ур. 4"
                    : PullK(ShipPos()) < .99f ? "Притяжение ядра: здесь прыжок не дальше " + Px.R(Range(ShipPos())) : "Вне дальности прыжка")
                : "Не хватает энергии: нужно " + TravelCost(s));
        }

        // ---------- отрисовка ----------

        // Связи территорий: для каждого владельца — остовное дерево минимальной длины по его звёздам.
        // Обе звезды исследованы — полупрозрачная линия; одна нет — линия тает в её сторону;
        // обе неизвестны — ничего не рисуем.
        // яркость линий: при видимых кольцах — приглушённая; когда кольца скрыты, линии сами показывают территории — как кольца
        const float LINK_A = .3f, LINK_A_FAR = .8f;
        readonly List<Star> linkStars = new List<Star>();
        bool[] inTree = new bool[0];
        float[] bestD = new float[0];
        int[] bestP = new int[0];

        const float ROUTE_ARROWS_ZOOM = .3f; // с какого приближения на карте видны транспортные пути

        /// <summary>Транспортные пути между системами: стрелки чуть правее линии империи, чтобы встречные не накладывались.</summary>
        void DrawRoutes(PixelCanvas c, float z, int W, int H, float tnow)
        {
            foreach (var r in G.routes)
            {
                if (r.InSystem) continue;
                Star sa = G.stars[r.a], sb = G.stars[r.b];
                var a = new Vector2((sa.x - camX) * z + W / 2f, (sa.y - camY) * z + H / 2f);
                var b = new Vector2((sb.x - camX) * z + W / 2f, (sb.y - camY) * z + H / 2f);
                var d = b - a;
                float len = d.magnitude;
                if (len < 12) continue;
                var u = d / len;
                var right = new Vector2(-u.y, u.x) * 3;
                a += u * 5 + right; b -= u * 5 - right;
                RouteEditor.DrawPath(c, a, b, r, tnow, false, .6f);
            }
        }

        /// <summary>Мягкое пятно 3×3 с аддитивным смешиванием: соседние звёзды сливаются в сплошное свечение.</summary>
        static void Splat(PixelCanvas c, int X, int Y, float a)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    c.Add(X + dx, Y + dy, cGlow, a * (dx == 0 && dy == 0 ? 1 : dx == 0 || dy == 0 ? .45f : .2f));
        }

        // Остовные деревья считаются заново, только когда у систем меняются владельцы (войны, захваты, новые империи):
        // пересчёт по всей галактике дорогой, а рисовать готовые рёбра — дёшево.
        readonly List<(Star a, Star b, int o)> linkEdges = new List<(Star, Star, int)>();
        int linkVer = -1;
        GameState linkFor;

        void DrawLinks(PixelCanvas c, float z, int W, int H)
        {
            // пересчёт — только когда у какой-то системы сменился владелец (или началась другая партия)
            if (Star.OwnerVersion != linkVer || linkFor != G) { linkVer = Star.OwnerVersion; linkFor = G; BuildLinks(); }
            foreach (var (a, b, o) in linkEdges)
                Link(c, a, b, o == Owner.PLAYER ? EmpCol : Px.Hex(G.emp[o].col), z, W, H);
        }

        void BuildLinks()
        {
            linkEdges.Clear();
            var byOwner = new Dictionary<int, List<Star>>();
            foreach (var s in G.stars)
            {
                if (s.core || s.owner < Owner.PLAYER) continue;
                if (!byOwner.TryGetValue(s.owner, out var l)) byOwner[s.owner] = l = new List<Star>();
                l.Add(s);
            }
            for (int o = Owner.PLAYER; o < G.emp.Count; o++)
            {
                if (!byOwner.TryGetValue(o, out var own)) continue;
                linkStars.Clear();
                linkStars.AddRange(own);
                int n = linkStars.Count;
                if (n < 2) continue;
                if (inTree.Length < n) { inTree = new bool[n]; bestD = new float[n]; bestP = new int[n]; }
                for (int i = 0; i < n; i++) { inTree[i] = false; bestD[i] = float.MaxValue; bestP[i] = -1; }

                // алгоритм Прима
                bestD[0] = 0;
                for (int step = 0; step < n; step++)
                {
                    int k = -1;
                    for (int i = 0; i < n; i++) if (!inTree[i] && (k < 0 || bestD[i] < bestD[k])) k = i;
                    inTree[k] = true;
                    // все системы владельца связаны, как бы далеко они ни были
                    if (bestP[k] >= 0) linkEdges.Add((linkStars[k], linkStars[bestP[k]], o));
                    for (int i = 0; i < n; i++)
                    {
                        if (inTree[i]) continue;
                        float d = Px.Dist(linkStars[k], linkStars[i]);
                        if (d < bestD[i]) { bestD[i] = d; bestP[i] = k; }
                    }
                }
            }
        }

        void Link(PixelCanvas c, Star a, Star b, Color32 col, float z, int W, int H)
        {
            if (!a.visited && !b.visited) return;
            float ax = (a.x - camX) * z + W / 2f, ay = (a.y - camY) * z + H / 2f;
            float bx = (b.x - camX) * z + W / 2f, by = (b.y - camY) * z + H / 2f;
            float dx = bx - ax, dy = by - ay, len = Mathf.Sqrt(dx * dx + dy * dy);
            // отступ от звёзд: вокруг колец, а когда кольца скрыты — почти до самой звезды, не задевая её пиксель
            float mk = MarkK(z);
            float gap = Mathf.Lerp(1.5f, 4, mk);
            if (len < gap * 2 + 1) return;
            float ux = dx / len * gap, uy = dy / len * gap;
            float la = Mathf.Lerp(LINK_A_FAR, LINK_A, mk);
            c.Line(ax + ux, ay + uy, bx - ux, by - uy, col, a.visited ? la : 0, b.visited ? la : 0);
        }

        public void Draw(PixelCanvas c, float tnow)
        {
            int W = c.W, H = c.H;
            lastW = W; lastH = H;
            float z = zoom;
            int TX(float x) => Px.R((x - camX) * z + W / 2f);
            int TY(float y) => Px.R((y - camY) * z + H / 2f);

            c.Clear(cBg);
            // 0 — отдельные звёзды своих цветов, 1 — цельное свечение галактики (минимальный масштаб)
            float far = Mathf.Clamp01((FAR0 - z) / (FAR0 - FAR1));
            far = far * far * (3 - 2 * far);
            // чем мельче масштаб, тем больше звёзд в одном пикселе — вклад каждой уменьшаем, чтобы преобладал фиолетовый,
            // а до белого светились только самые плотные места
            float glowA = GLOW_A * Mathf.Min(1, (z / GLOW_Z) * (z / GLOW_Z));
            foreach (var d in dust)
            {
                int X = TX(d.x), Y = TY(d.y);
                if (X < 0 || Y < 0 || X >= W || Y >= H) continue;
                c.Pset(X, Y, d.c, 1 - far * .6f);
            }

            int CX = TX(0), CY = TY(0);
            // свечение ядра: большие слабые круги видно и с края галактики
            foreach (var (r, a) in new[] { (1600f, .025f), (900f, .04f), (450f, .06f), (110f, .08f), (70f, .08f), (40f, .1f), (20f, .2f) })
                c.Circle(CX, CY, Mathf.Max(1, Px.R(r * z)), cCoreGlow, a);

            DrawLinks(c, z, W, H);
            if (z >= ROUTE_ARROWS_ZOOM) DrawRoutes(c, z, W, H, tnow);

            var cs = Cur;
            var sp = ShipPos(); // круг дальности — от корабля, в том числе посреди прыжка
            float rr = Range(sp) * z;
            c.Ring(TX(sp.x), TY(sp.y), rr, cRange, Mathf.Clamp(Mathf.FloorToInt(rr * Px.TAU / 4), 24, 2000), tnow * .05f, .45f);

            if (hover >= 0)
            {
                // лёгкое свечение под звездой, на которую наведён курсор
                var hs = G.stars[hover];
                int X = TX(hs.x), Y = TY(hs.y);
                var hc = cWhite; // не цвет звезды (красная выглядит как «недоступно») и не золото (сливается с жёлтыми звёздами)
                float pulse = .5f + .5f * Mathf.Sin(tnow * 4);
                c.Circle(X, Y, 4, hc, .16f + .08f * pulse);
                c.Circle(X, Y, 2, hc, .32f + .12f * pulse);
                c.Ring(X, Y, 5, hc, 12, tnow * .6f, .75f);
            }

            bool blink = Mathf.Sin(tnow * 8) > 0;
            Quests.Marks(qMarks);
            qStars.Clear();
            foreach (var m in qMarks) qStars.Add(m.star);
            foreach (var s in G.stars)
            {
                int X = TX(s.x), Y = TY(s.y);
                if (X < -8 || Y < -8 || X > W + 8 || Y > H + 8) continue;
                if (s.core) { c.Circle(X, Y, 3 + Px.R(Mathf.Sin(tnow * 2)), cWhiteHi); continue; }
                // владелец известен только после посещения; посещённая ничья — четыре серые точки по диагоналям.
                // При сильном отдалении кольца и метки не рисуем: они сливаются, территории видны по линиям связей.
                float mk = MarkK(z);
                if (mk <= 0) { }
                else if (s.owner == Owner.PLAYER) c.Ring(X, Y, 3, EmpCol, 8, 0, mk);
                else if (!s.visited) { }
                else if (Owner.IsEmp(s.owner)) c.Ring(X, Y, 3, Px.Hex(G.emp[s.owner].col), 8, 0, mk);
                else c.Ring(X, Y, 3, cVisited, 4, Mathf.PI / 4, .85f * mk);
                if (s.id == hover && !s.visited && Owner.IsEmp(s.owner)) // только у системы под курсором
                {
                    // радиоволны: в неисследованной системе кто-то живёт (кто именно — узнаете, прилетев)
                    float ph = tnow * .7f + (float)Px.Hash(s.id, 5, 9);
                    for (int w = 0; w < 2; w++)
                    {
                        float t = ph + w * .5f; t -= Mathf.Floor(t);
                        float rad = 3 + t * 8;
                        c.Ring(X, Y, rad, cWave, Mathf.Max(8, Px.R(rad * 2.2f)), 0, (1 - t) * .6f);
                    }
                }
                if (blink && G.raids.Count > 0)
                {
                    // красное — нападение на вас, золотое — на союзника (отношения 90+)
                    Raid rd = null;
                    foreach (var r in G.raids) if (r.star == s.id) { rd = r; break; }
                    if (rd != null && rd.def == Owner.PLAYER) c.Ring(X, Y, 6, cRaid, 12);
                    else if (rd != null && rd.def >= 0 && G.emp[rd.def].rel >= Diplo.ALLY) c.Ring(X, Y, 6, cAllyRaid, 12);
                    else if (rd != null && Faith.SeeAllRaids) c.Ring(X, Y, 6, cAnyRaid, 12); // Конклав видит все нападения
                }
                if (qStars.Contains(s.id))
                {
                    // цель поручения: оранжевые уголки-ромб, медленно «дышат»; видны при любом масштабе
                    int rq = 6 + Px.R(1.5f + 1.5f * Mathf.Sin(tnow * 3));
                    for (int k = 0; k < 4; k++)
                    {
                        float a = k * Mathf.PI / 2 + Mathf.PI / 4;
                        int qx = X + Px.R(Mathf.Cos(a) * rq), qy = Y + Px.R(Mathf.Sin(a) * rq);
                        c.Rect(qx - 1, qy - 1, 2, 2, cQuest);
                    }
                    c.Ring(X, Y, rq - 1, cQuest, 16, -tnow * .8f, .55f);
                }
                float al = s.visited ? 1 : .6f;
                var col = Px.Hex(s.col);
                // при отдалении звёзды тают в фиолетовое свечение, которое в гуще складывается до белого
                if (far < 1) c.Pset(X, Y, col, al * (1 - far));
                if (far > 0)
                {
                    // к центру галактики свечение ярче: там оно сходится в белое
                    float rc = Px.Len(s.x, s.y) / GLOW_CENTER_R;
                    Splat(c, X, Y, glowA * far * (1 + GLOW_CENTER * Mathf.Exp(-rc * rc)));
                }
                if (z > .55f)
                {
                    c.Pset(X - 1, Y, col, al); c.Pset(X + 1, Y, col, al);
                    c.Pset(X, Y - 1, col, al); c.Pset(X, Y + 1, col, al);
                }
            }

            if (sel >= 0)
            {
                var s = G.stars[sel];
                int X = TX(s.x), Y = TY(s.y);
                foreach (var (a, b) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                {
                    c.Rect(X + a * 6 - (a > 0 ? 1 : 0), Y + b * 6, 2, 1, cGold);
                    c.Rect(X + a * 6, Y + b * 6 - (b > 0 ? 1 : 0), 1, 2, cGold);
                }
            }

            if (travel != null)
            {
                float ax = travel.fx, ay = travel.fy;
                Star b = G.stars[travel.to];
                float t = travel.t;
                for (int k = 0; k < 12; k++)
                {
                    float tt = Mathf.Max(0, t - k * .02f);
                    c.Pset(TX(ax + (b.x - ax) * tt), TY(ay + (b.y - ay) * tt), cGold, .5f);
                }
                c.BlitRot(Sprites.Ship, TX(sp.x), TY(sp.y), Mathf.Atan2(b.y - ay, b.x - ax));
            }
            else if (Mathf.Sin(tnow * 5) > -.4f)
            {
                int X = TX(cs.x), Y = TY(cs.y) - 6;
                c.Rect(X - 1, Y - 1, 3, 1, cWhite);
                c.Pset(X, Y, cWhite);
            }

            // щипок сверх максимума: кольцо сжимается к текущей звезде — «проваливаемся» в систему
            if (over > 1 && travel == null)
            {
                float k = (over - 1) / (OVER_ENTER - 1);
                c.Ring(TX(cs.x), TY(cs.y), Mathf.Lerp(60, 8, k), cGold, 36, tnow, .4f + .6f * k);
            }
        }
    }
}

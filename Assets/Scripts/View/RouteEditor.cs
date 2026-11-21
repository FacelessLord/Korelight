using System.Collections.Generic;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    /// <summary>
    /// Окно транспортных путей поверх игрового поля: схема текущей системы (пути между планетами)
    /// или карта своих систем (пути между системами). Режим «+» — рисование нового пути
    /// перетаскиванием от одного узла к другому; в обычном режиме клик по пути удаляет его,
    /// а в системе клик по своей планете делает её приёмной для грузов из других систем.
    /// </summary>
    public sealed class RouteEditor
    {
        readonly GameManager gm;
        public bool Open, Galaxy, Creating;
        public readonly HashSet<string> sel = new HashSet<string>(); // ресурсы для нового пути

        // карта (для галактики): свой сдвиг и масштаб
        float camX, camY, zoom = .1f;
        int W = 200, H = 300;

        // ввод
        int dragFrom = -1;     // узел, от которого тянем новый путь
        Vector2 mouse;
        bool panOn, panMoved;
        Vector2 pan0; float panCX, panCY;
        public int hoverRoute = -1, hoverNode = -1;

        static readonly Color32 cBg = Px.Hex("#05040c"), cWhite = Px.Hex("#ffffff"), cOrbit = Px.Hex("#6c6a80"),
            cGrey = Px.Hex("#4c4a62"), cGold = Px.Hex("#ffcf4a");

        public RouteEditor(GameManager gm)
        {
            this.gm = gm;
            foreach (var k in Routes.Kinds) sel.Add(k); // по умолчанию везём всё
        }

        public void Show(bool galaxy)
        {
            Open = true; Galaxy = galaxy; Creating = false; dragFrom = -1; panOn = false;
            // масштаб и сдвиг — как на обычной звёздной карте
            camX = gm.gal.camX; camY = gm.gal.camY; zoom = gm.gal.zoom;
            gm.ReleaseInput();
            gm.ui.CloseModalSilently();
            gm.ui.Refresh();
        }

        public void Close()
        {
            // карта после окна путей остаётся на том же месте и в том же масштабе
            if (Galaxy) { gm.gal.camX = camX; gm.gal.camY = camY; gm.gal.zoom = zoom; }
            Open = false; Creating = false; dragFrom = -1; gm.ui.HideTip(); gm.ui.Refresh();
        }

        public void ToggleCreate() { Creating = !Creating; dragFrom = -1; gm.ui.Refresh(); }

        /// <summary>Esc: сначала снимает режим «+», затем закрывает окно.</summary>
        public void Escape() { if (Creating) ToggleCreate(); else Close(); }

        // ---------- узлы: планеты системы или свои звёзды ----------

        float SysZ() => Mathf.Min(W, H) / 2f * .82f / (SysR(Cur) + 14);

        List<int> Nodes()
        {
            var L = new List<int>();
            if (Galaxy) { foreach (var s in G.stars) if (s.owner == Owner.PLAYER && s.Loaded) L.Add(s.id); }
            else for (int i = 0; i < Cur.planets.Count; i++) L.Add(i);
            return L;
        }

        Vector2 NodePos(int n)
        {
            if (Galaxy)
            {
                var s = G.stars[n];
                return new Vector2((s.x - camX) * zoom + W / 2f, (s.y - camY) * zoom + H / 2f);
            }
            var pp = PPos(Cur.planets[n]) * SysZ();
            return new Vector2(W / 2f + pp.x, H / 2f + pp.y);
        }

        float NodeR(int n) => Galaxy ? 3 : Mathf.Max(2, Cur.planets[n].size * SysZ());

        const float GAL_RING = 3; // кольцо своей системы на карте путей — такого же размера, как на обычной карте

        /// <summary>Где путь начинается/заканчивается у узла: на кольце системы или у кольца планеты.</summary>
        float EndR(int n) => Galaxy ? GAL_RING : NodeR(n) + 2;

        bool NodeOwned(int n) => Galaxy ? G.stars[n].owner == Owner.PLAYER : Routes.Owned(Cur.planets[n]);

        int NodeAt(Vector2 p)
        {
            int best = -1; float bd = float.MaxValue;
            foreach (var n in Nodes())
            {
                float d = Vector2.Distance(NodePos(n), p);
                if (d <= NodeR(n) + 4 && d < bd) { bd = d; best = n; }
            }
            return best;
        }

        IEnumerable<int> MyRoutes()
        {
            for (int i = 0; i < G.routes.Count; i++)
            {
                var r = G.routes[i];
                if (Galaxy ? !r.InSystem : r.star == Cur.id) yield return i;
            }
        }

        /// <summary>Концы пути на экране (с отступом от узлов).</summary>
        void Ends(Route r, out Vector2 a, out Vector2 b)
        {
            a = NodePos(r.a); b = NodePos(r.b);
            var d = (b - a);
            float len = d.magnitude;
            if (len < 1) return;
            var u = d / len;
            // встречные пути чуть разводим вправо, чтобы не накладывались; одиночный путь рисуем по прямой
            bool reverse = Routes.Find(r.star, r.b, r.a) != null;
            var right = reverse ? new Vector2(-u.y, u.x) * 2 : Vector2.zero;
            a += u * EndR(r.a) + right;
            b -= u * EndR(r.b) - right;
        }

        int RouteAt(Vector2 p)
        {
            int best = -1; float bd = 4;
            foreach (var i in MyRoutes())
            {
                Ends(G.routes[i], out var a, out var b);
                float d = DistSeg(p, a, b);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude < 1e-4f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        // ---------- ввод ----------

        public void PointerDown(Vector2 p, bool left)
        {
            mouse = p;
            if (!left) return;
            int n = NodeAt(p);
            if (Creating && n >= 0)
            {
                if (!NodeOwned(n)) { gm.Toast(Galaxy ? "Пути — только между своими системами" : "Пути — только между своими постройками"); return; }
                dragFrom = n;
                return;
            }
            if (Galaxy) { panOn = true; panMoved = false; pan0 = p; panCX = camX; panCY = camY; }
        }

        public void PointerMove(Vector2 p)
        {
            mouse = p;
            if (!panOn) return;
            var d = p - pan0;
            if (d.magnitude > 3) panMoved = true;
            if (panMoved)
            {
                camX = Mathf.Clamp(panCX - d.x / zoom, -GalaxyGen.R, GalaxyGen.R);
                camY = Mathf.Clamp(panCY - d.y / zoom, -GalaxyGen.R, GalaxyGen.R);
            }
        }

        public void PointerUp(Vector2 p, bool left)
        {
            mouse = p;
            if (!left) return;
            if (dragFrom >= 0)
            {
                int to = NodeAt(p);
                int from = dragFrom;
                dragFrom = -1;
                if (to < 0 || to == from) return;
                if (!NodeOwned(to)) { gm.Toast(Galaxy ? "Пути — только между своими системами" : "Пути — только между своими постройками"); return; }
                if (sel.Count == 0) { gm.Toast("Отметьте, какие ресурсы везти"); return; }
                bool existed = Routes.Find(Galaxy ? -1 : Cur.id, from, to) != null;
                int cost = Routes.CostOf(!Galaxy);
                // новый путь платный, дополнение существующего — бесплатно
                if (!existed && P.credits < cost) { gm.Toast("Новый путь стоит " + cost + " кр. — не хватает кредитов"); return; }
                var r = Routes.AddOrMerge(Galaxy ? -1 : Cur.id, from, to, sel);
                if (!existed) { P.credits -= cost; r.paid += cost; }
                gm.Toast(existed ? "Путь дополнен" : "Путь создан: −" + cost + " кр.");
                Save();
                return;
            }
            bool wasPan = panOn && panMoved;
            panOn = false;
            if (Creating || wasPan) return;
            // обычный режим: клик по пути — удалить, клик по своей планете — сделать приёмной
            // клик по узлу не удаляет подходящий к нему путь
            int ri = NodeAt(p) >= 0 ? -1 : RouteAt(p);
            if (ri >= 0)
            {
                // при удалении возвращаем всё, что было вложено в путь
                int back = G.routes[ri].paid;
                P.credits += back;
                G.routes.RemoveAt(ri);
                hoverRoute = -1;
                gm.Toast(back > 0 ? "Путь удалён: +" + back + " кр." : "Путь удалён");
                Save();
                return;
            }
            if (!Galaxy)
            {
                int n = NodeAt(p);
                if (n >= 0 && NodeOwned(n))
                {
                    // повторный клик по выбранной приёмной планете снимает выбор — снова автоматически
                    if (Routes.IsHubSet(Cur, n)) { Routes.ClearHub(Cur); gm.Toast("Выбор снят: грузы пойдут туда, где больше места"); }
                    else { Routes.SetHub(Cur, n); gm.Toast("«" + Cur.planets[n].name + "» принимает грузы из других систем"); }
                    Save();
                }
            }
        }

        public void Wheel(bool zoomIn)
        {
            if (!Galaxy) return;
            zoom = Mathf.Clamp(zoom * (zoomIn ? 1.15f : 1 / 1.15f), GalaxyView.ZMIN, GalaxyView.ZMAX);
        }

        /// <summary>Наведение: подсветка и текст подсказки (null — без подсказки).</summary>
        public (string title, Color col, string body)? Hover(Vector2 p)
        {
            mouse = p;
            hoverRoute = -1; hoverNode = -1;
            // звезда/планета под курсором важнее пути, который к ней подходит
            if (!Creating && NodeAt(p) < 0)
            {
                hoverRoute = RouteAt(p);
                if (hoverRoute >= 0)
                {
                    var r = G.routes[hoverRoute];
                    // список ресурсов отделён пустыми строками сверху и снизу
                    return (NodeName(r.a) + " → " + NodeName(r.b), Color.white, "\n" + ResList(r.res) + "\n"
                        + "\n" + Routes.BatchOf(r) + " ед. за " + Routes.PERIOD + " с"
                        + "\nКлик — удалить путь" + (r.paid > 0 ? " (+" + r.paid + " кр.)" : ""));
                }
            }
            hoverNode = NodeAt(p);
            if (hoverNode < 0) return null;
            if (Galaxy)
            {
                var s = G.stars[hoverNode];
                int inn = 0, outn = 0;
                foreach (var i in MyRoutes()) { if (G.routes[i].b == s.id) inn++; if (G.routes[i].a == s.id) outn++; }
                return (s.name, (Color)EmpCol, "Входящих путей: " + inn + " · исходящих: " + outn);
            }
            var pl = Cur.planets[hoverNode];
            if (!NodeOwned(hoverNode)) return (pl.name, Color.gray, "Нет ваших построек");
            string body = "Хранилище: " + Colony.Used(pl) + "/" + Colony.Cap(pl);
            bool chosen = Routes.IsHubSet(Cur, hoverNode);
            if (Routes.HubOf(Cur) == hoverNode)
                body += chosen ? "\nСюда приходят грузы из других систем"
                               : "\nСюда приходят грузы из других систем\n(выбрано автоматически: здесь больше места)";
            if (!Creating) body += chosen ? "\nКлик — снять выбор (грузы пойдут туда, где больше места)"
                                          : "\nКлик — принимать здесь грузы из других систем";
            return (pl.name, (Color)EmpCol, body);
        }

        /// <summary>
        /// Отрезок пути под курсором (в пикселях кадра) и сторона, куда отводить подсказку:
        /// противоположная той, где рисуются точки ресурсов.
        /// </summary>
        public bool HoveredSegment(out Vector2 a, out Vector2 b, out Vector2 away)
        {
            a = b = away = Vector2.zero;
            if (hoverRoute < 0 || hoverRoute >= G.routes.Count) return false;
            Ends(G.routes[hoverRoute], out a, out b);
            var u = (b - a).normalized;
            away = -new Vector2(-u.y, u.x); // точки ресурсов лежат по +нормали (см. DrawPath)
            return true;
        }

        string NodeName(int n) => Galaxy ? G.stars[n].name : Cur.planets[n].name;

        /// <summary>Ресурсы пути: каждый на своей строке своим цветом; все виды — белым «Все ресурсы».</summary>
        static string ResList(List<string> res)
        {
            if (res.Count >= Routes.Kinds.Count) return "<color=#ffffff>Все ресурсы</color>";
            var lines = new List<string>();
            foreach (var id in res) { var d = Items.Get(id); lines.Add("<color=" + d.Col + ">" + d.N + "</color>"); }
            return string.Join("\n", lines);
        }

        // ---------- отрисовка ----------

        public void Draw(PixelCanvas c, float tnow)
        {
            W = c.W; H = c.H;
            c.Clear(cBg);
            if (Galaxy) DrawGalaxyMap(c); else DrawSystem(c, tnow);
            DrawPlayer(c, tnow);

            foreach (var i in MyRoutes()) DrawRoute(c, G.routes[i], i == hoverRoute, tnow);

            // тянем новый путь
            if (dragFrom >= 0)
            {
                var a = NodePos(dragFrom);
                c.Line(a.x, a.y, mouse.x, mouse.y, cGold, .9f, .9f);
            }
            if (hoverNode >= 0)
            {
                var p = NodePos(hoverNode);
                c.Ring(Px.R(p.x), Px.R(p.y), NodeR(hoverNode) + 4, Creating ? cGold : cWhite, 16, tnow, .8f);
            }
        }

        void DrawSystem(PixelCanvas c, float tnow)
        {
            var s = Cur;
            float z = SysZ();
            int cx = W / 2, cy = H / 2;
            foreach (var p in s.planets)
            {
                float r = p.orbit * z;
                int n = Mathf.Max(12, Mathf.FloorToInt(r * Px.TAU / 4));
                for (int i = 0; i < n; i += 2) // пунктир
                {
                    float a = (float)i / n * Px.TAU;
                    c.Pset(Px.R(cx + Mathf.Cos(a) * r), Px.R(cy + Mathf.Sin(a) * r), cOrbit, .6f);
                }
            }
            c.Circle(cx, cy, Mathf.Max(2, Px.R((s.core ? 26 : 12) * z)), cWhite);
            int hub = Routes.HubOf(s);
            for (int i = 0; i < s.planets.Count; i++)
            {
                var pos = NodePos(i);
                int X = Px.R(pos.x), Y = Px.R(pos.y), r = Px.R(NodeR(i));
                bool own = NodeOwned(i);
                c.Circle(X, Y, r, cWhite, own ? 1 : .3f);
                if (own) c.Ring(X, Y, r + 2, EmpCol, 12, 0, .8f);
                if (i == hub)
                {
                    // приёмная планета: стрелка «вниз» над ней — золотая, если выбрана вами, серая, если выбрана автоматически
                    var hc = Routes.IsHubSet(s, i) ? cGold : cOrbit;
                    int ty = Y - r - 6;
                    c.Rect(X - 2, ty, 5, 1, hc); c.Rect(X - 1, ty + 1, 3, 1, hc); c.Pset(X, ty + 2, hc);
                }
            }
        }

        /// <summary>Где сейчас корабль: на карте — с учётом прыжка, в системе — его место у планет.</summary>
        void DrawPlayer(PixelCanvas c, float tnow)
        {
            Vector2 p; float a;
            if (Galaxy)
            {
                var sp = gm.gal.ShipPos();
                p = new Vector2((sp.x - camX) * zoom + W / 2f, (sp.y - camY) * zoom + H / 2f);
                var tr = gm.gal.travel;
                a = tr != null ? Mathf.Atan2(G.stars[tr.to].y - tr.fy, G.stars[tr.to].x - tr.fx) : 0;
            }
            else
            {
                var sh = gm.sys.ship;
                float z = SysZ();
                p = new Vector2(W / 2f + sh.x * z, H / 2f + sh.y * z);
                a = sh.a;
            }
            int X = Px.R(p.x), Y = Px.R(p.y);
            if (!Galaxy) c.BlitRot(Sprites.Ship, X, Y, a, Mathf.Max(.5f, SysZ())); // на карте хватает метки
            // мигающая метка над кораблём, как на обычной карте
            if (Mathf.Sin(tnow * 5) > -.4f) { c.Rect(X - 1, Y - 8, 3, 1, cWhite); c.Pset(X, Y - 7, cWhite); }
        }

        void DrawGalaxyMap(PixelCanvas c)
        {
            // ярко — только свои системы, остальные серые
            foreach (var s in G.stars)
            {
                var p = new Vector2((s.x - camX) * zoom + W / 2f, (s.y - camY) * zoom + H / 2f);
                int X = Px.R(p.x), Y = Px.R(p.y);
                if (X < -4 || Y < -4 || X > W + 4 || Y > H + 4) continue;
                if (s.owner == Owner.PLAYER && s.Loaded)
                {
                    // кольцо империи вокруг своей системы — пути упираются в него, а не обрываются в пустоте
                    c.Ring(X, Y, GAL_RING, EmpCol, 8); // как кольцо колонии на обычной карте
                    c.Pset(X, Y, cWhite);
                }
                else c.Pset(X, Y, cGrey);
            }
        }

        void DrawRoute(PixelCanvas c, Route r, bool hover, float tnow)
        {
            Ends(r, out var a, out var b);
            DrawPath(c, a, b, r, tnow, hover, hover ? 1 : .75f);
        }

        const float COLOR_SWITCH = 1.2f; // через сколько секунд путь с несколькими ресурсами меняет цвет
        const float DASH_LEN = 2, DASH_PERIOD = 4, DASH_SPEED = 8; // пунктир: длина штриха, шаг, скорость (пикс/с)

        /// <summary>
        /// Цвет пути: все виды ресурсов — белый; иначе цвет ресурса, а если их несколько —
        /// периодически переключается между ними.
        /// </summary>
        public static Color32 PathColor(Route r, float tnow)
        {
            if (r.res.Count == 0 || r.res.Count >= Routes.Kinds.Count) return cWhite;
            int k = Mathf.FloorToInt(tnow / COLOR_SWITCH) % r.res.Count;
            return Routes.ColorOf(r.res[k]);
        }

        /// <summary>Путь — бегущий в его сторону пунктир; при наведении у середины — точки всех перевозимых ресурсов.</summary>
        public static void DrawPath(PixelCanvas c, Vector2 a, Vector2 b, Route r, float tnow, bool hover, float alpha)
        {
            var col = PathColor(r, tnow);
            var d = b - a;
            float len = d.magnitude;
            if (len < 1) return;
            var u = d / len;
            // пунктир бежит от начала пути к концу — так видно направление
            float shift = (tnow * DASH_SPEED) % DASH_PERIOD;
            for (float s = shift - DASH_PERIOD; s < len; s += DASH_PERIOD)
            {
                float s0 = Mathf.Max(0, s), s1 = Mathf.Min(len, s + DASH_LEN);
                if (s1 <= s0) continue;
                var p0 = a + u * s0; var p1 = a + u * s1;
                c.Line(p0.x, p0.y, p1.x, p1.y, col, alpha, alpha);
            }
            if (!hover || len < 6) return;
            var mid = (a + b) / 2;
            var n = new Vector2(-u.y, u.x);
            for (int k = 0; k < r.res.Count; k++)
            {
                // точки ресурсов — по центру пути, поровну в обе стороны от середины
                var q = mid + u * ((k - (r.res.Count - 1) / 2f) * 3) + n * 4;
                c.Rect(Px.R(q.x), Px.R(q.y), 2, 2, Routes.ColorOf(r.res[k]));
            }
        }
    }
}

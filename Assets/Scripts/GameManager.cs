using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using static PixelSpace.Game;

namespace PixelSpace
{
    /// <summary>Точка входа: камера, UI, главный цикл, переключение сцен «система»/«галактика».</summary>
    public sealed class GameManager : MonoBehaviour
    {
        public enum Mode { System, Galaxy }

        public static GameManager I { get; private set; }

        public Ui ui;
        public SystemView sys;
        public RouteEditor routes;
        public GalaxyView gal;
        public Mode mode = Mode.System;
        public bool Dead;

        readonly PixelCanvas canvas = new PixelCanvas();
        int S = 2, sw, sh, frame;
        float tnow;

        public bool Paused => ui.ModalOpen || tr != null || (routes != null && routes.Open);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameManager>() == null) new GameObject("Game").AddComponent<GameManager>();
        }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Px.HexC("#03020a");
            cam.cullingMask = 0;
            cam.orthographic = true;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            Battles.Toast = Toast;
            Faith.Swarmed = () =>
            {
                if (mode == Mode.System && !Dead) Battles.SpawnDrones(Battles.Get(P.cur), new Vector2(sys.ship.x, sys.ship.y), 12);
            };
            Battles.Capitulated = Menus.Capitulation;
            sys = new SystemView(this);
            routes = new RouteEditor(this);
            gal = new GalaxyView(this);
            ui = new Ui(this);
            ui.worldInput.Down = e => OnPointer(0, e);
            ui.worldInput.Move = e => OnPointer(1, e);
            ui.worldInput.Up = e => OnPointer(2, e);
            CheckResize();

            if (Game.Load())
            {
                Inv.Migrate();
                EmpireGen.Update(new Vector2(Cur.x, Cur.y));
                Sprites.ResetCaches();
                gal.InitDerived();
                EnterSystem(P.cur);
                // старое сохранение, где империю ещё не называли
                if (string.IsNullOrWhiteSpace(P.empireName)) Menus.AskEmpireName(Menus.Close);
            }
            else Menus.WorldSetup(null); // первый запуск: сначала настройки новой галактики
        }

        /// <summary>Создать галактику с настройками, войти на Родину и показать заставку.</summary>
        public void StartNewGame(int seed, float empScale)
        {
            NewGame(seed, empScale);
            ui.CloseModalSilently();
            EnterSystem(P.cur);
            Menus.Intro();
        }

        public void NewGame(int seed, float empScale)
        {
            Game.NewGame(seed, empScale);
            ui.ClearNotes();
            Inv.Migrate();
            EmpireGen.Update(new Vector2(Cur.x, Cur.y));
            Sprites.ResetCaches();
            gal.InitDerived();
            gal.CenterOnCurrent();
            Dead = false;
            sysStar = -1;
        }

        void CheckResize()
        {
            if (Screen.width == sw && Screen.height == sh) return;
            sw = Screen.width; sh = Screen.height;
            S = Mathf.Max(2, Mathf.RoundToInt(Mathf.Min(sw, sh) / 200f));
            canvas.Resize(Mathf.CeilToInt((float)sw / S), Mathf.CeilToInt((float)sh / S));
            ui.SetWorld(canvas.Tex, canvas.W, canvas.H, S);
            sys.SetViewport(canvas.W, canvas.H);
        }

        public void Toast(string s) => ui.Toast(s);

        public void After(float sec, Action a) => StartCoroutine(AfterCo(sec, a));

        static IEnumerator AfterCo(float sec, Action a)
        {
            yield return new WaitForSecondsRealtime(sec);
            a();
        }

        public void ReleaseInput()
        {
            sys.ReleaseInput();
            ui?.ReleaseFire();
            mouseFire = false;
        }

        // ---------- сцены ----------

        // звезда, чья система сейчас загружена в sys (-1 — нет): возврат с карты сохраняет позицию и бой
        int sysStar = -1;

        float pullAt = -1; // когда показать окно о притяжении ядра (-1 — не нужно)

        /// <summary>Прилёт к звезде: остаёмся на карте, система открывается приближением.</summary>
        public void Arrive(int id)
        {
            var s = G.stars[id];
            P.cur = id;
            bool fresh = !s.visited;
            s.visited = true;
            Faith.OnEnter(id, fresh); // испытания вер: новые системы, послания, проходы, ядро
            Quests.OnArrive(id);      // поручения: разведка, посольство, курьер
            sysStar = -1;
            gal.travel = null;
            gal.ResetZoomHint();
            ui.InvalidatePanel();
            // впервые залетели туда, где притяжение ядра укорачивает прыжки, — объясняем, что происходит
            // (окно — после остальных окон прилёта: см. Update)
            if (!P.pullSeen && pullAt < 0 && PullK(new Vector2(s.x, s.y)) < .999f) pullAt = Time.realtimeSinceStartup + 1.5f;
            Save();
        }

        public void EnterSystem(int id)
        {
            var s = G.stars[id];
            P.cur = id;
            s.visited = true;
            mode = Mode.System;
            gal.travel = null;
            gal.sel = -1;
            if (id == sysStar)
            {
                ReleaseInput();
                ui.Refresh();
                return;
            }
            sysStar = id;
            sys.Reset(s);
            Faith.OnSystemView();

            Battles.Leave(id); // прежний бой с охраной этой системы не продолжается
            var raid = Battles.RaidAt(id);
            if (raid != null)
            {
                string who = raid.att == Raid.PIRATES ? "Пираты" : G.emp[raid.att].name;
                if (raid.def == Owner.PLAYER) Toast(who + " атакуют колонию! Уничтожьте их!");
                else if (raid.att == Raid.PIRATES) Toast(who + " атакуют " + G.emp[raid.def].name + ". Можно вмешаться");
                else
                {
                    // война двух империй: сначала решите, на чьей вы стороне
                    var wb = Battles.Of(id);
                    if (wb != null && !wb.chosen) After(.6f, () => Menus.WarChoice(id));
                    else Toast(who + " атакует " + G.emp[raid.def].name);
                }
            }
            if (Owner.IsEmp(s.owner))
            {
                var e = G.emp[s.owner];
                if (e.rel < -30 && raid == null)
                {
                    Battles.SpawnGuards(Battles.Get(id), new Vector2(sys.ship.x, sys.ship.y), s.owner, 2 + P.lv.las);
                    Notes.Add(e.name + ": охрана системы " + s.name + " атакует вас!", Notes.ATTACK);
                }
                else if (raid == null) Toast(s.name + " · " + e.name);
            }
            else if (raid == null && !s.core) Toast("Система " + s.name);
            Quests.OnEnterSystem(id, new Vector2(sys.ship.x, sys.ship.y)); // конвой, подбитый корабль, пиратская база, рейд
            if (s.core && !P.won) After(.9f, Menus.Victory);
            else if (s.core) After(.9f, DiploMenu.CoreVisit); // «Наследие предтеч» начинается и принимается в ядре
            ui.Refresh();
            Save();
        }

        // ---------- плавный переход карта ↔ система ----------
        // Система «вырастает» из точки звезды: масштаб системы меняется по экспоненте от крошечного
        // до «вся система в кадре», а карта приближается в той же пропорции, пока система проявляется поверх.

        sealed class Transition
        {
            public bool toSystem;
            public float t;
            public float gz, gx, gy; // масштаб и центр карты на «карточной» стороне перехода
            public float sysZ;       // масштаб системы на «системной» стороне
        }

        Transition tr;
        const float TR_TIME = .8f;

        /// <summary>Вход в систему с анимацией. Камера системы приходит в «вся система в кадре».</summary>
        public void EnterSystemAnimated(int id)
        {
            if (tr != null || gal.travel != null) return;
            var t = new Transition { toSystem = true, gz = gal.zoom, gx = gal.camX, gy = gal.camY };
            EnterSystem(id);
            bool fresh = sys.AutoPilot;
            t.sysZ = sys.zoom = sys.Fit();
            sys.autoZoom = fresh; // после спирали камера сама приблизится к кораблю
            tr = t;
            ReleaseInput();
        }

        /// <summary>Выход на карту с анимацией; карта остаётся в масштабе targetZoom.</summary>
        public void ExitToGalaxy(float targetZoom)
        {
            if (tr != null || Dead || mode != Mode.System) return;
            tr = new Transition { toSystem = false, gz = targetZoom, gx = Cur.x, gy = Cur.y, sysZ = sys.zoom };
            ReleaseInput();
        }

        void UpdateTransition(float dt)
        {
            tr.t = Mathf.Min(1, tr.t + dt / TR_TIME);
            float u = tr.t * tr.t * (3 - 2 * tr.t);
            DrawTransition(tr.toSystem ? u : 1 - u);
            if (tr.t < 1) return;
            var done = tr;
            tr = null;
            if (done.toSystem) { sys.zoom = done.sysZ; sys.Adopt(activeTouches); }
            else { OpenGalaxy(done.gz); gal.Adopt(activeTouches); }

            // прокрутка, сделанная во время анимации, продолжает масштабировать уже новый вид
            int steps = wheelSteps;
            wheelSteps = 0;
            for (int i = 0; i < Mathf.Abs(steps); i++) WheelStep(steps > 0);
        }

        /// <summary>e = 0 — карта, e = 1 — система.</summary>
        void DrawTransition(float e)
        {
            var s = Cur;
            var C = new Vector2(canvas.W / 2f, canvas.H / 2f);
            var star = new Vector2(s.x, s.y);
            float sz0 = 3f / (SysR(s) + 20); // система размером с точку звезды
            float sz = Mathf.Exp(Mathf.Lerp(Mathf.Log(sz0), Mathf.Log(tr.sysZ), e));
            float gz = tr.gz * sz / sz0;

            // экранная точка звезды плавно переезжает из своего места на карте туда, где солнце в системе
            var Sg = (star - new Vector2(tr.gx, tr.gy)) * tr.gz + C;
            var Ss = C - sys.CamCenter(tr.sysZ) * tr.sysZ;
            var Sp = Vector2.Lerp(Sg, Ss, e);

            gal.zoom = gz;
            gal.camX = star.x - (Sp.x - C.x) / gz;
            gal.camY = star.y - (Sp.y - C.y) / gz;
            gal.Draw(canvas, tnow);

            var cen = sys.CamCenter(sz);
            canvas.GlobalAlpha = Mathf.Clamp01((e - .15f) / .6f);
            if (canvas.GlobalAlpha > 0) sys.DrawAt(canvas, tnow, gal.bgStars, sz, cen, Sp + cen * sz);
            canvas.GlobalAlpha = 1;
        }

        /// <summary>Карта по кнопке/клавише — в стандартном масштабе.</summary>
        public void OpenGalaxy() => ExitToGalaxy(GalaxyView.ZDEF);

        void OpenGalaxy(float zoom)
        {
            mode = Mode.Galaxy;
            gal.CenterOnCurrent();
            gal.zoom = zoom;
            ReleaseInput();
            ui.Refresh();
        }

        // ---------- колесо: прокрутка не прерывается переходом между видами ----------

        int wheelSteps; // шаги, накопленные во время анимации перехода (+ приближение, − отдаление)

        void WheelStep(bool zoomIn)
        {
            if (tr != null) { wheelSteps = Mathf.Clamp(wheelSteps + (zoomIn ? 1 : -1), -6, 6); return; }
            if (mode == Mode.Galaxy) gal.ZoomStep(zoomIn ? GalaxyView.WHEEL_STEP : 1 / GalaxyView.WHEEL_STEP);
            else sys.ZoomStep(zoomIn ? SystemView.WHEEL_STEP : 1 / SystemView.WHEEL_STEP);
        }

        // Все пальцы на экране: после перехода щипок подхватывается новым видом без повторного касания.
        readonly Dictionary<int, Vector2> activeTouches = new Dictionary<int, Vector2>();

        public void Die()
        {
            Faith.OnDeath(P.cur);
            Dead = true;
            P.hull = 0;
            var s = sys.ship;
            sys.Boom(s.x, s.y, 50, Px.Hex("#ffffff"), Px.Hex("#ffcf4a"), Px.Hex("#ff7a2a"), Px.Hex("#6fe3ff"));
            After(1.3f, () =>
            {
                int lost = Mathf.FloorToInt(P.credits * .2f);
                P.credits -= lost; P.hull = MaxH(); P.energy = MaxE();
                // эфир и товары гибнут вместе с кораблём; инструменты и артефакты спасательная капсула забирает
                P.items.RemoveAll(st => { var d = Items.Get(st.id); return d == null || d.Kind == ItemKind.Ether || d.Kind == ItemKind.Goods; });
                Action respawn = () => { Dead = false; sysStar = -1; ui.CloseModal(); EnterSystem(G.home); };
                ui.Modal("Корабль уничтожен", new List<string> { "Спасательная капсула доставила вас на Родину.", "Потеряны эфир, товары и " + lost + " кр." },
                    new List<MBtn> { new MBtn("Продолжить", respawn) }, respawn);
            });
        }

        // ---------- ввод ----------

        Vector2 ToPx(Vector2 screen) => new Vector2(screen.x / S, (Screen.height - screen.y) / S);
        Vector2 FromPx(Vector2 px) => new Vector2(px.x * S, Screen.height - px.y * S); // пиксели кадра → экран

        bool mouseFire; // ЛКМ зажата над полем системы

        /// <summary>Мышь: в системе — прицел и подсказка по планете, на карте — карточка звезды при наведении.</summary>
        void UpdateMouse(Mouse mouse)
        {
            sys.aimPx = null;
            sys.hover = -1;
            gal.hover = -1;
            bool active = mouse != null && UiKit.Desktop && G != null && tr == null && !ui.ModalOpen;
            if (routes.Open && mouse != null && !Dead) active = true;
            if (!active || (mode == Mode.System && Dead)) { ui.HideTip(); return; }
            var screen = mouse.position.ReadValue();
            var px = ToPx(screen);
            var hud = HudHint.Current;
            if (hud != null && hud.Get != null)
            {
                // курсор над панелью сверху: её подсказка важнее, мир под ней не подсвечиваем
                var (title, col, body) = hud.Get();
                ui.ShowTip(title, col, body, screen);
                if (mode == Mode.System) sys.aimPx = px;
                return;
            }
            if (routes.Open)
            {
                var tip = routes.Hover(px);
                if (!tip.HasValue) ui.HideTip();
                else if (routes.HoveredSegment(out var sa, out var sb, out var away))
                    // подсказка пути — по другую сторону от линии и точек ресурсов
                    ui.ShowTipAway(tip.Value.title, tip.Value.col, tip.Value.body, screen, FromPx(sa), FromPx(sb), new Vector2(away.x, -away.y));
                else ui.ShowTip(tip.Value.title, tip.Value.col, tip.Value.body, screen);
                return;
            }
            if (mode == Mode.System)
            {
                sys.aimPx = px;
                int pi = sys.PlanetAtPx(px);
                sys.hover = pi;
                if (pi >= 0) { var pl = Cur.planets[pi]; ui.ShowTip(pl.name, Menus.PlanetCol(pl), Menus.PlanetTip(pl), screen, pl); }
                else if (sys.SunAtPx(px)) ui.ShowTip(Cur.name, Px.HexC(Cur.col), Menus.StarClass(Cur), screen);
                else ui.HideTip();
            }
            else
            {
                int si = gal.StarAtPx(px);
                gal.hover = si;
                if (si >= 0) { var st = G.stars[si]; ui.ShowTip(st.name, Menus.StarCol(st), Menus.StarTip(st), screen); }
                else ui.HideTip();
            }
        }

        void OnPointer(int phase, Ptr e)
        {
            if (G == null) return;
            int id = e.id;
            var p = ToPx(e.pos);
            if (e.touch) { if (phase == 2) activeTouches.Remove(id); else activeTouches[id] = p; }
            if (routes.Open)
            {
                bool left = e.touch || e.button == Ptr.LEFT;
                if (phase == 0) routes.PointerDown(p, left);
                else if (phase == 1) routes.PointerMove(p);
                else routes.PointerUp(p, left);
                return;
            }
            if (mode == Mode.Galaxy)
            {
                // мышь: перетаскивание — пан, ЛКМ-клик — лететь (можно и в полёте); информация о звезде — при наведении
                if (phase == 0 && !Paused) gal.PointerDown(id, p);
                else if (phase == 1) gal.PointerMove(id, p);
                else if (phase == 2) gal.PointerUp(id, p, canvas, e.touch ? GalaxyView.Tap.Select : e.button == Ptr.LEFT ? GalaxyView.Tap.Fly : GalaxyView.Tap.None);
                return;
            }
            if (!e.touch)
            {
                // мышь в системе: полёт — WASD; ЛКМ по планете — лететь к ней, по пустому месту — огонь в сторону курсора
                if (e.button != Ptr.LEFT) return;
                if (phase == 0 && !Paused)
                {
                    int pi = sys.PlanetAtPx(p);
                    if (pi >= 0) sys.GoTo(pi);
                    else mouseFire = true;
                }
                else if (phase == 2) mouseFire = false;
                return;
            }
            if (phase == 0 && !Paused) sys.PointerDown(id, p);
            else if (phase == 1) sys.PointerMove(id, p);
            else if (phase == 2) sys.PointerUp(id);
        }

        // ---------- глобальные часы ----------

        void GlobalTick(float dt)
        {
            G.time += dt;
            P.energy = Mathf.Min(MaxE(), P.energy + .4f * dt);
            Sim.Tick(dt, gal.ShipPos()); // добыча и перевозки; дальние системы досчитываются при приближении

            bool inSys = mode == Mode.System && tr == null && !Dead;
            Patrols.InSys = inSys;
            Battles.Tick(dt, inSys ? P.cur : -1, sys.ship, !sys.AutoPilot && sys.ship.inv <= 0);
            Diplo.Tick(dt, gal.ShipPos()); // соседние империи иногда нападают друг на друга
            var fb = inSys ? Battles.Of(P.cur) : null;
            Faith.Tick(dt, fb != null && fb.Foes > 0 && Battles.VsPlayer(fb));
            Quests.Tick(dt, inSys, sys.ship);
            Growth.Tick(dt, gal.ShipPos()); // империи рядом медленно осваивают свободные системы и улучшают планеты

            G.nextRaid -= dt;
            if (G.nextRaid <= 0)
            {
                G.nextRaid = 70 + UnityEngine.Random.value * 70;
                StartRandomRaid();
            }
        }

        /// <summary>Нападение на случайную колонию: пираты или враждебная империя по соседству.</summary>
        void StartRandomRaid()
        {
            var c = new List<(int, int)>();
            foreach (var s in LoadedStars())
            {
                if (s.id == P.cur || Battles.Of(s.id) != null) continue;
                for (int i = 0; i < s.planets.Count; i++)
                {
                    var p = s.planets[i];
                    if (p.owner == Owner.PLAYER && !p.home && Colony.Buildings(p) > 0) c.Add((s.id, i));
                }
            }
            if (c.Count == 0) return;
            var (si, pi) = c[UnityEngine.Random.Range(0, c.Count)];
            var st = G.stars[si];
            // во время войны империя нападает на соседние системы, как пираты; систему с Родиной не захватывают.
            // Без войны империи нападают редко и сами, по своим отношениям с вами (Diplo.Mood)
            int att = Raid.PIRATES;
            if (!st.planets.Exists(p => p.home) && UnityEngine.Random.value < .5f)
            {
                float bd = 900;
                foreach (var o in G.stars)
                {
                    if (!Owner.IsEmp(o.owner) || !Battles.Hostile(o.owner)) continue;
                    float d = Px.Dist(o, st);
                    if (d < bd) { bd = d; att = o.owner; }
                }
            }
            if (att == Raid.PIRATES && UnityEngine.Random.value < Faith.PirateSkip) return; // «Атараксия»: пираты реже
            if (att == Raid.PIRATES && G.quiet.Exists(z => z.until > G.time && Px.Dist(z.x, z.y, st.x, st.y) < 1000) && UnityEngine.Random.value < .6f) return; // база уничтожена — пираты притихли
            G.quiet.RemoveAll(z => z.until <= G.time);
            int n = Battles.DefaultRaid(att) + UnityEngine.Random.Range(0, 3);
            Battles.StartRaid(si, pi, att, n);
            Notes.Add((att == Raid.PIRATES ? "Пираты атакуют" : G.emp[att].name + " атакует") + " колонию в системе " + st.name + "!", Notes.ATTACK);
        }

        // ---------- цикл ----------

        void Update()
        {
            float dt = Mathf.Min(.05f, Time.unscaledDeltaTime);
            frame++;
            tnow = Time.realtimeSinceStartup;
            CheckResize();

            var kb = Keyboard.current;
            Vector2 keys = Vector2.zero;
            bool keyFire = false;
            // Esc: в окне — закрыть или назад; без окна — открыть главное меню
            if (kb != null && G != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (ui.ModalOpen) ui.Escape();
                else if (routes.Open) routes.Escape(); // сначала снимает режим «+», потом закрывает окно
                else if (tr == null && !Dead) Menus.MainMenu();
            }
            else if (kb != null && kb.qKey.wasPressedThisFrame && Trade.HoldOnlyOpen) ui.cards.Close(); // Q — открыть и закрыть трюм
            else if (kb != null && kb.eKey.wasPressedThisFrame && ui.ModalOpen && !ui.cards.Open && ui.ModalTag == "planet") ui.CloseModal(); // E — открыть и закрыть меню планеты
            else if (kb != null && !Paused)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) keys.x -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) keys.x += 1;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) keys.y -= 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) keys.y += 1;
                keyFire = kb.spaceKey.isPressed;
                if (mode == Mode.System)
                    for (int i = 0; i < 9; i++)
                        if (kb[Key.Digit1 + i].wasPressedThisFrame) ui.WeaponKey(i);
                if (kb.fKey.wasPressedThisFrame && !Dead && tr == null && !routes.Open) ui.FaithKey(); // F — способность веры (на карте — только «мирные»)
                if (kb.eKey.wasPressedThisFrame && mode == Mode.System && sys.near >= 0 && !Dead) Menus.Planet(sys.near);
                else if (kb.qKey.wasPressedThisFrame && !Dead) Trade.OpenHold();
                else if (kb.jKey.wasPressedThisFrame) Menus.Journal(); // J — журнал уведомлений
                else if (kb.mKey.wasPressedThisFrame)
                {
                    if (mode == Mode.System) { if (!Dead) OpenGalaxy(); }
                    else if (gal.travel == null) EnterSystemAnimated(P.cur);
                }
            }
            var mouse = Mouse.current;
            if (mouse != null && !ui.ModalOpen)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel != 0) { if (routes.Open) routes.Wheel(wheel > 0); else WheelStep(wheel > 0); }
            }
            UpdateMouse(mouse);

            if (G != null)
            {
                if (!Paused) GlobalTick(dt); // окно путей, как и другие меню, останавливает время
                if (routes.Open) routes.Draw(canvas, tnow); // окно транспортных путей рисуется вместо мира
                else if (tr != null) UpdateTransition(dt);
                else if (mode == Mode.System)
                {
                    sys.fire = ui.FireHeld || mouseFire;
                    if (!Paused) sys.Update(dt, keys, keyFire);
                    sys.Draw(canvas, tnow, gal.bgStars);
                }
                else
                {
                    if (!Paused) gal.Update(dt);
                    gal.Draw(canvas, tnow);
                }
                canvas.Apply();
                // окно о притяжении ядра ждёт, пока закроются другие окна (испытания вер, победа и т. п.)
                if (pullAt > 0 && tnow >= pullAt && !ui.ModalOpen && !routes.Open && tr == null && !Dead)
                {
                    pullAt = -1;
                    P.pullSeen = true;
                    Notes.Add("Притяжение ядра галактики: ближе к центру прыжки короче. Дойти до ядра можно только по звёздным мостам с двигателем ур. 4", Notes.INFO);
                    Menus.CorePull();
                    Save();
                }
                if (frame % 8 == 0) ui.Refresh();
                if (frame % 30 == 0) EmpireGen.Update(gal.ShipPos()); // малые империи возникают вокруг корабля
                if (frame % 600 == 0) Save();
            }
            ui.Tick(dt);
        }

        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() => Save();
    }
}

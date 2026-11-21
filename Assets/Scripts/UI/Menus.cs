using System.Collections.Generic;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    /// <summary>Меню планет, верфь, справка, главное меню.</summary>
    public static class Menus
    {
        static GameManager gm => GameManager.I;
        static void Toast(string s) => gm.Toast(s);
        static void Modal(string t, List<string> l, List<MBtn> b, System.Action esc = null) => gm.ui.Modal(t, l, b, esc);
        public static void Close() => gm.ui.CloseModal();

        static string Bar(int n, string on, string off) => Px.Rep(on, n) + Px.Rep(off, 3 - n);

        // цвет названия в подсказке: по пригодности (от серого к зелёному), газовые гиганты — цветом своей палитры
        static readonly Color[] HabCol = { Px.HexC("#a8a4c0"), Px.HexC("#ffb347"), Px.HexC("#d8f08a"), Px.HexC("#59ffb0") };

        public static Color PlanetCol(PixelSpace.Planet p)
        {
            var T = Defs.Types[p.type];
            return T.Gas ? (Color)T.Pal[3] : HabCol[Habit(p)];
        }

        /// <summary>Тип звезды по спектральной классификации (Морган — Кинан): цвет звезды → класс.</summary>
        public static string StarClass(Star s)
        {
            if (s.core) return "Ядро галактики";
            switch (s.col)
            {
                case "#bcd6ff": return "Бело-голубая звезда · класс B";
                case "#ffffff": return "Белая звезда · класс A";
                case "#fff4c2": return "Жёлто-белая звезда · класс F";
                case "#ffd27a": return "Жёлтый карлик · класс G";
                case "#ff9e5e": return "Оранжевый карлик · класс K";
                case "#ff7a6a": return "Красный карлик · класс M";
                default: return "Звезда";
            }
        }

        /// <summary>Цвет названия звезды в карточке: цвет владельца (если он известен), иначе — цвет самой звезды.</summary>
        public static Color StarCol(Star s) =>
            s.owner == Owner.PLAYER ? (Color)EmpCol : s.visited && Owner.IsEmp(s.owner) ? Px.HexC(G.emp[s.owner].col) : Px.HexC(s.col);


        /// <summary>Карточка звезды при наведении на карте (без названия).</summary>
        public static string StarTip(Star s)
        {
            var gal = gm.gal;
            var sb = new System.Text.StringBuilder();
            if (s.core) sb.Append("Свет древнего маяка в центре галактики");
            else if (!s.visited)
            {
                sb.Append("Не исследована · планет: ?"); // владелец и планеты неизвестны до посещения
                if (Owner.IsEmp(s.owner)) sb.Append("\nСлышны радиосигналы");
            }
            else
            {
                sb.Append(s.owner == Owner.PLAYER ? EmpName
                    : Owner.IsEmp(s.owner) ? G.emp[s.owner].name + " (" + RelStr(G.emp[s.owner].rel) + ")" : "Свободная");
                sb.Append("\nПланет: ").Append(s.visited ? s.planets.Count.ToString() : "?");
                int col = s.planets.FindAll(p => p.owner == Owner.PLAYER).Count;
                if (col > 0) sb.Append(" · колоний: ").Append(col);
                var raid = G.raids.Find(r => r.star == s.id);
                if (raid != null) sb.Append('\n').Append(raid.att == Raid.PIRATES ? "Пираты атакуют колонию!"
                    : raid.def == Owner.PLAYER ? G.emp[raid.att].name + " атакует колонию!" : G.emp[raid.att].name + " атакует " + G.emp[raid.def].name + "!");
                int gave = s.planets.FindAll(p => p.surrendered).Count;
                if (gave > 0 && raid == null) sb.Append("\nСдались планет: ").Append(gave);
            }
            sb.Append('\n');
            if (gal.travel == null && s.id == P.cur) sb.Append("Вы здесь · клик — войти в систему");
            else if (gal.IsDestination(s)) sb.Append("Летим сюда");
            else if (!gal.InRange(s)) sb.Append(s.core ? "Нужен двигатель ур. 4" : "Вне дальности · " + gal.Distance(s) + " св.лет");
            else if (!gal.EnoughEnergy(s)) sb.Append("Не хватает энергии: нужно " + gal.TravelCost(s));
            else sb.Append("Прыжок: ").Append(gal.TravelCost(s)).Append(" энергии · ").Append(gal.Distance(s)).Append(" св.лет");
            return sb.ToString();
        }

        /// <summary>Подпись владельца колонии игрока: название его империи (и пометка для Родины).</summary>
        static string OwnColony(PixelSpace.Planet p) => p.home ? EmpName + " · Родина" : EmpName;

        /// <summary>Короткая подсказка при наведении (без названия): пригодность, владелец, эфир.</summary>
        public static string PlanetTip(PixelSpace.Planet p)
        {
            var T = Defs.Types[p.type];
            var sb = new System.Text.StringBuilder(T.Gas ? "Газовый гигант" : "Пригодность: " + Bar(Habit(p), "★", "☆"));
            if (p.owner == Owner.PLAYER)
                sb.Append('\n').Append(T.Gas ? "Газовые станции · " + EmpName : OwnColony(p))
                  .Append('\n').Append(BuildingsLine(p))
                  .Append(T.Gas ? "\nГаз: " : "\nЭфир: ").Append(Colony.Ether(p))
                  .Append(" · хранилище ").Append(Colony.Used(p)).Append('/').Append(Colony.Cap(p));
            else if (Owner.IsEmp(p.owner))
            {
                var e = G.emp[p.owner];
                if (T.Gas) sb.Append("\nГазовая станция");
                sb.Append('\n').Append(e.name).Append(" (").Append(RelStr(e.rel)).Append(')');
                // у колоний империй своего запаса эфира нет — показываем, почём они его покупают
                if (e.rel >= -30) sb.Append("\nПокупают эфир по ").Append(Px.R(Cur.price * (e.rel >= 60 ? 1.25f : 1))).Append(" кр.");
            }
            else sb.Append("\nСвободна");
            if (Battles.Defended(p))
            {
                if (p.surrendered) sb.Append("\nСдалась");
                else sb.Append("\nЩиты: ").Append(Battles.ShieldsLeft(p)).Append(" из ").Append(Battles.ShieldsOf(p));
            }
            return sb.ToString();
        }

        public static void Planet(int i)
        {
            var s = Cur;
            if (i < 0 || i >= s.planets.Count) return;
            var p = s.planets[i];
            var T = Defs.Types[p.type];
            int hab = Habit(p);
            var L = new List<string>();
            var B = new List<MBtn>();
            void Re() => Planet(i);

            L.Add("Тип: " + T.N);
            if (!T.Gas)
            {
                L.Add("Температура: " + Defs.Temps[p.temp + 3]);
                L.Add("Атмосфера: " + Bar(p.atmo, "♦", "·") + "   Пригодность: " + Bar(hab, "★", "☆"));
            }

            if (p.owner == Owner.PLAYER)
            {
                var ed = Items.Get(Items.EtherOf(p.type)); // у газовой станции — газ
                int have = Colony.Ether(p);
                L.Add(T.Gas ? "Газовые станции · " + EmpName : OwnColony(p));
                L.Add(BuildingsLine(p));
                L.Add("Хранилище: " + Colony.Used(p) + "/" + Colony.Cap(p) + " · " + ed.N.ToLower() + ": " + have);
                B.Add(new MBtn((T.Gas ? "Собрать газ (" : "Собрать эфир (") + have + ")", () =>
                {
                    if (have < 1) { Toast(T.Gas ? "Газ ещё не накопился" : "Эфир ещё не накопился"); return; }
                    int n = Inv.Add(ed.Id, have);
                    if (n <= 0) { Toast("Трюм полон"); return; }
                    Stacks.Remove(p.store, ed.Id, n);
                    Toast("+" + n + " · " + ed.N);
                    Re();
                }));
                if (p.depots > 0) B.Add(new MBtn("Склад", () => Trade.OpenStorage(p, Re)));
                B.Add(new MBtn("Заправка и ремонт", () =>
                {
                    if (P.hull < MaxH()) Quests.OnRepair();
                    P.hull = MaxH(); P.energy = MaxE();
                    Toast("Корабль готов к полёту");
                    Re();
                }, P.hull >= MaxH() && P.energy >= MaxE() - .5f));
                if (T.Gas) B.Add(new MBtn("Магазин станции", () => Trade.OpenShop(Market.ForStation(s, p), Re)));
                else B.Add(new MBtn("Верфь", () => Trade.OpenShop(Market.ForColony(s, p), Re)));
                BuildButtons(s, p, B, Re);
                if (!T.Gas && hab < 3) B.Add(new MBtn("Терраформирование", () => Terraform(i)));
            }
            else if (Owner.IsEmp(p.owner))
            {
                int ei = p.owner;
                var e = G.emp[ei];
                L.Add("Владелец: " + e.name + " — " + RelStr(e.rel) + " (" + e.rel + ")");
                if (e.rel >= -30)
                {
                    if (T.Gas) B.Add(new MBtn("Магазин станции", () => Trade.OpenShop(Market.ForStation(s, p, ei), Re)));
                    else B.Add(new MBtn("Рынок", () => Trade.OpenShop(Market.ForEmpire(s, ei, p), Re)));
                    // платные подзарядка и ремонт, по отдельности; союзникам — скидка
                    float disc = e.rel >= 60 ? .75f : 1;
                    B.Add(Service("Подзарядка", "энергии", P.energy, MaxE(), ENERGY_PRICE * disc, v => P.energy = v, Re));
                    B.Add(Service("Ремонт", "корпуса", P.hull, MaxH(), REPAIR_PRICE * disc, v => { P.hull = v; Quests.OnRepair(); }, Re));
                }
                else L.Add("Они враждебны и не торгуют с вами.");
                if (Quests.TerraTarget(p)) TerraformButtons(p, B, Re); // поручение «Терраформирование»
                B.Add(new MBtn("Дипломатия", () => DiploMenu.Open(ei, Re)));
            }
            else
            {
                if (!p.scanned)
                    B.Add(new MBtn("Сканировать", () =>
                    {
                        Faith.Event("scan");
                        Quests.OnScan(s);
                        if (p.art || Random.value < Faith.ArtChance)
                        {
                            var ad = Items.Get(Items.Artifacts[Random.Range(0, Items.Artifacts.Length)]);
                            if (Inv.Add(ad.Id, 1) == 0) { Toast("Найден артефакт, но трюм полон — освободите место"); return; }
                            Toast("Найден артефакт: " + ad.N + "!");
                            Faith.Event("artifact");
                        }
                        else Toast("Ничего ценного не найдено");
                        p.scanned = true;
                        Re();
                    }));

                if (T.Gas)
                {
                    L.Add("Газовый гигант нельзя колонизировать, но можно поставить газовую станцию или набрать топлива.");
                    bool ok = G.time - p.gt > 90;
                    B.Add(new MBtn(ok ? "Набрать газ (+40 энергии)" : "Газ восстанавливается…", () =>
                    {
                        p.gt = G.time;
                        P.energy = Mathf.Min(MaxE(), P.energy + 40);
                        Toast("+40 энергии");
                        Re();
                    }, !ok));
                    // газовая станция занимает систему, как колония
                    if (s.owner != Owner.NONE && s.owner != Owner.PLAYER) L.Add("Система принадлежит другой империи — станцию здесь не построить.");
                    BuildButtons(s, p, B, Re);
                }
                else
                {
                    TerraformButtons(p, B, Re);
                    if (s.owner != Owner.NONE && s.owner != Owner.PLAYER) L.Add("Система принадлежит другой империи — колонию здесь не основать.");
                    else if (hab < 1) L.Add("Для колонии нужна пригодность ★ хотя бы 1 (склад можно поставить и без неё).");
                    BuildButtons(s, p, B, Re);
                }
            }
            B.Add(new MBtn("× Закрыть", Close));
            Modal(p.name, L, B);
            gm.ui.ModalTag = "planet";
        }

        // ---------- постройки игрока: колонии, станции, склады ----------

        static string BuildingsLine(PixelSpace.Planet p)
        {
            bool gas = Defs.Types[p.type].Gas;
            return "Постройки " + Colony.Buildings(p) + "/" + Colony.MaxBuildings(p) + ": " + (gas ? "станций " : "колоний ") + p.colonies + ", складов " + p.depots;
        }

        /// <summary>Кнопки постройки колонии/станции и склада (всего до трёх построек на планете).</summary>
        static void BuildButtons(Star s, PixelSpace.Planet p, List<MBtn> B, System.Action re)
        {
            bool gas = Defs.Types[p.type].Gas;
            bool allowed = s.owner == Owner.NONE || s.owner == Owner.PLAYER;
            bool room = Colony.CanBuild(p);
            bool first = Colony.Buildings(p) == 0;
            string mainId = gas ? "station" : "kit";
            int mainHave = Inv.Count(mainId), depotHave = Inv.Count("depot");
            bool habOk = gas || Habit(p) >= 1;

            if (room)
            {
                string mainLabel = gas ? (first ? "Построить станцию" : "Ещё одна станция") : (first ? "Основать колонию" : "Ещё одна колония");
                B.Add(new MBtn(mainLabel + " (есть: " + mainHave + ")", () =>
                {
                    Inv.Remove(mainId, 1);
                    Colony.Build(s, p, false);
                    if (gas) Faith.Event("station");
                    else if (first) { Faith.Event("colony"); if (p.type == "garden") Faith.Event("flourish"); }
                    if (first) Quests.OnBuild(s, p); // «Отводок», «Новый улей»
                    string msg = gas ? "Газовая станция на «" + p.name + "» построена" : "Колония на «" + p.name + "» основана";
                    if (first) Notes.Add(msg + " (" + s.name + ")", Notes.WIN); else Toast(msg + "!");
                    Save();
                    re();
                }, !(allowed && room && habOk && mainHave > 0)));
                B.Add(new MBtn((first ? "Построить склад" : "Ещё один склад") + " (есть: " + depotHave + ")", () =>
                {
                    Inv.Remove("depot", 1);
                    Colony.Build(s, p, true);
                    Toast("Склад на «" + p.name + "» построен!");
                    Save();
                    re();
                }, !(allowed && room && depotHave > 0)));
            }
        }

        // ---------- услуги империй ----------

        const float ENERGY_PRICE = .25f;  // кр. за единицу энергии
        const float REPAIR_PRICE = 3f;    // кр. за единицу прочности корпуса

        /// <summary>Платное пополнение до максимума (или насколько хватит кредитов).</summary>
        static MBtn Service(string name, string what, float cur, float max, float price, System.Action<float> set, System.Action re)
        {
            int need = Mathf.CeilToInt(max - cur);
            if (need <= 0) return new MBtn(name + " — не нужна", null, true);
            int cost = Mathf.CeilToInt(need * price);
            return new MBtn(name + " (+" + need + " " + what + ") — " + cost + " кр.", () =>
            {
                int n = Mathf.Min(need, Mathf.FloorToInt(P.credits / price));
                if (n <= 0) { Toast("Не хватает кредитов"); return; }
                int pay = Mathf.CeilToInt(n * price);
                P.credits -= pay;
                set(Mathf.Min(max, cur + n));
                Toast(n < need ? "Хватило на +" + n + " за " + pay + " кр." : name + ": готово, " + pay + " кр.");
                re();
            });
        }

        // ---------- терраформирование ----------

        /// <summary>Кнопки инструментов терраформирования для планеты.</summary>
        static void TerraformButtons(PixelSpace.Planet p, List<MBtn> B, System.Action re)
        {
            int hab = Habit(p), atmo = Inv.Count("atmo"), heat = Inv.Count("heat"), cool = Inv.Count("cool");
            // инструмент: «Тихий Конклав» иногда его сохраняет; дошли до ★3 — засчитывается «Садовнику миров» и «Простым радостям»
            void Use(string id, System.Action apply, string msg)
            {
                int was = Habit(p);
                if (Random.value >= Faith.ToolSave) Inv.Remove(id, 1); else msg += " · инструмент уцелел";
                apply();
                if (was < 3 && Habit(p) >= 3) Faith.Event("flourish");
                Toast(msg + Habit(p));
                Quests.OnTerraform(p);
                re();
            }
            if (P.starSeed)
                B.Add(new MBtn("Звёздное семя: сделать цветущей", () =>
                {
                    int was = Habit(p);
                    p.atmo = 3; p.temp = 0;
                    if (was < 3) Faith.Event("flourish");
                    Toast("Планета расцвела!");
                    Quests.OnTerraform(p);
                    re();
                }, hab >= 3));
            if (p.atmo < 3)
                B.Add(new MBtn("Атмогенератор (есть: " + atmo + ")", () => Use("atmo", () => p.atmo++, "Атмосфера плотнее. Пригодность: "), atmo < 1));
            if (p.temp < 0)
                B.Add(new MBtn("Нагреватель (есть: " + heat + ")", () => Use("heat", () => p.temp++, "Теплее. Пригодность: "), heat < 1));
            if (p.temp > 0)
                B.Add(new MBtn("Охладитель (есть: " + cool + ")", () => Use("cool", () => p.temp--, "Прохладнее. Пригодность: "), cool < 1));
        }

        /// <summary>Отдельное меню терраформирования своей колонии (пока она не максимально пригодна).</summary>
        public static void Terraform(int i)
        {
            var p = Cur.planets[i];
            int hab = Habit(p);
            if (hab >= 3) { Toast("Колония «" + p.name + "» полностью пригодна"); Planet(i); return; }
            var L = new List<string>
            {
                "Температура: " + Defs.Temps[p.temp + 3],
                "Атмосфера: " + Bar(p.atmo, "♦", "·") + "   Пригодность: " + Bar(hab, "★", "☆"),
                "Выше пригодность — больше эфира и вместимость (сейчас " + CapOf(p) + " на колонию, " + CapOf(p) * Colony.DEPOT_MULT + " на склад).",
            };
            var B = new List<MBtn>();
            TerraformButtons(p, B, () => { Save(); Terraform(i); });
            if (B.Count == 0) L.Add("Нужных инструментов здесь нет — их продают на верфи и рынках империй.");
            B.Add(new MBtn("◀ Назад", () => Planet(i)));
            Modal("Терраформирование · " + p.name, L, B, () => Planet(i));
        }

        static readonly List<string> HELP = new List<string>
        {
            "Карта: ЛКМ по звезде внутри зелёного круга дальности — сразу лететь (прыжок тратит энергию, она понемногу восстанавливается); курс можно сменить прямо в полёте. Наведите курсор на звезду — появится информация о ней. Перетаскивание — сдвиг карты, колесо — масштаб. ЛКМ по своей звезде или приближение сверх максимума — вход в систему.",
            "В системе: WASD или стрелки — полёт, ЛКМ или пробел — огонь в сторону курсора. ЛКМ по планете — лететь к ней. Подлетите к планете и нажмите E или кнопку с её названием внизу. Колесо — масштаб; если отдалить дальше, чем видна вся система, откроется карта.",
            "Терраформинг: атмосфера и температура дают пригодность ★. Для колонии нужна ★1+. Инструменты продаются на верфи в ваших колониях.",
            "Колонии добывают эфир — у каждого типа планет свой вид. Собирайте его в трюм и продавайте на рынках империй. Товары в разных системах стоят по-разному: покупайте дёшево, продавайте дорого. Shift — по 10 штук за клик, Ctrl — всё сразу.",
            "Транспорт: кнопка «Пути» в системе или на карте. «+» — нарисовать путь стрелкой от одной своей планеты (системы) к другой, отметив, что везти; клик по пути — удалить. Грузы из других систем приходят на приёмную планету системы.",
            "Пираты и враждебные империи нападают на колонии — бой идёт, даже если вас там нет (красный мигающий круг на карте). У планеты столько щитов, сколько построек; после первого попадания в каждый щит с неё вылетают защитники.",
            "Война: в системе враждебной империи сначала уничтожьте охрану, затем пробейте щиты планет. Сдались все планеты — система ваша. Пираты не захватывают, а разрушают по постройке за каждый щит.",
            "Цель: улучшите двигатель до ур. 4 и долетите до Ядра Галактики в центре.",
            "Клавиши: WASD/стрелки — полёт, пробел — огонь, E — планета, Q — трюм, M — карта, Esc — меню, закрыть окно или назад.",
        };

        public static void Help(System.Action back = null) =>
            Modal("Как играть", HELP, new List<MBtn> { new MBtn("◀ Назад", back ?? (System.Action)Close) }, back);

        public static void MainMenu()
        {
            var L = new List<string> { EmpName + " · колоний: " + ColonyCount() + " · двигатель ур. " + P.lv.eng,
                "Список империй — в журнале, вкладка «Империи»." };
            Modal("Меню", L, new List<MBtn>
            {
                new MBtn("Моя империя", EmpireMenu),
                new MBtn("Журнал уведомлений", () => Journal(MainMenu)),
                new MBtn("Как играть", () => Help(MainMenu)),
                new MBtn("Новая игра", () => Modal("Начать заново?", new List<string> { "Текущий прогресс будет потерян." }, new List<MBtn>
                {
                    new MBtn("Да, к настройкам галактики", () => WorldSetup(MainMenu)),
                    new MBtn("Нет", MainMenu),
                }, MainMenu)),
                new MBtn("× Закрыть", Close),
            });
        }

        // ---------- настройки новой галактики ----------

        static float setupScale = GameState.DEFAULT_EMP_SCALE;
        static readonly float[] EmpScales = { .5f, 1, 1.5f, 2, 3, 4 };

        /// <summary>Настройки перед созданием галактики. back — куда вернуться (null — первый запуск, назад некуда).</summary>
        public static void WorldSetup(System.Action back)
        {
            int major = Mathf.RoundToInt(150 * setupScale);
            int s0 = Mathf.Max(3, Mathf.RoundToInt(3 * setupScale)), s1 = Mathf.Max(3, Mathf.RoundToInt(7 * setupScale)), s2 = Mathf.Max(3, Mathf.RoundToInt(30 * setupScale));
            var L = new List<string>
            {
                "Размер империй: ×" + setupScale.ToString("0.#"),
                "Большие империи — около " + major + " систем, малые — чаще " + s0 + "–" + s1 + ", изредка до " + s2 + ".",
            };
            var B = new List<MBtn>();
            foreach (var k in EmpScales)
            {
                float v = k;
                B.Add(new MBtn((Mathf.Approximately(v, setupScale) ? "√ " : "   ") + "Империи ×" + v.ToString("0.#"), () => { setupScale = v; WorldSetup(back); }));
            }
            B.Add(new MBtn("▶ Создать галактику", () => gm.StartNewGame(Random.Range(0, 1000000000), setupScale)));
            if (back != null) B.Add(new MBtn("◀ Назад", back));
            Modal("Новая галактика", L, B, back ?? Ui.NoEsc);
        }

        public static void Intro() => Modal("KORELIGHT", new List<string>
        {
            "Ваша цивилизация только что построила первый звёздный корабль.",
            "Исследуйте галактику, терраформируйте миры, основывайте колонии, торгуйте с инопланетянами и отбивайтесь от пиратов.",
            "Первый шаг: охладите пустынную планету рядом с Родиной и основайте там колонию.",
        }, new List<MBtn> { new MBtn("▶ Начать", () => AskEmpireName(Close)), new MBtn("Как играть", () => Help(Intro)) },
            () => AskEmpireName(Close)); // Esc на заставке — как «Начать»: имя империи всё равно нужно

        /// <summary>Спросить название империи игрока; then — после подтверждения, cancel — «Назад» (null — ввод обязателен).</summary>
        public static void AskEmpireName(System.Action then, System.Action cancel = null)
        {
            string def = string.IsNullOrWhiteSpace(P.empireName) ? "Земная Федерация" : P.empireName;
            gm.ui.Prompt("Название вашей империи", new List<string> { "Так вас будут называть в галактике. Оно показывается на ваших колониях и системах." },
                def, 24, "▶ Готово", s =>
                {
                    s = s.Trim();
                    if (s.Length == 0) { Toast("Введите название"); return; }
                    P.empireName = s;
                    Save();
                    then();
                }, cancel);
        }

        /// <summary>Своя империя: переименование и выбор цвета.</summary>
        public static void EmpireMenu()
        {
            string cur = string.IsNullOrEmpty(P.empireColor) ? DEFAULT_EMP_COLOR : P.empireColor;
            string curName = "";
            foreach (var (col, name) in EmpPalette) if (col == cur) curName = name;
            var L = new List<string> { "Название: " + EmpName, "Цвет: " + curName, "Цвет — у колец ваших колоний и линий вашей территории на карте." };
            var B = new List<MBtn> { new MBtn("Переименовать", () => AskEmpireName(EmpireMenu, EmpireMenu)) };
            foreach (var (col, name) in EmpPalette)
            {
                string c = col;
                B.Add(new MBtn((c == cur ? "√ " : "   ") + name, () =>
                {
                    P.empireColor = c;
                    Save();
                    EmpireMenu();
                }, false, Px.HexC(c)));
            }
            B.Add(new MBtn("◀ Назад", MainMenu));
            Modal("Моя империя", L, B, MainMenu);
        }

        /// <summary>Враг капитулировал: все его планеты в системе сдались.</summary>
        public static void Capitulation(int star, int ei)
        {
            var s = G.stars[star];
            var e = G.emp[ei];
            Save();
            if (s.owner != Owner.PLAYER && Owner.IsEmp(s.owner))
            {
                // «Передача системы»: захваченное ушло заказчику поручения
                Modal("КАПИТУЛЯЦИЯ", new List<string>
                {
                    e.name + " капитулирует в системе " + s.name + ".",
                    "По поручению система передана: " + G.emp[s.owner].name + ".",
                }, new List<MBtn> { new MBtn("Принять", Close) }, Close);
                return;
            }
            if (e.fallen)
            {
                // это была последняя система империи — она сдаётся целиком
                Modal("ИМПЕРИЯ ПАЛА", new List<string>
                {
                    "Пала последняя система: " + s.name + ". " + e.name + " капитулирует полностью.",
                    "Правители увидели силу империи «" + EmpName + "» и признали, что сопротивление бессмысленно. " +
                    "Флот сложил оружие, наместники присягнули новой власти, старые знамёна спущены над всеми мирами.",
                    e.name + " больше не существует. Планеты и постройки бывшей державы теперь служат вам.",
                }, new List<MBtn> { new MBtn("Принять", Close) }, Close);
                return;
            }
            Modal("КАПИТУЛЯЦИЯ", new List<string>
            {
                e.name + " капитулирует в системе " + s.name + ".",
                "Все планеты системы сдались и переходят под управление империи «" + EmpName + "». Их постройки теперь работают на вас.",
            }, new List<MBtn> { new MBtn("Принять", Close) }, Close);
        }

        /// <summary>Журнал важных уведомлений за всю игру, новые сверху.</summary>
        public static void Journal(System.Action back = null) => Journal(0, back);

        /// <summary>Журнал: 0 — уведомления, 1 — империи, 2 — поручения.</summary>
        static void Journal(int tab, System.Action back)
        {
            var L = new List<string>();
            bool empires = tab == 1;
            if (tab == 2)
            {
                var qs = Quests.AllActive();
                if (qs.Count == 0) L.Add("Взятых поручений нет. Их выдают империи в «Дипломатии».");
                foreach (var q in qs)
                {
                    string pr = Quests.Progress(q);
                    L.Add("<color=#ffcf4a>" + Quests.Title(q) + "</color> · " + G.emp[q.ei].name + " · осталось " + Quests.Left(q) + (pr != "" ? " · " + pr : "")
                        + "\n    " + Quests.Desc(q));
                }
            }
            else if (empires) EmpireLines(L);
            else
            {
                if (G.notes.Count == 0) L.Add("Пока ничего важного не произошло.");
                foreach (var n in G.notes) // старые сверху, новые снизу
                {
                    L.Add(UiKit.Sq(Notes.Col(n.lv)) + "<color=#8a86a8>" + Notes.Time(n.t) + "</color>  " + n.text);
                }
            }
            var tabs = new List<Tab>
            {
                new Tab("Уведомления", tab == 0, () => Journal(0, back)),
                new Tab("Империи", tab == 1, () => Journal(1, back)),
                new Tab("Поручения", tab == 2, () => Journal(2, back)),
            };
            System.Action esc = back ?? Close;
            gm.ui.Modal("Журнал", L, new List<MBtn> { new MBtn("◀ Назад", esc) }, esc, richText: true, tabs: tabs);
            if (tab == 0) gm.ui.ScrollModalToEnd(); // сразу видны самые новые
        }

        /// <summary>Только встреченные империи (были в их системе, они нападали) и только посещённые их системы;
        /// павшие — с пометкой в конце.</summary>
        static void EmpireLines(List<string> L)
        {
            var systems = new int[G.emp.Count];
            foreach (var s in G.stars)
            {
                if (!s.visited || !Owner.IsEmp(s.owner) || s.owner >= G.emp.Count) continue;
                systems[s.owner]++;
                G.emp[s.owner].met = true;
            }
            var fallen = new List<string>();
            for (int ei = 0; ei < G.emp.Count; ei++)
            {
                var e = G.emp[ei];
                string mark = UiKit.Sq(e.col); // цветной квадрат во всю высоту строки
                if (!e.met) continue; // о незнакомой империи вы ничего не знаете
                if (e.fallen) { fallen.Add(mark + "<color=#8a86a8>" + e.name + " — пала</color>"); continue; }
                string rc = e.rel < -30 ? "#ff5a6a" : e.rel < 30 ? "#e8e4ff" : "#59ffb0";
                string line = mark + e.name + " — <color=" + rc + ">" + RelStr(e.rel) + "</color> (" + (e.rel > 0 ? "+" : "") + e.rel + ") · посещено систем: " + systems[ei];
                // враждующие соседи — только среди встреченных
                var foes = Diplo.Enemies(ei).FindAll(o => G.emp[o].met).ConvertAll(o => G.emp[o].name);
                if (foes.Count > 0) line += "\n    враждует с: " + string.Join(", ", foes);
                // освоенные системы, о которых вы знаете: освоены у вас на виду или вы там побывали (и они всё ещё её)
                var known = new List<string>();
                for (int k = e.settled.Count - 1; k >= 0; k--)
                {
                    var ss = G.stars[e.settled[k]];
                    if (ss.owner == ei && (ss.visited || e.settledSeen.Contains(ss.id))) known.Add(ss.name);
                }
                if (known.Count > 0)
                    line += "\n    освоила: " + string.Join(", ", known.GetRange(0, Mathf.Min(6, known.Count))) + (known.Count > 6 ? " и ещё " + (known.Count - 6) : "");
                if (Diplo.Discount(ei)) line += "\n    <color=#ffcf4a>скидка " + Mathf.RoundToInt((1 - e.discountK) * 100) + "% ещё " + Mathf.CeilToInt((e.discountUntil - G.time) / 60) + " мин</color>";
                L.Add(line);
            }
            if (L.Count == 0 && fallen.Count == 0) L.Add("Вы ещё не встречали империй.");
            L.AddRange(fallen); // павшие — в конце списка
        }

        /// <summary>Вы прилетели в систему, где одна империя атакует другую: выбрать, кому помогать, а кого атаковать.</summary>
        public static void WarChoice(int star)
        {
            var b = Battles.Of(star);
            if (b == null || b.raid == null || b.chosen || P.cur != star || b.raid.att < 0 || b.raid.def < 0) return;
            var att = G.emp[b.raid.att];
            var def = G.emp[b.raid.def];
            string Rel(Empire e) => e.name + " — " + RelStr(e.rel) + " (" + (e.rel > 0 ? "+" : "") + e.rel + ")";
            void Pick(bool withAtt, bool join)
            {
                b.chosen = true;
                b.withAtt = withAtt;
                if (join) b.joined = true; // нападающие теперь стреляют и по вам
                Close();
                gm.Toast(withAtt ? "Вы атакуете " + def.name + " вместе с " + att.name : join ? "Вы помогаете " + def.name + " отбиться" : "Вы не вмешиваетесь");
            }
            Modal("ЧУЖАЯ ВОЙНА", new List<string>
            {
                att.name + " атакует систему " + G.stars[star].name + " (" + def.name + ").",
                "Нападающие: " + Rel(att) + ".\nОбороняющиеся: " + Rel(def) + ".",
                "Чью сторону вы займёте? Сбитые корабли портят отношения с их империей, а за помощь победившая сторона заплатит и даст скидку.",
            }, new List<MBtn>
            {
                new MBtn("Помочь обороняться: " + def.name, () => Pick(false, true)),
                new MBtn("Напасть вместе с " + att.name, () => Pick(true, false), false, UiKit.Pink),
                new MBtn("Не вмешиваться", () => Pick(false, false)),
            }, () => Pick(false, false));
        }

        /// <summary>Окно о притяжении ядра: показывается один раз, когда дальность впервые начинает падать.</summary>
        public static void CorePull()
        {
            var here = new Vector2(Cur.x, Cur.y);
            Modal("ПРИТЯЖЕНИЕ ЯДРА", new List<string>
            {
                "Корабль слушается всё хуже: здесь уже чувствуется притяжение центра галактики.",
                "Чем ближе к ядру, тем короче прыжки. Сейчас дальность — " + Mathf.RoundToInt(Range(here)) + " вместо " + Mathf.RoundToInt(BaseRange()) + ", а у пустоты вокруг ядра она станет примерно в четыре с половиной раза меньше обычной.",
                "Через пустоту вокруг ядра обычным прыжком не перелететь. Через неё ведут звёздные мосты — короткие цепочки звёзд, — но пройти по ним можно только с двигателем ур. 4.",
            }, new List<MBtn> { new MBtn("Понятно", Close) }, Close);
        }

        public static void Victory()
        {
            P.won = true; P.starSeed = true;
            Notes.Add("Вы первыми достигли центра галактики и получили Звёздное семя", Notes.WIN);
            Save();
            Modal("ЯДРО ГАЛАКТИКИ", new List<string>
            {
                "Вы первыми достигли центра галактики. В сиянии древнего маяка ваш корабль получает Звёздное семя.",
                "Теперь любую свободную каменистую планету можно мгновенно превратить в цветущий мир.",
                "Вы почувствовали устройство вселенной: притяжение ядра больше не укорачивает ваши прыжки.",
                "Игра продолжается — стройте свою империю дальше!",
            }, new List<MBtn> { new MBtn("Ура!", Close) });
        }
    }
}

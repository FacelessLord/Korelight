using System;
using System.Collections.Generic;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    /// <summary>Испытание пути посвящения: Ev — событие, которое засчитывается, Goal — сколько нужно.</summary>
    public sealed class FaithStep
    {
        public string N, D, Ev;
        public int Goal;
        public FaithStep(string n, string d, string ev, int goal) { N = n; D = d; Ev = ev; Goal = goal; }
    }

    public sealed class FaithDef
    {
        public string Id, N, D;      // id, название, суть веры
        public string Art;           // артефакт, который открывает путь (у «Наследия» — особые условия)
        public string Rival;         // соперничающая вера
        public string Col;           // цвет веры
        public FaithStep[] Steps;
        public string AbN, AbD;      // способность: название и описание
        public float AbTime, AbCd;   // действие и откат (от начала), с
        public string Bonus, Minus;  // постоянный бонус и минус — текстом для окна принятия
        public bool MapOk;           // способность можно включить на карте галактики (влияет на то, что там делают)
    }

    /// <summary>
    /// Веры империй. Путь посвящения открывается в «Дипломатии» империи этой веры при дружелюбных отношениях
    /// и с артефактом веры в трюме («Наследие предтеч» — в ядре галактики). Испытания идут по порядку и засчитываются
    /// по событиям игры (Event). Принятая вера даёт способность на время (клавиша F), постоянный бонус и минус;
    /// единоверцы теплеют, империи соперничающей веры — холодеют.
    /// </summary>
    public static class Faith
    {
        public const string FLAME = "flame", BONDS = "bonds", CONCLAVE = "conclave", SWARM = "swarm", ANCIENT = "ancient",
            WILL = "will", STOA = "stoa", MIDDLE = "middle", GARDEN = "garden";

        static FaithStep S(string n, string d, string ev, int goal) => new FaithStep(n, d, ev, goal);

        public static readonly FaithDef[] All =
        {
            new FaithDef { Id = FLAME, N = "Путь Пламени", Art = "art_ember", Rival = BONDS, Col = "#ff7a2a",
                D = "Сила рождается в огне битвы. Кто защищает своих и не боится сражения, тот достоин Пламени.",
                Steps = new[]
                {
                    S("Испытание боем", "сбейте 75 вражеских кораблей", "kill", 75),
                    S("Щит для слабых", "отбейте 5 нападений, сражаясь в той же системе: на свои колонии или на империи, которым помогаете", "defend", 5),
                    S("Огонь завоевания", "захватите 5 вражеских систем", "conquer", 5),
                },
                AbN = "Ярость Пламени", AbTime = 120, AbCd = 900, AbD = "урон всего оружия +50%, расход энергии вдвое меньше",
                Bonus = "Стойкость колоний: на каждый щит ваших колоний вылетает на одного защитника больше.",
                Minus = "Империи веры «Узы Союза» относятся к вам хуже." },

            new FaithDef { Id = BONDS, N = "Узы Союза", Art = "art_seal", Rival = FLAME, Col = "#4fc9ff",
                D = "Мир держится на договорах. Слово дороже оружия, а сделка крепче стены.",
                Steps = new[]
                {
                    S("Честная торговля", "продайте на рынках империй товаров и эфира на 25 000 кр.", "sale", 25000),
                    S("Посредник", "доставьте 5 посланий в столицы других империй (послание берут в «Дипломатии» империи Уз, на доставку — 20 минут)", "mail", 5),
                    S("Голубь над пламенем", "5 раз пройдите через систему враждебной вам империи, не сделав ни одного выстрела", "pass", 5),
                },
                AbN = "Золотой час", AbTime = 180, AbCd = 1200, AbD = "цены во всех магазинах −30%, продажа на 30% дороже",
                Bonus = "У единоверцев цены −15%, продажа +10%; отношения через переговоры меняются раз в 5 минут.",
                Minus = "Слово чести: объявить войну можно только тому, кто нападал на вас за последний час, иначе единоверцы сочтут вас предателем." },

            new FaithDef { Id = CONCLAVE, N = "Тихий Конклав", Art = "art_core", Rival = SWARM, Col = "#c67bff",
                D = "Истина скрыта в прошлом галактики. Знание — единственная власть, что не стареет.",
                Steps = new[]
                {
                    S("Подношение знаний", "отдайте храму Конклава 25 артефактов (в «Дипломатии» империи Конклава)", "donate", 25),
                    S("Картограф", "побывайте в 100 новых системах", "visit", 100),
                    S("Взгляд вглубь", "отсканируйте 50 планет", "scan", 50),
                    S("Свет в центре", "достигните ядра галактики", "core", 1),
                },
                AbN = "Прозрение", MapOk = true, AbTime = 180, AbCd = 1200, AbD = "прыжки не тратят энергию, дальность прыжка +50%",
                Bonus = "Артефакты находятся вдвое чаще, инструменты терраформирования с шансом 30% не тратятся, на карте видны все нападения.",
                Minus = "Хрупкие приборы: максимальная прочность корпуса −20%." },

            new FaithDef { Id = SWARM, N = "Единство Роя", Art = "art_larva", Rival = CONCLAVE, Col = "#ffb347",
                D = "Один — ничто, множество — всё. Расти, заселяй, не останавливайся.",
                Steps = new[]
                {
                    S("Расселение", "основайте 75 колоний", "colony", 75),
                    S("Улей", "постройте 50 газовых станций", "station", 50),
                    S("Неистребимые", "отбейте 5 нападений на свои колонии, не потеряв ни одной постройки", "nolosses", 5),
                },
                AbN = "Роение", AbTime = 90, AbCd = 900, AbD = "рядом с кораблём появляются 12 союзных дронов",
                Bonus = "Колонии добывают на 30% больше, на обычной планете — 4 постройки вместо 3.",
                Minus = "Чужаки: империи других вер не присылают подарков, а их рынки на 20% дороже." },

            new FaithDef { Id = ANCIENT, N = "Наследие предтеч", Col = "#59ffb0",
                D = "Вера без империи — для тех, кто собрал наследие исчезнувшей цивилизации.",
                Steps = new[]
                {
                    S("Археолог", "найдите 25 артефактов", "artifact", 25),
                    S("Садовник миров", "доведите 10 планет до пригодности ★3", "flourish", 10),
                    S("Возвращение к истоку", "5 раз прилетите в ядро галактики с дальнего края галактики", "corereturn", 5),
                },
                AbN = "Эхо предтеч", AbTime = 120, AbCd = 1200, AbD = "урон +25%, расход энергии −25%, корпус восстанавливается на 2 в секунду",
                Bonus = "+10% к урону и добыче колоний, −10% к ценам во всех магазинах, +10% к прочности корпуса и запасу энергии.",
                Minus = "Минуса нет, но и единоверцев у вас не будет." },

            new FaithDef { Id = WILL, N = "Воля к власти", Art = "art_hammer", Rival = MIDDLE, Col = "#ff5a6a",
                D = "Человек — это то, что должно превзойти. Каждое испытание делает сильнее.",
                Steps = new[]
                {
                    S("Преодоление", "сбейте 25 кораблей, когда корпус ниже трети", "lowkill", 25),
                    S("Переоценка всех ценностей", "5 раз объявите войну империи, с которой у вас не было вражды", "warfriend", 5),
                    S("Вечное возвращение", "победите в 5 системах, где ваш корабль был уничтожен", "revenge", 5),
                },
                AbN = "Сверхчеловек", AbTime = 90, AbCd = 900, AbD = "урон по корпусу вдвое меньше, каждый сбитый корабль даёт +5 корпуса и +5 с действия",
                Bonus = "Что не убивает, делает сильнее: пока корпус ниже 30%, урон оружия +30%.",
                Minus = "Гордыня: подарки улучшают отношения вдвое слабее." },

            new FaithDef { Id = STOA, N = "Столб пустоты", Art = "art_stone", Rival = GARDEN, Col = "#b8c6dc",
                D = "Не в нашей власти звёзды, но в нашей власти — наш ответ им.",
                Steps = new[]
                {
                    S("Дихотомия контроля", "пусть ваши колонии 10 раз отобьются сами, без вас", "alone", 10),
                    S("Memento mori", "5 раз опуститесь в бою до 10% корпуса и выживите (почините корпус хотя бы до половины)", "survive", 5),
                    S("Внутренняя крепость", "отбейте 5 нападений на свои колонии, не потеряв ни одного щита", "noshield", 5),
                },
                AbN = "Внутренняя крепость", AbTime = 60, AbCd = 900, AbD = "корпус не получает урона",
                Bonus = "Апатейя: вне боя корпус восстанавливается на 1 в секунду.",
                Minus = "Без страстей: урон всего оружия −10%." },

            new FaithDef { Id = MIDDLE, N = "Срединный путь", Art = "art_wheel", Rival = WILL, Col = "#e8d8a8",
                D = "Страдание рождается из жажды. Избегай крайностей — и обретёшь покой.",
                Steps = new[]
                {
                    S("Непривязанность", "подарите империям 5000 кр.", "gift", 5000),
                    S("Прекращение вражды", "5 раз выведите империю из войны с вами через дипломатию", "peace", 5),
                    S("Восьмеричный путь", "побывайте в 40 новых системах", "visit", 40),
                },
                AbN = "Безмятежность", AbTime = 60, AbCd = 1200, AbD = "враги в этой системе не стреляют",
                Bonus = "Сострадание: награда за помощь в обороне вдвое больше, подарки улучшают отношения в полтора раза сильнее.",
                Minus = "Ненасилие: каждый сбитый вами корабль империи портит отношения с ней вдвое сильнее." },

            new FaithDef { Id = GARDEN, N = "Сад Эпикура", Art = "art_cup", Rival = STOA, Col = "#79c24f",
                D = "Высшее благо — спокойная радость: друзья, достаток и отсутствие страха.",
                Steps = new[]
                {
                    S("Дружба", "имейте одновременно 5 союзников (отношения 60+)", "allies", 5),
                    S("Простые радости", "5 раз основайте колонию на цветущей планете или доведите планету до ★3", "flourish", 5),
                    S("Живи незаметно", "проведите 75 минут игрового времени без единого боя", "calm", 75),
                },
                AbN = "Пир друзей", MapOk = true, AbTime = 180, AbCd = 1200, AbD = "все ваши колонии добывают втрое больше",
                Bonus = "Атараксия: пираты нападают на 40% реже, магазины дружественных империй на 10% дешевле.",
                Minus = "Отвращение к войне: оружие тратит на 25% больше энергии." },
        };

        public static FaithDef Get(string id)
        {
            foreach (var f in All) if (f.Id == id) return f;
            return null;
        }

        public static FaithDef Mine => Get(P.faith);

        static readonly string[] MAJOR = { FLAME, BONDS, CONCLAVE, SWARM };
        static readonly string[] SMALL = { FLAME, BONDS, CONCLAVE, SWARM, WILL, STOA, MIDDLE, GARDEN };

        /// <summary>Вера империи ei.</summary>
        public static string Of(int ei)
        {
            if (ei < 0 || ei >= G.emp.Count) return null;
            var e = G.emp[ei];
            if (e.major)
                for (int i = 0; i < Defs.EmpPrefix.Length; i++)
                    if (e.name.StartsWith(Defs.EmpPrefix[i])) return MAJOR[i];
            return SMALL[(int)(Px.Hash(ei, 17, G.seed + 503) * SMALL.Length) % SMALL.Length];
        }

        public static bool SameFaith(int ei) => !string.IsNullOrEmpty(P.faith) && Of(ei) == P.faith;

        // ---------- путь посвящения ----------

        public const int REL_NEED = 30; // путь открывается при дружелюбных отношениях
        public const int REL_JOIN = 20, REL_RIVAL = -15; // отношения после принятия: с единоверцами и с соперниками

        /// <summary>Артефакты «Наследия предтеч»: святыни всех восьми вер империй (плюс Звёздное семя).</summary>
        public static readonly string[] ANCIENT_ARTS = { "art_ember", "art_seal", "art_core", "art_larva", "art_hammer", "art_stone", "art_wheel", "art_cup" };

        public static bool InPath => !string.IsNullOrEmpty(P.faithPath);
        static FaithDef PathDef => Get(P.faithPath);
        public static bool PathDone => InPath && P.faithStep >= PathDef.Steps.Length;
        public static FaithStep CurStep => InPath && !PathDone ? PathDef.Steps[P.faithStep] : null;
        public static bool Has(string id) => P.faith == id;

        public static bool CanBegin(FaithDef f)
        {
            if (InPath || !string.IsNullOrEmpty(P.faith)) return false;
            if (f.Id == ANCIENT) return P.starSeed && Array.TrueForAll(ANCIENT_ARTS, a => Inv.Count(a) > 0);
            return Inv.Count(f.Art) > 0;
        }

        /// <summary>Начать путь: святыня (артефакт) отдаётся.</summary>
        public static bool Begin(string id)
        {
            var f = Get(id);
            if (f == null || !CanBegin(f)) return false;
            if (f.Id == ANCIENT) foreach (var a in ANCIENT_ARTS) Inv.Remove(a, 1);
            else Inv.Remove(f.Art, 1);
            P.faithPath = id; P.faithStep = 0; P.faithProg = 0; P.calmT = 0;
            Notes.Add(f.N + ": вы ступили на путь посвящения. Первое испытание — «" + f.Steps[0].N + "»: " + f.Steps[0].D, Notes.INFO);
            return true;
        }

        /// <summary>Событие игры: если оно засчитывается текущим испытанием — прогресс.</summary>
        public static void Event(string ev, int n = 1)
        {
            var st = CurStep;
            if (st == null || st.Ev != ev || n <= 0) return;
            P.faithProg += n;
            if (P.faithProg >= st.Goal) StepDone();
        }

        /// <summary>Поручение империи своей веры: текущее испытание продвигается на 5% цели.</summary>
        public static void Boost()
        {
            var st = CurStep;
            if (st == null) return;
            P.faithProg += Mathf.Max(1, Mathf.CeilToInt(st.Goal * .05f));
            if (P.faithProg >= st.Goal) StepDone();
        }

        static void SetProgress(string ev, int v)
        {
            var st = CurStep;
            if (st == null || st.Ev != ev) return;
            P.faithProg = v;
            if (P.faithProg >= st.Goal) StepDone();
        }

        static void StepDone()
        {
            var f = PathDef;
            var done = f.Steps[P.faithStep];
            P.faithStep++; P.faithProg = 0; P.calmT = 0;
            if (P.faithStep < f.Steps.Length)
                Notes.Add(f.N + ": испытание «" + done.N + "» пройдено. Следующее — «" + f.Steps[P.faithStep].N + "»: " + f.Steps[P.faithStep].D, Notes.WIN);
            else
                Notes.Add(f.N + ": все испытания пройдены. " + (f.Id == ANCIENT ? "Вернитесь в ядро галактики" : "Посетите империю этой веры") + ", чтобы принять веру", Notes.WIN);
        }

        /// <summary>Принять веру: единоверцы теплеют, империи соперничающей веры охладевают.</summary>
        public static void Accept()
        {
            if (!PathDone) return;
            var f = PathDef;
            P.faith = P.faithPath; P.faithPath = "";
            P.furyUsed = false;
            for (int ei = 0; ei < G.emp.Count; ei++)
            {
                var e = G.emp[ei];
                if (e.fallen) continue;
                string of = Of(ei);
                if (of == f.Id) e.rel = Mathf.Clamp(e.rel + REL_JOIN, -100, 100);
                else if (f.Rival != null && of == f.Rival) e.rel = Mathf.Clamp(e.rel + REL_RIVAL, -100, 100);
            }
            Notes.Add("Вы приняли веру: " + f.N + (f.Id == ANCIENT ? ". Наследие предтеч теперь с вами" : ". Единоверцы видят в вас своего"), Notes.WIN);
        }

        // ---------- способность веры (клавиша F) ----------

        public static bool Active(string id) => Has(id) && P.furyUsed && G.time - P.furyAt < Mine.AbTime;
        public static bool AbilityActive => Mine != null && Active(P.faith);
        public static float AbilityLeft => AbilityActive ? Mine.AbTime - (G.time - P.furyAt) : 0;
        public static float AbilityCdLeft => Mine != null && P.furyUsed ? Mathf.Max(0, Mine.AbCd - (G.time - P.furyAt)) : 0;

        /// <summary>«Роение»: появляются дроны (стыкуется с боем в SystemView).</summary>
        public static Action Swarmed;

        /// <summary>Включить способность; onMap — игрок на карте галактики (боевые способности там не включаются).</summary>
        public static bool Activate(bool onMap = false)
        {
            if (Mine == null || AbilityCdLeft > 0 || onMap && !Mine.MapOk) return false;
            P.furyAt = G.time; P.furyUsed = true;
            if (Has(SWARM)) Swarmed?.Invoke();
            return true;
        }

        // старые имена для «Ярости Пламени»
        public static bool FuryActive => Active(FLAME);

        // ---------- бонусы и минусы ----------

        /// <summary>Множитель урона всего оружия.</summary>
        public static float DamageMul
        {
            get
            {
                float k = 1;
                if (Active(FLAME)) k *= 1.5f;
                if (Has(ANCIENT)) k *= Active(ANCIENT) ? 1.25f * 1.1f : 1.1f;
                if (Has(WILL) && P.hull < MaxH() * .3f) k *= 1.3f;
                if (Has(STOA)) k *= .9f;
                return k;
            }
        }

        /// <summary>Множитель расхода энергии оружием.</summary>
        public static float EnergyMul
        {
            get
            {
                float k = 1;
                if (Active(FLAME)) k *= .5f;
                if (Active(ANCIENT)) k *= .75f;
                if (Has(GARDEN)) k *= 1.25f;
                return k;
            }
        }

        /// <summary>Множитель урона по корпусу игрока.</summary>
        public static float HullDamageMul => Active(STOA) ? 0 : Active(WILL) ? .5f : 1;

        public static float HullMul => Has(CONCLAVE) ? .8f : Has(ANCIENT) ? 1.1f : 1;
        public static float EnergyCapMul => Has(ANCIENT) ? 1.1f : 1;
        public static float RangeMul => Active(CONCLAVE) ? 1.5f : 1;
        public static bool FreeJumps => Active(CONCLAVE);
        public static float ProdMul => (Has(SWARM) ? 1.3f : 1) * (Has(ANCIENT) ? 1.1f : 1) * (Active(GARDEN) ? 3 : 1);
        public static int ExtraBuildings => Has(SWARM) ? 1 : 0;
        public static int ExtraDefenders => Has(FLAME) ? 1 : 0;
        public static bool Serene => Active(MIDDLE);
        public static float GiftMul => Has(WILL) ? .5f : Has(MIDDLE) ? 1.5f : 1;
        public static float HelpRewardMul => Has(MIDDLE) ? 2 : 1;
        public static int KillRel => Has(MIDDLE) ? -10 : -5;
        public static float PirateSkip => Has(GARDEN) ? .4f : 0;
        public static float TalkCd => Has(BONDS) ? 300 : 600;
        public static bool SeeAllRaids => Has(CONCLAVE);
        public static float ArtChance => Has(CONCLAVE) ? .25f : 0;     // доп. шанс найти артефакт там, где его «нет»
        public static float ToolSave => Has(CONCLAVE) ? .3f : 0;        // шанс, что инструмент не потратится

        /// <summary>Цена покупки в магазине империи ei (-1 — своя верфь).</summary>
        public static float BuyMul(int ei)
        {
            float k = 1;
            if (Active(BONDS)) k *= .7f;
            if (ei >= 0)
            {
                if (Has(BONDS) && SameFaith(ei)) k *= .85f;
                if (Has(SWARM) && !SameFaith(ei)) k *= 1.2f;
                if (Has(GARDEN) && G.emp[ei].rel >= 30) k *= .9f;
            }
            if (Has(ANCIENT)) k *= .9f;
            return k;
        }

        public static float SellMul(int ei)
        {
            float k = 1;
            if (Active(BONDS)) k *= 1.3f;
            if (ei >= 0 && Has(BONDS) && SameFaith(ei)) k *= 1.1f;
            return k;
        }

        /// <summary>Присылает ли империя подарки («Единство Роя» — только единоверцы).</summary>
        public static bool GiftsFrom(int ei) => !Has(SWARM) || SameFaith(ei);

        // ---------- события, которые засчитываются испытаниями ----------

        public static void OnKill()
        {
            Event("kill");
            if (P.hull < MaxH() / 3f) Event("lowkill");
            if (Active(WILL)) { P.hull = Mathf.Min(MaxH(), P.hull + 5); P.furyAt += 5; } // «Сверхчеловек» длится дольше
        }

        public static void OnHurt()
        {
            if (P.hull > 0 && P.hull <= MaxH() * .1f) P.lowHull = true;
        }

        public static void OnDeath(int star)
        {
            P.lowHull = false;
            P.lastSys = -1;
            if (!P.deaths.Contains(star)) P.deaths.Add(star);
            if (P.deaths.Count > 50) P.deaths.RemoveAt(0);
        }

        /// <summary>Победа в системе (охрана перебита или нападение отбито при вас).</summary>
        public static void OnVictory(int star)
        {
            if (P.deaths.Remove(star)) Event("revenge");
        }

        public static void OnFire() => P.firedHere = true;

        /// <summary>Корабль вошёл в саму систему (вид системы), а не только прилетел к звезде на карте.</summary>
        public static void OnSystemView() => P.enteredHere = true;

        /// <summary>Корабль вошёл в систему id (newSystem — впервые).</summary>
        public static void OnEnter(int id, bool newSystem)
        {
            // прошлый проход: через враждебную систему без единого выстрела
            if (P.lastSys >= 0 && P.lastSys != id)
            {
                var ps = G.stars[P.lastSys];
                if (Owner.IsEmp(ps.owner) && Battles.Hostile(ps.owner) && P.enteredHere && !P.firedHere) Event("pass");
            }
            var prev = P.lastSys;
            P.lastSys = id; P.firedHere = false; P.enteredHere = false;
            if (newSystem) Event("visit");
            var s = G.stars[id];
            if (P.mailStar == id)
            {
                P.mailStar = -1;
                Notes.Add("Послание доставлено в систему " + s.name, Notes.INFO);
                Event("mail");
            }
            if (s.core)
            {
                Event("core");
                if (prev >= 0 && new Vector2(G.stars[prev].x, G.stars[prev].y).magnitude >= GalaxyGen.R * .6f) Event("corereturn");
            }
        }

        /// <summary>Взять послание для «Посредника»: в столицу другой империи.</summary>
        public static int TakeMail(int from)
        {
            var c = new List<int>();
            for (int i = 0; i < G.emp.Count; i++)
                if (i != from && Diplo.Alive(i) && G.emp[i].met) c.Add(G.emp[i].capital);
            if (c.Count == 0) for (int i = 0; i < G.emp.Count; i++) if (i != from && Diplo.Alive(i)) c.Add(G.emp[i].capital);
            if (c.Count == 0) return -1;
            P.mailStar = c[(int)(Px.Hash(P.faithProg, (int)G.time, 77) * c.Count) % c.Count];
            P.mailUntil = G.time + 1200;
            return P.mailStar;
        }

        /// <summary>Отдать храму все артефакты из трюма («Подношение знаний»).</summary>
        public static int Donate()
        {
            int n = 0;
            foreach (var s in P.items.ToArray())
            {
                var d = Items.Get(s.id);
                if (d == null || d.Kind != ItemKind.Artifact) continue;
                n += s.n;
                Inv.Remove(s.id, s.n);
            }
            Event("donate", n);
            return n;
        }

        /// <summary>Каждый кадр: таймеры испытаний, восстановление корпуса, послания.</summary>
        public static void Tick(float dt, bool fighting)
        {
            // «Живи незаметно»: минуты без боя подряд
            var st = CurStep;
            if (st != null && st.Ev == "calm")
            {
                if (fighting) { P.calmT = 0; P.faithProg = 0; }
                else
                {
                    P.calmT += dt;
                    while (P.calmT >= 60) { P.calmT -= 60; Event("calm"); }
                }
            }
            if (st != null && st.Ev == "allies")
            {
                int n = 0;
                foreach (var e in G.emp) if (e.met && !e.fallen && e.rel >= 60) n++;
                SetProgress("allies", n);
            }
            if (P.lowHull && P.hull >= MaxH() * .5f) { P.lowHull = false; Event("survive"); }
            if (P.mailStar >= 0 && G.time > P.mailUntil)
            {
                Notes.Add("Послание в систему " + G.stars[P.mailStar].name + " не доставлено вовремя", Notes.LOSS);
                P.mailStar = -1;
            }
            // восстановление корпуса: «Апатейя» вне боя, «Эхо предтеч»
            float regen = (Has(STOA) && !fighting ? 1 : 0) + (Active(ANCIENT) ? 2 : 0);
            if (regen > 0 && P.hull > 0) P.hull = Mathf.Min(MaxH(), P.hull + regen * dt);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    /// <summary>Разовое поручение империи: предложенное или взятое.</summary>
    [Serializable]
    public class Quest
    {
        public string kind;
        public int ei;                  // кто дал
        public int star = -1, planet = -1;
        public int target = -1;         // империя-цель (враг) или звезда назначения
        public string item = "";        // что привезти
        public int goal, prog;
        public float until;             // срок (игровое время) — после взятия
        public float dur;               // сколько даётся времени
        public int reward, rel;
        public bool active;
        public bool flag;               // особое условие нарушено (например, «Дуэль» — чинились)
        public int lost;                // потери (конвой)
        public float t;                 // таймер (патруль, спасение, волны пиратов)
        public List<int> list = new List<int>(); // отмеченные звёзды (разведка, экспедиция)
    }

    /// <summary>
    /// Поручения империй: выдаются в «Дипломатии», 1–3 на выбор, обновляются раз в 20 минут; одно активное у каждой империи.
    /// Каждое поручение приносит пользу и самой империи (скидка, защита, новые системы, сила в войне…).
    /// Поручения веры империи выпадают чаще, и за них ещё и продвигается путь посвящения той же веры.
    /// </summary>
    public static class Quests
    {
        public const string SUPPLY = "supply", PIRATES = "pirates", CONVOY = "convoy", RAID = "raid", TRANSFER = "transfer",
            SCOUT = "scout", ENVOY = "envoy", TERRA = "terra", RELIC = "relic", PATROL = "patrol", RESCUE = "rescue",
            BASE = "base", OUTPOST = "outpost", ENERGY = "energy", TRADE = "trade", COURIER = "courier",
            FEUD = "feud", GUARANTEE = "guarantee", EXPEDITION = "expedition", HIVE = "hive", DUEL = "duel",
            PATIENCE = "patience", RECONCILE = "reconcile", FEAST = "feast";

        public const float OFFER_PERIOD = 1200;
        static readonly System.Random rnd = new System.Random();
        static int R(int a, int b) => rnd.Next(a, b + 1);
        static float Rf() => (float)rnd.NextDouble();

        static readonly string[] COMMON = { SUPPLY, PIRATES, CONVOY, RAID, TRANSFER, SCOUT, ENVOY, TERRA, RELIC, PATROL, RESCUE, BASE, OUTPOST, ENERGY, TRADE, COURIER };

        static readonly Dictionary<string, (string[] fav, string special)> FAITH = new Dictionary<string, (string[], string)>
        {
            [Faith.FLAME] = (new[] { RAID, BASE }, FEUD),
            [Faith.BONDS] = (new[] { SUPPLY, TRADE, ENVOY }, GUARANTEE),
            [Faith.CONCLAVE] = (new[] { RELIC, SCOUT }, EXPEDITION),
            [Faith.SWARM] = (new[] { OUTPOST, TERRA }, HIVE),
            [Faith.WILL] = (new[] { RAID, PATROL }, DUEL),
            [Faith.STOA] = (new[] { PATROL, ENERGY }, PATIENCE),
            [Faith.MIDDLE] = (new[] { ENVOY, RESCUE }, RECONCILE),
            [Faith.GARDEN] = (new[] { SUPPLY, CONVOY }, FEAST),
        };

        // ---------- предложения ----------

        public static Quest Active(int ei) => G.quests.Find(q => q.active && q.ei == ei);
        public static List<Quest> AllActive() => G.quests.FindAll(q => q.active);

        /// <summary>Предложения империи (обновляются раз в 20 минут).</summary>
        public static List<Quest> Offers(int ei)
        {
            var e = G.emp[ei];
            if (!e.questsInit || G.time - e.questsAt >= OFFER_PERIOD)
            {
                e.questsInit = true; e.questsAt = G.time;
                G.quests.RemoveAll(q => !q.active && q.ei == ei);
                // пул: общие (по отношениям), любимые веры — дважды, особое — трижды
                var pool = new List<string>();
                int rel = e.rel;
                foreach (var k in COMMON)
                {
                    if (rel < 0 && k != SUPPLY && k != COURIER && k != TRADE && k != RELIC && k != PIRATES) continue;
                    if (rel < 60 && (k == TRANSFER || k == BASE)) continue;
                    pool.Add(k);
                }
                var fid = Faith.Of(ei);
                if (fid != null && FAITH.TryGetValue(fid, out var fv))
                {
                    foreach (var k in fv.fav) if (pool.Contains(k)) pool.Add(k);
                    if (rel >= 30 || fv.special == RECONCILE) for (int i = 0; i < 3; i++) pool.Add(fv.special);
                }
                int want = rel < 0 ? 1 : rel < 60 ? 2 : 3;
                var used = new HashSet<string>();
                for (int tries = 0; tries < 30 && used.Count < want && pool.Count > 0; tries++)
                {
                    string k = pool[rnd.Next(pool.Count)];
                    if (used.Contains(k)) continue;
                    var q = Make(k, ei);
                    if (q == null) { pool.RemoveAll(x => x == k); continue; }
                    used.Add(k);
                    G.quests.Add(q);
                }
            }
            return G.quests.FindAll(q => !q.active && q.ei == ei);
        }

        // ---------- поиск целей ----------

        static List<Star> EmpStars(int ei)
        {
            var L = new List<Star>();
            foreach (var s in G.stars) if (s.owner == ei && !s.core) L.Add(s);
            return L;
        }

        static Vector2 Pos(Star s) => new Vector2(s.x, s.y);
        static Vector2 Home(int ei) => Diplo.Alive(ei) ? Diplo.Cap(ei) : Vector2.zero;

        static int Enemy(int ei)
        {
            var L = Diplo.Enemies(ei);
            return L.Count > 0 ? L[rnd.Next(L.Count)] : -1;
        }

        static Star NearestOf(int owner, Vector2 from, Func<Star, bool> ok = null)
        {
            Star best = null; float bd = float.MaxValue;
            foreach (var s in G.stars)
            {
                if (s.owner != owner || s.core || s.id == G.home || (ok != null && !ok(s))) continue;
                float d = (Pos(s) - from).sqrMagnitude;
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        /// <summary>Свободная планета у границ империи: газовый гигант или пригодная для колонии.</summary>
        static (Star, int) FreeNear(int ei, bool gas)
        {
            var c = Home(ei);
            var near = new List<Star>();
            foreach (var s in G.stars) if (s.owner == Owner.NONE && !s.core && (Pos(s) - c).magnitude < 900) near.Add(s);
            near.Sort((a, b) => (Pos(a) - c).sqrMagnitude.CompareTo((Pos(b) - c).sqrMagnitude));
            foreach (var s in near.GetRange(0, Mathf.Min(40, near.Count)))
            {
                if (Battles.Of(s.id) != null) continue;
                int pi = s.planets.FindIndex(p => p.owner == Owner.NONE && Defs.Types[p.type].Gas == gas && (gas || Habit(p) >= 1));
                if (pi >= 0) return (s, pi);
            }
            return (null, -1);
        }

        static Star RandomOwn(int ei, Func<Star, bool> ok)
        {
            var L = EmpStars(ei).FindAll(s => ok(s));
            return L.Count > 0 ? L[rnd.Next(L.Count)] : null;
        }

        static readonly string[] GOODS = { "metal", "crystal", "spice" };
        static readonly string[] RELICS = { "art_relic", "art_core", "art_map", "art_idol" };

        public static Quest Make(string k, int ei)
        {
            var q = new Quest { kind = k, ei = ei, rel = 6, dur = 1800 };
            var cap = Home(ei);
            switch (k)
            {
                case SUPPLY:
                {
                    bool ether = Rf() < .5f;
                    if (ether)
                    {
                        string[] types = { "lava", "ice", "desert", "rock", "ocean", "garden" };
                        q.item = Items.EtherOf(types[rnd.Next(types.Length)]);
                        q.goal = R(20, 30);
                    }
                    else { q.item = GOODS[rnd.Next(GOODS.Length)]; q.goal = R(10, 15); }
                    q.reward = Round5(Items.Get(q.item).Price * q.goal * 1.6f);
                    break;
                }
                case PIRATES:
                {
                    var s = RandomOwn(ei, x => Battles.Of(x.id) == null && !x.empHome && x.planets.Exists(p => p.owner == ei));
                    if (s == null) return null;
                    q.star = s.id; q.goal = R(6, 8); q.reward = 120 * q.goal; q.dur = 1200;
                    break;
                }
                case CONVOY:
                {
                    var s = RandomOwn(ei, x => x.planets.Exists(p => p.owner == ei));
                    if (s == null) return null;
                    q.star = s.id; q.planet = s.planets.FindIndex(p => p.owner == ei); q.goal = 2; q.reward = 900;
                    break;
                }
                case RAID:
                case DUEL:
                {
                    int en = Enemy(ei);
                    if (en < 0) return null;
                    var s = NearestOf(en, cap, x => Battles.Of(x.id) == null);
                    if (s == null) return null;
                    q.target = en; q.star = s.id; q.goal = 1; q.reward = k == DUEL ? 1600 : 1200; q.rel = 8;
                    break;
                }
                case TRANSFER:
                {
                    int en = Enemy(ei);
                    if (en < 0) return null;
                    q.target = en; q.goal = 1; q.reward = 3000; q.rel = 12; q.dur = 3600;
                    break;
                }
                case SCOUT:
                {
                    int en = Enemy(ei);
                    if (en < 0 || EmpStars(en).Count < 5) return null;
                    q.target = en; q.goal = 5; q.reward = 700; q.dur = 2400;
                    break;
                }
                case ENVOY:
                {
                    int en = Enemy(ei);
                    if (en < 0 || !Diplo.Alive(en)) return null;
                    q.target = en; q.star = G.emp[en].capital; q.item = "gift_peace"; q.goal = 1; q.reward = 900; q.dur = 2400;
                    break;
                }
                case TERRA:
                {
                    foreach (var s in EmpStars(ei))
                    {
                        if ((Pos(s) - cap).magnitude > 1200) continue;
                        int pi = s.planets.FindIndex(p => p.owner == ei && !Defs.Types[p.type].Gas && Habit(p) < 3);
                        if (pi < 0) continue;
                        q.star = s.id; q.planet = pi; q.goal = Habit(s.planets[pi]) + 1; q.reward = 800; q.dur = 2400;
                        return q;
                    }
                    return null;
                }
                case RELIC:
                    q.item = RELICS[rnd.Next(RELICS.Length)]; q.goal = 1; q.reward = Items.Get(q.item).Price * 3; q.rel = 8; q.dur = 3600;
                    break;
                case PATROL:
                case GUARANTEE:
                case PATIENCE:
                {
                    // пограничная система: самая дальняя от столицы среди своих (не дальше 1200)
                    Star best = null; float bd = -1;
                    foreach (var s in EmpStars(ei))
                    {
                        float d = (Pos(s) - cap).magnitude;
                        if (d < 1200 && d > bd && Battles.Of(s.id) == null) { bd = d; best = s; }
                    }
                    if (best == null) return null;
                    q.star = best.id; q.goal = k == PATROL ? 600 : 900; q.reward = k == PATROL ? 800 : 1200; q.dur = 2400;
                    break;
                }
                case RESCUE:
                {
                    var s = RandomOwn(ei, x => Battles.Of(x.id) == null);
                    if (s == null) return null;
                    q.star = s.id; q.goal = 20; q.reward = 700;
                    break;
                }
                case BASE:
                {
                    Star best = null; float bd = float.MaxValue;
                    foreach (var s in G.stars)
                    {
                        if (s.owner != Owner.NONE || s.core || Battles.Of(s.id) != null) continue;
                        float d = (Pos(s) - cap).magnitude;
                        if (d > 300 && d < 900 && d < bd) { bd = d; best = s; }
                    }
                    if (best == null) return null;
                    q.star = best.id; q.goal = 1; q.reward = 2000; q.rel = 10; q.dur = 2400;
                    break;
                }
                case OUTPOST:
                case HIVE:
                {
                    var (s, pi) = FreeNear(ei, k == OUTPOST);
                    if (s == null) return null;
                    q.star = s.id; q.planet = pi; q.goal = 1; q.reward = k == OUTPOST ? 1200 : 1500; q.rel = 10; q.dur = 2400;
                    break;
                }
                case ENERGY:
                {
                    var r = G.raids.Find(x => x.def == ei);
                    if (r == null) return null;
                    q.star = r.star; q.planet = r.planet; q.item = "battery"; q.goal = R(5, 10); q.reward = 600 + 60 * q.goal; q.dur = 900;
                    break;
                }
                case TRADE:
                    q.goal = 3000; q.reward = 500; q.dur = 2400;
                    break;
                case COURIER:
                {
                    var s = RandomOwn(ei, x => (Pos(x) - cap).magnitude > 300);
                    if (s == null) return null;
                    q.target = s.id; q.item = "parcel"; q.goal = 1; q.reward = 400; q.dur = 600;
                    break;
                }
                case FEUD:
                {
                    int en = Enemy(ei);
                    if (en < 0) return null;
                    q.target = en; q.goal = 10; q.reward = 1500; q.rel = 10; q.dur = 3600;
                    break;
                }
                case EXPEDITION:
                {
                    var far = new List<Star>();
                    foreach (var s in G.stars)
                    {
                        float d = (Pos(s) - cap).magnitude;
                        if (!s.core && d > 1500 && d < 3500 && !s.visited) far.Add(s);
                    }
                    if (far.Count < 5) return null;
                    for (int i = 0; i < 5; i++) { var s = far[rnd.Next(far.Count)]; far.Remove(s); q.list.Add(s.id); }
                    q.goal = 5; q.reward = 1500; q.rel = 10; q.dur = 3600;
                    break;
                }
                case RECONCILE:
                    if (G.emp[ei].rel >= Diplo.WAR) return null;
                    q.goal = 1; q.reward = 800; q.rel = 0; q.dur = 3600;
                    break;
                case FEAST:
                    q.goal = 5; q.reward = 900; q.rel = 10; q.dur = 2400;
                    break;
                default: return null;
            }
            return q;
        }

        // ---------- тексты ----------

        static string E(int ei) => ei >= 0 && ei < G.emp.Count ? G.emp[ei].name : "?";
        static string St(int id) => id >= 0 && id < G.stars.Count ? G.stars[id].name : "?";
        static string It(string id) => Items.Get(id)?.N ?? id;

        public static string Title(Quest q) => q.kind switch
        {
            SUPPLY => "Поставка", PIRATES => "Охота на пиратов", CONVOY => "Сопровождение конвоя", RAID => "Карательный рейд",
            TRANSFER => "Передача системы", SCOUT => "Разведка", ENVOY => "Посольство мира", TERRA => "Терраформирование",
            RELIC => "Археологический заказ", PATROL => "Патруль", RESCUE => "Спасение экипажа", BASE => "Пиратская база",
            OUTPOST => "Отводок", ENERGY => "Энергия для щитов", TRADE => "Торговый договор", COURIER => "Курьер",
            FEUD => "Кровь за кровь", GUARANTEE => "Гарант сделки", EXPEDITION => "Пропавшая экспедиция", HIVE => "Новый улей",
            DUEL => "Дуэль", PATIENCE => "Испытание терпением", RECONCILE => "Примирение", FEAST => "Пир",
            _ => q.kind,
        };

        public static string Desc(Quest q)
        {
            string p = q.planet >= 0 && q.star >= 0 ? "«" + G.stars[q.star].planets[q.planet].name + "»" : "";
            switch (q.kind)
            {
                case SUPPLY: return "Привезите " + q.goal + " × " + It(q.item) + " на любую планету империи. Их рынки ненадолго станут дешевле.";
                case PIRATES: return "Пираты нападут на систему " + St(q.star) + ". Сбейте " + q.goal + " пиратов. Торговые пути станут безопаснее, и рынки империи на время подешевеют.";
                case CONVOY: return "Проведите конвой из 3 грузовых кораблей в системе " + St(q.star) + " до края системы — нужно, чтобы дошли хотя бы двое. Пираты уже ждут. Магазины империи получат внеочередной завоз.";
                case RAID: return "Перебейте охрану системы " + St(q.star) + " (" + E(q.target) + "). После рейда империя сама нападёт на ослабленную систему.";
                case DUEL: return "Перебейте охрану системы " + St(q.star) + " (" + E(q.target) + "), ни разу не починившись. Слабым не место среди сильных.";
                case TRANSFER: return "Захватите любую систему империи «" + E(q.target) + "» — она перейдёт не к вам, а к " + E(q.ei) + ".";
                case SCOUT: return "Побывайте в 5 системах империи «" + E(q.target) + "». Следующее нападение на неё будет сильнее.";
                case ENVOY: return "Отвезите «Дар мира» в столицу империи «" + E(q.target) + "» (система " + St(q.star) + "). Их вражда с " + E(q.ei) + " утихнет.";
                case TERRA: return "Поднимите пригодность планеты " + p + " (система " + St(q.star) + ") до ★" + q.goal + " инструментами терраформирования. Её рынок станет богаче.";
                case RELIC: return "Принесите артефакт «" + It(q.item) + "». Святыня укрепит веру и флот империи.";
                case PATROL: return "Продержитесь " + q.goal / 60 + " минут в пограничной системе " + St(q.star) + ", отбивая налёты. Потом полчаса на неё никто не нападёт.";
                case GUARANTEE: return "Охраняйте встречу послов в системе " + St(q.star) + " " + q.goal / 60 + " минут. Потом полчаса на неё никто не нападёт.";
                case PATIENCE: return "Проведите " + q.goal / 60 + " минут в системе " + St(q.star) + ", не сделав ни одного выстрела (выстрел — отсчёт заново). Потом полчаса на неё никто не нападёт.";
                case RESCUE: return "В системе " + St(q.star) + " дрейфует подбитый корабль империи. Продержитесь рядом с ним 20 секунд, пока его берут на буксир. При следующей обороне у империи будет больше защитников.";
                case BASE: return "Уничтожьте пиратскую базу в системе " + St(q.star) + ". Пираты на час притихнут в округе — и у вас тоже.";
                case OUTPOST: return "Постройте газовую станцию на " + p + " (система " + St(q.star) + "). Она отойдёт империи.";
                case HIVE: return "Основайте колонию на " + p + " (система " + St(q.star) + "). Она отойдёт империи как новый улей.";
                case ENERGY: return "Привезите " + q.goal + " энергоячеек в систему " + St(q.star) + ", где планета " + p + " держит оборону, и сдайте их в «Дипломатии». Щит планеты восстановится.";
                case TRADE: return "Продайте на рынках империи товаров на " + q.goal + " кр. Их рынки час будут покупать у вас дороже.";
                case COURIER: return "Отвезите посылку в систему " + St(q.target) + " за 10 минут. При следующей обороне у империи будет больше защитников.";
                case FEUD: return "Сбейте 10 кораблей империи «" + E(q.target) + "». Следующее нападение на неё будет сильнее.";
                case EXPEDITION: return "Отсканируйте по планете в каждой из систем: " + string.Join(", ", q.list.ConvertAll(St)) + ". Найденные знания пополнят рынки империи.";
                case RECONCILE: return "Помиритесь с империей «" + E(q.ei) + "»: выведите её из войны с вами через дипломатию.";
                case FEAST: return "Привезите к празднику по 5 единиц металлов, кристаллов и пряностей. Соседи империи станут к ней дружелюбнее.";
            }
            return "";
        }

        public static string Progress(Quest q)
        {
            switch (q.kind)
            {
                case SUPPLY: case RELIC: case ENERGY: return "в трюме: " + Inv.Count(q.item) + " из " + q.goal;
                case FEAST: return "металлы " + Inv.Count("metal") + "/5, кристаллы " + Inv.Count("crystal") + "/5, пряности " + Inv.Count("spice") + "/5";
                case PATROL: case GUARANTEE: case PATIENCE: return Mathf.FloorToInt(q.t / 60) + " из " + q.goal / 60 + " мин";
                case RESCUE: return Mathf.FloorToInt(q.t) + " из 20 с";
                case CONVOY: return "дошло: " + q.prog + " из 3, потеряно: " + q.lost;
                case TERRA: return "пригодность: ★" + Habit(G.stars[q.star].planets[q.planet]) + " из ★" + q.goal;
                case TRADE: return q.prog + " из " + q.goal + " кр.";
                default: return q.goal > 1 ? q.prog + " из " + q.goal : "";
            }
        }

        public static string Left(Quest q) => Mathf.Max(0, Mathf.CeilToInt((q.until - G.time) / 60)) + " мин";

        // ---------- взять, сдать, отказаться ----------

        public static bool Accept(Quest q)
        {
            if (Active(q.ei) != null) return false;
            q.active = true; q.until = G.time + q.dur; q.prog = 0; q.t = 0;
            switch (q.kind)
            {
                case PIRATES:
                {
                    var s = G.stars[q.star];
                    int pi = s.planets.FindIndex(p => p.owner == q.ei);
                    if (Battles.Of(q.star) != null || pi < 0) { q.active = false; return false; }
                    Battles.StartRaid(q.star, pi, Raid.PIRATES, q.goal + 1, q.ei);
                    break;
                }
                case ENVOY: Inv.Add("gift_peace", 1); break;
                case COURIER: Inv.Add("parcel", 1); break;
            }
            Notes.Add("Поручение «" + Title(q) + "» (" + E(q.ei) + "): " + Desc(q), Notes.INFO);
            return true;
        }

        public static void Abandon(Quest q, string why = null)
        {
            if (!q.active) return;
            G.quests.Remove(q);
            if (q.kind == ENVOY) Inv.Remove("gift_peace", Inv.Count("gift_peace"));
            if (q.kind == COURIER) Inv.Remove("parcel", Inv.Count("parcel"));
            var e = G.emp[q.ei];
            e.rel = Mathf.Clamp(e.rel - 5, -100, 100);
            Notes.Add("Поручение «" + Title(q) + "» провалено" + (why != null ? ": " + why : "") + ". Отношения с " + e.name + " ухудшились", Notes.LOSS);
        }

        /// <summary>Поручения «привезти»: можно сдать в «Дипломатии».</summary>
        public static bool HandIn(Quest q) => q.kind == SUPPLY || q.kind == RELIC || q.kind == FEAST || q.kind == ENERGY;

        public static bool CanHandIn(Quest q)
        {
            switch (q.kind)
            {
                case SUPPLY: case RELIC: return Inv.Count(q.item) >= q.goal;
                case ENERGY: return P.cur == q.star && Inv.Count("battery") >= q.goal;
                case FEAST: return Inv.Count("metal") >= 5 && Inv.Count("crystal") >= 5 && Inv.Count("spice") >= 5;
            }
            return false;
        }

        public static string HandInWhy(Quest q) => q.kind == ENERGY && P.cur != q.star ? "Сдать можно только в системе " + St(q.star) : "Не хватает груза";

        public static void Deliver(Quest q)
        {
            if (!CanHandIn(q)) return;
            if (q.kind == FEAST) { Inv.Remove("metal", 5); Inv.Remove("crystal", 5); Inv.Remove("spice", 5); }
            else Inv.Remove(q.item, q.goal);
            Complete(q);
        }

        static void Complete(Quest q)
        {
            if (!q.active) return;
            G.quests.Remove(q);
            var e = G.emp[q.ei];
            P.credits += q.reward;
            if (q.rel > 0) e.rel = Mathf.Clamp(e.rel + q.rel, -100, 100);
            string benefit = Benefit(q);
            // поручение своей веры продвигает путь посвящения
            if (Faith.InPath && P.faithPath == Faith.Of(q.ei)) Faith.Boost();
            Notes.Add("Поручение «" + Title(q) + "» выполнено: +" + q.reward + " кр. " + e.name + (benefit != "" ? ": " + benefit : " благодарит вас"), Notes.WIN);
        }

        /// <summary>Польза для империи — применяется при выполнении; возвращает её описание.</summary>
        static string Benefit(Quest q)
        {
            var e = G.emp[q.ei];
            switch (q.kind)
            {
                case SUPPLY: Diplo.GiveDiscount(q.ei, .9f, 1200); return "скидка 10% в её магазинах на 20 минут";
                case PIRATES: Diplo.GiveDiscount(q.ei, .9f, 1800); return "торговые пути безопаснее, скидка 10% на 30 минут";
                case CONVOY: case EXPEDITION: Restock(q.ei); return "её магазины получили внеочередной завоз";
                case RAID: case DUEL: Diplo.Strike(q.ei, q.star); return "её флот атакует ослабленную систему " + St(q.star);
                case SCOUT: case FEUD: e.warBoost += 3; return "следующее её нападение на врага будет сильнее";
                case ENVOY: Diplo.Add(q.ei, q.target, 40); return "вражда с " + E(q.target) + " утихла";
                case TERRA: G.stars[q.star].planets[q.planet].shopInit = false; return "рынок планеты стал богаче";
                case RELIC:
                    e.defBonus++;
                    for (int o = 0; o < G.emp.Count; o++) if (o != q.ei && Faith.Of(o) == Faith.Of(q.ei)) Diplo.Add(q.ei, o, 10);
                    return "её планеты защищает больше кораблей, единоверцы её уважают";
                case PATROL: case GUARANTEE: case PATIENCE: e.safeStar = q.star; e.safeUntil = G.time + 1800; return "на систему " + St(q.star) + " полчаса никто не нападёт";
                case RESCUE: e.defOnce += 2; return "экипаж спасён — при следующей обороне защитников больше";
                case COURIER: e.defOnce += 1; return "посылка дошла — при следующей обороне защитников больше";
                case OUTPOST: return "у неё новая газовая станция";
                case HIVE: return "у неё новый улей";
                case TRANSFER: return "её территория выросла";
                case RECONCILE: return "мир восстановлен";
                case BASE: G.quiet.Add(new QuietZone { x = G.stars[q.star].x, y = G.stars[q.star].y, until = G.time + 3600 }); return "пираты на час притихли в округе";
                case ENERGY:
                {
                    var p = G.stars[q.star].planets[q.planet];
                    if (p.broken > 0) { p.broken--; p.shieldDmg = 0; p.defOut = false; }
                    return "щит планеты «" + p.name + "» восстановлен";
                }
                case TRADE: e.sellUntil = G.time + 3600; return "её рынки час покупают у вас на 10% дороже";
                case FEAST:
                    for (int o = 0; o < G.emp.Count; o++) if (Diplo.Close(q.ei, o)) Diplo.Add(q.ei, o, 5);
                    return "соседи стали к ней дружелюбнее";
            }
            return "";
        }

        static void Restock(int ei)
        {
            foreach (var s in LoadedStars())
                if (s.owner == ei) foreach (var p in s.planets) if (p.owner == ei) p.shopInit = false;
        }

        /// <summary>Передать империи планету (и систему, если в ней больше нет ваших планет).</summary>
        static void Give(Star s, int pi, int ei)
        {
            var p = s.planets[pi];
            p.owner = ei;
            Battles.ResetShield(p);
            G.routes.RemoveAll(r => r.star == s.id && (r.a == pi || r.b == pi));
            if (!s.planets.Exists(x => x.owner == Owner.PLAYER)) s.owner = ei;
        }

        // ---------- события игры ----------

        public static void OnKill(Battle b, BShip e, bool byPlayer)
        {
            foreach (var q in AllActive())
            {
                if (q.kind == PIRATES && byPlayer && e.pirate && b.star == q.star) { if (++q.prog >= q.goal) Complete(q); }
                else if (q.kind == FEUD && byPlayer && e.ei == q.target && !e.pirate) { if (++q.prog >= q.goal) Complete(q); }
                else if (q.kind == BASE && e.cls == ShipClass.BASE && b.star == q.star) Complete(q);
                else if (q.kind == CONVOY && e.cargo && b.star == q.star) { if (++q.lost >= 2) Abandon(q, "конвой разбит"); }
                else if (q.kind == RESCUE && e.wreck && b.star == q.star) Abandon(q, "корабль уничтожен");
            }
        }

        public static void OnCargoArrived(Battle b)
        {
            var q = AllActive().Find(x => x.kind == CONVOY && x.star == b.star);
            if (q != null && ++q.prog >= q.goal) Complete(q); // дошли двое — конвой проведён
        }

        /// <summary>Охрана системы перебита при игроке.</summary>
        public static void OnVictory(int star)
        {
            foreach (var q in AllActive())
                if ((q.kind == RAID || q.kind == DUEL) && q.star == star)
                {
                    if (q.kind == DUEL && q.flag) Abandon(q, "вы чинились во время дуэли");
                    else Complete(q);
                }
        }

        /// <summary>Прилёт к звезде (на карте).</summary>
        public static void OnArrive(int id)
        {
            var s = G.stars[id];
            foreach (var q in AllActive())
            {
                if (q.kind == SCOUT && s.owner == q.target && !q.list.Contains(id)) { q.list.Add(id); if (++q.prog >= q.goal) Complete(q); }
                else if (q.kind == ENVOY && id == q.star && Inv.Remove("gift_peace", 1)) Complete(q);
                else if (q.kind == COURIER && id == q.target && Inv.Remove("parcel", 1)) Complete(q);
            }
        }

        /// <summary>Захват системы игроком: «Передача системы» отдаёт её заказчику. true — система передана.</summary>
        public static bool OnConquer(Star s, int from)
        {
            var q = AllActive().Find(x => x.kind == TRANSFER && x.target == from);
            if (q == null) return false;
            for (int i = 0; i < s.planets.Count; i++) if (s.planets[i].owner == Owner.PLAYER) Give(s, i, q.ei);
            s.owner = q.ei;
            Complete(q);
            return true;
        }

        /// <summary>Постройка колонии или станции: «Отводок» и «Новый улей» отдают планету заказчику.</summary>
        public static void OnBuild(Star s, Planet p)
        {
            int pi = s.planets.IndexOf(p);
            var q = AllActive().Find(x => (x.kind == OUTPOST || x.kind == HIVE) && x.star == s.id && x.planet == pi);
            if (q == null) return;
            Give(s, pi, q.ei);
            Complete(q);
        }

        public static void OnScan(Star s)
        {
            foreach (var q in AllActive())
                if (q.kind == EXPEDITION && q.list.Remove(s.id)) { if (++q.prog >= q.goal) Complete(q); }
        }

        public static void OnSale(int ei, int amount)
        {
            var q = Active(ei);
            if (q != null && q.kind == TRADE && (q.prog += amount) >= q.goal) Complete(q);
        }

        public static void OnPeace(int ei)
        {
            var q = Active(ei);
            if (q != null && q.kind == RECONCILE) Complete(q);
        }

        public static void OnTerraform(Planet p)
        {
            foreach (var q in AllActive())
                if (q.kind == TERRA && G.stars[q.star].planets[q.planet] == p && Habit(p) >= q.goal) Complete(q);
        }

        public static void OnRepair() { foreach (var q in AllActive()) if (q.kind == DUEL) q.flag = true; }
        public static void OnFire() { foreach (var q in AllActive()) if (q.kind == PATIENCE && P.cur == q.star) q.t = 0; }

        /// <summary>
        /// Цели взятых поручений для подсветки: звезда и планета (-1 — вся система).
        /// Цели «сделать что-то там» — пока поручение не выполнено; места сдачи («отвезти туда») — только когда груз уже в трюме.
        /// </summary>
        public static void Marks(List<(int star, int planet)> L)
        {
            L.Clear();
            foreach (var q in G.quests)
            {
                if (!q.active) continue;
                switch (q.kind)
                {
                    case TERRA: case OUTPOST: case HIVE: case CONVOY: L.Add((q.star, q.planet)); break;
                    case PIRATES: case RAID: case DUEL: case PATROL: case GUARANTEE: case PATIENCE: case RESCUE: case BASE: L.Add((q.star, -1)); break;
                    case EXPEDITION: foreach (var id in q.list) L.Add((id, -1)); break;
                    case ENVOY: if (Inv.Count("gift_peace") > 0) L.Add((q.star, -1)); break;
                    case COURIER: if (Inv.Count("parcel") > 0) L.Add((q.target, -1)); break;
                    case ENERGY: if (Inv.Count("battery") >= q.goal) L.Add((q.star, q.planet)); break;
                    case SUPPLY: case FEAST: case RELIC:
                        // сдать можно на любой планете империи — подсвечиваем ближайшие её системы, когда груз уже собран
                        if (CanHandIn(q)) foreach (var s in NearestOwn(q.ei)) for (int i = 0; i < s.planets.Count; i++) if (s.planets[i].owner == q.ei) L.Add((s.id, i));
                        break;
                }
            }
        }

        const int HAND_IN_MARKS = 3; // сколько ближайших систем империи подсвечивать для сдачи
        static readonly Dictionary<int, (int from, int at, List<Star> list)> nearCache = new Dictionary<int, (int, int, List<Star>)>();

        /// <summary>Ближайшие к кораблю системы империи с её планетами (пересчёт — при смене звезды или владельцев).</summary>
        static List<Star> NearestOwn(int ei)
        {
            if (nearCache.TryGetValue(ei, out var c) && c.from == P.cur && c.at == Star.OwnerVersion) return c.list;
            var here = G.stars[P.cur];
            var best = new List<(float d, Star s)>();
            foreach (var s in G.stars)
            {
                if (s.owner != ei || s.core) continue;
                float d = Px.Dist(s, here);
                if (best.Count < HAND_IN_MARKS || d < best[best.Count - 1].d)
                {
                    best.Add((d, s));
                    best.Sort((a, b) => a.d.CompareTo(b.d));
                    if (best.Count > HAND_IN_MARKS) best.RemoveAt(best.Count - 1);
                }
            }
            var list = best.ConvertAll(x => x.s);
            nearCache[ei] = (P.cur, Star.OwnerVersion, list);
            return list;
        }

        /// <summary>Есть ли поручение на терраформирование этой планеты (тогда её можно терраформировать).</summary>
        public static bool TerraTarget(Planet p) => AllActive().Exists(q => q.kind == TERRA && G.stars[q.star].planets[q.planet] == p);

        /// <summary>Вход в саму систему: появляются конвой, подбитый корабль, пиратская база, охрана для рейда.</summary>
        public static void OnEnterSystem(int id, Vector2 ship)
        {
            foreach (var q in AllActive())
            {
                if (q.star != id) continue;
                var s = G.stars[id];
                switch (q.kind)
                {
                    case CONVOY:
                    {
                        var b = Battles.Get(id);
                        var from = PPos(s.planets[q.planet]);
                        // путь — через всю систему на противоположный край; засада на полпути
                        float a = Mathf.Atan2(from.y, from.x) + Mathf.PI;
                        var to = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (SysR(s) + 120);
                        Battles.SpawnConvoy(b, from, to, q.ei, 3 - q.prog - q.lost);
                        Battles.SpawnPirates(b, Vector2.Lerp(from, to, .45f) + new Vector2(0, 60), R(5, 7));
                        break;
                    }
                    case RESCUE:
                    {
                        var b = Battles.Get(id);
                        var at = PPos(s.planets[0]) * .5f + new Vector2(40, 30);
                        Battles.SpawnWreck(b, at, q.ei);
                        Battles.SpawnPirates(b, at + new Vector2(180, 0), R(4, 6));
                        q.t = 0;
                        break;
                    }
                    case BASE:
                    {
                        var b = Battles.Get(id);
                        var pl = s.planets.Count > 0 ? PPos(s.planets[s.planets.Count / 2]) : Vector2.zero;
                        Battles.SpawnBase(b, pl + new Vector2(30, -30));
                        break;
                    }
                    case RAID:
                    case DUEL:
                    {
                        var b = Battles.Get(id);
                        if (b.Foes == 0 && Owner.IsEmp(s.owner)) Battles.SpawnGuards(b, ship, s.owner, 3 + P.lv.las);
                        q.flag = false;
                        break;
                    }
                }
            }
        }

        /// <summary>Каждый кадр: сроки, патрули с налётами, буксировка, исчезнувшие пираты.</summary>
        public static void Tick(float dt, bool inSys, ShipState ship)
        {
            foreach (var q in AllActive())
            {
                if (G.time > q.until) { Abandon(q, "время вышло"); continue; }
                bool here = inSys && P.cur == q.star;
                switch (q.kind)
                {
                    case PATROL:
                    case GUARANTEE:
                    case PATIENCE:
                        if (!here) break;
                        q.t += dt;
                        // налёты: волна пиратов раз в 2,5 минуты (у «Терпения» — без них)
                        if (q.kind != PATIENCE && Mathf.FloorToInt(q.t / 150) > Mathf.FloorToInt((q.t - dt) / 150))
                            Battles.SpawnPirates(Battles.Get(q.star), new Vector2(ship.x, ship.y) + new Vector2(200, 0), R(3, 4));
                        if (q.t >= q.goal) Complete(q);
                        break;
                    case RESCUE:
                    {
                        if (!here) break;
                        var b = Battles.Of(q.star);
                        var w = b?.ships.Find(e => e.wreck && e.hp > 0);
                        if (w == null) break;
                        if (Px.Dist(w.x, w.y, ship.x, ship.y) < 30) q.t += dt;
                        if (q.t >= q.goal) { w.hp = 0; Complete(q); }
                        break;
                    }
                    case PIRATES:
                        if (Battles.RaidAt(q.star) == null && q.prog < q.goal) Abandon(q, "пираты ушли или победили без вас");
                        break;
                    case ENVOY:
                        if (!Diplo.Alive(q.target)) Abandon(q, "адресата больше нет");
                        break;
                }
            }
        }
    }

    /// <summary>Округа, где пираты притихли (уничтожена пиратская база).</summary>
    [Serializable]
    public class QuietZone
    {
        public float x, y, until;
    }
}

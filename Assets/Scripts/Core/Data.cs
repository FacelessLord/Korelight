using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelSpace
{
    /// <summary>Владелец звезды/планеты: NONE, PLAYER или индекс империи 0..3.</summary>
    public static class Owner
    {
        public const int NONE = -2;
        public const int PLAYER = -1;
        public static bool IsEmp(int o) => o >= 0;
    }

    [Serializable]
    public class Planet
    {
        public string name, type;
        public int orbit, size, temp, atmo, seed;
        public float ang, spd, ether;
        public int owner = Owner.NONE;
        public bool scanned, art, home;
        public float gt = -999; // время последней заправки газом
        // постройки игрока: до 3 в сумме; колонии (на газовых гигантах — станции) добывают, склады только хранят
        public int colonies, depots;
        public List<ItemStack> store = new List<ItemStack>(); // хранилище планеты: добытое и сложенное на склад
        public bool capital;    // главная планета столицы империи: до 5 построек
        // оборона: щитов столько, сколько построек; ломаются по очереди (3, 5, 10, 20, 50 выстрелов)
        public int shields;     // щитов в начале боя (0 — боя не было, считаем по постройкам)
        public int broken;      // сколько щитов уже пробито
        public float shieldDmg; // урон по текущему щиту
        public bool defOut;     // защитники текущего щита уже вылетели
        public bool surrendered; // планета сдалась захватчику (система помнит это, пока бой не кончится)
        // магазин планеты: ограниченный запас, завоз раз в 10 минут (см. Shop)
        public List<ItemStack> shop = new List<ItemStack>();
        public List<string> shopUp = new List<string>(); // улучшения в продаже: "u:eng", "w:laser"
        public float shopAt;    // время последнего завоза
        public bool shopInit;   // запас уже завозили
        public int shopN;       // номер завоза (для случайности)
        public int shopKind;    // вид магазина при завозе: верфь, станция, рынок империи
    }

    [Serializable]
    public class Star
    {
        public int id;
        public float x, y;
        public string name, col;
        public bool core, visited;
        public int seed;                       // из него создаются планеты
        public int initOwner = Owner.NONE;     // владелец при генерации (империи, родина)
        public bool empHome;                   // столица империи
        public float simT = -1;                // время последнего пересчёта добычи и перевозок (дальние системы «заморожены»)
        List<Planet> _planets;
        public bool Loaded => _planets != null;
        /// <summary>Планеты создаются при первом обращении (в галактике тысячи звёзд).</summary>
        public List<Planet> planets { get => _planets ??= GalaxyGen.Planets(this); set => _planets = value; }
        int _owner = Owner.NONE;
        /// <summary>Владелец системы. Каждая смена увеличивает OwnerVersion — по ней карта пересчитывает линии территорий.</summary>
        public int owner { get => _owner; set { if (_owner != value) { _owner = value; OwnerVersion++; } } }
        public static int OwnerVersion;
        public int price;
    }

    [Serializable]
    public class Empire
    {
        public string name, col;
        public int rel;
        public int capital = -1; // звезда-столица
        public bool major;       // одна из четырёх больших исходных империй
        public bool fallen;      // империя пала: все её системы захвачены
        public bool met;         // игрок встречал империю: был в её системе, она нападала или пала от его руки
        public float discountUntil; // до этого игрового времени — скидка 20% за помощь в обороне
        // дипломатия игрока: отношения через переговоры меняются не чаще раза в 10 минут
        public float talkAt;        // когда отношения в последний раз изменились через дипломатию
        public bool talked;         // переговоры уже были
        public float goodwill;      // подаренные кредиты, которые ещё не повлияли на отношения
        public float attackedAt = -1e9f; // когда империя в последний раз нападала на игрока («Слово чести»)
        public float discountK = .8f;    // множитель цен скидки (0,8 — за помощь в обороне, 0,9 — за поручения)
        // поручения и их польза для империи
        public bool questsInit;
        public float questsAt;           // когда обновлялся список поручений
        public int warBoost;             // столько кораблей добавится к её следующему нападению
        public int defBonus, defOnce;    // защитников больше: всегда / при следующей обороне
        public int safeStar = -1;        // система под охраной (после патруля)
        public float safeUntil, sellUntil; // до каких пор: охрана; рынок покупает дороже
        public int size0;                // исходный размер (империя растёт не больше чем в полтора раза)
        public float nextGrow, nextTerra; // когда империя освоит новую систему и улучшит планету
        public List<int> settled = new List<int>();     // освоенные империей системы (по порядку)
        public List<int> settledSeen = new List<int>(); // из них — освоенные у вас на виду (рядом с кораблём)
    }

    [Serializable]
    public class Raid
    {
        public const int PIRATES = -3;
        public int star, planet;
        public float t;
        public int att = PIRATES; // кто напал: пираты или индекс империи
        public int def = Owner.PLAYER; // на кого напали: игрок или индекс империи
        public int n = -1;        // сколько нападающих ещё живо (для сохранения; -1 — ещё не считали)
    }

    [Serializable]
    public class Levels
    {
        public int eng = 1, las = 1, arm = 1, cargo = 1;

        public int Get(string k) => k switch { "eng" => eng, "las" => las, "arm" => arm, _ => cargo };

        public void Inc(string k)
        {
            switch (k) { case "eng": eng++; break; case "las": las++; break; case "arm": arm++; break; default: cargo++; break; }
        }
    }

    [Serializable]
    public class Tools
    {
        public int kit = 1, atmo, heat, cool = 1;

        public int Get(string k) => k switch { "kit" => kit, "atmo" => atmo, "heat" => heat, _ => cool };

        public void Add(string k, int d)
        {
            switch (k) { case "kit": kit += d; break; case "atmo": atmo += d; break; case "heat": heat += d; break; default: cool += d; break; }
        }
    }

    [Serializable]
    public class PlayerData
    {
        public float credits = 600, energy = 100, hull = 100, cargo;
        public Levels lv = new Levels();
        public Tools tools = new Tools();
        public int cur;
        public bool won;
        public bool pullSeen; // окно о притяжении ядра уже показано
        public bool starSeed; // награда Ядра
        public string empireName = "";        // название империи игрока, вводится в начале игры
        public string empireColor = "#6cff8a"; // цвет империи игрока: кольца колоний, линии территорий
        public List<ItemStack> items = new List<ItemStack>(); // трюм
        public bool invMigrated;                              // старые cargo/tools уже перенесены в трюм
        public List<string> weapons = new List<string>();     // купленное оружие (бластер есть всегда)
        public string weapon = Weapons.BLASTER;               // выбранное активное оружие
        public List<string> weaponsOff = new List<string>();  // выключенное пассивное оружие
        // вера (см. Faith)
        public string faith = "";       // принятая вера
        public string faithPath = "";   // вера, путь посвящения которой проходится
        public int faithStep, faithProg; // текущее испытание и прогресс в нём
        public float furyAt;            // когда последний раз включена «Ярость Пламени»
        public bool furyUsed;           // (поля «fury» — способность любой принятой веры)
        public List<int> deaths = new List<int>(); // системы, где погиб корабль («Вечное возвращение»)
        public int mailStar = -1;       // куда везём послание («Посредник»)
        public float mailUntil;
        public bool lowHull;            // корпус опускался до 10% («Memento mori»)
        public float calmT;             // секунды без боя («Живи незаметно»)
        public int lastSys = -1;        // предыдущая система («Голубь над пламенем», «Возвращение к истоку»)
        public bool firedHere;          // стреляли ли в текущей системе
        public bool enteredHere;        // заходили ли в саму систему (а не только прилетели на карте)
    }

    /// <summary>Изменённая звезда в сохранении: всё прочее восстанавливается из seed.</summary>
    [Serializable]
    public class StarState
    {
        public int id, owner, initOwner = Owner.NONE;
        public bool empHome;
        public float simT = -1;
        public bool visited, hasPlanets;
        public List<Planet> planets = new List<Planet>();
    }

    [Serializable]
    public class GameState
    {
        public int seed;
        public float empScale = DEFAULT_EMP_SCALE; // модификатор размера империй (настройка новой галактики)
        public const float DEFAULT_EMP_SCALE = 2;
        public const int VERSION = 4; // 4 — модификатор размера империй
        public int version = VERSION;
        [NonSerialized] public List<Star> stars = new List<Star>(); // не сохраняется: строится из seed
        public List<StarState> starStates = new List<StarState>();
        public List<long> genCells = new List<long>(); // клетки карты, где малые империи уже созданы
        public List<Route> routes = new List<Route>(); // транспортные пути
        public List<Hub> hubs = new List<Hub>();       // приёмные планеты систем
        public List<Empire> emp = new List<Empire>();
        public int home;
        public List<Raid> raids = new List<Raid>();
        public float nextRaid = 100, time;
        public List<Note> notes = new List<Note>(); // журнал важных уведомлений
        public List<EmpRel> erels = new List<EmpRel>(); // отношения империй между собой
        public float nextEmpRaid = 120;                 // когда соседи-враги снова нападут друг на друга
        public float nextMood = 60;                     // когда империи снова «вспомнят» об игроке: нападение или подарок
        public List<Quest> quests = new List<Quest>();  // предложенные и взятые поручения империй
        public List<QuietZone> quiet = new List<QuietZone>(); // округа уничтоженных пиратских баз
        public List<Patrol> patrols = new List<Patrol>();     // патрульные дроны в системах игрока
        public PlayerData p = new PlayerData();
    }

    public sealed class PType
    {
        public string N;
        public int T, A;
        public Color32[] Pal;
        public bool Gas;
    }

    public sealed class Upgrade
    {
        public string Key, N, D;
        public int Base;      // цена улучшения до ур.2
        public const float COST_GROWTH = 2.5f; // каждый следующий уровень дороже в 2.5 раза
        /// <summary>Цена перехода на уровень lv (2…Max).</summary>
        public int Cost(int lv) => Game.Round5(Base * Mathf.Pow(COST_GROWTH, lv - 2));
        public int Max;
    }

    public sealed class ToolDef
    {
        public string Key, N;
        public int C;
    }

    /// <summary>Статические таблицы игры.</summary>
    public static class Defs
    {
        static PType T(string n, int t, int a, bool gas, params string[] pal) =>
            new PType { N = n, T = t, A = a, Gas = gas, Pal = Array.ConvertAll(pal, Px.Hex) };

        public static readonly Dictionary<string, PType> Types = new Dictionary<string, PType>
        {
            ["lava"] = T("Лавовая", 3, 1, false, "#1a0603", "#4a1006", "#8f2309", "#d4500f", "#ff9a2e"),
            ["ice"] = T("Ледяная", -2, 1, false, "#1b2a44", "#3d5f8a", "#7fa6c9", "#c4dcef", "#ffffff"),
            ["desert"] = T("Пустынная", 1, 1, false, "#3a2410", "#7a4d22", "#b57c3c", "#dcb06a", "#f3dca0"),
            ["rock"] = T("Скалистая", 0, 0, false, "#1c1c22", "#3a3a44", "#5d5d6a", "#8a8a96", "#b8b8c2"),
            ["ocean"] = T("Океаническая", 0, 2, false, "#0a1840", "#14306e", "#1f52a3", "#3c86d1", "#8fd0ff"),
            ["garden"] = T("Цветущая", 0, 3, false, "#0f2a14", "#1f5a26", "#3c8f3a", "#79c24f", "#d8f08a"),
            ["gas"] = T("Газовый гигант", 0, 0, true, "#3a1f4a", "#6b3a7a", "#a8649e", "#e09a9a", "#ffd6b0"),
        };

        public static readonly Color32[] GasAltPal = Array.ConvertAll(new[] { "#3d200c", "#7a4416", "#c27a2e", "#e8b465", "#fff0c4" }, Px.Hex);

        public static readonly (string type, double w)[] TW =
        {
            ("lava", .14), ("ice", .17), ("desert", .2), ("rock", .2), ("ocean", .1), ("garden", .04), ("gas", .15)
        };

        public static readonly string[] StarCols = { "#fff4c2", "#ffd27a", "#ff9e5e", "#bcd6ff", "#ffffff", "#ff7a6a" };
        public static readonly string[] Temps = { "ледяной ад", "очень холодно", "холодно", "умеренно", "тепло", "жарко", "пекло" };
        public static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI" };

        public static readonly Upgrade[] Upg =
        {
            new Upgrade { Key = "eng", N = "Двигатель", D = "дальность прыжка, энергия, скорость", Base = 400, Max = 5 },
            new Upgrade { Key = "las", N = "Бластер", D = "урон всего оружия", Base = 350, Max = 3 },
            new Upgrade { Key = "arm", N = "Броня", D = "прочность корпуса", Base = 300, Max = 3 },
            new Upgrade { Key = "cargo", N = "Трюм", D = "вместимость", Base = 250, Max = 5 },
        };

        public static readonly ToolDef[] ToolList =
        {
            new ToolDef { Key = "kit", N = "Набор колонии", C = 250 },
            new ToolDef { Key = "atmo", N = "Атмогенератор", C = 120 },
            new ToolDef { Key = "heat", N = "Нагреватель", C = 80 },
            new ToolDef { Key = "cool", N = "Охладитель", C = 80 },
        };

        public static readonly string[] EmpCols = { "#ff5a6a", "#4fc9ff", "#c67bff", "#ffb347" };
        public static readonly string[] EmpPrefix = { "Империя", "Союз", "Конклав", "Рой" };
    }
}

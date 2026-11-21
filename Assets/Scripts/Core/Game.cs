using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelSpace
{
    /// <summary>Состояние партии, производные характеристики, сохранение и генерация галактики.</summary>
    public static class Game
    {

        public static GameState G;
        public static PlayerData P => G.p;

        static readonly int[] RANGE = { 0, 95, 140, 190, 310, 525 };

        // каждый уровень двигателя — в 1.7 раза больше энергии, с округлением до 5: 400 / 680 / 1155 / 1965
        public static float MaxE() => Round5(400 * Mathf.Pow(1.7f, P.lv.eng - 1) * Faith.EnergyCapMul);

        /// <summary>Округление до ближайшего числа, кратного 5 (для красивых значений в интерфейсе).</summary>
        public static int Round5(float v) => Mathf.RoundToInt(v / 5f) * 5;
        public static float MaxH() => Mathf.Round((60 + P.lv.arm * 40) * Faith.HullMul);
        // каждый уровень трюма — в 1.7 раза вместительнее, с округлением до 5: 60 / 100 / 175 / 295 / 500
        public static int CargoMax() => Round5(60 * Mathf.Pow(1.7f, P.lv.cargo - 1));
        /// <summary>Дальность прыжка без притяжения ядра: двигатель и бонусы.</summary>
        public static float BaseRange() => RANGE[P.lv.eng] * Faith.RangeMul;

        /// <summary>Побывав в ядре, игрок «почувствовал устройство вселенной» — притяжение центра больше не мешает.</summary>
        public static bool PullFree => G.stars.Count > 0 && G.stars[0].visited;

        /// <summary>Во сколько раз притяжение ядра укорачивает прыжок из точки from (1 — не укорачивает).</summary>
        public static float PullK(Vector2 from) => PullFree ? 1 : GalaxyGen.Pull(from.magnitude);

        /// <summary>Дальность прыжка из точки from: ближе к ядру управлять кораблём труднее — прыжки короче.</summary>
        public static float Range(Vector2 from) => BaseRange() * PullK(from);
        public static float Range() => Range(new Vector2(Cur.x, Cur.y));

        public static int Habit(Planet p) => Defs.Types[p.type].Gas ? 0 : Px.Clamp(p.atmo - Mathf.Abs(p.temp), 0, 3);
        // запас и добыча: колонии — по пригодности, газовые станции — постоянные
        public const int GAS_CAP = 40;
        public const float GAS_RATE = .1f;
        public static int CapOf(Planet p) => Defs.Types[p.type].Gas ? GAS_CAP : 10 + Habit(p) * 15;
        public static float Rate(Planet p) => (Defs.Types[p.type].Gas ? GAS_RATE : Habit(p) * .05f) * Faith.ProdMul;
        public static Vector2 PPos(Planet p) => new Vector2(Mathf.Cos(p.ang) * p.orbit, Mathf.Sin(p.ang) * p.orbit);
        public static float SysR(Star s) => s.planets.Count > 0 ? s.planets[s.planets.Count - 1].orbit : 60;
        public static Star Cur => G.stars[P.cur];

        /// <summary>Название империи игрока (для старых сохранений, где его ещё не вводили, — заглушка).</summary>
        public static string EmpName => string.IsNullOrWhiteSpace(P.empireName) ? "Ваша империя" : P.empireName;

        public const string DEFAULT_EMP_COLOR = "#6cff8a";

        /// <summary>Цвет империи игрока (для старых сохранений — зелёный по умолчанию).</summary>
        public static Color32 EmpCol => Px.Hex(string.IsNullOrEmpty(P.empireColor) ? DEFAULT_EMP_COLOR : P.empireColor);

        /// <summary>Цвета, которые можно выбрать для своей империи: не совпадают с цветами ИИ-империй.</summary>
        public static readonly (string col, string name)[] EmpPalette =
        {
            ("#6cff8a", "Изумрудный"), ("#3fe0c8", "Бирюзовый"), ("#b6ff4a", "Лаймовый"), ("#ffe14a", "Солнечный"),
            ("#ff7ad9", "Розовый"), ("#7a8cff", "Индиго"), ("#e8e4ff", "Серебряный"),
        };

        public static string RelStr(int r) => r < -30 ? "враждебны" : r < 30 ? "нейтральны" : r < 60 ? "дружелюбны" : "союзники";

        /// <summary>Звёзды с уже созданными планетами — только среди них могут быть колонии игрока.</summary>
        public static IEnumerable<Star> LoadedStars()
        {
            foreach (var s in G.stars) if (s.Loaded) yield return s;
        }

        public static int ColonyCount() => LoadedStars().Sum(s => s.planets.Count(p => p.owner == Owner.PLAYER));

        // ---------- сохранение: файл, в нём только изменённые звёзды ----------

        static string SavePath => System.IO.Path.Combine(Application.persistentDataPath, "save.json");

        public static void Save()
        {
            if (G == null) return;
            Battles.Store();
            G.starStates.Clear();
            foreach (var s in G.stars)
            {
                if (!s.visited && s.owner == Owner.NONE && s.initOwner == Owner.NONE && !s.Loaded) continue;
                var st = new StarState { id = s.id, owner = s.owner, initOwner = s.initOwner, empHome = s.empHome, simT = s.simT, visited = s.visited, hasPlanets = s.Loaded };
                if (s.Loaded) st.planets = s.planets;
                G.starStates.Add(st);
            }
            try
            {
                string tmp = SavePath + ".tmp";
                System.IO.File.WriteAllText(tmp, JsonUtility.ToJson(G));
                if (System.IO.File.Exists(SavePath)) System.IO.File.Delete(SavePath);
                System.IO.File.Move(tmp, SavePath);
            }
            catch (System.Exception e) { Debug.LogWarning("Не удалось сохранить игру: " + e.Message); }
        }

        /// <summary>Загрузка: галактика заново строится из seed, поверх накладываются изменения.
        /// Сохранения старой маленькой галактики (PlayerPrefs) не подходят — тогда начинается новая игра.</summary>
        public static bool Load()
        {
            try
            {
                if (!System.IO.File.Exists(SavePath)) return false;
                var g = JsonUtility.FromJson<GameState>(System.IO.File.ReadAllText(SavePath));
                if (g == null || g.version != GameState.VERSION) return false;
                GalaxyGen.Build(g.seed, g.empScale, out var stars, out int home, out _);
                g.stars = stars;
                g.home = home;
                foreach (var st in g.starStates)
                {
                    if (st.id < 0 || st.id >= stars.Count) continue;
                    var s = stars[st.id];
                    s.owner = st.owner;
                    s.visited = st.visited;
                    s.initOwner = st.initOwner; // малые империи появляются по ходу игры — их территорию берём из сохранения
                    s.empHome = st.empHome;
                    s.simT = st.simT;
                    if (st.hasPlanets) s.planets = st.planets;
                }
                G = g;
                return true;
            }
            catch (System.Exception e) { Debug.LogWarning("Сохранение не загрузилось: " + e.Message); return false; }
        }

        // ---------- новая игра ----------

        public static void NewGame(int seed, float empScale)
        {
            GalaxyGen.Build(seed, empScale, out var stars, out int home, out var emp);
            G = new GameState { seed = seed, empScale = empScale, stars = stars, home = home, emp = emp, nextRaid = 100 };
            G.p.cur = home;
            P.energy = MaxE();
            P.hull = MaxH();
        }
    }
}

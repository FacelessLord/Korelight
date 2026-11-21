using System.Collections.Generic;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    public sealed class WeaponDef
    {
        public string Id, N, D, Col;
        public int Price;      // 0 — есть с самого начала
        public float Cd;       // откат, с
        public float En;       // энергия за каждое использование (у лазера — за тик 0,1 с), попало или нет
        public bool Passive;   // стреляет само; можно включать и выключать
        public bool Continuous; // непрерывное (луч): урон тиками, вуаль отката не рисуется
    }

    /// <summary>
    /// Оружие корабля. Не занимает места в трюме: покупается на верфи как улучшение.
    /// Активное оружие выбирается в боковой панели и стреляет по ЛКМ/пробелу/кнопке огня,
    /// пассивное стреляет само, пока включено. Урон всего оружия растёт с улучшением «Бластер».
    /// </summary>
    public static class Weapons
    {
        public const string BLASTER = "blaster", LASER = "laser", BOMB = "bomb", BOLT = "bolt",
            MISSILE = "missile", AUTO = "auto", BREAKER = "breaker";

        public static readonly WeaponDef[] All =
        {
            new WeaponDef { Id = BLASTER, N = "Бластер", Col = "#6fe3ff", Cd = .22f, En = .6f,
                D = "Быстрые выстрелы по прицелу" },
            new WeaponDef { Id = LASER, N = "Лазер", Col = "#ff5a7a", Cd = BEAM_TICK, En = .2f, Price = 900, Continuous = true,
                D = "Непрерывный луч через прицел за край экрана: пробивает все корабли на линии. Урон невысокий, держать цель трудно" },
            new WeaponDef { Id = BOMB, N = "Бомбы", Col = "#ff9a2e", Cd = 1.2f, En = 3, Price = 1200,
                D = "Взрыв при попадании задевает корабли вокруг" },
            new WeaponDef { Id = BOLT, N = "Молния", Col = "#b8d8ff", Cd = .6f, En = 1.5f, Price = 1000,
                D = "Слабый разряд, который перескакивает по цепочке на соседние корабли" },
            new WeaponDef { Id = MISSILE, N = "Ракеты", Col = "#e8e4ff", Cd = .9f, En = 2.5f, Price = 1500,
                D = "Самонаводящиеся ракеты" },
            new WeaponDef { Id = AUTO, N = "Автобластер", Col = "#59ffb0", Cd = .5f, En = .5f, Price = 800, Passive = true,
                D = "Сам стреляет по ближайшему врагу. Планеты не трогает" },
            new WeaponDef { Id = BREAKER, N = "Пробиватель щита", Col = "#c67bff", Cd = .4f, En = 1.2f, Price = 1100,
                D = "Втрое сильнее бьёт по щитам планет, но слабо — по кораблям" },
        };

        public static WeaponDef Get(string id)
        {
            foreach (var w in All) if (w.Id == id) return w;
            return All[0];
        }

        public const float BEAM_TICK = .1f, BEAM_DPS = 16, BEAM_LEN = 3000;

        public static bool Owned(string id) => id == BLASTER || P.weapons.Contains(id);

        /// <summary>Купленное оружие в порядке панели.</summary>
        public static List<WeaponDef> OwnedList()
        {
            var L = new List<WeaponDef>();
            foreach (var w in All) if (Owned(w.Id)) L.Add(w);
            return L;
        }

        /// <summary>Выбранное активное оружие.</summary>
        public static string Sel => Owned(P.weapon) && !Get(P.weapon).Passive ? P.weapon : BLASTER;

        public static bool On(string id) => !P.weaponsOff.Contains(id);

        /// <summary>Клик по иконке: активное — выбрать, пассивное — включить или выключить.</summary>
        public static void Use(string id)
        {
            if (!Owned(id)) return;
            if (Get(id).Passive) { if (!P.weaponsOff.Remove(id)) P.weaponsOff.Add(id); }
            else P.weapon = id;
        }

        public static void Buy(string id)
        {
            if (!Owned(id)) P.weapons.Add(id);
            if (!Get(id).Passive) P.weapon = id;
        }

        /// <summary>Множитель урона от улучшения «Бластер»: 1 на первом уровне.</summary>
        public static float Power => (6 + P.lv.las * 4) / 10f * Faith.DamageMul;

        /// <summary>Энергия за использование с учётом веры («Ярость Пламени» — вдвое меньше).</summary>
        public static float Cost(WeaponDef w) => w.En * Faith.EnergyMul;

        // ---------- откат ----------

        static readonly Dictionary<string, float> cd = new Dictionary<string, float>();

        public static float Left(string id) => cd.TryGetValue(id, out var v) ? Mathf.Max(0, v) : 0;
        /// <summary>Доля оставшегося отката: 1 — только что выстрелило, 0 — готово.</summary>
        public static float Frac(string id) => Mathf.Clamp01(Left(id) / Get(id).Cd);

        static readonly List<string> keys = new List<string>();

        public static void Tick(float dt)
        {
            keys.Clear();
            keys.AddRange(cd.Keys);
            foreach (var k in keys) cd[k] -= dt;
        }

        public static void ResetCooldowns() => cd.Clear();

        // ---------- стрельба ----------

        /// <summary>Выстрел выбранным оружием из корабля s в сторону точки aim (мировые координаты).</summary>
        public static bool Fire(int star, ShipState s, Vector2 aim)
        {
            string id = Sel;
            if (Left(id) > 0) return false;
            var w = Get(id);
            if (P.energy < Cost(w))
            {
                if (G.time >= noEnergyAt) { noEnergyAt = G.time + 4; Battles.Toast?.Invoke("Нет энергии — оружие не стреляет"); }
                return false;
            }
            cd[id] = w.Cd;
            P.energy -= Cost(w);
            Faith.OnFire();
            Quests.OnFire();
            float k = Power;
            float a = Mathf.Atan2(aim.y - s.y, aim.x - s.x), c = Mathf.Cos(a), sn = Mathf.Sin(a);
            float x = s.x + c * 5, y = s.y + sn * 5;
            switch (id)
            {
                case LASER:
                    Battles.Beam(star, x, y, a, BEAM_LEN, BEAM_DPS * BEAM_TICK * k, BEAM_DPS * BEAM_TICK * k);
                    break;
                case BOMB:
                    Battles.AddPlayerShot(star, x, y, c * 2.6f, sn * 2.6f, 35 * k, 35 * k, BShot.BOMB, 1.5f);
                    break;
                case BOLT:
                    Battles.Bolt(star, s.x, s.y, aim, 170, 12 * k);
                    break;
                case MISSILE:
                    Battles.AddPlayerShot(star, x, y, c * 2.8f, sn * 2.8f, 30 * k, 30 * k, BShot.MISSILE, 3f);
                    break;
                case BREAKER:
                    Battles.AddPlayerShot(star, x, y, c * 4f, sn * 4f, 4 * k, 30 * k, BShot.BREAKER, .9f);
                    break;
                default:
                    Battles.AddPlayerShot(star, x, y, c * 4.5f, sn * 4.5f, 10 * k, 10 * k, BShot.NORMAL, .9f);
                    break;
            }
            return true;
        }

        static float noEnergyAt;
        public const float AUTO_RANGE = 160;

        /// <summary>Пассивное оружие: стреляет само, если рядом есть враг.</summary>
        public static void Passive(int star, ShipState s)
        {
            if (!Owned(AUTO) || !On(AUTO) || Left(AUTO) > 0 || P.energy < Cost(Get(AUTO))) return;
            var b = Battles.Of(star);
            if (b == null || !Battles.VsPlayer(b)) return; // в чужую войну автобластер сам не вмешивается
            BShip t = null; float bd = AUTO_RANGE;
            foreach (var e in b.ships)
            {
                if (e.side != Battles.Foe(b) || e.hp <= 0) continue;
                float d = Px.Dist(e.x, e.y, s.x, s.y);
                if (d < bd) { bd = d; t = e; }
            }
            if (t == null) return;
            cd[AUTO] = Get(AUTO).Cd;
            P.energy -= Cost(Get(AUTO));
            // упреждение: куда враг долетит, пока летит выстрел
            float lt = bd / 4f;
            float a = Mathf.Atan2(t.y + t.vy * lt - s.y, t.x + t.vx * lt - s.x);
            Battles.AddPlayerShot(star, s.x + Mathf.Cos(a) * 5, s.y + Mathf.Sin(a) * 5, Mathf.Cos(a) * 4f, Mathf.Sin(a) * 4f,
                6 * Power, 0, BShot.AUTO, .9f);
        }
    }
}

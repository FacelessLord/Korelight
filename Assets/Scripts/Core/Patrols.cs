using System;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    /// <summary>Патрульные дроны в одной системе игрока.</summary>
    [Serializable]
    public class Patrol
    {
        public int star, n;
    }

    /// <summary>
    /// Патрульные дроны: покупаются на верфях, ставятся из трюма в своей системе (до пяти).
    /// В мирное время облетают систему, а когда в ней появляются враги — вступают в бой на стороне защитников.
    /// Сбитый дрон потерян; уцелевшие после боя снова патрулируют.
    /// </summary>
    public static class Patrols
    {
        public const int MAX = 5;
        public const string ITEM = "patrol";

        /// <summary>Корабль игрока сейчас в системе (а не на карте) — выставляет GameManager.</summary>
        public static bool InSys;

        static Patrol Find(int star) => G.patrols.Find(x => x.star == star);

        /// <summary>Сколько дронов патрулирует систему (в чужой системе — ни одного: они служат только вам).</summary>
        public static int Count(int star)
        {
            var p = Find(star);
            return p == null || G.stars[star].owner != Owner.PLAYER ? 0 : p.n;
        }

        /// <summary>Поставить дрон из трюма в текущую систему.</summary>
        public static bool Place(out string msg)
        {
            var s = Cur;
            if (!InSys) { msg = "Дрон ставят, находясь в самой системе"; return true; }
            if (s.owner != Owner.PLAYER) { msg = "Дрон можно поставить только в своей системе"; return true; }
            var p = Find(s.id);
            if (p == null) { p = new Patrol { star = s.id }; G.patrols.Add(p); }
            if (p.n >= MAX) { msg = "В системе уже " + MAX + " дронов — больше не поставить"; return true; }
            Inv.Remove(ITEM, 1);
            p.n++;
            // если бой уже идёт — новый дрон сразу в нём
            var b = Battles.Of(s.id);
            if (b != null && b.patrolOut) Battles.SpawnPatrol(b, p.n - 1);
            msg = "Дрон патрулирует систему " + s.name + ": " + p.n + " из " + MAX;
            return true;
        }

        /// <summary>Дрон сбит.</summary>
        public static void Lost(int star)
        {
            var p = Find(star);
            if (p == null) return;
            if (--p.n <= 0) G.patrols.Remove(p);
        }

        /// <summary>Где сейчас k-й дрон в мирном облёте системы и куда он смотрит.</summary>
        public static Vector2 Pos(Star s, int k, float t, out float ang)
        {
            float R = SysR(s) * (.38f + .13f * k) + 20;
            float speed = 30f / R;                                   // ~30 единиц в секунду по орбите
            float dir = k % 2 == 0 ? 1 : -1;                         // соседние дроны летят навстречу друг другу
            float a = dir * t * speed + k * 2.4f + (float)Px.Hash(s.id, k, 77) * Px.TAU;
            float wob = 1 + .08f * Mathf.Sin(t * .7f + k);           // орбита чуть «дышит»
            var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R * wob;
            ang = a + dir * Mathf.PI / 2;
            return p;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    /// <summary>
    /// Медленный рост империй: изредка империя осваивает свободную систему у своей границы (колония или газовая станция)
    /// и поднимает пригодность одной из своих планет. Живьём считаются только империи рядом с кораблём;
    /// далёкие «замораживаются», и когда игрок подлетает, пропущенное время досчитывается (не больше нескольких событий).
    /// </summary>
    public static class Growth
    {
        public const float NEAR = Diplo.NEAR_PLAYER;  // империи, чьи столицы ближе этого к кораблю, растут вживую
        public const float GROW_PERIOD = 1500;        // в среднем раз в 25 минут — новая система
        public const float TERRA_PERIOD = 1200;       // в среднем раз в 20 минут — планета пригоднее
        public const float BORDER = 150;              // осваивают системы не дальше этого от своих
        public const float MAX_GROWTH = 1.5f;         // империя вырастает не больше чем в полтора раза
        public const int CATCH_UP = 4;                // при возвращении игрока досчитываем не больше стольких событий каждого вида
        const float HOME_FREE = 450;                  // у Родины игрока империи не селятся
        const float CHECK = 5;                        // проверка раз в 5 секунд — дёшево
        public const float SEEN = 1000;               // освоение ближе этого к кораблю игрок замечает (оно попадает в журнал)

        static readonly System.Random rnd = new System.Random();
        static float Rf() => (float)rnd.NextDouble();
        static float timer;
        static GalaxyGen.StarIndex index;
        static GameState forGame;
        static readonly List<Star> near = new List<Star>();

        // «Рой» расселяется вдвое чаще, «Сад» вдвое чаще улучшает планеты
        static float GrowPeriod(int ei) => GROW_PERIOD * (Faith.Of(ei) == Faith.SWARM ? .5f : 1);
        static float TerraPeriod(int ei) => TERRA_PERIOD * (Faith.Of(ei) == Faith.GARDEN ? .5f : 1);
        static float Next(float period) => period * (.5f + Rf()); // случайный интервал вокруг среднего

        public static void Tick(float dt, Vector2 ship)
        {
            timer -= dt;
            if (timer > 0) return;
            timer = CHECK;
            if (forGame != G) { forGame = G; index = new GalaxyGen.StarIndex(G.stars); }
            for (int ei = 0; ei < G.emp.Count; ei++)
            {
                if (!Diplo.Alive(ei) || (Diplo.Cap(ei) - ship).magnitude > NEAR) continue;
                var e = G.emp[ei];
                if (e.size0 <= 0)
                {
                    // первая встреча с империей: запоминаем исходный размер, первое событие — через случайное время
                    e.size0 = Count(ei);
                    e.nextGrow = G.time + Next(GrowPeriod(ei));
                    e.nextTerra = G.time + Next(TerraPeriod(ei));
                    continue;
                }
                for (int k = 0; k < CATCH_UP && G.time >= e.nextGrow; k++)
                {
                    e.nextGrow += Next(GrowPeriod(ei));
                    Expand(ei, ship);
                }
                if (G.time >= e.nextGrow) e.nextGrow = G.time + Next(GrowPeriod(ei)); // остальное пропущенное — не в счёт
                for (int k = 0; k < CATCH_UP && G.time >= e.nextTerra; k++)
                {
                    e.nextTerra += Next(TerraPeriod(ei));
                    Terraform(ei);
                }
                if (G.time >= e.nextTerra) e.nextTerra = G.time + Next(TerraPeriod(ei));
            }
        }

        static int Count(int ei)
        {
            int n = 0;
            foreach (var s in G.stars) if (s.owner == ei) n++;
            return n;
        }

        /// <summary>Свободная система, которую империя может занять.</summary>
        static bool Free(Star s, Star home)
        {
            if (s.core || s.owner != Owner.NONE || s.id == P.cur || Px.Dist(s, home) < HOME_FREE) return false;
            if (Battles.Of(s.id) != null) return false;
            foreach (var q in G.quests) if (q.star == s.id) return false; // цель поручения не трогаем
            return true;
        }

        /// <summary>Лучшая планета для освоения: самая пригодная (★1+), иначе газовый гигант под станцию; -1 — некуда.</summary>
        static int BestPlanet(Star s)
        {
            int best = -1, bh = 0, gas = -1;
            for (int i = 0; i < s.planets.Count; i++)
            {
                var p = s.planets[i];
                if (p.owner != Owner.NONE) continue;
                if (Defs.Types[p.type].Gas) { if (gas < 0) gas = i; continue; }
                int h = Habit(p);
                if (h > bh) { bh = h; best = i; }
            }
            return best >= 0 ? best : gas;
        }

        /// <summary>Освоить свободную систему у границы — ближайшую к столице. true — получилось.</summary>
        public static bool Expand(int ei, Vector2 ship)
        {
            var e = G.emp[ei];
            var own = new List<Star>();
            foreach (var s in G.stars) if (s.owner == ei && !s.core) own.Add(s);
            if (own.Count == 0 || own.Count >= Mathf.Max(e.size0 + 1, Mathf.RoundToInt(e.size0 * MAX_GROWTH))) return false;
            if (index == null || forGame != G) { forGame = G; index = new GalaxyGen.StarIndex(G.stars); }
            var home = G.stars[G.home];
            var cap = G.stars[e.capital];
            var seen = new HashSet<int>();
            var cand = new List<Star>();
            foreach (var s in own)
            {
                index.Near(s.x, s.y, BORDER, near);
                foreach (var o in near) if (seen.Add(o.id) && Free(o, home)) cand.Add(o);
            }
            cand.Sort((a, b) => Px.Dist(a, cap).CompareTo(Px.Dist(b, cap)));
            foreach (var s in cand)
            {
                int pi = BestPlanet(s);
                if (pi < 0) continue;
                var p = s.planets[pi];
                p.owner = ei;
                p.colonies = 1;
                p.depots = 0;
                if (!Defs.Types[p.type].Gas) p.temp = 0; // освоенная планета обжита: климат выровнен
                Battles.ResetShield(p);
                s.owner = ei;
                bool onView = Px.Dist(s.x, s.y, ship.x, ship.y) < SEEN;
                e.settled.Add(s.id);
                if (onView) e.settledSeen.Add(s.id);
                if ((s.visited || onView) && e.met) Notes.Add(e.name + ": освоена система " + s.name, Notes.INFO);
                return true;
            }
            return false;
        }

        /// <summary>Поднять пригодность одной из своих планет на ступень. true — получилось.</summary>
        public static bool Terraform(int ei)
        {
            var list = new List<Planet>();
            foreach (var s in G.stars)
            {
                if (s.owner != ei || !s.Loaded && !s.empHome) continue; // незагруженные окраины — без улучшений, дёшево
                foreach (var p in s.planets)
                    if (p.owner == ei && !Defs.Types[p.type].Gas && Habit(p) < 3 && !Quests.TerraTarget(p)) list.Add(p);
            }
            if (list.Count == 0) return false;
            var t = list[rnd.Next(list.Count)];
            if (t.temp != 0) t.temp += t.temp > 0 ? -1 : 1;
            else t.atmo++;
            t.shopInit = false; // богаче стала планета — богаче и её рынок
            return true;
        }
    }
}

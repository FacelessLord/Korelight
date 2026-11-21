using System.Collections.Generic;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    /// <summary>
    /// Экономика своих систем: добыча и перевозки. Системы далеко от корабля не обновляются каждый кадр —
    /// они «замораживаются», а когда корабль снова оказывается рядом, всё пропущенное время пересчитывается
    /// по событиям в правильном порядке: добыча до очередной отправки груза, отправка, снова добыча…
    /// Системы, связанные путями между системами, всегда считаются вместе (одной группой).
    /// </summary>
    public static class Sim
    {
        public const float ACTIVE_R = 1550; // в этом радиусе от корабля системы живут «вживую»

        public static void Tick(float dt, Vector2 ship)
        {
            foreach (var group in Groups())
            {
                bool active = false;
                foreach (var s in group.stars)
                    if (Px.Dist(s.x, s.y, ship.x, ship.y) <= ACTIVE_R) { active = true; break; }
                if (!active) continue; // заморожена: s.simT остаётся временем последнего обновления
                Advance(group, G.time);
            }
        }

        sealed class Group
        {
            public readonly List<Star> stars = new List<Star>();
            public readonly List<Route> routes = new List<Route>();
        }

        static bool HasOwn(Star s)
        {
            if (!s.Loaded) return false;
            foreach (var p in s.planets) if (p.owner == Owner.PLAYER) return true;
            return false;
        }

        /// <summary>Группы систем: свои системы, связанные путями между системами.</summary>
        static List<Group> Groups()
        {
            var own = new List<Star>();
            foreach (var s in LoadedStars()) if (HasOwn(s)) own.Add(s);
            var groupOf = new Dictionary<int, Group>();
            var groups = new List<Group>();
            // объединение по путям между системами
            var parent = new Dictionary<int, int>();
            int Root(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            foreach (var s in own) parent[s.id] = s.id;
            foreach (var r in G.routes)
                if (!r.InSystem && parent.ContainsKey(r.a) && parent.ContainsKey(r.b)) parent[Root(r.a)] = Root(r.b);
            foreach (var s in own)
            {
                int k = Root(s.id);
                if (!groupOf.TryGetValue(k, out var g)) { g = new Group(); groupOf[k] = g; groups.Add(g); }
                g.stars.Add(s);
            }
            foreach (var r in G.routes)
            {
                int key = r.InSystem ? r.star : r.a;
                if (parent.ContainsKey(key) && groupOf.TryGetValue(Root(key), out var g)) g.routes.Add(r);
            }
            return groups;
        }

        /// <summary>Довести группу до момента now: по событиям, каждая система — от своего simT.</summary>
        static void Advance(Group g, float now)
        {
            foreach (var s in g.stars)
            {
                if (s.simT < 0 || s.simT > now) s.simT = now; // новая система (или время сброшено) — считаем с этого момента
                foreach (var p in s.planets) if (p.owner == Owner.PLAYER) Colony.Migrate(p);
            }
            float t = now;
            foreach (var s in g.stars) t = Mathf.Min(t, s.simT);

            int guard = 0;
            while (t < now && guard++ < 100000)
            {
                // ближайшее событие — отправка груза по какому-нибудь пути
                float step = now - t;
                foreach (var r in g.routes) step = Mathf.Min(step, Mathf.Max(0, Routes.PERIOD - r.t));
                float t1 = t + step;

                // добыча за отрезок — только для тех систем, которые на нём действительно пропускали время
                foreach (var s in g.stars)
                {
                    float from = Mathf.Max(t, s.simT);
                    if (t1 <= from) continue;
                    foreach (var p in s.planets) if (p.owner == Owner.PLAYER) Colony.Produce(p, t1 - from);
                }
                // таймеры путей и отправка тех, чьё время пришло
                foreach (var r in g.routes)
                {
                    float from = Mathf.Max(t, RouteStart(r));
                    if (t1 <= from) continue;
                    r.t += t1 - from;
                    if (r.t >= Routes.PERIOD - 1e-4f) { r.t = 0; Routes.Ship(r); }
                }
                t = t1;
                if (step <= 0) t = Mathf.Min(now, t + 1e-3f); // защита от зацикливания на нулевом шаге
            }
            foreach (var s in g.stars) s.simT = now;
        }

        // путь живёт с того момента, когда обновлены обе его системы
        static float RouteStart(Route r) =>
            r.InSystem ? G.stars[r.star].simT : Mathf.Max(G.stars[r.a].simT, G.stars[r.b].simT);
    }
}

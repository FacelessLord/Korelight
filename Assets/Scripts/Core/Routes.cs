using System;
using System.Collections.Generic;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    /// <summary>
    /// Транспортный путь. star ≥ 0 — путь внутри системы star между планетами a → b;
    /// star = -1 — путь по галактике между системами a → b.
    /// </summary>
    [Serializable]
    public class Route
    {
        public int star = -1, a, b;
        public List<string> res = new List<string>(); // какие виды эфира/газа везти
        public float t;                               // таймер до следующей партии
        public int next;                              // с какого ресурса начинать (по кругу)
        public int paid;                              // сколько заплачено за путь (вернём при удалении; сюда же пойдут улучшения)
        public bool InSystem => star >= 0;
    }

    /// <summary>Приёмная планета системы: сюда приходят грузы из других систем.</summary>
    [Serializable]
    public class Hub { public int star, planet; }

    /// <summary>Перевозка ресурсов: раз в PERIOD секунд по BatchOf(путь) единиц (в системе 5, между системами 50).</summary>
    public static class Routes
    {
        public const float PERIOD = 10;
        public const int BATCH_SYSTEM = 5, BATCH_GALAXY = 50;  // единиц за одну отправку
        public const int COST_SYSTEM = 50, COST_GALAXY = 500;  // цена нового пути
        public static int BatchOf(Route r) => r.InSystem ? BATCH_SYSTEM : BATCH_GALAXY;
        public static int CostOf(bool inSystem) => inSystem ? COST_SYSTEM : COST_GALAXY;

        static List<string> kinds;
        /// <summary>Все перевозимые ресурсы: виды эфира и газ.</summary>
        public static List<string> Kinds
        {
            get
            {
                if (kinds != null) return kinds;
                kinds = new List<string>();
                foreach (var d in Items.All) if (d.Kind == ItemKind.Ether) kinds.Add(d.Id);
                return kinds;
            }
        }

        public static Color32 ColorOf(string id) => Px.Hex(Items.Get(id).Col);

        public static bool Owned(Planet p) => p.owner == Owner.PLAYER && Colony.Buildings(p) > 0;

        public static Route Find(int star, int a, int b) => G.routes.Find(r => r.star == star && r.a == a && r.b == b);

        /// <summary>Создать путь a → b или добавить ресурсы в уже существующий.</summary>
        public static Route AddOrMerge(int star, int a, int b, IEnumerable<string> res)
        {
            var r = Find(star, a, b);
            if (r == null) { r = new Route { star = star, a = a, b = b }; G.routes.Add(r); }
            foreach (var id in res) if (!r.res.Contains(id)) r.res.Add(id);
            r.res.Sort((x, y) => Kinds.IndexOf(x).CompareTo(Kinds.IndexOf(y)));
            return r;
        }

        // ---------- приёмная планета ----------

        public static int HubOf(Star s)
        {
            var h = G.hubs.Find(x => x.star == s.id);
            if (h != null && h.planet < s.planets.Count && Owned(s.planets[h.planet])) return h.planet;
            // не задана — та, где больше всего свободного места
            int best = -1, bf = -1;
            for (int i = 0; i < s.planets.Count; i++)
            {
                var p = s.planets[i];
                if (!Owned(p)) continue;
                int f = Colony.Free(p);
                if (f > bf) { bf = f; best = i; }
            }
            return best;
        }

        public static bool IsHubSet(Star s, int pi) => G.hubs.Exists(x => x.star == s.id && x.planet == pi);

        public static void ClearHub(Star s) => G.hubs.RemoveAll(x => x.star == s.id);

        public static void SetHub(Star s, int pi)
        {
            G.hubs.RemoveAll(x => x.star == s.id);
            G.hubs.Add(new Hub { star = s.id, planet = pi });
        }

        // ---------- перевозка ----------

        public static void Ship(Route r)
        {
            if (r.res.Count == 0) return;
            if (r.InSystem)
            {
                var s = G.stars[r.star];
                if (!s.Loaded || r.a >= s.planets.Count || r.b >= s.planets.Count) return;
                Planet pa = s.planets[r.a], pb = s.planets[r.b];
                if (!Owned(pa) || !Owned(pb)) return;
                int left = BatchOf(r);
                for (int k = 0; k < r.res.Count && left > 0; k++)
                {
                    string id = r.res[(r.next + k) % r.res.Count];
                    left -= Move(pa, pb, id, left);
                }
                r.next = (r.next + 1) % r.res.Count;
            }
            else
            {
                Star sa = G.stars[r.a], sb = G.stars[r.b];
                if (!sa.Loaded || !sb.Loaded) return;
                int hub = HubOf(sb);
                if (hub < 0) return;
                var dest = sb.planets[hub];
                int left = BatchOf(r); // свободное место в рейсе
                // грузим по очереди со всех своих планет системы: забрали всё с одной — идём к следующей,
                // пока не кончатся планеты или место в рейсе (или место на приёмной планете)
                foreach (var src in sa.planets)
                {
                    if (left <= 0) break;
                    if (!Owned(src)) continue;
                    for (int k = 0; k < r.res.Count && left > 0; k++)
                    {
                        string id = r.res[(r.next + k) % r.res.Count];
                        left -= Move(src, dest, id, left);
                    }
                    if (Colony.Free(dest) == 0) break;
                }
                r.next = (r.next + 1) % r.res.Count;
            }
        }

        static int Move(Planet from, Planet to, string id, int n)
        {
            n = Mathf.Min(n, Stacks.Count(from.store, id));
            n = Stacks.Add(to.store, id, n, Colony.Free(to));
            if (n > 0) Stacks.Remove(from.store, id, n);
            return n;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    /// <summary>
    /// Запасы магазинов. У каждой планеты свой ограниченный запас; раз в 10 минут игрового времени завоз.
    /// Чем пригоднее планета (уровень 1…3), тем больше товаров и их видов:
    /// 1 — по 1–2 инструмента терраформирования и энергоячейки, один набор колонии или станция;
    /// 2 — каждого на 1–2 больше, плюс 1–2 модуля склада и 1–2 улучшения;
    /// 3 — ещё на 1–2 больше всего, плюс все улучшения.
    /// Купленное улучшение до завоза не сменяется следующим уровнем.
    /// </summary>
    public static class Shop
    {
        public const float RESTOCK = 600;

        /// <summary>Уровень магазина: пригодность планеты, но не ниже 1 (газовые гиганты и пустоши — 1).</summary>
        public static int Tier(Planet p) => Mathf.Max(1, Habit(p));

        public static float NextIn(Planet p) => Mathf.Max(0, RESTOCK - (G.time - p.shopAt));

        /// <summary>Подготовить запас магазина: первый раз или если пришло время завоза.</summary>
        public static void Ensure(Market m)
        {
            var p = m.Planet;
            if (p == null) return;
            if (!p.shopInit || G.time - p.shopAt >= RESTOCK || p.shopKind != m.Kind) Restock(m);
        }

        // 1 или 2 — для товара с номером k на шаге t
        static int R12(Planet p, int k, int t) => Px.Hash(p.seed + 7919 * p.shopN, k * 16 + t, 4243) < .5 ? 1 : 2;

        static bool Has(Planet p, int k, int t, double chance) => Px.Hash(p.seed + 7919 * p.shopN, k * 16 + t, 5501) < chance;

        public static void Restock(Market m)
        {
            var p = m.Planet;
            p.shopN++;
            p.shopAt = G.time;
            p.shopInit = true;
            p.shopKind = m.Kind;
            p.shop.Clear();
            p.shopUp.Clear();
            int T = Tier(p);
            bool kitFirst = Has(p, 99, 0, .5); // на первом уровне — либо набор колонии, либо станция
            for (int k = 0; k < m.Sells.Count; k++)
            {
                string id = m.Sells[k];
                int n = 0;
                for (int t = 1; t <= T; t++)
                {
                    switch (id)
                    {
                        case "kit":
                        case "station":
                            if (t == 1) n += (id == "kit") == kitFirst ? 1 : 0;
                            else n += R12(p, k, t);
                            break;
                        case "depot":
                        case Patrols.ITEM: // патрульные дроны — как склады: на верфях уровня 2+
                            if (t >= 2) n += R12(p, k, t);
                            break;
                        case "metal":
                        case "crystal":
                        case "spice":
                            n += 2 + R12(p, k, t); // товары для торговли — по 3–4 за уровень
                            break;
                        case "battery":
                            // газовая станция — источник энергии: 4–8 ячеек
                            n += m.Station ? 4 + (int)(Px.Hash(p.seed + 7919 * p.shopN, k * 16 + t, 3301) * 5) % 5 : R12(p, k, t);
                            break;
                        case "repair":
                            // как энергоячейки; станции всегда уровня 1 — там их 1–2
                            n += R12(p, k, t);
                            break;
                        default: // инструменты терраформирования
                            n += R12(p, k, t);
                            break;
                    }
                }
                if (n > 0) p.shop.Add(new ItemStack { id = id, n = n });
            }

            if (!m.Shipyard || T < 2) return;
            // улучшения: на уровне 2 — одно-два случайных, на уровне 3 — все доступные
            var offers = new List<string>();
            foreach (var u in Defs.Upg) if (P.lv.Get(u.Key) < u.Max) offers.Add("u:" + u.Key);
            foreach (var w in Weapons.All) if (w.Price > 0 && !Weapons.Owned(w.Id)) offers.Add("w:" + w.Id);
            if (T == 2)
            {
                // перемешиваем детерминированно и берём 1–2
                for (int i = offers.Count - 1; i > 0; i--)
                {
                    int j = (int)(Px.Hash(p.seed + 7919 * p.shopN, i, 6007) * (i + 1)) % (i + 1);
                    (offers[i], offers[j]) = (offers[j], offers[i]);
                }
                int take = Mathf.Min(offers.Count, R12(p, 50, 0));
                offers.RemoveRange(take, offers.Count - take);
            }
            p.shopUp.AddRange(offers);
        }

        /// <summary>Сколько предмета id осталось в магазине; -1 — здесь его не завозят.</summary>
        public static int Left(Planet p, string id)
        {
            foreach (var s in p.shop) if (s.id == id) return s.n;
            return -1;
        }

        public static void Take(Planet p, string id, int n)
        {
            foreach (var s in p.shop) if (s.id == id) { s.n = Mathf.Max(0, s.n - n); return; }
        }

        public static bool Offers(Planet p, string key) => p.shopUp.Contains(key);
        public static void TakeOffer(Planet p, string key) => p.shopUp.Remove(key);

        /// <summary>«через 7 мин» / «через 40 с».</summary>
        public static string RestockStr(Planet p)
        {
            float t = NextIn(p);
            return t >= 60 ? "через " + Mathf.CeilToInt(t / 60) + " мин" : "через " + Mathf.CeilToInt(t) + " с";
        }

        /// <summary>Захваченная планета: склад частично заполнен её эфиром и товарами.</summary>
        public static void FillCaptured(Planet p)
        {
            int cap = Colony.Cap(p);
            if (cap <= 0) return;
            double h = Px.Hash(p.seed, 77, (int)G.time);
            int total = Mathf.RoundToInt(cap * (float)(.3 + .4 * h)); // 30–70% вместимости
            int ether = Mathf.RoundToInt(total * .7f);
            Stacks.Add(p.store, Colony.EtherId(p), ether, Colony.Free(p));
            var goods = new[] { "metal", "crystal", "spice", "battery" };
            int rest = total - ether;
            for (int i = 0; rest > 0 && i < 8; i++)
            {
                string id = goods[(int)(Px.Hash(p.seed, 80 + i, (int)G.time) * goods.Length) % goods.Length];
                int n = Mathf.Min(rest, 1 + (int)(Px.Hash(p.seed, 90 + i, 3) * rest));
                rest -= Stacks.Add(p.store, id, n, Colony.Free(p));
            }
        }
    }
}

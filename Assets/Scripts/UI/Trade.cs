using System;
using System.Collections.Generic;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    /// <summary>Окна трюма и магазина: карточки, покупка и продажа кликом (Shift — по 10, Ctrl — всё).</summary>
    public static class Trade
    {
        static GameManager gm => GameManager.I;
        static Market mk;       // открытый магазин; null — трюм открыт сам по себе
        static bool holdTab;
        static Action back;     // куда вернуться по Esc (например, в меню планеты)

        static readonly Color Muted = Px.HexC("#8a86a8");
        const string UP_COL = "#ffcf4a";

        static PixelSpace.Planet depot; // открытый склад планеты; null — не склад

        public static void OpenShop(Market m, Action onBack)
        {
            gm.ui.CloseModalSilently();
            mk = m; depot = null; holdTab = false; back = onBack;
            Render();
        }

        public static void OpenHold()
        {
            gm.ui.CloseModalSilently();
            mk = null; depot = null; holdTab = true; back = null;
            Render();
        }

        /// <summary>Склад планеты: перенос предметов между трюмом и хранилищем, без цен.</summary>
        public static void OpenStorage(PixelSpace.Planet p, Action onBack)
        {
            gm.ui.CloseModalSilently();
            mk = null; depot = p; holdTab = false; back = onBack;
            Render();
        }

        public static bool HoldOnlyOpen => gm.ui.cards.Open && mk == null && depot == null;

        static void Render()
        {
            if (depot != null) { RenderStorage(); return; }
            if (mk != null) Shop.Ensure(mk);
            var cards = holdTab ? HoldCards() : ShopCards();
            var tabs = new List<Tab>();
            if (mk != null)
            {
                tabs.Add(new Tab("Магазин", !holdTab, () => { holdTab = false; Render(); }));
                tabs.Add(new Tab("Трюм", holdTab, () => { holdTab = true; Render(); }));
            }
            string footer = "Кредиты: " + Mathf.FloorToInt(P.credits) + "   Трюм: " + Inv.Used() + " / " + CargoMax()
                + (mk != null ? "   ·   Завоз " + Shop.RestockStr(mk.Planet) + "   ·   Shift — по 10, Ctrl — всё" : "   ·   Энергоячейку можно использовать кликом");
            string empty = holdTab ? "Трюм пуст" : "Здесь ничего не продают";
            gm.ui.cards.Show((mk != null ? mk.Title : "") + (holdTab ? ":трюм" : ":магазин"),
                mk != null ? mk.Title : "Трюм", tabs, cards, footer, empty, Esc);
            gm.ui.Refresh();
        }

        static void Esc()
        {
            gm.ui.cards.Close();
            back?.Invoke();
        }

        // ---------- склад ----------

        static void RenderStorage()
        {
            var p = depot;
            var cards = new List<Card>();
            // вкладка «Склад»: клик — в трюм; вкладка «Трюм»: клик — на склад
            var list = holdTab ? P.items : p.store;
            foreach (var s in list)
            {
                var d = Items.Get(s.id);
                if (d == null) continue;
                string id = d.Id;
                cards.Add(new Card
                {
                    Icon = Sprites.Icon(d.Icon, d.Col), Name = d.N, Line1 = "×" + s.n, Line1Col = UiKit.TextCol,
                    Line2 = holdTab ? "на склад" : "в трюм",
                    Click = mult => Move(id, mult),
                });
            }
            var tabs = new List<Tab>
            {
                new Tab("Склад", !holdTab, () => { holdTab = false; Render(); }),
                new Tab("Трюм", holdTab, () => { holdTab = true; Render(); }),
            };
            string footer = "Склад: " + Colony.Used(p) + " / " + Colony.Cap(p) + "   Трюм: " + Inv.Used() + " / " + CargoMax()
                + "   ·   Shift — по 10, Ctrl — всё";
            gm.ui.cards.Show("склад:" + p.name + (holdTab ? ":трюм" : ":склад"), "Склад · " + p.name, tabs, cards, footer,
                holdTab ? "Трюм пуст" : "Склад пуст", Esc);
            gm.ui.Refresh();
        }

        static void Move(string id, int mult)
        {
            var p = depot;
            var d = Items.Get(id);
            if (holdTab)
            {
                int n = Mathf.Min(mult, Inv.Count(id));
                n = Stacks.Add(p.store, id, n, Colony.Free(p));
                if (n <= 0) { gm.Toast("Склад полон"); return; }
                Inv.Remove(id, n);
                gm.Toast("На склад: " + d.N + " ×" + n);
            }
            else
            {
                int n = Mathf.Min(mult, Stacks.Count(p.store, id));
                n = Inv.Add(id, n);
                if (n <= 0) { gm.Toast("Трюм полон"); return; }
                Stacks.Remove(p.store, id, n);
                gm.Toast("В трюм: " + d.N + " ×" + n);
            }
            Save();
            Render();
        }

        // ---------- магазин ----------

        static List<Card> ShopCards()
        {
            var L = new List<Card>();
            var p = mk.Planet;
            var soldOut = new List<Card>(); // раскупленное — в конец списка
            if (mk.Shipyard)
            {
                // в продаже только завезённые улучшения; купленное сменится следующим уровнем при завозе
                foreach (var u in Defs.Upg) if (Shop.Offers(p, "u:" + u.Key)) L.Add(UpgradeCard(u));
                foreach (var w in Weapons.All) if (w.Price > 0 && Shop.Offers(p, "w:" + w.Id)) L.Add(WeaponCard(w));
            }
            foreach (var id in mk.Sells)
            {
                var d = Items.Get(id);
                int left = Shop.Left(p, id);
                if (left < 0) continue; // здесь такое не завозят
                int price = mk.BuyPrice(d), have = Inv.Count(id);
                if (left == 0)
                {
                    soldOut.Add(new Card
                    {
                        Icon = Sprites.Icon(d.Icon, d.Col), Name = d.N, Line1 = "нет в наличии", Line1Col = Muted,
                        Dim = true,
                        Click = _ => gm.Toast(d.N + ": всё раскупили, завоз " + Shop.RestockStr(p)),
                    });
                    continue;
                }
                L.Add(new Card
                {
                    Icon = Sprites.Icon(d.Icon, d.Col), Name = d.N,
                    Line1 = price + " кр.", Line2 = "в наличии: " + left, Line3 = have > 0 ? "в трюме: " + have : null,
                    Dim = P.credits < price || Inv.Free() == 0,
                    Click = mult => Buy(d, price, mult),
                });
            }
            // сначала то, что можно купить; не по карману — дальше; раскупленное — в самом конце
            var ok = L.FindAll(c => !c.Dim);
            ok.AddRange(L.FindAll(c => c.Dim));
            ok.AddRange(soldOut);
            return ok;
        }

        static Card UpgradeCard(Upgrade u)
        {
            int lv = P.lv.Get(u.Key);
            var icon = Sprites.Icon("up_" + u.Key, UP_COL);
            if (lv >= u.Max)
                return new Card { Icon = icon, Name = u.N + " ур." + lv, Line1 = "максимум", Line1Col = Muted, Dim = true,
                    Click = _ => gm.Toast(u.N + ": уже максимальный уровень") };
            int cost = u.Cost(lv + 1);
            return new Card
            {
                Icon = icon, Name = u.N + " ур." + (lv + 1), NameCol = Gold(), Line1 = cost + " кр.", Line2 = u.D,
                Dim = P.credits < cost,
                Click = _ =>
                {
                    if (P.credits < cost) { gm.Toast("Не хватает кредитов"); return; }
                    P.credits -= cost;
                    P.lv.Inc(u.Key);
                    Shop.TakeOffer(mk.Planet, "u:" + u.Key);
                    if (u.Key == "eng") P.energy = MaxE();
                    if (u.Key == "arm") P.hull = MaxH();
                    gm.Toast(u.N + ": уровень " + P.lv.Get(u.Key));
                    Save();
                    Render();
                },
            };
        }

        /// <summary>Оружие покупается как улучшение: места в трюме не занимает.</summary>
        static Card WeaponCard(WeaponDef w)
        {
            var icon = Sprites.Icon("w_" + w.Id, w.Col);
            if (Weapons.Owned(w.Id))
                return new Card { Icon = icon, Name = w.N, Line1 = "куплено", Line1Col = Muted, Line2 = w.D, Dim = true,
                    Click = _ => gm.Toast(w.N + ": уже установлено") };
            int cost = w.Price;
            return new Card
            {
                Icon = icon, Name = w.N, NameCol = Px.HexC(w.Col), Line1 = cost + " кр.", Line2 = w.D,
                Dim = P.credits < cost,
                Click = _ =>
                {
                    if (P.credits < cost) { gm.Toast("Не хватает кредитов"); return; }
                    P.credits -= cost;
                    Weapons.Buy(w.Id);
                    Shop.TakeOffer(mk.Planet, "w:" + w.Id);
                    gm.Toast("Установлено: " + w.N + (w.Passive ? " — включается в панели оружия слева" : " — выбрано в панели оружия слева"));
                    Save();
                    Render();
                },
            };
        }

        static Color Gold() => UiKit.Gold;

        static void Buy(ItemDef d, int price, int mult)
        {
            if (Inv.Free() == 0) { gm.Toast("Трюм полон"); return; }
            if (P.credits < price) { gm.Toast("Не хватает кредитов"); return; }
            int left = Shop.Left(mk.Planet, d.Id);
            if (left == 0) { gm.Toast(d.N + ": всё раскупили"); return; }
            int n = Mathf.Min(mult, Mathf.FloorToInt(P.credits / price), Inv.Free(), left);
            n = Inv.Add(d.Id, n);
            Shop.Take(mk.Planet, d.Id, n);
            P.credits -= n * price;
            gm.Toast("Куплено: " + d.N + " ×" + n + " за " + n * price + " кр.");
            Save();
            Render();
        }

        // ---------- трюм ----------

        static List<Card> HoldCards()
        {
            var L = new List<Card>();
            foreach (var s in P.items)
            {
                var d = Items.Get(s.id);
                if (d == null) continue;
                var c = new Card { Icon = Sprites.Icon(d.Icon, d.Col), Name = d.N, Line1 = "×" + s.n, Line1Col = UiKit.TextCol };
                if (mk != null)
                {
                    int price = mk.SellPrice(d);
                    if (price > 0) { c.Line2 = "продать: " + price + " кр."; c.Click = mult => Sell(d, price, mult); }
                    else { c.Line2 = "здесь не купят"; c.Dim = true; string why = mk.NoSellReason(d) ?? "Это здесь не покупают"; c.Click = _ => gm.Toast(why); }
                }
                else
                {
                    c.Line2 = Items.KindName(d.Kind);
                    string id = d.Id, desc = d.D;
                    c.Click = _ =>
                    {
                        if (Inv.Use(id, out var msg)) { if (msg != null) gm.Toast(msg); Save(); Render(); }
                        else gm.Toast(desc);
                    };
                }
                L.Add(c);
            }
            return L;
        }

        static void Sell(ItemDef d, int price, int mult)
        {
            int n = Mathf.Min(mult, Inv.Count(d.Id));
            if (n <= 0 || !Inv.Remove(d.Id, n)) return;
            P.credits += n * price;
            if (mk.Emp >= 0) { Faith.Event("sale", n * price); Quests.OnSale(mk.Emp, n * price); } // «Честная торговля», «Торговый договор»
            // крупная продажа эфира улучшает отношения, как раньше
            if (d.Kind == ItemKind.Ether && n >= 5 && mk.Emp >= 0) G.emp[mk.Emp].rel = Mathf.Min(100, G.emp[mk.Emp].rel + 2);
            gm.Toast("Продано: " + d.N + " ×" + n + " за " + n * price + " кр.");
            Save();
            Render();
        }
    }
}

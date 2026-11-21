using System;
using System.Collections.Generic;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    public enum ItemKind { Ether, Artifact, Quest, Goods, Tool }

    /// <summary>Стопка предметов в трюме.</summary>
    [Serializable]
    public class ItemStack
    {
        public string id;
        public int n;
    }

    public sealed class ItemDef
    {
        public string Id, N, D;  // id, название, описание
        public ItemKind Kind;
        public int Price;        // базовая цена в кредитах
        public string Icon;      // ключ иконки в Sprites
        public string Col;       // основной цвет иконки
    }

    /// <summary>Справочник предметов.</summary>
    public static class Items
    {
        public static readonly List<ItemDef> All = new List<ItemDef>();
        static readonly Dictionary<string, ItemDef> byId = new Dictionary<string, ItemDef>();

        static void Def(string id, string n, ItemKind k, int price, string icon, string col, string d)
        {
            var it = new ItemDef { Id = id, N = n, Kind = k, Price = price, Icon = icon, Col = col, D = d };
            All.Add(it);
            byId[id] = it;
        }

        static Items()
        {
            // эфир: свой вид у каждого типа планет (газовые гиганты эфир не дают)
            Def("ether_lava", "Лавовый эфир", ItemKind.Ether, 18, "gem", "#ff9a2e", "Добывается на лавовых мирах");
            Def("ether_ice", "Ледяной эфир", ItemKind.Ether, 14, "gem", "#c4dcef", "Добывается на ледяных мирах");
            Def("ether_desert", "Пустынный эфир", ItemKind.Ether, 15, "gem", "#dcb06a", "Добывается в пустынях");
            Def("ether_rock", "Каменный эфир", ItemKind.Ether, 12, "gem", "#b8b8c2", "Добывается на скалистых мирах");
            Def("ether_ocean", "Океанский эфир", ItemKind.Ether, 17, "gem", "#3c86d1", "Добывается на океанских мирах");
            Def("ether_garden", "Цветущий эфир", ItemKind.Ether, 21, "gem", "#79c24f", "Добывается на цветущих мирах");
            Def("ether_gas", "Газ", ItemKind.Ether, 13, "gas", "#e09a9a", "Добывается газовыми станциями");

            // артефакты — находки при сканировании
            Def("art_relic", "Древняя реликвия", ItemKind.Artifact, 160, "relic", "#ffcf4a", "Сосуд исчезнувшей цивилизации");
            Def("art_core", "Кристалл-ядро", ItemKind.Artifact, 240, "crystal", "#c67bff", "Пульсирует слабым светом");
            Def("art_map", "Звёздная карта", ItemKind.Artifact, 200, "map", "#e8d8a8", "Маршруты древних кораблей");
            Def("art_idol", "Идол предтеч", ItemKind.Artifact, 320, "idol", "#59ffb0", "Неизвестный материал");
            Def("art_seal", "Печать первого договора", ItemKind.Artifact, 280, "seal", "#4fc9ff", "Скрепляла первый союз звёзд. Святыня Уз Союза — открывает путь посвящения");
            Def("art_larva", "Королевская личинка", ItemKind.Artifact, 280, "larva", "#ffb347", "Тёплая и шевелится. Святыня Единства Роя — открывает путь посвящения");
            Def("art_hammer", "Молот философа", ItemKind.Artifact, 280, "hammer", "#ff5a6a", "Им простукивают идолов. Святыня Воли к власти — открывает путь посвящения");
            Def("art_stone", "Камень безмолвия", ItemKind.Artifact, 280, "stone", "#b8c6dc", "Рядом с ним стихают мысли. Святыня Столба пустоты — открывает путь посвящения");
            Def("art_wheel", "Колесо закона", ItemKind.Artifact, 280, "wheel", "#e8d8a8", "Восемь спиц, ни одной лишней. Святыня Срединного пути — открывает путь посвящения");
            Def("art_cup", "Чаша дружбы", ItemKind.Artifact, 280, "cup", "#79c24f", "Из неё пьют вместе. Святыня Сада Эпикура — открывает путь посвящения");
            Def("art_ember", "Угли Первого костра", ItemKind.Artifact, 280, "ember", "#ff7a2a", "Не гаснут тысячи лет. Святыня Пути Пламени — открывает путь посвящения");

            // товары для торговли: в разных системах цены разные
            Def("battery", "Энергоячейка", ItemKind.Goods, 40, "battery", "#59ffb0", "+" + Inv.BATTERY + " энергии. Клик в трюме — использовать");
            Def("repair", "Ремонтный набор", ItemKind.Goods, 180, "repair", "#b8c6dc", "+" + Inv.REPAIR + " прочности корпуса — чинит корабль без верфи. Клик в трюме — использовать");
            Def("metal", "Металлы", ItemKind.Goods, 30, "ingot", "#b8b8c2", "Нужны всем верфям галактики");
            Def("crystal", "Кристаллы", ItemKind.Goods, 55, "crystal", "#6fe3ff", "Ценятся в электронике");
            Def("spice", "Пряности", ItemKind.Goods, 45, "spice", "#ff7a6a", "Деликатес с цветущих миров");

            // предметы поручений
            Def("gift_peace", "Дар мира", ItemKind.Quest, 0, "relic", "#e8d8a8", "Отвезите в столицу империи, указанной в поручении");
            Def("parcel", "Посылка", ItemKind.Quest, 0, "map", "#ffcf4a", "Отвезите в систему, указанную в поручении");

            // инструменты колонизации и терраформинга
            Def("kit", "Набор колонии", ItemKind.Tool, 250, "kit", "#ffcf4a", "Основать колонию на планете ★1+");
            Def("atmo", "Атмогенератор", ItemKind.Tool, 120, "atmo", "#8fd0ff", "Атмосфера планеты +1");
            Def("heat", "Нагреватель", ItemKind.Tool, 80, "heat", "#ff7a2a", "Температура планеты +1");
            Def("cool", "Охладитель", ItemKind.Tool, 80, "cool", "#c4dcef", "Температура планеты −1");
            Def("station", "Газовая станция", ItemKind.Tool, 500, "station", "#ffb347", "Построить станцию на газовом гиганте");
            Def(Patrols.ITEM, "Патрульный дрон", ItemKind.Tool, 400, "drone", "#6fe3ff", "Ставится в своей системе, до " + Patrols.MAX + ": облетает её и защищает в бою. Клик в трюме, находясь в системе");
            Def("depot", "Модуль склада", ItemKind.Tool, 300, "up_cargo", "#b8b8c2", "Склад: хранит в 10 раз больше колонии, но ничего не добывает");
        }

        public static ItemDef Get(string id) => byId.TryGetValue(id, out var d) ? d : null;
        public static string EtherOf(string planetType) => "ether_" + planetType;
        public static readonly string[] Artifacts = { "art_relic", "art_core", "art_map", "art_idol", "art_ember",
            "art_seal", "art_larva", "art_hammer", "art_stone", "art_wheel", "art_cup" };

        public static string KindName(ItemKind k) => k switch
        {
            ItemKind.Ether => "Эфир", ItemKind.Artifact => "Артефакты", ItemKind.Quest => "Задания",
            ItemKind.Goods => "Товары", _ => "Инструменты",
        };
    }

    /// <summary>Трюм игрока: занятость считается по числу предметов.</summary>
    public static class Inv
    {
        public const int BATTERY = 240; // энергии в одной энергоячейке
        public const int REPAIR = 50;   // прочности корпуса в одном ремонтном наборе

        static List<ItemStack> L => P.items;

        public static int Count(string id)
        {
            foreach (var s in L) if (s.id == id) return s.n;
            return 0;
        }

        public static int Used() { int n = 0; foreach (var s in L) n += s.n; return n; }
        public static int Free() => Mathf.Max(0, CargoMax() - Used());

        /// <summary>Положить сколько влезет; возвращает, сколько положили.</summary>
        public static int Add(string id, int n)
        {
            n = Mathf.Min(n, Free());
            if (n <= 0) return 0;
            foreach (var s in L) if (s.id == id) { s.n += n; return n; }
            L.Add(new ItemStack { id = id, n = n });
            // порядок карточек: по виду, затем по справочнику
            L.Sort((a, b) => Items.All.IndexOf(Items.Get(a.id)).CompareTo(Items.All.IndexOf(Items.Get(b.id))));
            return n;
        }

        public static bool Remove(string id, int n)
        {
            for (int i = 0; i < L.Count; i++)
            {
                if (L[i].id != id) continue;
                if (L[i].n < n) return false;
                L[i].n -= n;
                if (L[i].n == 0) L.RemoveAt(i);
                return true;
            }
            return n == 0;
        }

        /// <summary>Перенос старых сохранений: инструменты и безымянный эфир становятся предметами трюма.</summary>
        public static void Migrate()
        {
            if (P.invMigrated) return;
            P.invMigrated = true;
            if (P.items == null) P.items = new List<ItemStack>();
            void Move(string id, int n) { if (n > 0) L.Add(new ItemStack { id = id, n = n }); }
            Move("ether_desert", Mathf.FloorToInt(P.cargo));
            Move("kit", P.tools.kit); Move("atmo", P.tools.atmo); Move("heat", P.tools.heat); Move("cool", P.tools.cool);
            P.cargo = 0;
            P.tools = new Tools { kit = 0, cool = 0 };
        }

        /// <summary>Использовать предмет из трюма (вне магазина). false — предмет не используется.</summary>
        public static bool Use(string id, out string msg)
        {
            msg = null;
            if (id == Patrols.ITEM) return Patrols.Place(out msg);
            if (id == "repair")
            {
                if (P.hull >= MaxH() - .5f) { msg = "Корпус цел"; return true; }
                Remove("repair", 1);
                P.hull = Mathf.Min(MaxH(), P.hull + REPAIR);
                Quests.OnRepair();
                msg = "Корпус отремонтирован: +" + REPAIR;
                return true;
            }
            if (id != "battery") return false;
            if (P.energy >= MaxE() - .5f) { msg = "Энергия уже полная"; return true; }
            Remove("battery", 1);
            P.energy = Mathf.Min(MaxE(), P.energy + BATTERY);
            msg = "+" + BATTERY + " энергии";
            return true;
        }
    }

    /// <summary>Магазин: кто что продаёт и почём покупает.</summary>
    public sealed class Market
    {
        public string Title;
        public Star Star;
        public Planet Planet;           // планета, на которой рынок: свой эфир здесь не нужен
        public int Emp = -1;            // империя-владелец рынка; -1 — своя колония (верфь)
        public bool Station;            // газовая станция: только дешёвые энергоячейки, ничего не покупает
        public bool Shipyard => Emp < 0 && !Station;
        /// <summary>Вид магазина: при смене (например, после захвата планеты) запас завозится заново.</summary>
        public int Kind => Shipyard ? 1 : Station ? 2 + (Emp + 1) * 10 : 3 + (Emp + 1) * 10;
        public readonly List<string> Sells = new List<string>();

        // своя колония выкупает всё, но дёшево; инструменты везде принимают за полцены и меньше
        const float COLONY_BUY = .5f, TOOL_BUY_EMP = .6f, TOOL_BUY_COLONY = .4f;
        const float STATION_BATTERY = .7f; // энергоячейки на газовой станции дешевле обычного

        public static Market ForColony(Star s, Planet p)
        {
            var m = new Market { Title = "Верфь · " + p.name, Star = s, Planet = p };
            m.Sells.AddRange(new[] { "kit", "station", "depot", Patrols.ITEM, "atmo", "heat", "cool", "battery", "repair" });
            return m;
        }

        /// <summary>Магазин газовой станции (своей или империи ei).</summary>
        public static Market ForStation(Star s, Planet p, int ei = -1)
        {
            var m = new Market { Title = "Газовая станция · " + p.name + Diplo.DiscountTag(ei), Star = s, Planet = p, Emp = ei, Station = true };
            m.Sells.Add("battery");
            m.Sells.Add("repair");
            return m;
        }

        public static Market ForEmpire(Star s, int ei, Planet p)
        {
            var m = new Market { Title = "Рынок · " + G.emp[ei].name + Diplo.DiscountTag(ei), Star = s, Planet = p, Emp = ei };
            m.Sells.AddRange(new[] { "battery", "repair", "metal", "crystal", "spice" });
            // инструменты терраформирования: у каждой империи свой набор (хотя бы один)
            var terra = new[] { "atmo", "heat", "cool" };
            int added = 0;
            for (int k = 0; k < terra.Length; k++)
                if (Px.Hash(ei, k, G.seed + 31) < .6) { m.Sells.Add(terra[k]); added++; }
            if (added == 0) m.Sells.Add(terra[(int)(Px.Hash(ei, 9, G.seed + 31) * terra.Length) % terra.Length]);
            return m;
        }

        /// <summary>Почему рынок не покупает предмет; null — покупает.</summary>
        public string NoSellReason(ItemDef d)
        {
            if (Station) return "Газовая станция ничего не покупает";
            if (d.Kind == ItemKind.Quest) return "Предметы заданий не продаются";
            if (d.Kind == ItemKind.Ether && Planet != null && d.Id == Items.EtherOf(Planet.type))
                return "Такой эфир добывают прямо здесь — его не купят";
            return null;
        }

        // множитель цены товара в конкретной системе: 0.7…1.4 — отсюда выгода «купи дёшево, продай дорого»
        float Local(ItemDef d) => .7f + .7f * (float)Px.Hash(Star.id, Items.All.IndexOf(d), 77);

        bool Ally => Emp >= 0 && G.emp[Emp].rel >= 60;

        /// <summary>Цена, за которую игрок покупает здесь предмет.</summary>
        public int BuyPrice(ItemDef d)
        {
            int price = BasePrice(d);
            // скидка империи за помощь в обороне
            float k = Diplo.DiscountK(Emp) * Faith.BuyMul(Emp);
            return k == 1 ? price : Mathf.Max(1, Px.R(price * k));
        }

        int BasePrice(ItemDef d)
        {
            if (Station) return Mathf.Max(1, Px.R(d.Price * (d.Id == "battery" ? STATION_BATTERY : 1)));
            if (Shipyard) return d.Price;
            // у империй цена зависит от системы, как у товаров (инструменты то дешевле, то дороже, чем на своей верфи)
            return Mathf.Max(1, Px.R(d.Price * Local(d) * 1.15f));
        }

        /// <summary>Цена, за которую рынок покупает предмет у игрока; 0 — не покупает.</summary>
        public int SellPrice(ItemDef d)
        {
            int p = BaseSell(d);
            float k = Faith.SellMul(Emp) * (Emp >= 0 && G.emp[Emp].sellUntil > G.time ? 1.1f : 1); // «Торговый договор»
            return p <= 0 ? 0 : Mathf.Max(1, Px.R(p * k));
        }

        int BaseSell(ItemDef d)
        {
            if (NoSellReason(d) != null) return 0;
            if (d.Kind == ItemKind.Tool) return Mathf.Max(1, Px.R(d.Price * (Shipyard ? TOOL_BUY_COLONY : TOOL_BUY_EMP)));
            if (Shipyard) return Mathf.Max(1, Px.R(d.Price * COLONY_BUY));
            switch (d.Kind)
            {
                case ItemKind.Ether: return Px.R(d.Price * Star.price / 15f * (Ally ? 1.25f : 1));
                case ItemKind.Artifact: return Px.R(d.Price * (Ally ? 1.1f : 1));
                default: return Mathf.Max(1, Px.R(d.Price * Local(d) * .85f)); // товары
            }
        }
    }
}

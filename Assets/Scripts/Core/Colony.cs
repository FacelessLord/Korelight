using System.Collections.Generic;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    /// <summary>
    /// Постройки игрока на планете: до трёх в сумме. Колония (на газовом гиганте — станция) добывает
    /// эфир/газ и хранит немного; склад ничего не добывает, но вмещает в 10 раз больше одной колонии.
    /// Всё добытое и сложенное лежит в хранилище планеты (p.store).
    /// </summary>
    public static class Colony
    {
        public const int MAX_BUILDINGS = 3, MAX_HOME = 5; // на Родине и на столицах империй — до пяти
        public const int DEPOT_MULT = 10;

        public static int Buildings(Planet p) => p.colonies + p.depots;
        public static int MaxBuildings(Planet p) => p.home || p.capital ? MAX_HOME : p.owner == Owner.PLAYER ? MAX_BUILDINGS + Faith.ExtraBuildings : MAX_BUILDINGS;
        public static bool CanBuild(Planet p) => Buildings(p) < MaxBuildings(p);

        /// <summary>Вместимость хранилища: колонии — по базовому запасу, склады — в 10 раз больше.</summary>
        public static int Cap(Planet p) => CapOf(p) * (p.colonies + p.depots * DEPOT_MULT);

        public static int Used(Planet p) => Stacks.Used(p.store);
        public static int Free(Planet p) => Mathf.Max(0, Cap(p) - Used(p));
        public static string EtherId(Planet p) => Items.EtherOf(p.type);
        public static int Ether(Planet p) => Stacks.Count(p.store, EtherId(p));

        /// <summary>Добыча за dt: скорость одной колонии × число колоний; дробная часть копится в p.ether.</summary>
        public static void Produce(Planet p, float dt)
        {
            if (p.colonies <= 0) return;
            p.ether += Rate(p) * p.colonies * dt;
            int whole = Mathf.FloorToInt(p.ether);
            if (whole <= 0) return;
            int added = Stacks.Add(p.store, EtherId(p), whole, Free(p));
            p.ether -= added;
            if (added < whole) p.ether = Mathf.Min(p.ether, .999f); // хранилище полно — добыча стоит
        }

        /// <summary>Добавить постройку; первая занимает планету (и систему, если та ничья).</summary>
        public static void Build(Star s, Planet p, bool depot)
        {
            if (depot) p.depots++; else p.colonies++;
            p.owner = Owner.PLAYER;
            if (s.owner == Owner.NONE) s.owner = Owner.PLAYER;
        }

        /// <summary>Старые сохранения: колония без счётчиков — это одна колония, накопленный эфир — в хранилище.</summary>
        public static void Migrate(Planet p)
        {
            if (p.owner != Owner.PLAYER || Buildings(p) > 0) return;
            p.colonies = 1;
            int n = Mathf.FloorToInt(p.ether);
            if (n > 0) Stacks.Add(p.store, EtherId(p), n, Free(p));
            p.ether -= n;
        }
    }

    /// <summary>Операции со стопками предметов (трюм, хранилища планет).</summary>
    public static class Stacks
    {
        public static int Count(List<ItemStack> L, string id)
        {
            foreach (var s in L) if (s.id == id) return s.n;
            return 0;
        }

        public static int Used(List<ItemStack> L) { int n = 0; foreach (var s in L) n += s.n; return n; }

        /// <summary>Положить не больше free штук; возвращает, сколько положили.</summary>
        public static int Add(List<ItemStack> L, string id, int n, int free)
        {
            n = Mathf.Min(n, free);
            if (n <= 0) return 0;
            foreach (var s in L) if (s.id == id) { s.n += n; return n; }
            L.Add(new ItemStack { id = id, n = n });
            L.Sort((a, b) => Items.All.IndexOf(Items.Get(a.id)).CompareTo(Items.All.IndexOf(Items.Get(b.id))));
            return n;
        }

        public static bool Remove(List<ItemStack> L, string id, int n)
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
    }
}

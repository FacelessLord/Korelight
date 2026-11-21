using System;
using System.Collections.Generic;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    /// <summary>Отношения двух империй (a &lt; b).</summary>
    [Serializable]
    public class EmpRel
    {
        public int a, b, rel;
    }

    /// <summary>
    /// Дипломатия между империями. У соседних империй (столицы ближе CLOSE) свои отношения;
    /// враждующие соседи иногда нападают друг на друга. Нападения идут только у империй рядом с игроком.
    /// Если напали на вашего союзника (отношения 90+), приходит уведомление; за помощь в отражении —
    /// кредиты и скидка 20% в магазинах этой империи на 20 минут.
    /// </summary>
    public static class Diplo
    {
        public const float CLOSE = 1400;         // соседи: столицы ближе этого
        public const float NEAR_PLAYER = 2500;   // воюют только империи, чьи столицы не дальше этого от корабля
        public const int ALLY = 90;              // с какого уровня отношений империя — ваш союзник
        public const int WAR = -30;              // ниже — соседи враждуют
        public const float DISCOUNT = .8f, DISCOUNT_TIME = 1200; // скидка за помощь: 20% на 20 минут
        public const int MAX_WARS = 2;           // одновременных нападений империй друг на друга

        static readonly System.Random rnd = new System.Random();
        static float Rnd() => (float)rnd.NextDouble();

        public static bool Alive(int i) => i >= 0 && i < G.emp.Count && !G.emp[i].fallen && G.emp[i].capital >= 0 && G.emp[i].capital < G.stars.Count;

        public static Vector2 Cap(int i)
        {
            var s = G.stars[G.emp[i].capital];
            return new Vector2(s.x, s.y);
        }

        public static bool Close(int a, int b) => a != b && Alive(a) && Alive(b) && (Cap(a) - Cap(b)).magnitude < CLOSE;

        static EmpRel Find(int a, int b)
        {
            if (a > b) (a, b) = (b, a);
            foreach (var r in G.erels) if (r.a == a && r.b == b) return r;
            // первая встреча: от явной вражды до тёплой дружбы, чаще прохладно
            var n = new EmpRel { a = a, b = b, rel = Mathf.RoundToInt(-70 + 120 * (float)Px.Hash(a, b, G.seed + 911)) };
            G.erels.Add(n);
            return n;
        }

        public static int Rel(int a, int b) => Find(a, b).rel;

        public static void Add(int a, int b, int d)
        {
            if (a < 0 || b < 0 || a == b) return;
            var r = Find(a, b);
            r.rel = Mathf.Clamp(r.rel + d, -100, 100);
        }

        public static bool Discount(int ei) => ei >= 0 && ei < G.emp.Count && G.emp[ei].discountUntil > G.time;
        public static float DiscountK(int ei) => Discount(ei) ? G.emp[ei].discountK : 1;
        public static string DiscountTag(int ei) => Discount(ei) ? " · скидка " + Mathf.RoundToInt((1 - G.emp[ei].discountK) * 100) + "%" : "";

        /// <summary>Временная скидка в магазинах империи (более выгодная действующая не теряется).</summary>
        public static void GiveDiscount(int ei, float k, float time)
        {
            var e = G.emp[ei];
            if (Discount(ei) && e.discountK <= k) { e.discountUntil = Mathf.Max(e.discountUntil, G.time + time); return; }
            e.discountK = k; e.discountUntil = G.time + time;
        }

        /// <summary>Система под охраной после патруля — на неё не нападают.</summary>
        public static bool Safe(Star s)
        {
            foreach (var e in G.emp) if (e.safeStar == s.id && e.safeUntil > G.time) return true;
            return false;
        }

        /// <summary>Империя ei нападает на конкретную систему (после «Карательного рейда»).</summary>
        public static bool Strike(int ei, int star)
        {
            var s = G.stars[star];
            var ob = Battles.Of(star);
            if (!Owner.IsEmp(s.owner) || s.owner == ei || (ob != null && ob.raid != null)) return false;
            int def = s.owner, pi = s.planets.FindIndex(p => p.owner == def);
            if (pi < 0) return false;
            int n = 4 + rnd.Next(4) + G.emp[ei].warBoost;
            G.emp[ei].warBoost = 0;
            Battles.StartRaid(star, pi, ei, n, def);
            Add(ei, def, -5);
            return true;
        }

        /// <summary>Соседи империи ei, с которыми она враждует.</summary>
        public static List<int> Enemies(int ei)
        {
            var L = new List<int>();
            for (int o = 0; o < G.emp.Count; o++) if (Close(ei, o) && Rel(ei, o) < WAR) L.Add(o);
            return L;
        }

        // ---------- дипломатия игрока ----------

        public const float TALK_CD = 600; // отношения через переговоры меняются не чаще раза в 10 минут

        public static bool CanTalk(Empire e) => !e.talked || G.time - e.talkAt >= Faith.TalkCd;
        public static float TalkIn(Empire e) => CanTalk(e) ? 0 : Faith.TalkCd - (G.time - e.talkAt);

        /// <summary>Подарок: кредиты копятся как добрая воля и меняют отношения, когда это разрешено (раз в 10 минут).</summary>
        public static void Gift(int ei, int amount)
        {
            if (amount <= 0) return;
            P.credits -= amount;
            G.emp[ei].goodwill += amount;
            Faith.Event("gift", amount); // «Непривязанность»
            ApplyGoodwill(ei);
        }

        static void ApplyGoodwill(int ei)
        {
            var e = G.emp[ei];
            if (e.goodwill <= 0 || !CanTalk(e) || e.fallen) return;
            // щедрость окупается всё меньше: 100 кр. — заметно, 2000 — сильно, но не больше 20 за раз
            int d = Mathf.Clamp(Mathf.RoundToInt(3 * Mathf.Sqrt(e.goodwill / 100f) * Faith.GiftMul), 1, 30);
            e.goodwill = 0;
            ChangeRel(ei, d);
        }

        /// <summary>Недовольство в переговорах: мягко (0), претензия (1), оскорбление (2).</summary>
        public static readonly int[] COMPLAIN = { -6, -14, -25 };

        public static bool Complain(int ei, int level)
        {
            var e = G.emp[ei];
            if (!CanTalk(e)) return false;
            ChangeRel(ei, COMPLAIN[Mathf.Clamp(level, 0, COMPLAIN.Length - 1)]);
            return true;
        }

        /// <summary>Объявить войну: империя сразу становится враждебной (перерыв переговоров не нужен).</summary>
        public static void DeclareWar(int ei)
        {
            var e = G.emp[ei];
            if (e.rel >= WAR) Faith.Event("warfriend"); // «Переоценка всех ценностей»
            // «Слово чести»: без повода война — предательство в глазах единоверцев
            if (Faith.Has(Faith.BONDS) && !Provoked(e))
            {
                for (int o = 0; o < G.emp.Count; o++)
                    if (o != ei && Faith.SameFaith(o) && !G.emp[o].fallen) G.emp[o].rel = Mathf.Clamp(G.emp[o].rel - 30, -100, 100);
                Notes.Add("Единоверцы осуждают войну без повода: отношения с империями Уз Союза ухудшились", Notes.LOSS);
            }
            e.rel = Mathf.Min(e.rel, -60);
            e.goodwill = 0;
            e.talkAt = G.time; e.talked = true;
            Notes.Add("Вы объявили войну: " + e.name, Notes.ATTACK);
        }

        /// <summary>Изменение отношений игрока через дипломатию; уведомление, если изменился статус.</summary>
        static void ChangeRel(int ei, int d)
        {
            var e = G.emp[ei];
            string was = RelStr(e.rel);
            bool war = e.rel < WAR;
            e.rel = Mathf.Clamp(e.rel + d, -100, 100);
            e.talkAt = G.time; e.talked = true;
            string now = RelStr(e.rel);
            if (!war && e.rel < WAR) Notes.Add(e.name + " объявляет вам войну", Notes.ATTACK);
            else if (war && e.rel >= WAR) { Notes.Add(e.name + " прекращает войну с вами", Notes.INFO); Faith.Event("peace"); Quests.OnPeace(ei); }
            else if (now != was) Notes.Add(e.name + ": теперь " + now, Notes.INFO);
        }

        /// <summary>Есть повод для войны: империя нападала на вас за последний час.</summary>
        public static bool Provoked(Empire e) => G.time - e.attackedAt < 3600;

        // ---------- настроение империй: нападения при плохих отношениях, подарки при хороших ----------

        public const float MOOD_PERIOD = 60;    // раз в минуту каждая империя «вспоминает» об игроке
        public const float ATTACK_MAX = .06f;   // шанс нападения за проверку при отношениях −100 (≈ раз в 15–20 мин)
        public const float ATTACK_RANGE = 900;  // нападают на колонии, до которых от их систем не дальше этого
        public const int GIFT_FROM = 40;        // с каких отношений империя может прислать подарок
        public const float GIFT_MIN = .005f, GIFT_MAX = .025f; // шанс подарка за проверку: при 40 и при 100

        /// <summary>Шанс внезапного нападения без войны за минуту: только при отрицательных отношениях (−30…0),
        /// растёт как квадрат неприязни. Во время войны империя нападает чаще — через обычные налёты.</summary>
        public static float AttackChance(int rel) => rel >= 0 || rel < WAR ? 0 : ATTACK_MAX * (rel / 100f) * (rel / 100f);

        public const float DECLARE_MAX = .012f; // шанс объявить войну за минуту при отношениях на грани (−30)

        /// <summary>Шанс, что империя сама объявит войну: при отрицательных отношениях, чем ближе к −30, тем выше.</summary>
        public static float DeclareChance(int rel) => rel >= 0 || rel < WAR ? 0 : DECLARE_MAX * (rel / (float)WAR) * (rel / (float)WAR);

        public static float GiftChance(int rel) => rel < GIFT_FROM ? 0 : Mathf.Lerp(GIFT_MIN, GIFT_MAX, (rel - GIFT_FROM) / (100f - GIFT_FROM));

        static void Mood()
        {
            for (int ei = 0; ei < G.emp.Count; ei++)
            {
                var e = G.emp[ei];
                if (!Alive(ei)) continue;
                if (e.met && Rnd() < DeclareChance(e.rel))
                {
                    // терпение кончилось: империя сама объявляет войну
                    e.rel = Mathf.Min(e.rel, -50);
                    Notes.Add(e.name + " объявляет вам войну!", Notes.ATTACK);
                }
                else if (Rnd() < AttackChance(e.rel)) AttackPlayer(ei);
                else if (e.met && Faith.GiftsFrom(ei) && Rnd() < GiftChance(e.rel))
                {
                    // подарок: чем теплее отношения, тем щедрее
                    float k = (e.rel - GIFT_FROM) / (100f - GIFT_FROM);
                    int amount = Round5((50 + 250 * k) * (.6f + .8f * Rnd()));
                    P.credits += amount;
                    Notes.Add(e.name + " присылает вам подарок в знак дружбы: +" + amount + " кр.", Notes.INFO);
                }
            }
        }

        /// <summary>Империя сама нападает на ближайшую к своим системам колонию игрока (систему с Родиной не трогает).</summary>
        public static bool AttackPlayer(int ei)
        {
            int bestS = -1, bestP = -1;
            float bd = ATTACK_RANGE;
            foreach (var s in LoadedStars())
            {
                if (s.id == P.cur || Battles.Of(s.id) != null || s.planets.Exists(p => p.home)) continue;
                int pi = s.planets.FindIndex(p => p.owner == Owner.PLAYER && Colony.Buildings(p) > 0);
                if (pi < 0) continue;
                foreach (var o in G.stars)
                {
                    if (o.owner != ei) continue;
                    float d = Px.Dist(o, s);
                    if (d < bd) { bd = d; bestS = s.id; bestP = pi; }
                }
            }
            if (bestS < 0) return false;
            Battles.StartRaid(bestS, bestP, ei, Battles.DefaultRaid(ei) + rnd.Next(3));
            Notes.Add(G.emp[ei].name + " атакует колонию в системе " + G.stars[bestS].name + "!", Notes.ATTACK);
            return true;
        }

        public static void Tick(float dt, Vector2 ship)
        {
            for (int i = 0; i < G.emp.Count; i++) if (G.emp[i].goodwill > 0) ApplyGoodwill(i); // отложенные дары
            G.nextMood -= dt;
            if (G.nextMood <= 0) { G.nextMood = MOOD_PERIOD; Mood(); }
            G.nextEmpRaid -= dt;
            if (G.nextEmpRaid > 0) return;
            G.nextEmpRaid = 90 + Rnd() * 90;

            var near = new List<int>();
            for (int i = 0; i < G.emp.Count; i++) if (Alive(i) && (Cap(i) - ship).magnitude < NEAR_PLAYER) near.Add(i);
            var wars = new List<(int, int)>();
            for (int x = 0; x < near.Count; x++)
                for (int y = x + 1; y < near.Count; y++)
                {
                    int a = near[x], b = near[y];
                    if (!Close(a, b)) continue;
                    Add(a, b, rnd.Next(-4, 5)); // отношения соседей понемногу меняются
                    if (Rel(a, b) < WAR) wars.Add((a, b));
                }
            int active = 0;
            foreach (var r in G.raids) if (r.def >= 0) active++;
            if (active >= MAX_WARS || wars.Count == 0) return;
            var (p, q) = wars[rnd.Next(wars.Count)];
            if (rnd.Next(2) == 0) (p, q) = (q, p);
            StartWar(p, q);
        }

        /// <summary>Империя att нападает на ближайшую к своей столице систему империи def.</summary>
        public static bool StartWar(int att, int def)
        {
            var ca = Cap(att);
            Star best = null;
            float bd = float.MaxValue;
            foreach (var s in G.stars)
            {
                if (s.owner != def || s.core || s.id == G.home || Battles.Of(s.id) != null || Safe(s)) continue;
                float d = (new Vector2(s.x, s.y) - ca).magnitude;
                if (d < bd) { bd = d; best = s; }
            }
            if (best == null) return false;
            int pi = best.planets.FindIndex(p => p.owner == def);
            if (pi < 0) return false;
            Battles.StartRaid(best.id, pi, att, 4 + rnd.Next(4) + G.emp[att].warBoost, def);
            G.emp[att].warBoost = 0;
            Add(att, def, -5);
            if (G.emp[def].rel >= ALLY)
                Notes.Add("Нападение на союзника! " + G.emp[att].name + " атакует систему " + best.name + " (" + G.emp[def].name + "). Помогите отбить нападение!", Notes.ATTACK);
            return true;
        }
    }
}

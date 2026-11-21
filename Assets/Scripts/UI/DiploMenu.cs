using System;
using System.Collections.Generic;
using UnityEngine;
using static PixelSpace.Game;

namespace PixelSpace
{
    /// <summary>
    /// Меню «Дипломатия» в окне чужой планеты: дары, недовольство, объявление войны.
    /// Отношения через переговоры меняются не чаще раза в 10 минут (подарки при этом копятся).
    /// Здесь же позже появятся поручения и сюжетные задания империи.
    /// </summary>
    public static class DiploMenu
    {
        static GameManager gm => GameManager.I;
        static void Modal(string t, List<string> l, List<MBtn> b, Action esc) => gm.ui.Modal(t, l, b, esc);

        public static void Open(int ei, Action back)
        {
            var e = G.emp[ei];
            void Re() => Open(ei, back);
            bool war = e.rel < Diplo.WAR;
            var L = new List<string> { e.name, "Отношения: " + RelStr(e.rel) };
            if (war) L.Add("Вы в состоянии войны. Дары и время могут это изменить.");
            if (e.goodwill > 0) L.Add("Ваши дары ещё обсуждают при дворе.");
            if (!Diplo.CanTalk(e)) L.Add("Послы недавно говорили с вами. Новые переговоры повлияют на отношения через " + Minutes(Diplo.TalkIn(e)) + ".");

            var B = new List<MBtn>
            {
                new MBtn("Подарить кредиты", () => GiftMenu(ei, Re)),
                new MBtn("Выразить недовольство", () => ComplainMenu(ei, Re)),
            };
            FaithBlock(ei, L, B, Re);
            if (!war) B.Add(new MBtn("Объявить войну…", () => ConfirmWar(ei, Re), false, UiKit.Pink));
            var aq = Quests.Active(ei);
            B.Add(new MBtn(aq != null ? "Поручение: " + Quests.Title(aq) : "Поручения", () => QuestMenu(ei, Re)));
            B.Add(new MBtn("◀ Назад", back));
            Modal("Дипломатия", L, B, back);
        }

        // ---------- поручения ----------

        static void QuestMenu(int ei, Action back)
        {
            var e = G.emp[ei];
            void Re() => QuestMenu(ei, back);
            var q = Quests.Active(ei);
            var L = new List<string>();
            var B = new List<MBtn>();
            if (q != null)
            {
                L.Add(Quests.Title(q) + " — " + Quests.Desc(q));
                string pr = Quests.Progress(q);
                if (pr != "") L.Add("Сделано: " + pr);
                L.Add("Награда: " + q.reward + " кр. · осталось " + Quests.Left(q));
                if (Quests.HandIn(q))
                    B.Add(new MBtn("Сдать", () =>
                    {
                        if (!Quests.CanHandIn(q)) { gm.Toast(Quests.HandInWhy(q)); return; }
                        Quests.Deliver(q);
                        Save();
                        back();
                    }, !Quests.CanHandIn(q)));
                B.Add(new MBtn("Отказаться…", () => Modal("Отказаться от поручения?", new List<string> { "Отношения с " + e.name + " ухудшатся." }, new List<MBtn>
                {
                    new MBtn("Нет", Re),
                    new MBtn("Да, отказаться", () => { Quests.Abandon(q, "вы отказались"); Save(); back(); }, false, UiKit.Pink),
                }, Re)));
            }
            else
            {
                var offers = Quests.Offers(ei);
                if (offers.Count == 0) L.Add(e.name + " пока ничего не поручает. Загляните позже.");
                else L.Add("Можно взять одно поручение. Новые появляются раз в 20 минут.");
                foreach (var o in offers)
                {
                    var oq = o;
                    B.Add(new MBtn(Quests.Title(o) + " — " + o.reward + " кр.", () => Offer(oq, Re)));
                }
            }
            B.Add(new MBtn("◀ Назад", back));
            Modal("Поручения · " + e.name, L, B, back);
        }

        static void Offer(Quest q, Action back)
        {
            var L = new List<string>
            {
                Quests.Desc(q),
                "Награда: " + q.reward + " кр. · на выполнение " + Mathf.RoundToInt(q.dur / 60) + " мин",
            };
            if (Faith.InPath && P.faithPath == Faith.Of(q.ei)) L.Add("Это империя вашей будущей веры: поручение продвинет путь посвящения.");
            Modal(Quests.Title(q), L, new List<MBtn>
            {
                new MBtn("Взять поручение", () =>
                {
                    if (!Quests.Accept(q)) { gm.Toast("Сейчас это поручение не взять"); return; }
                    Save();
                    back();
                }),
                new MBtn("◀ Назад", back),
            }, back);
        }

        // ---------- вера ----------

        /// <summary>Вера империи и путь посвящения: открывается при дружелюбных отношениях и с артефактом веры.</summary>
        static void FaithBlock(int ei, List<string> L, List<MBtn> B, Action re)
        {
            var f = Faith.Get(Faith.Of(ei));
            if (f == null) return;
            L.Add("Вера: " + f.N);
            if (Faith.Has(f.Id)) { L.Add("Вы разделяете их веру."); return; }
            if (!string.IsNullOrEmpty(P.faith)) return; // у вас уже другая вера
            if (Faith.InPath)
            {
                if (P.faithPath != f.Id) return;
                var col = Px.HexC(f.Col);
                if (Faith.PathDone) { B.Add(new MBtn("Принять веру: " + f.N, () => ConfirmFaith(f, re), false, col)); return; }
                B.Add(new MBtn(f.N + ": испытания", () => PathStatus(f, re), false, col));
                var st = Faith.CurStep;
                // особые действия испытаний: послание (Узы) и подношение артефактов (Конклав)
                if (st.Ev == "mail")
                {
                    if (P.mailStar >= 0) L.Add("Послание везут в систему " + G.stars[P.mailStar].name + " — осталось " + Minutes(P.mailUntil - G.time) + ".");
                    else B.Add(new MBtn("Взять послание", () =>
                    {
                        int to = Faith.TakeMail(ei);
                        if (to < 0) { gm.Toast("Сейчас некуда везти послание"); return; }
                        Notes.Add("Послание нужно доставить в систему " + G.stars[to].name + " за 20 минут", Notes.INFO);
                        Save();
                        re();
                    }, false, col));
                }
                if (st.Ev == "donate")
                {
                    int arts = 0;
                    foreach (var s in P.items) { var d = Items.Get(s.id); if (d != null && d.Kind == ItemKind.Artifact) arts += s.n; }
                    B.Add(new MBtn("Отдать храму артефакты (" + arts + ")", () =>
                    {
                        int n = Faith.Donate();
                        gm.Toast("Храм принимает " + n + " артефактов");
                        Save();
                        re();
                    }, arts == 0, col));
                }
                return;
            }
            if (f.Id == Faith.ANCIENT) return;
            if (G.emp[ei].rel < Faith.REL_NEED) B.Add(new MBtn(f.N + " — нужны дружелюбные отношения", null, true));
            else if (!Faith.CanBegin(f)) B.Add(new MBtn(f.N + " — нужен артефакт «" + Items.Get(f.Art).N + "»", null, true));
            else B.Add(new MBtn("Путь посвящения: " + f.N, () => PathIntro(f, re), false, Px.HexC(f.Col)));
        }

        static List<string> Steps(FaithDef f)
        {
            var L = new List<string>();
            bool mine = P.faithPath == f.Id;
            for (int k = 0; k < f.Steps.Length; k++)
            {
                var st = f.Steps[k];
                string mark = !mine ? (k + 1) + "." : k < P.faithStep ? "√" : k == P.faithStep ? "→" : "·";
                string prog = mine && k == P.faithStep && st.Goal > 1 ? " (" + P.faithProg + "/" + st.Goal + ")" : "";
                L.Add(mark + " " + st.N + ": " + st.D + prog);
            }
            return L;
        }

        static string Shrine(FaithDef f) => f.Id == Faith.ANCIENT
            ? "святыни всех восьми вер"
            : "«" + Items.Get(f.Art).N + "»";

        static void PathIntro(FaithDef f, Action back)
        {
            var L = new List<string>
            {
                f.D,
                (f.Id == Faith.ANCIENT ? "Сияние ядра пробуждает " : "Жрецы узнают ") + Shrine(f) + " в вашем трюме. Святыни останутся здесь.",
                "Путь посвящения — " + f.Steps.Length + " испытания. Они тяжелы:",
            };
            L.AddRange(Steps(f));
            Modal(f.N, L, new List<MBtn>
            {
                new MBtn("Ступить на путь", () =>
                {
                    if (!Faith.Begin(f.Id)) { gm.Toast("Не получилось: нужны святыни"); return; }
                    Save();
                    PathStatus(f, back);
                }, false, Px.HexC(f.Col)),
                new MBtn("◀ Назад", back),
            }, back);
        }

        static void PathStatus(FaithDef f, Action back)
        {
            var L = new List<string> { "Путь посвящения: " + f.N };
            L.AddRange(Steps(f));
            if (Faith.PathDone) L.Add("Все испытания пройдены — можно принять веру.");
            Modal(f.N, L, new List<MBtn> { new MBtn("◀ Назад", back) }, back);
        }

        static void ConfirmFaith(FaithDef f, Action back)
        {
            var rival = Faith.Get(f.Rival);
            var L = new List<string>
            {
                "Принять веру «" + f.N + "»? Сменить её потом будет непросто.",
                f.AbN + " — " + UiDur(f.AbTime) + ": " + f.AbD + ". Откат " + UiDur(f.AbCd) + ". Клавиша F или иконка в панели оружия.",
                f.Bonus,
                f.Minus,
            };
            if (f.Id != Faith.ANCIENT)
                L.Add("Единоверцы станут относиться к вам лучше" + (rival != null ? ", а империи веры «" + rival.N + "» — хуже." : "."));
            Modal("Принять веру", L, new List<MBtn>
            {
                new MBtn("Принять веру", () => { Faith.Accept(); Save(); back(); }, false, Px.HexC(f.Col)),
                new MBtn("Пока нет", back),
            }, back);
        }

        static string UiDur(float sec) => Ui.Dur(sec);

        /// <summary>Ядро галактики: начало и принятие «Наследия предтеч».</summary>
        public static void CoreVisit()
        {
            if (gm.ui.ModalOpen) return;
            var f = Faith.Get(Faith.ANCIENT);
            if (P.faithPath == Faith.ANCIENT)
            {
                if (Faith.PathDone) ConfirmFaith(f, Menus.Close);
                return;
            }
            if (Faith.CanBegin(f)) PathIntro(f, Menus.Close);
        }

        static string Minutes(float sec) => sec >= 60 ? Mathf.CeilToInt(sec / 60) + " мин" : Mathf.CeilToInt(sec) + " с";

        // ---------- подарки ----------

        static readonly int[] GIFTS = { 100, 500, 1000, 5000 };

        static void GiftMenu(int ei, Action back)
        {
            var e = G.emp[ei];
            int have = Mathf.FloorToInt(P.credits);
            var L = new List<string> { "Подарок для: " + e.name, "У вас: " + have + " кр." };
            var B = new List<MBtn>();
            foreach (int n in GIFTS)
            {
                int amount = n;
                B.Add(new MBtn(amount + " кр.", () => Give(ei, amount, back), have < amount));
            }
            B.Add(new MBtn("Другая сумма…", () => gm.ui.Prompt("Подарок", new List<string> { "Сколько кредитов подарить? У вас: " + have + " кр." },
                "", 7, "Подарить", s =>
                {
                    if (!int.TryParse(s.Trim(), out int n) || n <= 0) { gm.Toast("Введите сумму"); return; }
                    if (n > P.credits) { gm.Toast("Столько кредитов нет"); return; }
                    Give(ei, n, back);
                }, () => GiftMenu(ei, back)), have < 1));
            B.Add(new MBtn("◀ Назад", back));
            Modal("Подарить кредиты", L, B, back);
        }

        static void Give(int ei, int amount, Action back)
        {
            Diplo.Gift(ei, amount);
            gm.Toast(G.emp[ei].name + " принимает дар: " + amount + " кр.");
            Save();
            back();
        }

        // ---------- недовольство ----------

        static readonly string[] COMPLAIN_BTN = { "Мягко упрекнуть", "Предъявить претензию", "Оскорбить посла" };
        static readonly string[] COMPLAIN_REPLY =
        {
            "Посол сдержанно кивает: «Мы примем ваши слова к сведению».",
            "Посол хмурится: «Подобный тон не пойдёт на пользу нашей дружбе».",
            "Посол бледнеет и покидает зал, не сказав ни слова.",
        };

        static void ComplainMenu(int ei, Action back)
        {
            var e = G.emp[ei];
            bool can = Diplo.CanTalk(e);
            var L = new List<string> { "Разговор с послом: " + e.name, "Чем резче слова, тем сильнее испортятся отношения." };
            if (!can) L.Add("Посол ещё не оправился от прошлой беседы. Можно будет поговорить через " + Minutes(Diplo.TalkIn(e)) + ".");
            var B = new List<MBtn>();
            for (int k = 0; k < COMPLAIN_BTN.Length; k++)
            {
                int lvl = k;
                B.Add(new MBtn(COMPLAIN_BTN[k], () =>
                {
                    if (!Diplo.Complain(ei, lvl)) return;
                    Save();
                    Modal("Дипломатия", new List<string> { COMPLAIN_REPLY[lvl] }, new List<MBtn> { new MBtn("◀ Назад", back) }, back);
                }, !can));
            }
            B.Add(new MBtn("◀ Назад", back));
            Modal("Выразить недовольство", L, B, back);
        }

        // ---------- война ----------

        static void ConfirmWar(int ei, Action back)
        {
            var e = G.emp[ei];
            var L = new List<string>
            {
                "Объявить войну: " + e.name + "?",
                "Охрана их систем будет нападать на вас, рынки закроются, а их флот сможет атаковать ваши колонии.",
            };
            if (e.rel >= 30) L.Add("Сейчас вы " + RelStr(e.rel) + "! Восстановить такие отношения будет очень непросто.");
            // «Нет» — первой кнопкой, чтобы случайный клик или Enter не начал войну
            Modal("Объявление войны", L, new List<MBtn>
            {
                new MBtn("Нет, сохранить мир", back),
                new MBtn("Да, объявить войну", () =>
                {
                    Diplo.DeclareWar(ei);
                    // вы прямо в их системе — охрана нападает сразу
                    if (Cur.owner == ei && gm.mode == GameManager.Mode.System)
                        Battles.SpawnGuards(Battles.Get(P.cur), new Vector2(gm.sys.ship.x, gm.sys.ship.y), ei, 2 + P.lv.las);
                    Save();
                    back();
                }, false, UiKit.Pink),
            }, back);
        }
    }
}

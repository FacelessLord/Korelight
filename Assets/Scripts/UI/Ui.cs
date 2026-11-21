using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static PixelSpace.Game;
using static PixelSpace.UiKit;

namespace PixelSpace
{
    public readonly struct MBtn
    {
        public readonly string label;
        public readonly Action act;
        public readonly bool disabled;
        public readonly Color? col; // цвет подписи (например, образец цвета империи)
        public MBtn(string label, Action act, bool disabled = false, Color? col = null) { this.label = label; this.act = act; this.disabled = disabled; this.col = col; }
    }

    /// <summary>HUD, нижние панели, модальные окна и тосты (uGUI поверх пиксельного кадра).</summary>
    public sealed class Ui
    {
        readonly GameManager gm;
        public readonly WorldInput worldInput;
        readonly RawImage world;
        readonly RectTransform safe;

        readonly Text cr, cg, loc;
        readonly RectTransform enb, hpb;
        readonly GameObject sbar, gbar, panel;
        readonly HoldButton fire;
        readonly Button act;
        readonly Text actText;

        readonly GameObject modalRoot;
        readonly RectTransform overlay, box, content;
        readonly RectTransform head, foot, scrollRt; // закреплённые шапка (заголовок, вкладки) и низ (кнопки) — вне прокрутки
        const float PAD = 14, PIN_GAP = 8;
        readonly ScrollRect scroll;

        readonly RectTransform toastRoot;
        readonly List<(GameObject go, CanvasGroup g, float t)> toasts = new List<(GameObject, CanvasGroup, float)>();

        Rect lastSafe;
        string panelSig = "";

        readonly RectTransform tip;
        readonly Text tipTitle, tipText;
        readonly RawImage tipShields;
        readonly LayoutElement tipShieldsLE;
        Texture2D tipShieldTex;
        string tipShieldSig = "";

        // значок щита 7×8: o — контур, f — заполнение
        static readonly string[] SHIELD =
        {
            ".ooooo.",
            "offfffo",
            "offfffo",
            "offfffo",
            "offfffo",
            ".offfo.",
            "..ofo..",
            "...o...",
        };
        static readonly Color32 shOut = new Color32(0xbf, 0xf6, 0xff, 255), shFill = new Color32(0x3a, 0xa6, 0xe0, 255),
            shEmpty = new Color32(0x1a, 0x2a, 0x40, 255), shBrokenOut = new Color32(0x5a, 0x56, 0x70, 255),
            shBrokenFill = new Color32(0x24, 0x1f, 0x35, 255), shCrack = new Color32(0x8a, 0x86, 0xa8, 255);
        const int SH_W = 7, SH_H = 8, SH_GAP = 2, SH_SCALE = 2;

        /// <summary>Ряд щитов планеты в подсказке: пробитые — серые с трещиной, текущий — заполнен по прочности.</summary>
        void SetTipShields(Planet p)
        {
            bool show = p != null && Battles.Defended(p);
            if (tipShields.gameObject.activeSelf != show) tipShields.gameObject.SetActive(show);
            if (!show) return;
            int total = Battles.ShieldsOf(p), broken = p.surrendered ? total : Mathf.Min(p.broken, total);
            int fill = Mathf.CeilToInt(Battles.ShieldFrac(p) * 6); // строк заполнения у текущего щита (0..6)
            string sig = total + "," + broken + "," + fill;
            if (sig == tipShieldSig) return;
            tipShieldSig = sig;
            int w = total * (SH_W + SH_GAP) - SH_GAP;
            if (tipShieldTex == null || tipShieldTex.width != w)
            {
                if (tipShieldTex != null) UnityEngine.Object.Destroy(tipShieldTex);
                tipShieldTex = new Texture2D(w, SH_H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            }
            var px = new Color32[w * SH_H];
            for (int i = 0; i < total; i++)
            {
                int ox = i * (SH_W + SH_GAP);
                bool isBroken = i < broken, cur = i == broken;
                for (int y = 0; y < SH_H; y++)
                    for (int x = 0; x < SH_W; x++)
                    {
                        char ch = SHIELD[y][x];
                        if (ch == '.') continue;
                        Color32 c;
                        if (isBroken)
                        {
                            c = ch == 'o' ? shBrokenOut : shBrokenFill;
                            if (ch == 'f' && (x == 3 && y < 3 || x == 2 && (y == 3 || y == 4) || x == 3 && y == 5)) c = shCrack; // трещина
                        }
                        else if (ch == 'o') c = shOut;
                        else c = !cur || 6 - y <= fill ? shFill : shEmpty; // текущий щит «вытекает» сверху вниз
                        px[(SH_H - 1 - y) * w + ox + x] = c; // в текстуре y снизу вверх
                    }
            }
            tipShieldTex.SetPixels32(px);
            tipShieldTex.Apply();
            tipShields.texture = tipShieldTex;
            tipShieldsLE.preferredWidth = w * SH_SCALE;
            tipShieldsLE.preferredHeight = SH_H * SH_SCALE;
        }

        /// <summary>Показать подсказку рядом с курсором (screen — экранные пиксели, y вверх).
        /// shields — планета, чьи щиты нарисовать значками под заголовком.</summary>
        public void ShowTip(string title, Color titleCol, string text, Vector2 screen, Planet shields = null)
        {
            SetTipShields(shields);
            if (tipTitle.text != title) tipTitle.text = title;
            tipTitle.color = titleCol;
            if (tipText.text != text) tipText.text = text;
            tipText.gameObject.SetActive(!string.IsNullOrEmpty(text)); // подсказка может быть из одного заголовка
            tip.gameObject.SetActive(true);
            var root = (RectTransform)tip.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local);
            LayoutRebuilder.ForceRebuildLayoutImmediate(tip);
            // справа-снизу от курсора; у правого/нижнего края — переносим на другую сторону
            var half = root.rect.size / 2;
            bool flipX = local.x + 16 + tip.rect.width > half.x, flipY = local.y - 16 - tip.rect.height < -half.y;
            tip.pivot = new Vector2(flipX ? 1 : 0, flipY ? 0 : 1);
            tip.anchoredPosition = local + new Vector2(flipX ? -12 : 16, flipY ? 12 : -16);
            tip.SetAsLastSibling();
        }

        /// <summary>
        /// Подсказка, которая не заслоняет линию: прямоугольник целиком лежит по сторону side от прямой a–b
        /// (все точки — экранные пиксели, y вверх). Если по эту сторону не помещается — пробуем другую,
        /// а если и там нет места — обычное размещение у курсора.
        /// </summary>
        public void ShowTipAway(string title, Color titleCol, string text, Vector2 screen, Vector2 a, Vector2 b, Vector2 side)
        {
            ShowTip(title, titleCol, text, screen);
            var root = (RectTransform)tip.parent;
            Vector2 L(Vector2 sp) { RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out var l); return l; }
            Vector2 c = L(screen), la = L(a), lb = L(b);
            Vector2 dirL = (lb - la).normalized;
            var normal = new Vector2(-dirL.y, dirL.x);
            var sideL = (L(screen + side * 10) - c);
            if (Vector2.Dot(normal, sideL) < 0) normal = -normal; // нормаль к прямой, смотрящая в нужную сторону
            var half = root.rect.size / 2;
            var size = tip.rect.size;
            foreach (var n in new[] { normal, -normal })
            {
                // угол прямоугольника у курсора, сам прямоугольник растёт в сторону n
                var min = new Vector2(n.x >= 0 ? c.x : c.x - size.x, n.y >= 0 ? c.y : c.y - size.y);
                float nearest = float.MaxValue;
                foreach (var k in new[] { min, min + new Vector2(size.x, 0), min + new Vector2(0, size.y), min + size })
                    nearest = Mathf.Min(nearest, Vector2.Dot(k - la, n));
                min += n * Mathf.Max(0, TIP_GAP - nearest); // отодвигаем от линии
                // не вылезать за экран
                min.x = Mathf.Clamp(min.x, -half.x + 4, half.x - size.x - 4);
                min.y = Mathf.Clamp(min.y, -half.y + 4, half.y - size.y - 4);
                float worst = float.MaxValue;
                foreach (var k in new[] { min, min + new Vector2(size.x, 0), min + new Vector2(0, size.y), min + size })
                    worst = Mathf.Min(worst, Vector2.Dot(k - la, n));
                if (worst < TIP_GAP * .5f) continue; // после сдвига к краю экрана опять задели линию
                tip.pivot = Vector2.zero;
                tip.anchoredPosition = min;
                return;
            }
        }

        const float TIP_GAP = 10; // зазор между подсказкой и линией пути (в единицах интерфейса)

        public void HideTip()
        {
            if (tip.gameObject.activeSelf) tip.gameObject.SetActive(false);
        }

        public CardWindow cards;
        public bool ModalOpen => modalRoot.activeSelf || (cards != null && cards.Open);
        public bool FireHeld => fire.Held;

        static Canvas MkCanvas(string name, int order, bool scaled)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)) { layer = 5 };
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            c.pixelPerfect = false;
            var sc = go.GetComponent<CanvasScaler>();
            if (scaled)
            {
                sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                // десктоп: при 720p один юнит = пиксель, интерфейс компактный; телефон: крупные кнопки под палец
                sc.referenceResolution = Desktop ? new Vector2(1280, 720) : new Vector2(400, 800);
                sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                sc.matchWidthOrHeight = .5f;
            }
            else sc.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            return c;
        }

        public Ui(GameManager gm)
        {
            this.gm = gm;
            InitFont();

            // ---- слой мира: низкое разрешение, растянутое без сглаживания ----
            var wc = MkCanvas("WorldCanvas", 0, false);
            var wrt = Rt("World", wc.transform);
            wrt.anchorMin = wrt.anchorMax = wrt.pivot = new Vector2(0, 1);
            world = wrt.gameObject.AddComponent<RawImage>();
            world.raycastTarget = true;
            worldInput = wrt.gameObject.AddComponent<WorldInput>();

            // ---- интерфейс ----
            var uc = MkCanvas("UiCanvas", 1, true);
            safe = Rt("Safe", uc.transform);
            Stretch(safe);

            // верхний HUD
            var top = Rt("Top", safe);
            top.anchorMin = new Vector2(0, 1); top.anchorMax = Vector2.one; top.pivot = new Vector2(.5f, 1);
            top.sizeDelta = new Vector2(0, 0);
            Img(top, new Color(0, 0, 0, .55f));
            var tv = VLG(top.gameObject, 6, 8);
            top.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var row = Rt("Row", top);
            HLG(row.gameObject, 12);
            cr = HudItem(row, Sprites.IcoCoin, () => ("Кредиты", Gold, ""));
            enb = HudBar(row, Sprites.IcoBolt, Green, () => ("Энергия", Green, Mathf.FloorToInt(P.energy) + " / " + Mathf.FloorToInt(MaxE())));
            hpb = HudBar(row, Sprites.IcoHeart, Pink, () => ("Корпус", Pink, Mathf.CeilToInt(P.hull) + " / " + Mathf.FloorToInt(MaxH())));
            cg = HudItem(row, Sprites.IcoGem, () => ("Трюм", Px.HexC("#6fe3ff"), ""), Trade.OpenHold);
            loc = Label(top, "", Px.HexC("#b9b0ff"), Mathf.Max(8, FS - 1));

            // тосты
            toastRoot = Rt("Toasts", safe);
            if (Desktop)
            {
                // узкая колонка по центру сверху, а не во всю ширину
                toastRoot.anchorMin = toastRoot.anchorMax = toastRoot.pivot = new Vector2(.5f, 1);
                toastRoot.anchoredPosition = new Vector2(0, -52); toastRoot.sizeDelta = new Vector2(420, 0);
            }
            else
            {
                toastRoot.anchorMin = new Vector2(0, 1); toastRoot.anchorMax = Vector2.one; toastRoot.pivot = new Vector2(.5f, 1);
                toastRoot.anchoredPosition = new Vector2(0, -64); toastRoot.sizeDelta = new Vector2(-24, 0);
            }
            VLG(toastRoot.gameObject, 6).childAlignment = TextAnchor.UpperCenter;
            toastRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // нижняя панель системы
            var sb = BottomBar("SysBar");
            sbar = sb.gameObject;
            Btn(sb, "Карта", () => { if (!gm.Dead) gm.OpenGalaxy(); }, out _);
            Btn(sb, "Трюм", () => { if (!gm.Dead) Trade.OpenHold(); }, out _);
            Btn(sb, "Пути", () => { if (!gm.Dead) gm.routes.Show(false); }, out _);
            Btn(sb, "Журнал", () => { if (!gm.Dead) Menus.Journal(); }, out _);
            act = Btn(sb, "", () => { if (gm.sys.near >= 0 && !gm.Dead) Menus.Planet(gm.sys.near); }, out actText, TextAnchor.MiddleCenter, Px.HexC("#183a2a"), Green);

            // кнопка огня
            var frt = Rt("Fire", safe);
            frt.anchorMin = frt.anchorMax = new Vector2(1, 0);
            frt.pivot = new Vector2(1, 0);
            frt.anchoredPosition = new Vector2(-14, 70);
            frt.sizeDelta = new Vector2(78, 78);
            var fimg = Img(frt, Px.HexC("#7a1530"), true);
            fimg.sprite = CircleSprite(128);
            var fo = frt.gameObject.AddComponent<Outline>(); fo.effectColor = Px.HexC("#ff6a8a"); fo.effectDistance = new Vector2(2, -2);
            fire = frt.gameObject.AddComponent<HoldButton>();
            fire.img = fimg; fire.normal = Px.HexC("#7a1530"); fire.pressed = Px.HexC("#c42a50");
            var fl = Label(frt, "ОГОНЬ", Color.white, 0, TextAnchor.MiddleCenter);
            Stretch(fl.rectTransform);

            // панель выбранной звезды
            var prt = Rt("Panel", safe);
            if (Desktop)
            {
                // компактная карточка в левом нижнем углу над панелью кнопок
                prt.anchorMin = prt.anchorMax = prt.pivot = Vector2.zero;
                prt.anchoredPosition = new Vector2(8, 46); prt.sizeDelta = new Vector2(380, 0);
            }
            else
            {
                prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 0); prt.pivot = new Vector2(.5f, 0);
                prt.anchoredPosition = new Vector2(0, 64); prt.sizeDelta = new Vector2(-16, 0);
            }
            Img(prt, new Color(BoxBg.r, BoxBg.g, BoxBg.b, .93f), true);
            Frame(prt.gameObject);
            VLG(prt.gameObject, 6, Desktop ? 8 : 10);
            prt.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            panel = prt.gameObject;

            // нижняя панель карты
            var gb = BottomBar("GalBar");
            gbar = gb.gameObject;
            Btn(gb, "В систему", () => { if (gm.gal.travel == null) gm.EnterSystemAnimated(P.cur); }, out _);
            Btn(gb, "Трюм", Trade.OpenHold, out _);
            Btn(gb, "Пути", () => gm.routes.Show(true), out _);
            Btn(gb, "Журнал", () => Menus.Journal(), out _);
            var sp = Rt("Spacer", gb); sp.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            Btn(gb, "Меню", Menus.MainMenu, out _);

            // всплывающая подсказка у курсора (наведение на планету)
            tip = Rt("Tip", safe.parent);
            tip.anchorMin = tip.anchorMax = new Vector2(.5f, .5f);
            Img(tip, new Color(BoxBg.r, BoxBg.g, BoxBg.b, .95f));
            Frame(tip.gameObject, 2, 3);
            var tv2 = VLG(tip.gameObject, 6, 7);
            tv2.childForceExpandWidth = false;
            var tf = tip.gameObject.AddComponent<ContentSizeFitter>();
            tf.horizontalFit = tf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            tipTitle = Label(tip, "", Gold);   // название — отдельной строкой и своим цветом
            tipTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            tipShields = Rt("Shields", tip).gameObject.AddComponent<RawImage>();
            tipShields.raycastTarget = false;
            tipShieldsLE = tipShields.gameObject.AddComponent<LayoutElement>();
            tipShields.gameObject.SetActive(false);
            tipText = Label(tip, "", TextCol);
            tipText.supportRichText = true; // строки подсказки можно красить (например, ресурсы пути)
            tipText.horizontalOverflow = HorizontalWrapMode.Overflow;
            tip.gameObject.SetActive(false);

            // модальное окно
            overlay = Rt("Modal", safe.parent);
            Stretch(overlay);
            Img(overlay, new Color(0, 0, 0, .66f), true);
            modalRoot = overlay.gameObject;
            box = Rt("Box", overlay);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(.5f, .5f);
            Img(box, BoxBg, true);
            Frame(box.gameObject, 2, 4);
            // шапка и низ окна: в окнах с вкладками (журнал) там заголовок, вкладки и кнопки — их не надо прокручивать
            head = Rt("Head", box);
            head.anchorMin = new Vector2(0, 1); head.anchorMax = Vector2.one; head.pivot = new Vector2(.5f, 1);
            head.anchoredPosition = new Vector2(0, -PAD); head.sizeDelta = new Vector2(-2 * PAD, 0);
            VLG(head.gameObject, 8);
            head.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            foot = Rt("Foot", box);
            foot.anchorMin = Vector2.zero; foot.anchorMax = new Vector2(1, 0); foot.pivot = new Vector2(.5f, 0);
            foot.anchoredPosition = new Vector2(0, PAD); foot.sizeDelta = new Vector2(-2 * PAD, 0);
            VLG(foot.gameObject, 8);
            foot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var srt = Rt("Scroll", box);
            scrollRt = srt;
            Stretch(srt, PAD, PAD, PAD, PAD);
            scroll = srt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 20;
            var vp = Rt("Viewport", srt);
            Stretch(vp);
            Img(vp, new Color(0, 0, 0, 0), true);
            vp.gameObject.AddComponent<RectMask2D>();
            content = Rt("Content", vp);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            VLG(content.gameObject, 8);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = vp; scroll.content = content;
            modalRoot.SetActive(false);

            // окно с карточками (трюм, магазин)
            cards = new CardWindow(safe.parent);
            BuildRoutePanel();
            BuildWeaponPanel();
            BuildNotes();
        }

        RectTransform BottomBar(string name)
        {
            var b = Rt(name, safe);
            b.anchorMin = new Vector2(0, 0); b.anchorMax = new Vector2(1, 0); b.pivot = new Vector2(.5f, 0);
            b.anchoredPosition = new Vector2(0, Desktop ? 8 : 10); b.sizeDelta = new Vector2(-16, Desktop ? 28 : 40);
            HLG(b.gameObject, 8, TextAnchor.LowerLeft);
            return b;
        }

        /// <summary>Невидимая подложка, ловящая наведение, + подсказка для элемента HUD.</summary>
        static void Hint(RectTransform it, Func<(string, Color, string)> hint, Action click = null)
        {
            Img(it, new Color(0, 0, 0, 0), true);
            var h = it.gameObject.AddComponent<HudHint>();
            h.Get = hint; h.Click = click;
        }

        Text HudItem(RectTransform row, PixelSprite icon, Func<(string, Color, string)> hint, Action click = null)
        {
            var it = Rt("Item", row);
            HLG(it.gameObject, 5);
            Hint(it, hint, click);
            Icon(it, icon, 14);
            return Label(it, "0", TextCol, 0, TextAnchor.MiddleLeft);
        }

        RectTransform HudBar(RectTransform row, PixelSprite icon, Color fill, Func<(string, Color, string)> hint)
        {
            var it = Rt("Item", row);
            HLG(it.gameObject, 5);
            Hint(it, hint);
            Icon(it, icon, 14);
            var bar = Rt("Bar", it);
            Img(bar, Px.HexC("#110d24"));
            var o = bar.gameObject.AddComponent<Outline>(); o.effectColor = Border; o.effectDistance = new Vector2(2, -2);
            var le = bar.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = 54; le.preferredHeight = le.minHeight = 8;
            var f = Rt("Fill", bar);
            f.anchorMin = Vector2.zero; f.anchorMax = Vector2.one; f.offsetMin = f.offsetMax = Vector2.zero;
            Img(f, fill);
            return f;
        }

        // ---------- кадр мира ----------

        public void SetWorld(Texture tex, int W, int H, int S)
        {
            world.texture = tex;
            world.rectTransform.sizeDelta = new Vector2(W * S, H * S);
        }

        // ---------- тосты ----------

        public void Toast(string s)
        {
            var rt = Rt("Toast", toastRoot);
            Img(rt, new Color(BoxBg.r, BoxBg.g, BoxBg.b, .93f));
            var o = rt.gameObject.AddComponent<Outline>(); o.effectColor = Gold; o.effectDistance = new Vector2(2, -2);
            var v = VLG(rt.gameObject, 0, 8);
            v.padding = Desktop ? new RectOffset(7, 7, 5, 5) : new RectOffset(9, 9, 7, 7);
            Label(rt, s, TextCol, 0, TextAnchor.MiddleCenter);
            toasts.Add((rt.gameObject, rt.gameObject.AddComponent<CanvasGroup>(), 0));
        }

        public void Tick(float dt)
        {
            RefreshWeapons();
            TickNotes(dt);
            cards.Tick();
            TickToasts(dt);
        }

        void TickToasts(float dt)
        {
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var (go, g, t) = toasts[i];
                t += dt;
                g.alpha = t < 2.8f ? 1 : Mathf.Clamp01(1 - (t - 2.8f) / .5f);
                if (t >= 3.4f) { UnityEngine.Object.Destroy(go); toasts.RemoveAt(i); }
                else toasts[i] = (go, g, t);
            }

            var sa = Screen.safeArea;
            if (sa != lastSafe && Screen.width > 0 && Screen.height > 0)
            {
                lastSafe = sa;
                safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
                safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
                safe.offsetMin = safe.offsetMax = Vector2.zero;
            }

            // размер окна зависит от размера холста: при старте и повороте экрана он меняется
            if (ModalOpen && overlay.rect.size != boxFor) LayoutBox();
        }

        Vector2 boxFor;

        void LayoutBox()
        {
            var area = overlay.rect;
            boxFor = area.size;
            if (area.width <= 0 || area.height <= 0) return;
            float w = Mathf.Min(440, area.width - 24);
            box.sizeDelta = new Vector2(w, 100);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            LayoutRebuilder.ForceRebuildLayoutImmediate(head);
            LayoutRebuilder.ForceRebuildLayoutImmediate(foot);
            float hh = head.childCount > 0 ? LayoutUtility.GetPreferredHeight(head) + PIN_GAP : 0;
            float fh = foot.childCount > 0 ? LayoutUtility.GetPreferredHeight(foot) + PIN_GAP : 0;
            // прокручивается только середина: между шапкой и низом
            scrollRt.offsetMax = new Vector2(-PAD, -PAD - hh);
            scrollRt.offsetMin = new Vector2(PAD, PAD + fh);
            float h = LayoutUtility.GetPreferredHeight(content) + hh + fh + 2 * PAD;
            box.sizeDelta = new Vector2(w, Mathf.Min(h, area.height * .88f));
        }

        // ---------- модальные окна ----------

        /// <summary>Для окон, которые Esc закрывать не должен (например, обязательный ввод).</summary>
        public static readonly Action NoEsc = () => { };

        Action escAction;

        /// <summary>Esc: выполнить «назад» текущего окна.</summary>
        public void Escape()
        {
            if (cards.Open) cards.Escape();
            else if (modalRoot.activeSelf) (escAction ?? CloseModal)();
        }

        /// <param name="esc">Что делает Esc: по умолчанию закрывает окно; NoEsc — ничего.</param>
        /// <summary>Метка открытого окна (например, «planet» — меню планеты закрывается и по E).</summary>
        public string ModalTag;

        public void Modal(string title, IList<string> lines, IList<MBtn> buttons, Action esc = null, bool richText = false, IList<Tab> tabs = null)
        {
            escAction = esc;
            ModalTag = null;
            if (cards.Open) cards.Hide();
            gm.ReleaseInput();
            foreach (var rt in new[] { content, head, foot })
            {
                foreach (Transform ch in rt) UnityEngine.Object.Destroy(ch.gameObject);
                rt.DetachChildren();
            }
            bool pin = tabs != null; // окна с вкладками: заголовок, вкладки и кнопки закреплены
            var top = pin ? head : content;
            var bottom = pin ? foot : content;

            var t = Label(top, title, Gold, FS + 2);
            t.lineSpacing = 1.2f;
            if (tabs != null)
            {
                // вкладки под заголовком: активная — с золотой рамкой
                var row = Rt("Tabs", top);
                HLG(row.gameObject, 8);
                foreach (var tab in tabs)
                {
                    var act = tab.Act;
                    Btn(row, tab.Label, () => act(), out var tt, TextAnchor.MiddleCenter, null, tab.Active ? Gold : (Color?)null);
                    tt.color = tab.Active ? Gold : Color.white;
                }
            }
            foreach (var l in lines)
            {
                if (l.Length > 2 && l[0] == '\u0001')
                {
                    // строка с цветным квадратом: квадрат во всю высоту строки, справа — текст
                    int e = l.IndexOf('\u0001', 1);
                    var row = Rt("Row", content);
                    var h = HLG(row.gameObject, Mathf.Round(FS * .6f), TextAnchor.UpperLeft);
                    var sq = Rt("Sq", row);
                    var img = sq.gameObject.AddComponent<Image>();
                    img.color = Px.HexC(l.Substring(1, e - 1)); img.raycastTarget = false;
                    float side = Mathf.Round(FS * 1.3f); // высота строки: размер шрифта × межстрочный интервал
                    var le = sq.gameObject.AddComponent<LayoutElement>();
                    le.minWidth = le.preferredWidth = le.minHeight = le.preferredHeight = side;
                    var lt = Label(row, l.Substring(e + 1), TextCol);
                    lt.supportRichText = richText;
                    var tle = lt.gameObject.AddComponent<LayoutElement>();
                    tle.preferredWidth = 1; tle.flexibleWidth = 1; // ширину текст берёт из остатка строки и переносится, а не растягивает окно
                }
                else Label(content, l, TextCol).supportRichText = richText;
            }
            if (!pin) { var gap = Rt("Gap", content); gap.gameObject.AddComponent<LayoutElement>().preferredHeight = 2; }
            foreach (var b in buttons)
            {
                var btn = Btn(bottom, b.label, b.act, out var bt, TextAnchor.MiddleLeft);
                if (b.col.HasValue) bt.color = b.col.Value;
                SetEnabled(btn, !b.disabled);
            }

            modalRoot.SetActive(true);
            modalRoot.transform.SetAsLastSibling();
            HideTip();
            LayoutBox();
            content.anchoredPosition = Vector2.zero;
            scroll.velocity = Vector2.zero;
        }

        /// <summary>Прокрутить открытое окно в самый низ (журнал: новые уведомления — внизу).</summary>
        public void ScrollModalToEnd()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = 0;
            scroll.velocity = Vector2.zero;
        }

        /// <summary>Окно с полем ввода. ok получает введённый текст (Enter тоже подтверждает).</summary>
        /// <param name="cancel">Если задано — кнопка «Назад» и Esc; иначе ввод обязателен.</param>
        public void Prompt(string title, IList<string> lines, string value, int maxLen, string okLabel, Action<string> ok, Action cancel = null)
        {
            InputField input = null;
            var buttons = new List<MBtn> { new MBtn(okLabel, () => ok(input.text)) };
            if (cancel != null) buttons.Add(new MBtn("◀ Назад", cancel));
            Modal(title, lines, buttons, cancel ?? NoEsc);

            // поле ввода — сразу после текста, перед кнопкой
            var rt = Rt("Input", content);
            rt.SetSiblingIndex(content.Find("Gap").GetSiblingIndex()); // перед отступом и кнопками
            var img = Img(rt, BtnBg, true);
            Frame(rt.gameObject);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = FS * 2 + 12;
            var txt = Label(rt, "", Color.white, 0, TextAnchor.MiddleLeft);
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(txt.rectTransform, 8, 8, 2, 2);
            input = rt.gameObject.AddComponent<InputField>();
            input.textComponent = txt;
            input.targetGraphic = img;
            input.characterLimit = maxLen;
            input.lineType = InputField.LineType.SingleLine;
            input.text = value;
            input.onEndEdit.AddListener(s =>
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) ok(s);
            });

            LayoutBox();
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(rt.gameObject);
            input.ActivateInputField();
        }

        /// <summary>Закрыть окно без обновления HUD — перед открытием другого окна.</summary>
        public void CloseModalSilently() => modalRoot.SetActive(false);

        public void CloseModal()
        {
            modalRoot.SetActive(false);
            Refresh();
        }

        public void ReleaseFire() => fire.Release();

        // ---------- экранный список уведомлений (справа сверху) ----------
        // Последние важные уведомления с иконкой уровня важности; через NOTE_LIFE секунд гаснут.
        // Клик по уведомлению открывает журнал за всю игру.

        RectTransform noteRoot;
        readonly List<(GameObject go, CanvasGroup g, float t)> noteRows = new List<(GameObject, CanvasGroup, float)>();
        const float NOTE_LIFE = 14;
        const int NOTE_MAX = 5;
        static readonly string[] NOTE_ICON = { "..yyy..", ".yyWyy.", "yyyWyyy", "yyyWyyy", "yyyyyyy", ".yyWyy.", "..yyy.." };
        readonly Dictionary<int, Texture> noteTex = new Dictionary<int, Texture>();

        Texture NoteIcon(int lv)
        {
            if (!noteTex.TryGetValue(lv, out var t))
                noteTex[lv] = t = PixelSprite.FromRows(NOTE_ICON, ('y', Notes.Col(lv)), ('W', "#1a1430")).ToTexture();
            return t;
        }

        void BuildNotes()
        {
            noteRoot = Rt("Notes", safe);
            noteRoot.anchorMin = noteRoot.anchorMax = noteRoot.pivot = Vector2.one;
            noteRoot.anchoredPosition = new Vector2(-10, Desktop ? -56 : -70);
            noteRoot.sizeDelta = new Vector2(Desktop ? 300 : 230, 0);
            var v = VLG(noteRoot.gameObject, 4);
            v.childAlignment = TextAnchor.UpperRight;
            noteRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Notes.Added = AddNoteRow;
        }

        void AddNoteRow(Note n)
        {
            var rt = Rt("Note", noteRoot);
            var im = Img(rt, new Color(BoxBg.r, BoxBg.g, BoxBg.b, .9f), true);
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = Color.Lerp(Px.HexC(Notes.Col(n.lv)), BoxBg, .35f); o.effectDistance = new Vector2(2, -2); o.useGraphicAlpha = false;
            var h = HLG(rt.gameObject, 6, TextAnchor.UpperLeft);
            h.padding = Desktop ? new RectOffset(6, 7, 5, 5) : new RectOffset(7, 8, 6, 6);
            var ic = Rt("Icon", rt);
            var ri = ic.gameObject.AddComponent<RawImage>();
            ri.texture = NoteIcon(n.lv); ri.raycastTarget = false;
            var le = ic.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = le.preferredHeight = le.minHeight = 14;
            var t = Label(rt, n.text, TextCol, Mathf.Max(8, FS - 1));
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = im;
            b.onClick.AddListener(() => { if (!ModalOpen) Menus.Journal(); });
            // новые — снизу (строка добавляется в конец списка)
            noteRows.Add((rt.gameObject, rt.gameObject.AddComponent<CanvasGroup>(), 0));
            while (noteRows.Count > NOTE_MAX) { UnityEngine.Object.Destroy(noteRows[0].go); noteRows.RemoveAt(0); }
        }

        void TickNotes(float dt)
        {
            if (noteRoot == null) return;
            bool show = G != null && !gm.routes.Open;
            if (noteRoot.gameObject.activeSelf != show) noteRoot.gameObject.SetActive(show);
            for (int i = noteRows.Count - 1; i >= 0; i--)
            {
                var (go, g, t) = noteRows[i];
                t += dt;
                g.alpha = Mathf.Clamp01((NOTE_LIFE - t) / 1.2f);
                if (t >= NOTE_LIFE) { UnityEngine.Object.Destroy(go); noteRows.RemoveAt(i); }
                else noteRows[i] = (go, g, t);
            }
        }

        /// <summary>Новая игра или загрузка: старые уведомления с экрана убираем (они остаются в журнале).</summary>
        public void ClearNotes()
        {
            foreach (var r in noteRows) UnityEngine.Object.Destroy(r.go);
            noteRows.Clear();
        }

        // ---------- боковая панель оружия ----------
        // Иконки купленного оружия. Откат — тёмная вуаль, которая опускается к нижнему краю иконки.
        // Над пассивным оружием — полоска: золотая — включено, бледно-фиолетовая — выключено;
        // во время отката золото заполняет её слева направо.

        sealed class WSlot { public string id; public Outline frame; public RectTransform veil, barFill; public Image barImg; }
        RectTransform wpRoot;
        string wpSig;
        readonly List<WSlot> wslots = new List<WSlot>();
        static readonly Color BarOff = Color.Lerp(Border, BoxBg, .35f);

        void BuildWeaponPanel()
        {
            wpRoot = Rt("Weapons", safe);
            wpRoot.anchorMin = wpRoot.anchorMax = wpRoot.pivot = new Vector2(0, .5f);
            wpRoot.anchoredPosition = new Vector2(10, 0);
            var v = VLG(wpRoot.gameObject, Desktop ? 6 : 8);
            v.childForceExpandWidth = false;
            var f = wpRoot.gameObject.AddComponent<ContentSizeFitter>();
            f.horizontalFit = f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            wpRoot.gameObject.SetActive(false);
        }

        void RebuildWeapons()
        {
            foreach (Transform ch in wpRoot) UnityEngine.Object.Destroy(ch.gameObject);
            wpRoot.DetachChildren();
            wslots.Clear();
            int n = 0;
            if (gm.mode == GameManager.Mode.System)
            foreach (var w in Weapons.OwnedList())
            {
                n++;
                var def = w;
                int num = n;
                MakeSlot(w.Id, Sprites.Icon("w_" + w.Id, w.Col), w.Passive, num <= 9 ? num.ToString() : null,
                    () => (def.N, Px.HexC(def.Col), WeaponHint(def, num)), () => Weapons.Use(def.Id));
            }
            // способность веры — отдельной иконкой под оружием (клавиша F)
            var f = Faith.Mine;
            if (f != null)
                MakeSlot(FAITH_SLOT, FaithIcon(f), true, "F", () => (f.AbN, Px.HexC(f.Col), AbilityHint(f)), () => FaithKey());
        }

        const string FAITH_SLOT = "faith";

        /// <summary>Иконка в боковой панели: рамка, иконка, вуаль отката, подпись клавиши; bar — полоска сверху.</summary>
        void MakeSlot(string id, Texture icon, bool bar, string key, Func<(string, Color, string)> hint, Action click)
        {
            float S = Desktop ? 34 : 46, BH = Desktop ? 4 : 5;
            var slot = Rt("Slot", wpRoot);
            var le = slot.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = S;
            le.preferredHeight = le.minHeight = S + BH + 3;
            var ws = new WSlot { id = id };

            if (bar)
            {
                var b = Rt("Bar", slot);
                b.anchorMin = new Vector2(0, 1); b.anchorMax = Vector2.one; b.pivot = new Vector2(.5f, 1);
                b.sizeDelta = new Vector2(0, BH); b.anchoredPosition = Vector2.zero;
                Img(b, BarOff);
                var fill = Rt("Fill", b);
                fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
                ws.barImg = Img(fill, Gold);
                ws.barFill = fill;
            }

            var box = Rt("Box", slot);
            box.anchorMin = Vector2.zero; box.anchorMax = new Vector2(1, 0); box.pivot = new Vector2(.5f, 0);
            box.sizeDelta = new Vector2(0, S); box.anchoredPosition = Vector2.zero;
            Img(box, BtnBg, true);
            var o = box.gameObject.AddComponent<Outline>();
            o.effectColor = Border; o.effectDistance = new Vector2(2, -2); o.useGraphicAlpha = false;
            ws.frame = o;

            var ic = Rt("Icon", box);
            Stretch(ic, 5, 5, 5, 5);
            var ri = ic.gameObject.AddComponent<RawImage>();
            ri.texture = icon;
            ri.raycastTarget = false;

            var veil = Rt("Veil", box);
            veil.anchorMin = Vector2.zero; veil.anchorMax = new Vector2(1, 0); veil.offsetMin = veil.offsetMax = Vector2.zero;
            Img(veil, new Color(0, 0, 0, .62f));
            ws.veil = veil;

            if (Desktop && key != null)
            {
                var k = Label(box, key, TextCol, Mathf.Max(7, FS - 3), TextAnchor.LowerRight);
                Stretch(k.rectTransform, 2, 2, 2, 1);
            }

            var h = box.gameObject.AddComponent<HudHint>();
            h.Get = hint;
            h.Click = click;
            wslots.Add(ws);
        }

        /// <summary>Иконка способности — святыня веры.</summary>
        public static Texture FaithIcon(FaithDef f)
        {
            var art = Items.Get(f.Art ?? "art_idol");
            return Sprites.Icon(art.Icon, f.Col);
        }

        /// <summary>F или клик: способность принятой веры.</summary>
        public void FaithKey()
        {
            var f = Faith.Mine;
            if (f == null) return;
            if (Faith.AbilityActive) { Toast(f.AbN + " уже действует"); return; }
            if (Faith.AbilityCdLeft > 0) { Toast(f.AbN + ": ещё не готово, " + Mathf.CeilToInt(Faith.AbilityCdLeft / 60) + " мин"); return; }
            bool onMap = gm.mode != GameManager.Mode.System;
            if (onMap && !f.MapOk) { Toast(f.AbN + " включается только в системе"); return; }
            Faith.Activate(onMap);
            Toast(f.AbN + "! " + Dur(f.AbTime) + ": " + f.AbD);
        }

        public static string Dur(float sec) => sec >= 60 && sec % 60 == 0 ? (sec / 60) + " мин" : sec >= 60 ? (sec / 60f).ToString("0.#") + " мин" : sec + " с";

        static string AbilityHint(FaithDef f)
        {
            string s = Dur(f.AbTime) + ": " + f.AbD + ".\nОткат " + Dur(f.AbCd) + ".";
            if (Faith.AbilityActive) return s + "\nДействует ещё " + Mathf.CeilToInt(Faith.AbilityLeft) + " с";
            if (Faith.AbilityCdLeft > 0) return s + "\nГотово через " + Mathf.CeilToInt(Faith.AbilityCdLeft / 60) + " мин";
            return s + (f.MapOk ? "\nГотово · клик или F (и на карте)" : "\nГотово · клик или F (только в системе)");
        }

        static string WeaponHint(WeaponDef w, int num)
        {
            string s = w.D + "\nЭнергия: " + (w.Continuous ? (w.En / w.Cd).ToString("0.#") + " в секунду" : w.En.ToString("0.#") + " за выстрел") + (w.Continuous ? "\nУрон в секунду: " + Mathf.RoundToInt(Weapons.BEAM_DPS * Weapons.Power) : "\nОткат: " + w.Cd.ToString("0.##") + " с");
            if (w.Passive)
            {
                bool on = Weapons.On(w.Id);
                return s + "\n" + (on ? "Включено" : "Выключено") + " · клик" + (num <= 9 ? " или " + num : "") + " — " + (on ? "выключить" : "включить");
            }
            return s + (Weapons.Sel == w.Id ? "\nВыбрано" : "\nКлик" + (num <= 9 ? " или " + num : "") + " — выбрать");
        }

        /// <summary>Клавиша 1…9: выбрать оружие или переключить пассивное.</summary>
        public void WeaponKey(int i)
        {
            var L = Weapons.OwnedList();
            if (i >= 0 && i < L.Count) Weapons.Use(L[i].Id);
        }

        void RefreshWeapons()
        {
            bool sysMode = G != null && gm.mode == GameManager.Mode.System;
            bool show = G != null && !gm.routes.Open && !gm.Dead && (sysMode || Faith.Mine != null);
            if (wpRoot.gameObject.activeSelf != show) wpRoot.gameObject.SetActive(show);
            if (!show) return;
            string sig = string.Join(",", P.weapons) + "|" + P.faith + "|" + sysMode;
            if (sig != wpSig) { wpSig = sig; RebuildWeapons(); }
            string sel = Weapons.Sel;
            foreach (var ws in wslots)
            {
                if (ws.id == FAITH_SLOT)
                {
                    // горит — оранжевая полоска убывает; откат — вуаль и золото, заполняющее полоску
                    var mf = Faith.Mine;
                    if (mf == null) continue;
                    bool act = Faith.AbilityActive;
                    var fc = Px.HexC(mf.Col);
                    float cd = act ? 0 : Mathf.Clamp01(Faith.AbilityCdLeft / (mf.AbCd - mf.AbTime));
                    ws.veil.anchorMax = new Vector2(1, cd);
                    ws.frame.effectColor = act ? fc : Border;
                    ws.barImg.color = act ? fc : Gold;
                    ws.barFill.anchorMax = new Vector2(act ? Faith.AbilityLeft / mf.AbTime : 1 - cd, 1);
                    continue;
                }
                float fr = Weapons.Get(ws.id).Continuous ? 0 : Weapons.Frac(ws.id);
                ws.veil.anchorMax = new Vector2(1, fr);
                ws.frame.effectColor = ws.id == sel ? Gold : Border;
                if (ws.barFill != null) ws.barFill.anchorMax = new Vector2(Weapons.On(ws.id) ? 1 - fr : 0, 1);
            }
        }

        // ---------- окно транспортных путей ----------

        RectTransform routeRoot, routeChecks;
        Text routeTitle, routeHint;
        Button routePlus;
        Text routePlusText;
        string routeSig = "";

        void BuildRoutePanel()
        {
            routeRoot = Rt("Routes", safe);
            Stretch(routeRoot);

            // заголовок и подсказка — слева сверху, под HUD
            var head = Rt("Head", routeRoot);
            head.anchorMin = head.anchorMax = head.pivot = new Vector2(0, 1);
            head.anchoredPosition = new Vector2(10, -56); head.sizeDelta = new Vector2(520, 0);
            Img(head, new Color(BoxBg.r, BoxBg.g, BoxBg.b, .85f));
            VLG(head.gameObject, 4, 8);
            head.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            routeTitle = Label(head, "", Gold, FS + 1);
            routeHint = Label(head, "", TextCol, Mathf.Max(8, FS - 1));

            // кнопки «+» и закрыть — справа сверху
            var btns = Rt("Buttons", routeRoot);
            btns.anchorMin = btns.anchorMax = btns.pivot = new Vector2(1, 1);
            btns.anchoredPosition = new Vector2(-10, -56); btns.sizeDelta = new Vector2(200, 30);
            HLG(btns.gameObject, 8, TextAnchor.UpperRight);
            routePlus = Btn(btns, "+", () => gm.routes.ToggleCreate(), out routePlusText);
            Btn(btns, "×", () => gm.routes.Close(), out _);

            // чекбоксы ресурсов — слева, только в режиме «+»
            routeChecks = Rt("Checks", routeRoot);
            routeChecks.anchorMin = routeChecks.anchorMax = routeChecks.pivot = new Vector2(0, 1);
            routeChecks.anchoredPosition = new Vector2(10, -150); routeChecks.sizeDelta = new Vector2(240, 0);
            Img(routeChecks, new Color(BoxBg.r, BoxBg.g, BoxBg.b, .9f), true);
            Frame(routeChecks.gameObject);
            VLG(routeChecks.gameObject, 4, 8);
            routeChecks.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            routeRoot.gameObject.SetActive(false);
        }

        void RefreshRoutes()
        {
            var re = gm.routes;
            string sig = re.Open + "|" + re.Galaxy + "|" + re.Creating + "|" + string.Join(",", re.sel) + "|" + (re.Galaxy ? "" : Cur.name);
            if (sig == routeSig) return;
            routeSig = sig;
            routeRoot.gameObject.SetActive(re.Open);
            if (!re.Open) return;
            routeRoot.SetAsLastSibling();
            routeTitle.text = re.Galaxy ? "Транспорт между системами" : "Транспорт · система " + Cur.name;
            routeHint.text = re.Creating
                ? (re.Galaxy ? "Протяните стрелку от своей системы к другой. Отметьте слева, что везти." : "Протяните стрелку от своей планеты к другой. Отметьте слева, что везти.")
                  + "\nEsc или «+» — выйти из режима рисования"
                : (re.Galaxy ? "Колесо — масштаб, перетаскивание — сдвиг. Клик по пути — удалить."
                             : "Клик по пути — удалить. Клик по своей планете — принимать на ней грузы из других систем.")
                  + "\n«+» — новый путь за " + Routes.CostOf(!re.Galaxy) + " кр. (при удалении вернём). Перевозка: "
                  + (re.Galaxy ? Routes.BATCH_GALAXY : Routes.BATCH_SYSTEM) + " ед. за " + Routes.PERIOD + " с";
            // залипшая кнопка «+»
            routePlus.GetComponent<Outline>().effectColor = re.Creating ? Gold : Border;
            routePlusText.color = re.Creating ? Gold : Color.white;

            routeChecks.gameObject.SetActive(re.Creating);
            foreach (Transform ch in routeChecks) UnityEngine.Object.Destroy(ch.gameObject);
            routeChecks.DetachChildren();
            if (!re.Creating) return;
            Label(routeChecks, "Что везти:", Gold);
            foreach (var id in Routes.Kinds)
            {
                string k = id;
                bool on = re.sel.Contains(k);
                Btn(routeChecks, (on ? "√ " : "   ") + Items.Get(k).N, () =>
                {
                    if (!re.sel.Remove(k)) re.sel.Add(k);
                    RefreshRoutes();
                }, out var t, TextAnchor.MiddleLeft);
                t.color = on ? (Color)Routes.ColorOf(k) : new Color(.6f, .6f, .7f);
            }
        }

        // ---------- обновление HUD ----------

        public void Refresh()
        {
            if (G == null) return;
            cr.text = Mathf.FloorToInt(P.credits).ToString();
            enb.anchorMax = new Vector2(Mathf.Clamp01(P.energy / MaxE()), 1);
            hpb.anchorMax = new Vector2(Mathf.Clamp01(P.hull / MaxH()), 1);
            cg.text = Inv.Used() + "/" + CargoMax();

            bool sys = gm.mode == GameManager.Mode.System;
            var r = G.raids.Find(x => x.star != P.cur && x.def == Owner.PLAYER);
            loc.text = (sys ? "Система " + Cur.name : "Карта галактики · " + (gm.gal.travel != null ? "полёт к " + G.stars[gm.gal.travel.to].name : Cur.name)) + (r != null ? "   " + (r.att == Raid.PIRATES ? "ПИРАТЫ" : "НАПАДЕНИЕ") + ": " + G.stars[r.star].name : "");

            bool ro = gm.routes.Open; // окно путей прячет нижние панели
            sbar.SetActive(sys && !ro);
            fire.gameObject.SetActive(sys && !Desktop && !ro); // на десктопе огонь — ЛКМ или пробел
            gbar.SetActive(!sys && !ro);
            bool showAct = sys && gm.sys.near >= 0 && !gm.Dead;
            act.gameObject.SetActive(showAct);
            if (showAct)
            {
                string at = Cur.planets[gm.sys.near].name;
                if (actText.text != at) actText.text = at;
            }
            RenderPanel();
            RefreshRoutes();
        }

        public void InvalidatePanel()
        {
            panelSig = "";
            RenderPanel();
        }

        void RenderPanel()
        {
            var gal = gm.gal;
            if (gm.mode != GameManager.Mode.Galaxy || gal.sel < 0 || gal.travel != null)
            {
                panel.SetActive(false);
                panelSig = "";
                return;
            }
            var s = G.stars[gal.sel];
            bool cf = gal.CanFly(s);
            string sig = gal.sel + "|" + cf + "|" + P.cur + "|" + Mathf.FloorToInt(P.energy / 5);
            if (sig == panelSig) return;
            panelSig = sig;

            panel.SetActive(true);
            foreach (Transform ch in panel.transform) UnityEngine.Object.Destroy(ch.gameObject);
            panel.transform.DetachChildren();

            string own = s.core ? "—" : s.owner == Owner.PLAYER ? EmpName : !s.visited ? "Не исследована" : Owner.IsEmp(s.owner) ? G.emp[s.owner].name + " (" + RelStr(G.emp[s.owner].rel) + ")" : "Свободная";
            int d = Px.R(Px.Dist(s, Cur));
            int col = s.planets.FindAll(p => p.owner == Owner.PLAYER).Count;
            Label(panel.transform, s.name, Gold);
            Label(panel.transform, s.core
                ? "Свет древнего маяка. Нужен двигатель ур. 4."
                : own + " · планет: " + (s.visited ? s.planets.Count.ToString() : "?") + (col > 0 ? " · колоний: " + col : "") + (s.id != P.cur ? " · " + d + " св.лет" : " · вы здесь"), TextCol);

            var bd = Rt("Buttons", panel.transform);
            HLG(bd.gameObject, 8);
            if (s.id == P.cur) Btn(bd, "Войти", () => gm.EnterSystemAnimated(s.id), out _);
            else
            {
                var b = Btn(bd, gal.InRange(s) ? "Лететь (энергия " + gal.TravelCost(s) + ")" : "Вне дальности", () =>
                {
                    gal.Fly(s);
                    RenderPanel();
                }, out _);
                SetEnabled(b, cf);
            }
        }
    }
}

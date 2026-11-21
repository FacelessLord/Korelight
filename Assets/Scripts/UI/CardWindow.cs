using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static PixelSpace.UiKit;

namespace PixelSpace
{
    /// <summary>Карточка в окне трюма/магазина.</summary>
    public sealed class Card
    {
        public Texture Icon;
        public string Name, Line1, Line2, Line3; // Line3 — ещё одна строка под Line2 (например, «в трюме»)
        public Color NameCol = Color.white, Line1Col = Gold;
        public bool Dim;                 // приглушённая (купить/продать нельзя), но клик всё равно объясняет почему
        public Action<int> Click;        // аргумент — сколько штук за клик (Shift — 10, Ctrl — всё)
    }

    public readonly struct Tab
    {
        public readonly string Label;
        public readonly bool Active;
        public readonly Action Act;
        public Tab(string label, bool active, Action act) { Label = label; Active = active; Act = act; }
    }

    /// <summary>Отдельное окно с сеткой карточек и вкладками (трюм, магазин).</summary>
    public sealed class CardWindow
    {
        public const int ALL = int.MaxValue;

        readonly GameObject root;
        readonly RectTransform overlay, box, tabs, content;
        readonly Text title, footer, empty;
        readonly ScrollRect scroll;
        Action esc;
        string lastKey;

        public bool Open => root.activeSelf;

        static float CardW => Desktop ? 132 : 150;
        static float CardH => Desktop ? 132 : 156; // место под три строки снизу

        public CardWindow(Transform parent)
        {
            overlay = Rt("CardWindow", parent);
            Stretch(overlay);
            Img(overlay, new Color(0, 0, 0, .66f), true);
            root = overlay.gameObject;

            box = Rt("Box", overlay);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(.5f, .5f);
            Img(box, BoxBg, true);
            Frame(box.gameObject, 2, 4);
            var v = VLG(box.gameObject, 10, 14);
            v.childForceExpandHeight = false;

            // шапка: заголовок, вкладки, закрыть
            var head = Rt("Head", box);
            HLG(head.gameObject, 8);
            title = Label(head, "", Gold, FS + 2, TextAnchor.MiddleLeft);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            tabs = Rt("Tabs", head);
            HLG(tabs.gameObject, 6);
            Btn(head, "×", () => Escape(), out _); // как Esc: из магазина — обратно в меню планеты

            // сетка карточек с прокруткой
            var srt = Rt("Scroll", box);
            srt.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            scroll = srt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            var vp = Rt("Viewport", srt);
            Stretch(vp);
            Img(vp, new Color(0, 0, 0, 0), true);
            vp.gameObject.AddComponent<RectMask2D>();
            content = Rt("Grid", vp);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            var g = content.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(CardW, CardH);
            g.spacing = new Vector2(8, 8);
            g.childAlignment = TextAnchor.UpperLeft;
            // отступ под рамку и тень карточек: иначе маска прокрутки срезает их у левого и верхнего края
            g.padding = new RectOffset(3, 5, 3, 6);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = vp; scroll.content = content;
            empty = Label(vp, "", TextCol, 0, TextAnchor.MiddleCenter);
            Stretch(empty.rectTransform);

            footer = Label(box, "", Px.HexC("#b9b0ff"), Mathf.Max(8, FS - 1));
            root.SetActive(false);
        }

        /// <param name="key">Что показано (например, «магазин:трюм»): при перерисовке того же вида прокрутка сохраняется.</param>
        public void Show(string key, string titleText, IList<Tab> tabList, IList<Card> cards, string footerText, string emptyText, Action onEsc)
        {
            bool same = Open && key == lastKey;
            float scrollY = content.anchoredPosition.y;
            lastKey = key;
            esc = onEsc;
            title.text = titleText;
            footer.text = footerText;

            foreach (Transform ch in tabs) UnityEngine.Object.Destroy(ch.gameObject);
            tabs.DetachChildren();
            foreach (var t in tabList)
            {
                var b = Btn(tabs, t.Label, t.Act, out var bt);
                if (t.Active)
                {
                    // активная вкладка — золотая рамка и подпись
                    b.GetComponent<Outline>().effectColor = Gold;
                    bt.color = Gold;
                }
            }

            foreach (Transform ch in content) UnityEngine.Object.Destroy(ch.gameObject);
            content.DetachChildren();
            foreach (var c in cards) MakeCard(c);
            empty.text = cards.Count == 0 ? emptyText : "";

            root.SetActive(true);
            root.transform.SetAsLastSibling();
            Layout();
            content.anchoredPosition = new Vector2(0, same ? scrollY : 0);
            scroll.velocity = Vector2.zero;
        }

        public void Close()
        {
            root.SetActive(false);
            lastKey = null;
            GameManager.I.ui.Refresh();
        }

        public void Escape() => (esc ?? Close)();

        /// <summary>Спрятать без обновления HUD — когда поверх открывается обычное окно.</summary>
        public void Hide() { root.SetActive(false); lastKey = null; }

        /// <summary>Подогнать размер окна под экран (и после поворота/изменения окна).</summary>
        public void Layout()
        {
            var area = overlay.rect;
            if (area.width <= 0) return;
            // ширина — целое число карточек
            float inner = Mathf.Min(Desktop ? 5 * CardW + 4 * 8 + 10 : 9999, area.width - 24 - 28 - 10);
            int cols = Mathf.Max(1, Mathf.FloorToInt((inner + 8) / (CardW + 8)));
            float w = cols * CardW + (cols - 1) * 8 + 28 + 10;
            box.sizeDelta = new Vector2(Mathf.Max(w, 320), Mathf.Min(Desktop ? 470 : area.height * .85f, area.height - 24));
        }

        public void Tick()
        {
            if (Open && Mathf.Abs(overlay.rect.width - lastW) > .5f) { lastW = overlay.rect.width; Layout(); }
        }

        float lastW;

        static int Multiplier()
        {
            var kb = Keyboard.current;
            if (kb == null) return 1;
            if (kb.ctrlKey.isPressed) return ALL;
            if (kb.shiftKey.isPressed) return 10;
            return 1;
        }

        void MakeCard(Card c)
        {
            var rt = Rt("Card", content);
            var im = Img(rt, BtnBg, true);
            Frame(rt.gameObject, 2, 3);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = im;
            var cb = b.colors; cb.pressedColor = new Color(.7f, .7f, .7f); cb.highlightedColor = Color.white; cb.selectedColor = Color.white; b.colors = cb;
            var click = c.Click;
            b.onClick.AddListener(() => click?.Invoke(Multiplier()));
            rt.gameObject.AddComponent<HoverTint>().Init(b, im, rt.GetComponent<Outline>());
            if (c.Dim) rt.gameObject.AddComponent<CanvasGroup>().alpha = .45f;

            var ic = Rt("Icon", rt);
            ic.anchorMin = ic.anchorMax = new Vector2(.5f, 1); ic.pivot = new Vector2(.5f, 1);
            ic.anchoredPosition = new Vector2(0, -8); ic.sizeDelta = new Vector2(32, 32);
            var ri = ic.gameObject.AddComponent<RawImage>();
            ri.texture = c.Icon; ri.raycastTarget = false;

            float fh = FS * 1.35f;
            var name = Label(rt, c.Name, c.NameCol, 0, TextAnchor.UpperCenter);
            var nr = name.rectTransform;
            nr.anchorMin = new Vector2(0, 1); nr.anchorMax = Vector2.one; nr.pivot = new Vector2(.5f, 1);
            nr.offsetMin = new Vector2(6, -48 - fh * 2.2f); nr.offsetMax = new Vector2(-6, -46);

            bool three = !string.IsNullOrEmpty(c.Line3);
            float lh = fh + 1;
            Line(rt, c.Line1, c.Line1Col, 6 + lh + 2 + (three ? lh : 0));
            Line(rt, c.Line2, TextCol, 6 + (three ? lh : 0));
            Line(rt, c.Line3, TextCol, 6);
        }

        static void Line(RectTransform card, string s, Color col, float y)
        {
            if (string.IsNullOrEmpty(s)) return;
            var t = Label(card, s, col, Mathf.Max(8, FS - 1), TextAnchor.LowerCenter);
            var r = t.rectTransform;
            r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(.5f, 0);
            r.anchoredPosition = new Vector2(0, y); r.sizeDelta = new Vector2(-8, FS * 1.4f);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PixelSpace
{
    /// <summary>Указатель над игровым полем: мышь (с кнопкой) или палец.</summary>
    public readonly struct Ptr
    {
        public const int LEFT = 0, RIGHT = 1, MIDDLE = 2;
        public readonly int id, button;
        public readonly Vector2 pos; // экранные пиксели, y вверх
        public readonly bool touch;

        public Ptr(PointerEventData e)
        {
            id = e.pointerId;
            pos = e.position;
            button = e.button == PointerEventData.InputButton.Right ? RIGHT : e.button == PointerEventData.InputButton.Middle ? MIDDLE : LEFT;
            touch = e is ExtendedPointerEventData x && x.pointerType == UIPointerType.Touch;
        }
    }

    /// <summary>Мышь и касания по игровому полю.</summary>
    public sealed class WorldInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        public Action<Ptr> Down, Move, Up;
        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;
        public void OnPointerDown(PointerEventData e) => Down?.Invoke(new Ptr(e));
        public void OnDrag(PointerEventData e) => Move?.Invoke(new Ptr(e));
        public void OnPointerUp(PointerEventData e) => Up?.Invoke(new Ptr(e));
    }

    /// <summary>Состояние наведения: кнопка светлеет, рамка становится ярче.</summary>
    public sealed class HoverTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Button btn;
        Image img;
        Outline outline;
        Color saveImg, saveOut;
        bool hovered;

        public void Init(Button b, Image i, Outline o) { btn = b; img = i; outline = o; }

        public void OnPointerEnter(PointerEventData e)
        {
            if (hovered || btn == null || !btn.interactable) return;
            hovered = true;
            saveImg = img.color; saveOut = outline.effectColor;
            img.color = Color.Lerp(saveImg, Color.white, .16f);
            outline.effectColor = Color.Lerp(saveOut, Color.white, .45f);
        }

        public void OnPointerExit(PointerEventData e) => Restore();
        void OnDisable() => Restore();

        void Restore()
        {
            if (!hovered) return;
            hovered = false;
            img.color = saveImg;
            outline.effectColor = saveOut;
        }
    }

    /// <summary>Подсказка при наведении на элемент HUD.</summary>
    public sealed class HudHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Func<(string title, Color col, string body)> Get;
        public Action Click; // например, клик по трюму открывает его окно

        public void OnPointerClick(PointerEventData e)
        {
            if (Click != null && e.button == PointerEventData.InputButton.Left && !GameManager.I.ui.ModalOpen) Click();
        }
        public static HudHint Current; // элемент под курсором

        public void OnPointerEnter(PointerEventData e) => Current = this;
        public void OnPointerExit(PointerEventData e) { if (Current == this) Current = null; }
        void OnDisable() { if (Current == this) Current = null; }
    }

    /// <summary>Кнопка, которую держат пальцем (огонь).</summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool Held;
        public Image img;
        public Color normal, pressed;

        void Set(bool v) { Held = v; if (img) img.color = v ? pressed : normal; }
        public void Release() => Set(false);
        public void OnPointerDown(PointerEventData e) => Set(true);
        public void OnPointerUp(PointerEventData e) => Set(false);
        public void OnPointerExit(PointerEventData e) => Set(false);
    }

    /// <summary>Хелперы для построения uGUI из кода в пиксельном стиле.</summary>
    public static class UiKit
    {
        public static Font Font;
        public static int FS = 13;

        /// <summary>Десктоп: компактный интерфейс, управление мышью и клавиатурой. На телефоне — крупные кнопки и касания.</summary>
        public static bool Desktop => !Application.isMobilePlatform;

        public static readonly Color TextCol = Px.HexC("#e8e4ff"), Border = Px.HexC("#8a7cff"), BtnBg = Px.HexC("#1d1840"),
            BoxBg = Px.HexC("#0d0a20"), Gold = Px.HexC("#ffcf4a"), Green = Px.HexC("#59ffb0"), Pink = Px.HexC("#ff5a6a");

        public static void InitFont()
        {
            // Положите PressStart2P-Regular.ttf в Assets/Resources/Fonts/ — шрифт подхватится автоматически.
            Font = Resources.Load<Font>("Fonts/PressStart2P-Regular");
            if (Font != null) FS = Desktop ? 10 : 9;
            else { Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); FS = Desktop ? 14 : 13; }
        }

        public static RectTransform Rt(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt, float l = 0, float r = 0, float t = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        public static Image Img(RectTransform rt, Color c, bool raycast = false)
        {
            var im = rt.gameObject.AddComponent<Image>();
            im.color = c;
            im.raycastTarget = raycast;
            return im;
        }

        /// <summary>Пиксельная рамка + жёсткая тень, как в CSS-версии.</summary>
        public static void Frame(GameObject go, float border = 2, float shadow = 3)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = Border; o.effectDistance = new Vector2(border, -border); o.useGraphicAlpha = false;
            var s = go.AddComponent<Shadow>();
            s.effectColor = Color.black; s.effectDistance = new Vector2(shadow, -shadow); s.useGraphicAlpha = false;
        }

        /// <summary>Метка в начале строки окна: перед строкой рисуется цветной квадрат во всю высоту строки.
        /// (В пиксельном шрифте нет символа «■», а подставленный из системного шрифта выходит не квадратным.)</summary>
        public static string Sq(string col) => "\u0001" + col + "\u0001";

        public static Text Label(Transform parent, string s, Color c, int size = 0, TextAnchor a = TextAnchor.UpperLeft)
        {
            var rt = Rt("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.fontSize = size > 0 ? size : FS; t.color = c; t.alignment = a;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.3f; t.supportRichText = false; t.raycastTarget = false;
            t.text = s;
            return t;
        }

        public static HorizontalLayoutGroup HLG(GameObject go, float spacing, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childAlignment = align;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            return h;
        }

        public static VerticalLayoutGroup VLG(GameObject go, float spacing, int pad = 0)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.padding = new RectOffset(pad, pad, pad, pad);
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return v;
        }

        public static Button Btn(Transform parent, string label, Action onClick, out Text txt, TextAnchor align = TextAnchor.MiddleCenter, Color? bg = null, Color? border = null)
        {
            var rt = Rt("Button", parent);
            var im = Img(rt, bg ?? BtnBg, true);
            Frame(rt.gameObject);
            if (border.HasValue) rt.GetComponent<Outline>().effectColor = border.Value;
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = Desktop ? new RectOffset(8, 8, 5, 5) : new RectOffset(10, 10, 9, 9);
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = im;
            var cb = b.colors; cb.pressedColor = new Color(.7f, .7f, .7f); cb.disabledColor = Color.white; cb.highlightedColor = Color.white; cb.selectedColor = Color.white; b.colors = cb;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            rt.gameObject.AddComponent<HoverTint>().Init(b, im, rt.GetComponent<Outline>());
            txt = Label(rt, label, Color.white, 0, align);
            return b;
        }

        public static void SetEnabled(Button b, bool on)
        {
            // Прозрачность всей кнопки просвечивала бы рамку-Outline сквозь фон, поэтому тускнеют текст и рамка.
            b.interactable = on;
            float a = on ? 1 : .4f;
            var o = b.GetComponent<Outline>();
            var oc = o.effectColor; oc.a = a; o.effectColor = oc;
            var t = b.GetComponentInChildren<Text>();
            var tc = t.color; tc.a = a; t.color = tc;
        }

        public static RawImage Icon(Transform parent, PixelSprite s, float size)
        {
            var rt = Rt("Icon", parent);
            var ri = rt.gameObject.AddComponent<RawImage>();
            ri.texture = s.ToTexture(); ri.raycastTarget = false;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = size;
            le.preferredHeight = le.minHeight = size * s.h / s.w;
            return ri;
        }

        public static Sprite CircleSprite(int d)
        {
            var t = new Texture2D(d, d, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float r = d / 2f;
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                {
                    float dist = Px.Len(x + .5f - r, y + .5f - r);
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - dist)));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, d, d), new Vector2(.5f, .5f), 100);
        }
    }
}

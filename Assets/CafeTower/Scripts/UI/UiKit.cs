using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CafeTowerDefense
{
    /// <summary>색상 팔레트. UI 톤을 바꾸려면 여기만 고치면 됨.</summary>
    public static class Theme
    {
        static Color C(uint argb) => MenuInfo.Argb(argb);

        // 카페 톤: 에스프레소 배경, 라떼 카드, 크림 글자, 캐러멜 강조
        public static readonly Color Bg = C(0xFF1A120D);
        public static readonly Color BgCenter = C(0xFF3A2A1F);
        public static readonly Color Panel = C(0xF6241913);
        public static readonly Color Card = C(0xFF34261D);
        public static readonly Color CardHi = C(0xFF453226);
        public static readonly Color Inset = C(0xFF1B130E);
        public static readonly Color Line = C(0x24FFE8D0);

        public static readonly Color Text = C(0xFFF7EBDC);
        public static readonly Color Dim = C(0xFFD0B9A2);
        public static readonly Color Hint = C(0xFF927C68);

        public static readonly Color Gold = C(0xFFF5BE6B);
        public static readonly Color Green = C(0xFF8ED89A);
        public static readonly Color Red = C(0xFFFF8A80);
        public static readonly Color Blue = C(0xFF8CCBF0);
        public static readonly Color Teal = C(0xFF7FE0D3);
        public static readonly Color Purple = C(0xFFE0A8E8);
        public static readonly Color Orange = C(0xFFFFB074);

        public static readonly Color BtnNeutral = C(0xFF6A4E3D);
        public static readonly Color BtnDisabled = C(0xFF392A21);
        public static readonly Color BtnGreen = C(0xFF4E9A6E);
        public static readonly Color BtnGold = C(0xFFC2843A);
        public static readonly Color BtnPurple = C(0xFFA2577F);
        public static readonly Color BtnRed = C(0xFFC2504A);
        public static readonly Color BtnTeal = C(0xFF3E958C);
        public static readonly Color BtnBlue = C(0xFF4F7FA8);
        public static readonly Color BtnSet = C(0xFFCF6A55); // 세트 메뉴 (코랄)

        static readonly Color[] GradeColors = { C(0xFFC9B8A6), C(0xFF9ED68F), C(0xFF7FC4F0), C(0xFFE3A3F0), C(0xFFFFC163) };
        public static Color GradeColor(Grade q) => GradeColors[(int)q];

        // 기본 · 스페셜 · 마스터 · 세트 · 전설 · 코스
        static readonly Color[] TierColors = { C(0xFF7FC4F0), C(0xFFE3A3F0), C(0xFFFFC163), C(0xFFFF9A85), C(0xFFFF6FB5), C(0xFF6FE0C0) };
        public static Color Tier(int tier) => TierColors[Mathf.Clamp(tier, 0, TierColors.Length - 1)];

        public static Color Shade(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);
        public static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
        public static string Paint(string s, Color c) => $"<color=#{Hex(c)}>{s}</color>";
    }

    /// <summary>코드로 uGUI를 만드는 도우미 + 절차적으로 만든 스프라이트(둥근 사각형, 아이콘)</summary>
    public static class Ui
    {
        public static Font Font;
        public static Sprite Round, RoundSm, Pill, Circle, Coin, Shadow, Cup, Cake, Bug;
        public static Texture2D Vignette;

        public static void Init()
        {
            if (Font != null) return;
            Font = LoadFont();
            Round = RoundedRect(48, 14f, 1f, 0f);
            RoundSm = RoundedRect(24, 7f, 1f, 0f);
            Pill = RoundedRect(32, 16f, 1f, 0f);
            Shadow = RoundedRect(96, 26f, 18f, 18f);
            Circle = Shape(64, (x, y) => x * x + y * y <= 1f);
            Coin = Shape(64, (x, y) =>
            {
                float d = x * x + y * y;
                return d <= 1f && (d >= 0.5f || d <= 0.33f);
            });
            // 머그컵: 몸통 + 오른쪽 손잡이 + 받침
            Cup = Shape(64, (x, y) =>
            {
                bool body = y >= -0.7f && y <= 0.55f && Mathf.Abs(x + 0.15f) <= 0.6f - (0.55f - y) * 0.08f;
                float hx = x - 0.5f, hy = y + 0.02f;
                float hd = hx * hx + hy * hy;
                bool handle = hd <= 0.12f && hd >= 0.04f && x > 0.4f;
                bool saucer = y >= -0.9f && y <= -0.76f && Mathf.Abs(x + 0.1f) <= 0.85f;
                return body || handle || saucer;
            });
            // 케이크 조각 + 체리
            var cakePoly = new[] { new Vector2(-0.9f, -0.7f), new Vector2(0.9f, -0.7f), new Vector2(0.9f, 0.2f), new Vector2(-0.9f, 0.45f) };
            Cake = Shape(64, (x, y) =>
            {
                float cx = x - 0.45f, cy = y - 0.6f;
                return InConvex(cakePoly, new Vector2(x, y)) || cx * cx + cy * cy <= 0.07f;
            });
            // 벌레: 몸통 + 머리 + 다리
            Bug = Shape(64, (x, y) =>
            {
                float bx = x / 0.5f, by = (y + 0.15f) / 0.7f;
                float hx = x / 0.32f, hy = (y - 0.68f) / 0.26f;
                bool legs = (Mathf.Abs(Mathf.Abs(y + 0.1f) - 0.3f) < 0.07f || Mathf.Abs(y + 0.1f) < 0.05f) && Mathf.Abs(x) < 0.85f;
                return bx * bx + by * by <= 1f || hx * hx + hy * hy <= 1f || legs;
            });
            Vignette = MakeVignette(128);
        }

        // ------------------------------------------------------------------ 리소스

        static Font LoadFont()
        {
            string[] wanted = { "Malgun Gothic", "맑은 고딕", "Noto Sans CJK KR", "Noto Sans KR", "Apple SD Gothic Neo", "NanumGothic" };
            try
            {
                var installed = new HashSet<string>(Font.GetOSInstalledFontNames());
                foreach (var name in wanted)
                    if (installed.Contains(name)) return Font.CreateDynamicFontFromOSFont(name, 32);
            }
            catch (Exception)
            {
                // 일부 플랫폼은 OS 글꼴 목록을 지원하지 않음
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>둥근 사각형 9-slice 스프라이트. soft &gt; 1 이면 그림자처럼 흐릿한 가장자리.</summary>
        static Sprite RoundedRect(int size, float radius, float soft, float pad)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float half = size / 2f;
            var b = new Vector2(half - pad, half - pad);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                var q = new Vector2(Mathf.Abs(p.x) - b.x + radius, Mathf.Abs(p.y) - b.y + radius);
                float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
                float a = soft <= 1f ? Mathf.Clamp01(0.5f - d) : 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d + soft) / (soft * 2f)));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            float border = Mathf.Min(half - 1f, radius + pad + 2f);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        /// <summary>[-1,1] 좌표에서 inside(x,y)가 참인 영역을 4x 슈퍼샘플링으로 그린 아이콘</summary>
        static Sprite Shape(int size, Func<float, float, bool> inside)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                {
                    float u = (x + (sx + 0.5f) / 4f) / size * 2f - 1f;
                    float v = (y + (sy + 0.5f) / 4f) / size * 2f - 1f;
                    if (inside(u * 1.04f, v * 1.04f)) hits++;
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, hits / 16f));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static bool InConvex(Vector2[] poly, Vector2 p)
        {
            bool? sign = null;
            for (int i = 0; i < poly.Length; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Length];
                float cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                bool s = cross >= 0f;
                if (sign == null) sign = s;
                else if (sign != s) return false;
            }
            return true;
        }

        static Texture2D MakeVignette(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) * 0.6f + new Vector2(u, v).magnitude * 0.4f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.05f, d))));
            }
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ 기본 요소

        public static RectTransform RT(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        public static void Center(RectTransform rt, Vector2 size, Vector2 pos = default)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        public static RectOffset Pad(int l, int r, int t, int b) => new RectOffset(l, r, t, b);
        public static RectOffset Pad(int all) => new RectOffset(all, all, all, all);

        public static Image Img(Transform parent, Color c, Sprite sprite = null, string name = "Image")
        {
            var rt = RT(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
                img.preserveAspect = img.type == Image.Type.Simple;
            }
            return img;
        }

        public static Text Txt(Transform parent, string s, int size, Color c, bool bold = false, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var rt = RT("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.color = c;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.12f;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.text = s;
            return t;
        }

        public static T Shadowed<T>(T g, float alpha = 0.45f, float dist = 2f) where T : Graphic
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, alpha);
            s.effectDistance = new Vector2(0f, -dist);
            return g;
        }

        public static LayoutElement LE(Component c, float prefW = -1, float prefH = -1, float flexW = -1, float flexH = -1,
            float minW = -1, float minH = -1)
        {
            if (!c.TryGetComponent(out LayoutElement le)) le = c.gameObject.AddComponent<LayoutElement>();
            if (prefW >= 0) le.preferredWidth = prefW;
            if (prefH >= 0) le.preferredHeight = prefH;
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            if (minW >= 0) le.minWidth = minW;
            if (minH >= 0) le.minHeight = minH;
            return le;
        }

        public static VerticalLayoutGroup V(GameObject go, RectOffset pad, float spacing, TextAnchor align = TextAnchor.UpperLeft)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = pad;
            v.spacing = spacing;
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup H(GameObject go, RectOffset pad, float spacing, TextAnchor align = TextAnchor.MiddleLeft,
            bool expandWidth = false)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.padding = pad;
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = expandWidth;
            h.childForceExpandHeight = false;
            return h;
        }

        /// <summary>가로 줄. height &gt; 0 이면 높이 고정.</summary>
        public static RectTransform Row(Transform parent, float spacing = 8, float height = -1, bool expandWidth = false)
        {
            var rt = RT("Row", parent);
            var h = H(rt.gameObject, Pad(0), spacing, TextAnchor.MiddleLeft, expandWidth);
            if (height > 0)
            {
                h.childForceExpandHeight = true;
                // childForceExpandHeight는 부모에게 '남는 높이를 받아도 됨(flexible 1)'으로도 보고되므로
                // flexibleHeight 0으로 막아야 지정한 높이 그대로 유지됨
                LE(rt, prefH: height, minH: height, flexH: 0);
            }
            return rt;
        }

        public static RectTransform Column(Transform parent, float spacing = 4)
        {
            var rt = RT("Column", parent);
            V(rt.gameObject, Pad(0), spacing);
            return rt;
        }

        public static void Spacer(Transform parent, float flex = 1)
        {
            LE(RT("Spacer", parent), flexW: flex);
        }

        public static void Gap(Transform parent, float h)
        {
            LE(RT("Gap", parent), prefH: h);
        }

        public static void Divider(Transform parent)
        {
            LE(Img(parent, Theme.Line, null, "Divider"), prefH: 1);
        }

        public static Image Card(Transform parent, Color? color = null, int pad = 16, float spacing = 10)
        {
            var img = Img(parent, color ?? Theme.Card, Round, "Card");
            V(img.gameObject, Pad(pad), spacing);
            return img;
        }

        public static Image Icon(Transform parent, Sprite s, Color c, float size)
        {
            var img = Img(parent, c, s, "Icon");
            img.raycastTarget = false;
            LE(img, prefW: size, prefH: size, minW: size, minH: size);
            return img;
        }

        /// <summary>둥근 알약 모양 라벨</summary>
        public static Text Tag(Transform parent, string text, Color bg, Color fg, int size = 17)
        {
            var img = Img(parent, bg, Pill, "Tag");
            img.raycastTarget = false;
            H(img.gameObject, Pad(12, 12, 4, 5), 0, TextAnchor.MiddleCenter);
            var t = Txt(img.transform, text, size, fg, true, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        /// <summary>key ······ value 한 줄</summary>
        public static Text KeyValue(Transform parent, string key, string value, Color valueColor, int size = 19)
        {
            var row = Row(parent, 10);
            ((HorizontalLayoutGroup)row.GetComponent<LayoutGroup>()).childAlignment = TextAnchor.UpperLeft;
            var k = Txt(row, key, size, Theme.Dim);
            LE(k, prefW: 130, minW: 130, flexW: 0);
            var v = Txt(row, value, size, valueColor, true, TextAnchor.UpperRight);
            LE(v, flexW: 1);
            return v;
        }

        /// <summary>진행 막대. 반환된 Image의 anchorMax.x로 채움 정도를 조절.</summary>
        public static RectTransform Bar(Transform parent, Color fill, float height = 12)
        {
            var bg = Img(parent, Theme.Inset, Pill, "Bar");
            LE(bg, prefH: height, minH: height);
            var f = Img(bg.transform, fill, Pill, "Fill");
            var rt = f.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(0f, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static void SetBar(RectTransform fill, float f)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(f), 1f);
            fill.gameObject.SetActive(f > 0.001f);
        }

        public static ButtonFx Button(Transform parent, string label, Color color, Action onClick, Func<bool> enabled = null,
            int fontSize = 22, float height = 58)
        {
            var lip = Img(parent, Theme.Shade(color, 0.55f), Round, "Button");
            var btn = lip.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = lip;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            var face = Img(lip.transform, color, Round, "Face");
            face.raycastTarget = false;
            Stretch(face.rectTransform, 0, ButtonFx.Lip, 0, 0);

            var txt = Txt(face.transform, label, fontSize, Color.white, true, TextAnchor.MiddleCenter);
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 12;
            txt.resizeTextMaxSize = fontSize;
            Stretch(txt.rectTransform, 10, 0, 10, 0);
            Shadowed(txt, 0.35f, 1.5f);

            var fx = lip.gameObject.AddComponent<ButtonFx>();
            fx.Enabled = enabled;
            fx.Init(btn, lip, face, txt, color);
            btn.onClick.AddListener(() =>
            {
                if (fx.Interactable) onClick();
            });
            if (height > 0) LE(lip, prefH: height, minH: height);
            return fx;
        }

        public static ScrollRect Scroll(Transform parent, out RectTransform content, RectOffset pad, float spacing)
        {
            var rt = RT("Scroll", parent);
            var sr = rt.gameObject.AddComponent<ScrollRect>();
            var vp = RT("Viewport", rt);
            Stretch(vp);
            vp.gameObject.AddComponent<RectMask2D>();
            vp.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0); // 빈 곳에서도 드래그되게
            content = RT("Content", vp);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            V(content.gameObject, pad, spacing);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = vp;
            sr.content = content;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 40;
            return sr;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                c.SetActive(false);
                UnityEngine.Object.Destroy(c);
            }
        }

        // ------------------------------------------------------------------ 이징

        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}

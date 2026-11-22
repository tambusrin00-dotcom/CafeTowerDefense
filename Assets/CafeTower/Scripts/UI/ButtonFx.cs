using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CafeTowerDefense
{
    /// <summary>
    /// 입체 버튼 연출: 아래쪽 '턱'(Lip) 위에 앞면(Face)이 떠 있다가 누르면 내려앉음.
    /// 마우스를 올리면 밝아짐. 활성 여부·라벨·색은 매 프레임 함수로 갱신.
    /// 드래그 이벤트는 받지 않으므로 스크롤 영역 안에서도 스크롤을 막지 않음.
    /// </summary>
    public class ButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public const float Lip = 5f;

        public Func<bool> Enabled;
        public Func<string> DynLabel;
        public Func<Color> DynColor;
        public Color BaseColor;

        Button button;
        Image lip;
        RectTransform face;
        Image faceImage;
        Text label;
        bool hover, down;
        float hoverT, pressT;

        public bool Interactable => Enabled == null || Enabled();

        public void Init(Button b, Image lipImage, Image faceImg, Text text, Color color)
        {
            button = b;
            lip = lipImage;
            faceImage = faceImg;
            face = faceImg.rectTransform;
            label = text;
            BaseColor = color;
            Apply(true);
        }

        void OnDisable()
        {
            hover = down = false;
        }

        void Update() => Apply(false);

        void Apply(bool instant)
        {
            if (button == null) return;
            bool on = Interactable;
            button.interactable = on;
            if (DynLabel != null)
            {
                var s = DynLabel();
                if (label.text != s) label.text = s;
            }
            var baseC = DynColor != null ? DynColor() : BaseColor;

            float dt = instant ? 1f : Time.unscaledDeltaTime;
            hoverT = Mathf.MoveTowards(hoverT, hover && on ? 1f : 0f, dt * 10f);
            pressT = Mathf.MoveTowards(pressT, down && on ? 1f : 0f, dt * 25f);

            var c = on ? Color.Lerp(baseC, Color.white, hoverT * 0.14f) : Theme.BtnDisabled;
            faceImage.color = c;
            lip.color = on ? Theme.Shade(baseC, 0.55f) : Theme.Shade(Theme.BtnDisabled, 0.75f);
            label.color = on ? Color.white : Theme.Hint;

            float lipNow = on ? Mathf.Lerp(Lip, 1f, pressT) : 2f;
            float top = on ? Lip - lipNow : 0f;
            face.offsetMin = new Vector2(0f, lipNow);
            face.offsetMax = new Vector2(0f, -top);
        }

        public void OnPointerEnter(PointerEventData e) => hover = true;

        public void OnPointerExit(PointerEventData e)
        {
            hover = false;
            down = false;
        }

        public void OnPointerDown(PointerEventData e) => down = true;
        public void OnPointerUp(PointerEventData e) => down = false;
    }
}

using System;
using UnityEngine;
using UnityEngine.UI;

namespace CafeTowerDefense
{
    /// <summary>되돌릴 수 없는 행동 전에 띄우는 확인 팝업 (화면 전체를 덮어 뒤쪽 입력을 막음)</summary>
    public partial class CafeUI
    {
        GameObject confirmRoot;
        CanvasGroup confirmGroup;
        RectTransform confirmCard;
        Text confirmTitle, confirmBody;
        RectTransform confirmButtons;
        float confirmAge;

        public bool ShowConfirm => confirmRoot != null && confirmRoot.activeSelf;

        void BuildConfirm(Transform canvasRoot)
        {
            var root = Ui.RT("Confirm", canvasRoot);
            Ui.Stretch(root);
            confirmRoot = root.gameObject;
            confirmGroup = confirmRoot.AddComponent<CanvasGroup>();

            // 바깥(어두운 배경)을 누르면 취소
            var backdrop = Ui.Img(root, new Color(0f, 0f, 0f, 0.6f), null, "Backdrop");
            Ui.Stretch(backdrop.rectTransform);
            var cancel = backdrop.gameObject.AddComponent<Button>();
            cancel.transition = Selectable.Transition.None;
            cancel.onClick.AddListener(CloseConfirm);

            var card = Ui.Img(root, Theme.Panel, Ui.Round, "Card");
            confirmCard = card.rectTransform;
            Ui.Center(confirmCard, new Vector2(560, 0));
            Ui.V(card.gameObject, Ui.Pad(36, 36, 32, 30), 16, TextAnchor.UpperCenter);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            confirmTitle = Ui.Txt(card.transform, "", 34, Theme.Text, true, TextAnchor.MiddleCenter);
            Ui.Shadowed(confirmTitle, 0.5f, 2f);
            confirmBody = Ui.Txt(card.transform, "", 20, Theme.Dim, false, TextAnchor.MiddleCenter);
            confirmBody.lineSpacing = 1.2f;
            Ui.Gap(card.transform, 4);
            confirmButtons = Ui.Row(card.transform, 12, 64, true);
            confirmRoot.SetActive(false);
        }

        /// <summary>확인 팝업을 띄움. 확인을 누르면 팝업을 닫고 onYes 실행.</summary>
        public void AskConfirm(string title, string body, string yesLabel, Action onYes)
        {
            confirmTitle.text = title;
            confirmBody.text = body;
            Ui.Clear(confirmButtons);
            Ui.Button(confirmButtons, "취소", Theme.BtnNeutral, CloseConfirm, null, 23, 64);
            Ui.Button(confirmButtons, yesLabel, Theme.BtnRed, () =>
            {
                CloseConfirm();
                onYes();
            }, null, 23, 64);
            confirmAge = 0f;
            confirmGroup.alpha = 0f;
            confirmRoot.SetActive(true);
            confirmRoot.transform.SetAsLastSibling(); // 다른 창보다 위에
        }

        public void CloseConfirm()
        {
            if (confirmRoot != null) confirmRoot.SetActive(false);
        }

        void UpdateConfirm(float dt)
        {
            if (!ShowConfirm) return;
            confirmAge += dt;
            float k = Ui.EaseOut(confirmAge / 0.18f);
            confirmGroup.alpha = k;
            confirmCard.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, k);
        }

        /// <summary>남겨 둔 음료를 무료로 버리기 전에 확인</summary>
        void AskDemolish(MenuTower t)
        {
            string body = $"{t.Stats.Name}을(를) 버리면 되돌릴 수 없어요.\n코인은 들지 않고, 자리는 빈 칸이 돼요.";
            if (t.Signature == SignatureMenu.InstaTea)
                body += "\n" + Theme.Paint("전설 메뉴예요! 다시 나올 확률은 0.00001%예요.", Theme.Red);
            else if (t.Signature.HasValue)
                body += "\n" + Theme.Paint("시그니처 메뉴와 강화 단계가 모두 사라져요.", Theme.Red);
            AskConfirm("정말 버릴까요?", body, "버리기", () => G.Demolish(t.Cell));
        }
    }
}

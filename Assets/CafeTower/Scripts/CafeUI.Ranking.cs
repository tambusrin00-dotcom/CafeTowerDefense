using UnityEngine;
using UnityEngine.UI;

namespace CafeTowerDefense
{
    /// <summary>게임 중에 여는 랭킹 창: 명예의 전당 TOP 10 + 지금 판의 예상 순위</summary>
    public partial class CafeUI
    {
        public bool ShowRanking;

        CanvasGroup rankGroup;
        RectTransform rankCard, rankList;
        ScrollRect rankScroll;
        float rankAge, rankRefreshAt;

        void BuildRanking(Transform area)
        {
            var root = Ui.RT("Ranking", area);
            Ui.Stretch(root);
            rankGroup = root.gameObject.AddComponent<CanvasGroup>();

            // 배경을 누르면 닫힘
            var backdrop = Ui.Img(root, new Color(0f, 0f, 0f, 0.55f), null, "Backdrop");
            Ui.Stretch(backdrop.rectTransform);
            var close = backdrop.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(() => ShowRanking = false);

            var card = Ui.Img(root, Theme.Panel, Ui.Round, "Window");
            rankCard = card.rectTransform;
            Ui.Stretch(rankCard, 28, 28, 28, 28);
            Ui.V(card.gameObject, Ui.Pad(28, 28, 22, 20), 10);
            var edge = card.gameObject.AddComponent<Outline>();
            edge.effectColor = Theme.Alpha(Theme.Gold, 0.3f);
            edge.effectDistance = new Vector2(2f, -2f);

            var head = Ui.Row(card.transform, 12);
            Ui.Icon(head, Ui.Coin, Theme.Gold, 34);
            var title = Ui.Txt(head, "명예의 전당", 31, Theme.Gold, true);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Tag(head, "TOP 10", Coral, Color.white, 18);
            Ui.Spacer(head);
            var share = Ui.Button(head, "1등 공유", Theme.BtnTeal, ShareBest, () => app.Records.Items.Count > 0, 19, 48);
            share.DynLabel = () => Time.unscaledTime < shareCopiedUntil ? "복사됨!" : "1등 공유";
            Ui.LE(share, prefW: 140, minW: 140);
            var cb = Ui.Button(head, "닫기", Theme.BtnNeutral, () => ShowRanking = false, null, 20, 48);
            Ui.LE(cb, prefW: 120, minW: 120);

            // 지금 판 요약 + 예상 순위
            var now = Ui.Txt(card.transform, "", 21, Theme.Teal, true);
            now.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiTicker.Attach(now, () =>
            {
                if (!ShowRanking) return;
                var r = app.CurrentRun();
                int rank = app.Records.RankOf(r);
                string where = rank > 0 ? $"지금 끝나면 {rank}위" : "아직 10위 밖";
                now.text = $"지금 영업  ·  웨이브 {G.Wave}  ·  총 시간 {RunRecords.FormatTime(r.TotalTime)}  ·  {where}";
            });
            Ui.Txt(card.transform, "최종 보스 피해 → 웨이브 → 보스 시간 → 총 시간 → 디저트 → 코인 순 · 이 기기에 저장된 기록", 17, Theme.Hint);
            Ui.Divider(card.transform);

            var heads = Ui.RT("RecordHead", card.transform);
            var headLayout = Ui.H(heads.gameObject, Ui.Pad(14, 14, 0, 0), 8);
            headLayout.childForceExpandHeight = true;
            Ui.LE(heads, prefH: 32, minH: 32, flexH: 0);
            for (int i = 0; i < RecordHeads.Length; i++) RecordCell(heads, RecordHeads[i], i, 19, Theme.Dim, true);

            rankScroll = Ui.Scroll(card.transform, out rankList, Ui.Pad(0, 6, 2, 2), 6);
            Ui.LE(rankScroll, flexH: 1);
            root.gameObject.SetActive(false);
        }

        void UpdateRanking(float dt)
        {
            var go = rankGroup.gameObject;
            if (go.activeSelf != ShowRanking)
            {
                go.SetActive(ShowRanking);
                if (ShowRanking)
                {
                    rankAge = 0f;
                    rankRefreshAt = 0f;
                    rankScroll.verticalNormalizedPosition = 1f;
                }
            }
            if (!ShowRanking) return;
            // 진행 중인 판의 '지금' 줄은 1초마다 다시 그림
            if (Time.unscaledTime >= rankRefreshAt)
            {
                rankRefreshAt = Time.unscaledTime + 1f;
                FillRecords(rankList, G.IsOver ? app.LastRank : 0, G.IsOver ? null : app.CurrentRun());
            }
            rankAge += dt;
            rankGroup.alpha = Ui.EaseOut(rankAge / 0.18f);
            float s = Mathf.Lerp(0.96f, 1f, Ui.EaseOut(rankAge / 0.22f));
            rankCard.localScale = new Vector3(s, s, 1f);
        }
    }
}

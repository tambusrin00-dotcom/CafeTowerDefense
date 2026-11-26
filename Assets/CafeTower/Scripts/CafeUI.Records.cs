using UnityEngine;
using UnityEngine.UI;

namespace CafeTowerDefense
{
    /// <summary>게임 시작 전 화면: 카페 입구 분위기의 로고·영업 시작 버튼과 상위 10위 기록</summary>
    public partial class CafeUI
    {
        const float AwningHeight = 62f;

        static readonly float[] RecordWeights = { 0.7f, 0.8f, 1.4f, 1.1f, 1.1f, 0.8f, 1f };
        static readonly string[] RecordHeads = { "순위", "웨이브", "보스 피해", "보스 시간", "총 시간", "디저트", "코인" };
        static readonly Color Coral = MenuInfo.Argb(0xFFCF6A55);
        static readonly Color Silver = MenuInfo.Argb(0xFFD8DEE6);
        static readonly Color Bronze = MenuInfo.Argb(0xFFE0A070);

        static Texture2D titleGlow;

        GameObject titleRoot;
        RectTransform recordList;
        float shareCopiedUntil;

        /// <summary>시작 화면이 떠 있으면 게임은 멈추고 게임판 입력도 받지 않음</summary>
        public bool ShowTitle => titleRoot != null && titleRoot.activeSelf;

        void BuildTitle(Transform canvasRoot)
        {
            var bg = Ui.Img(canvasRoot, Theme.Bg, null, "Title");
            Ui.Stretch(bg.rectTransform);
            titleRoot = bg.gameObject;

            BuildTitleLight(titleRoot.transform);
            BuildFloatingTreats(titleRoot.transform);
            BuildAwning(titleRoot.transform);

            var content = Ui.RT("Content", titleRoot.transform);
            Ui.Stretch(content);
            var rootRow = Ui.H(content.gameObject, Ui.Pad(52, 44, (int)AwningHeight + 26, 28), 40, TextAnchor.MiddleCenter);
            rootRow.childForceExpandHeight = true;

            BuildTitleLogo(content);
            BuildRecordCard(content);
            RebuildRecords();
        }

        // ------------------------------------------------------------------ 배경

        /// <summary>왼쪽 로고 쪽에서 퍼지는 따뜻한 조명</summary>
        static void BuildTitleLight(Transform parent)
        {
            if (titleGlow == null)
            {
                const int size = 128;
                titleGlow = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                    titleGlow.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
                titleGlow.Apply();
            }
            var light = Ui.RT("Light", parent).gameObject.AddComponent<RawImage>();
            light.texture = titleGlow;
            light.color = Theme.Alpha(Theme.Orange, 0.3f);
            light.raycastTarget = false;
            var rt = light.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(1300, 1300);
            rt.anchoredPosition = new Vector2(285, -10);
        }

        /// <summary>배경에 은은하게 떠다니는 컵·케이크·코인</summary>
        static void BuildFloatingTreats(Transform parent)
        {
            var layer = Ui.RT("Treats", parent);
            Ui.Stretch(layer);
            var sprites = new[] { Ui.Cup, Ui.Cake, Ui.Coin };
            var spots = new[]
            {
                new Vector2(0.03f, 0.82f), new Vector2(0.22f, 0.86f), new Vector2(0.27f, 0.58f), new Vector2(0.03f, 0.42f),
                new Vector2(0.26f, 0.2f), new Vector2(0.06f, 0.1f), new Vector2(0.15f, 0.05f), new Vector2(0.98f, 0.5f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var icon = Ui.Img(layer, Theme.Alpha(Theme.Gold, 0.11f), sprites[i % sprites.Length], "Treat");
                icon.raycastTarget = false;
                var rt = icon.rectTransform;
                rt.anchorMin = rt.anchorMax = spots[i];
                rt.sizeDelta = Vector2.one * (46f + i * 7 % 34);
                float phase = i * 1.7f;
                UiTicker.Attach(icon, () =>
                {
                    float t = Time.unscaledTime * 0.55f + phase;
                    rt.anchoredPosition = new Vector2(0f, Mathf.Sin(t) * 12f);
                    rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.7f) * 14f);
                });
            }
        }

        /// <summary>화면 위쪽의 줄무늬 카페 차양</summary>
        static void BuildAwning(Transform parent)
        {
            AwningRow(parent, "AwningShadow", -7f, _ => new Color(0f, 0f, 0f, 0.32f));
            AwningRow(parent, "Awning", 0f, i => i % 2 == 0 ? Coral : Theme.Text);
        }

        static void AwningRow(Transform parent, string name, float y, System.Func<int, Color> color)
        {
            const float hiddenTop = 34f; // 둥근 윗부분은 화면 밖으로 밀어 아래쪽만 둥글게 보임
            var row = Ui.RT(name, parent);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = Vector2.one;
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(0f, AwningHeight + hiddenTop);
            row.anchoredPosition = new Vector2(0f, hiddenTop + y);
            var h = Ui.H(row.gameObject, Ui.Pad(0), 0, TextAnchor.UpperLeft, true);
            h.childForceExpandHeight = true;
            for (int i = 0; i < 28; i++)
            {
                var stripe = Ui.Img(row, color(i), Ui.Pill, "Stripe");
                stripe.raycastTarget = false;
                Ui.LE(stripe, prefW: 0, minW: 0, flexW: 1);
            }
        }

        // ------------------------------------------------------------------ 왼쪽: 로고

        void BuildTitleLogo(Transform parent)
        {
            var left = Ui.RT("Start", parent);
            Ui.V(left.gameObject, Ui.Pad(0), 14, TextAnchor.MiddleCenter);
            Ui.LE(left, prefW: 480, minW: 480, flexW: 0);

            var logoRow = Ui.Row(left, 0, 230);
            var logoLayout = logoRow.GetComponent<HorizontalLayoutGroup>();
            logoLayout.childAlignment = TextAnchor.MiddleCenter;
            logoLayout.childForceExpandHeight = false;
            BuildMedallion(logoRow);

            var line1 = Ui.Txt(left, "카페 타워", 74, Theme.Gold, true, TextAnchor.MiddleCenter);
            line1.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Shadowed(line1, 0.6f, 4f);
            var line2 = Ui.Txt(left, "디펜스", 62, Theme.Text, true, TextAnchor.MiddleCenter);
            line2.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Shadowed(line2, 0.6f, 3f);

            var deco = Ui.Row(left, 12, 18);
            var decoLayout = deco.GetComponent<HorizontalLayoutGroup>();
            decoLayout.childAlignment = TextAnchor.MiddleCenter;
            decoLayout.childForceExpandHeight = false;
            DecoLine(deco);
            Ui.Icon(deco, Ui.Circle, Coral, 12);
            Ui.Icon(deco, Ui.Circle, Theme.Gold, 16);
            Ui.Icon(deco, Ui.Circle, Coral, 12);
            DecoLine(deco);

            Ui.Txt(left, "달콤한 디저트를 벌레로부터 지켜 주세요", 23, Theme.Dim, false, TextAnchor.MiddleCenter);
            Ui.Gap(left, 18);

            // 버튼 자체는 눌림 효과를 쓰므로 바깥 묶음만 살짝 숨 쉬듯 커졌다 작아짐
            var holder = Ui.Column(left, 14);
            // 저장된 영업이 있을 때만 보이는 이어하기 버튼
            var cont = Ui.Button(holder, "이어하기", Theme.BtnGold, ContinueFromTitle, null, 32, 88);
            cont.DynLabel = () => app.SavedGame != null ? $"이어하기  ·  웨이브 {app.SavedGame.Wave}" : "이어하기";
            Ui.Button(holder, "영업 시작", Theme.BtnGreen, StartFromTitle, null, 38, 100).DynLabel =
                () => app.SavedGame != null ? "새 영업 시작" : "영업 시작";
            UiTicker.Attach(holder, () =>
            {
                bool hasSave = app.SavedGame != null;
                if (cont.gameObject.activeSelf != hasSave) cont.gameObject.SetActive(hasSave);
                float s = 1f + (Mathf.Sin(Time.unscaledTime * 2.4f) * 0.5f + 0.5f) * 0.03f;
                holder.localScale = new Vector3(s, s, 1f);
            });
        }

        static void DecoLine(Transform parent)
        {
            var l = Ui.Img(parent, Theme.Alpha(Theme.Gold, 0.45f), null, "Line");
            l.raycastTarget = false;
            Ui.LE(l, prefW: 92, minW: 92, prefH: 2, minH: 2);
        }

        /// <summary>금색 테두리 원 안의 컵과 피어오르는 김</summary>
        static void BuildMedallion(Transform parent)
        {
            var m = Ui.RT("Medallion", parent);
            Ui.LE(m, prefW: 230, prefH: 230, minW: 230, minH: 230);

            var glow = Ui.Img(m, Theme.Alpha(Theme.Gold, 0.16f), Ui.Circle, "Glow");
            glow.raycastTarget = false;
            Ui.Center(glow.rectTransform, new Vector2(230, 230));
            UiTicker.Attach(glow, () =>
            {
                float s = 0.94f + Mathf.Sin(Time.unscaledTime * 1.6f) * 0.05f;
                glow.rectTransform.localScale = new Vector3(s, s, 1f);
            });

            var ring = Ui.Img(m, Theme.Gold, Ui.Circle, "Ring");
            ring.raycastTarget = false;
            Ui.Center(ring.rectTransform, new Vector2(182, 182));
            Ui.Shadowed(ring, 0.5f, 4f);
            var inner = Ui.Img(m, Theme.Card, Ui.Circle, "Inner");
            inner.raycastTarget = false;
            Ui.Center(inner.rectTransform, new Vector2(166, 166));

            var cup = Ui.Img(m, Theme.Gold, Ui.Cup, "Cup");
            cup.raycastTarget = false;
            Ui.Center(cup.rectTransform, new Vector2(96, 96), new Vector2(0f, -12f));

            for (int i = 0; i < 3; i++)
            {
                var steam = Ui.Img(m, Color.white, Ui.Circle, "Steam");
                steam.raycastTarget = false;
                var rt = steam.rectTransform;
                Ui.Center(rt, new Vector2(15, 15));
                float baseX = (i - 1) * 18f;
                float phase = i / 3f;
                UiTicker.Attach(steam, () =>
                {
                    float t = Mathf.Repeat(Time.unscaledTime * 0.42f + phase, 1f);
                    rt.anchoredPosition = new Vector2(baseX + Mathf.Sin(t * 7f + phase * 6f) * 5f, 36f + t * 36f);
                    rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.25f, t);
                    steam.color = new Color(1f, 0.96f, 0.88f, Mathf.Sin(t * Mathf.PI) * 0.55f);
                });
            }
        }

        // ------------------------------------------------------------------ 오른쪽: 기록

        void BuildRecordCard(Transform parent)
        {
            var card = Ui.Card(parent, Theme.Panel, 22, 7);
            Ui.LE(card, prefW: 0, flexW: 1, minW: 0);
            var edge = card.gameObject.AddComponent<Outline>();
            edge.effectColor = Theme.Alpha(Theme.Gold, 0.3f);
            edge.effectDistance = new Vector2(2f, -2f);

            var top = Ui.Row(card.transform, 12, 50);
            top.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            Ui.Icon(top, Ui.Coin, Theme.Gold, 40);
            var head = Ui.Txt(top, "명예의 전당", 36, Theme.Gold, true);
            head.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Spacer(top);
            Ui.Tag(top, "TOP 10", Coral, Color.white, 20);
            var share = Ui.Button(top, "1등 공유", Theme.BtnTeal, ShareBest, () => app.Records.Items.Count > 0, 21, 46);
            share.DynLabel = () => Time.unscaledTime < shareCopiedUntil ? "복사됨!" : "1등 공유";
            Ui.LE(share, prefW: 150, minW: 150);

            Ui.Txt(card.transform, "최종 보스 피해 → 웨이브 → 보스 시간 → 총 시간 → 디저트 → 코인", 18, Theme.Hint);
            Ui.Divider(card.transform);

            var heads = Ui.RT("RecordHead", card.transform);
            var headLayout = Ui.H(heads.gameObject, Ui.Pad(14, 14, 0, 0), 8);
            headLayout.childForceExpandHeight = true;
            Ui.LE(heads, prefH: 34, minH: 34, flexH: 0);
            for (int i = 0; i < RecordHeads.Length; i++) RecordCell(heads, RecordHeads[i], i, 19, Theme.Dim, true);

            recordList = Ui.Column(card.transform, 6);
            Ui.LE(recordList, flexH: 1);
        }

        void RebuildRecords() => FillRecords(recordList, app.LastRank, null);

        /// <summary>
        /// TOP 10 줄을 채움. highlight = 방금 세운 기록의 순위(★ 표시),
        /// live = 진행 중인 판 (10위 안에 들면 그 자리에 '지금' 줄로 끼워 넣음).
        /// </summary>
        void FillRecords(Transform listRoot, int highlight, RunRecord live)
        {
            Ui.Clear(listRoot);
            var list = new System.Collections.Generic.List<RunRecord>(app.Records.Items);
            if (live != null)
            {
                int at = app.Records.RankOf(live);
                if (at > 0) list.Insert(at - 1, live);
            }
            if (list.Count == 0)
            {
                BuildEmptyRecords(listRoot);
                return;
            }
            for (int i = 0; i < RunRecords.MaxEntries; i++)
            {
                var r = i < list.Count ? list[i] : null;
                bool isLive = r != null && ReferenceEquals(r, live);
                bool latest = r != null && !isLive && highlight == i + 1;
                Color medal = i == 0 ? Theme.Gold : i == 1 ? Silver : Bronze;
                Color rowColor = r == null ? Theme.Alpha(Theme.Card, 0.35f)
                    : isLive ? Color.Lerp(Theme.Card, Theme.Teal, 0.22f)
                    : latest ? Theme.CardHi
                    : i < 3 ? Color.Lerp(Theme.Card, medal, 0.1f) : Theme.Card;
                var row = Ui.Img(listRoot, rowColor, Ui.RoundSm, "RecordRow");
                row.raycastTarget = false;
                var h = Ui.H(row.gameObject, Ui.Pad(14, 14, 0, 0), 8);
                h.childForceExpandHeight = true;
                Ui.LE(row, prefH: 60, minH: 60, flexH: 0);
                if (latest || isLive)
                {
                    var o = row.gameObject.AddComponent<Outline>();
                    o.effectColor = isLive ? Theme.Teal : Theme.Gold;
                    o.effectDistance = new Vector2(2f, -2f);
                }

                if (isLive) RecordCell(row.transform, $"지금 {i + 1}", 0, 23, Theme.Teal, true);
                else if (r != null && i < 3) RankMedal(row.transform, i + 1, medal);
                else RecordCell(row.transform, latest ? $"★ {i + 1}" : $"{i + 1}", 0, 25, r != null ? Theme.Dim : Theme.Hint, true);

                string[] v = r == null
                    ? new[] { "—", "—", "—", "—", "—", "—" }
                    : new[]
                    {
                        r.Victory ? "완주" : $"{r.Wave}",
                        RunRecords.FormatNumber(r.BossDamage),
                        RunRecords.FormatTime(r.BossTime),
                        RunRecords.FormatTime(r.TotalTime),
                        $"{r.Desserts}",
                        RunRecords.FormatNumber(r.Coins),
                    };
                for (int c = 0; c < v.Length; c++)
                    RecordCell(row.transform, v[c], c + 1, 23, r == null ? Theme.Hint : c == 1 ? Theme.Red : Theme.Text, c == 1);
            }
        }

        /// <summary>기록이 하나도 없을 때의 안내</summary>
        void BuildEmptyRecords(Transform listRoot)
        {
            var box = Ui.RT("Empty", listRoot);
            Ui.V(box.gameObject, Ui.Pad(0), 14, TextAnchor.MiddleCenter);
            Ui.LE(box, flexH: 1);
            var iconRow = Ui.Row(box, 0, 110);
            var layout = iconRow.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandHeight = false;
            Ui.Icon(iconRow, Ui.Cake, Theme.Alpha(Theme.Gold, 0.6f), 100);
            Ui.Txt(box, "아직 기록이 없어요", 34, Theme.Text, true, TextAnchor.MiddleCenter);
            Ui.Txt(box, "첫 영업을 시작해서 명예의 전당에 이름을 올려 보세요", 22, Theme.Dim, false, TextAnchor.MiddleCenter);
        }

        static void RankMedal(Transform row, int rank, Color color)
        {
            var cell = Ui.RT("Medal", row);
            Ui.LE(cell, prefW: 0, minW: 0, flexW: RecordWeights[0]);
            var disc = Ui.Img(cell, color, Ui.Circle, "Disc");
            disc.raycastTarget = false;
            Ui.Center(disc.rectTransform, new Vector2(44, 44));
            Ui.Shadowed(disc, 0.4f, 2f);
            var t = Ui.Txt(disc.transform, rank.ToString(), 25, Theme.Bg, true, TextAnchor.MiddleCenter);
            Ui.Stretch(t.rectTransform);
        }

        static void RecordCell(Transform row, string s, int column, int size, Color color, bool bold)
        {
            var t = Ui.Txt(row, s, size, color, bold, TextAnchor.MiddleCenter);
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 13;
            t.resizeTextMaxSize = size;
            Ui.LE(t, prefW: 0, minW: 0, flexW: RecordWeights[column]);
        }

        void ShareBest()
        {
            if (app.Records.Items.Count == 0) return;
            // 공유창이 없는 환경에서는 클립보드에 복사되었다고 버튼에 잠깐 표시
            if (!ScoreShare.Share(app.Records.Items[0])) shareCopiedUntil = Time.unscaledTime + 2f;
        }

        void StartFromTitle()
        {
            if (app.SavedGame != null)
            {
                AskConfirm("새 영업을 시작할까요?",
                    $"저장된 영업(웨이브 {app.SavedGame.Wave})은 지워지고 처음부터 시작해요.",
                    "새로 시작", BeginFromTitle);
                return;
            }
            BeginFromTitle();
        }

        void BeginFromTitle()
        {
            app.ClearLastRank();
            titleRoot.SetActive(false);
            StartNewGame();
        }

        void ContinueFromTitle()
        {
            app.ClearLastRank();
            StartNewGame();
            if (!app.ContinueSaved()) return; // 저장이 깨졌으면 버튼이 사라지고 시작 화면에 남음
            titleRoot.SetActive(false);
            // 불러온 상태를 기준으로 화면 변화 감지를 다시 맞춤 (코인 획득 표시 등이 잘못 뜨지 않게)
            lastGold = G.Coins;
            lastLives = G.Desserts;
            lastState = G.State;
            bodySig = null;
        }

        void OpenTitle()
        {
            StartNewGame();
            RebuildRecords();
            titleRoot.SetActive(true);
        }
    }
}

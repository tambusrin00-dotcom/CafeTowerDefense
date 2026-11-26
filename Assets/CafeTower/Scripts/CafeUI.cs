using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CafeTowerDefense
{
    /// <summary>
    /// 오른쪽 정보 패널 + 보드 위 오버레이(알림, 웨이브 배너, 조합법 창, 게임 종료, 코인 팝업)를 코드로 만드는 uGUI.
    /// 구조가 바뀔 때(Cafe.Version, 선택 칸 등)만 패널 본문을 다시 만들고,
    /// 숫자·버튼 상태는 각 요소가 매 프레임 스스로 갱신함(UiTicker, ButtonFx).
    /// </summary>
    public partial class CafeUI
    {
        /// <summary>정보 패널 폭 (캔버스 단위, 화면 높이 1080 기준). 패널은 고정 폭이고 나머지는 모두 게임판.</summary>
        public const float PanelWidth = 560f;
        /// <summary>패널을 숨겼을 때 보이는 간략 버튼 열의 폭</summary>
        public const float CompactWidth = 132f;
        const float RefHeight = 1080f;

        // 패널 숨기기
        public bool PanelVisible = true;
        float panelT = 1f; // 1 = 패널, 0 = 간략 버튼 열 (애니메이션 중간값 포함)
        RectTransform panelGroup, compactRT, boardArea;
        CanvasGroup panelCG, compactCG;

        /// <summary>게임판 오른쪽을 가리는 UI 폭(화면 픽셀). 카메라 영역 계산용.</summary>
        public float RightInsetPx => RightInsetUnits * ScaleFactor;
        float RightInsetUnits => Mathf.Lerp(CompactWidth, PanelWidth, Mathf.SmoothStep(0f, 1f, panelT));

        public void TogglePanel() => PanelVisible = !PanelVisible;

        /// <summary>패널과 간략 버튼 열이 함께 쓰는 행동 목록</summary>
        enum Act { Pause, Speed1, Speed2, Speed3, Recipes, ZoomIn, ZoomFit, ZoomOut, NewGame, Music, Ranking, TogglePanel }

        readonly CafeApp app;
        Cafe G => app.Cafe;

        Canvas canvas, fxCanvas;
        public float ScaleFactor => canvas != null ? canvas.scaleFactor : 1f;
        public bool ShowRecipes;

        // 헤더
        Text waveValue, livesValue, goldValue;
        Image livesChip;
        int lastGold, lastLives;
        float goldPunch, livesFlash;

        // 패널 본문
        ScrollRect bodyScroll;
        RectTransform body;
        string bodySig;
        Cell? lastSelected;

        // 보드 오버레이
        CanvasGroup toastGroup;
        RectTransform toastRT;
        Text toastText;
        string lastMessage;
        float toastAge = 99f;

        CanvasGroup bannerGroup;
        Text bannerTitle, bannerSub;
        float bannerAge = 99f;
        GameState lastState;

        GameObject pausePill;

        RawImage vignette;
        float vignetteT;

        CanvasGroup overGroup;
        RectTransform overCard;
        Text overTitle, overSub;
        RectTransform overStats;
        float overAge;

        CanvasGroup recipeGroup;
        RectTransform recipeCard, recipeContent;
        ScrollRect recipeScroll;
        int recipeVersion = -1;
        float recipeAge;

        Text[] wpLabels;
        float newGameArmedUntil;

        class Floater
        {
            public Text Text;
            public Vector3 World;
            public float Age, Life;
        }

        readonly List<Floater> floaters = new List<Floater>();
        readonly Stack<Floater> floaterPool = new Stack<Floater>();
        RectTransform floaterRoot;

        public CafeUI(CafeApp app, Transform parent)
        {
            this.app = app;
            Ui.Init();
            BuildFxCanvas(parent);
            BuildMainCanvas(parent);
            lastGold = G.Coins;
            lastLives = G.Desserts;
            lastState = G.State;
            G.Killed += OnKilled;
            G.Leaked += OnLeaked;
        }

        // =================================================================== 캔버스

        Canvas NewCanvas(Transform parent, string name, int order, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            if (raycast) go.AddComponent<GraphicRaycaster>();
            return c;
        }

        /// <summary>보드 위에 떠 있는 글자(체크포인트, 코인 팝업)와 피격 화면 효과. 클릭을 막지 않음.</summary>
        void BuildFxCanvas(Transform parent)
        {
            fxCanvas = NewCanvas(parent, "BoardFx", 0, false);
            var ct = fxCanvas.transform;

            var wps = Cafe.Waypoints;
            wpLabels = new Text[wps.Length];
            for (int i = 0; i < wps.Length; i++)
            {
                string label = i == 0 ? "입구" : i == wps.Length - 1 ? "진열대" : i.ToString();
                var t = Ui.Txt(ct, label, 20, Color.white, true, TextAnchor.MiddleCenter);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.rectTransform.sizeDelta = new Vector2(120, 60);
                Ui.Shadowed(t, 0.6f, 2f);
                wpLabels[i] = t;
            }

            floaterRoot = Ui.RT("Floaters", ct);
            Ui.Stretch(floaterRoot);

            vignette = Ui.RT("Vignette", ct).gameObject.AddComponent<RawImage>();
            vignette.texture = Ui.Vignette;
            vignette.color = Theme.Alpha(Theme.Red, 0f);
            vignette.raycastTarget = false;
            Ui.Stretch(vignette.rectTransform);
        }

        void BuildMainCanvas(Transform parent)
        {
            canvas = NewCanvas(parent, "CafeUI", 10, true);
            var ct = canvas.transform;
            BuildPanel(ct);
            BuildCompact(ct);

            boardArea = Ui.RT("BoardArea", ct);
            Ui.Stretch(boardArea, 0, 0, PanelWidth, 0);
            BuildToast(boardArea);
            BuildBanner(boardArea);
            BuildPausePill(boardArea);
            BuildGameOver(boardArea);
            BuildRecipes(boardArea);
            BuildRanking(boardArea);
            BuildTitle(ct);
            BuildConfirm(ct);
        }

        // =================================================================== 오른쪽 패널

        void BuildPanel(Transform canvasRoot)
        {
            // 패널 전체(그림자·패널·토글 탭)를 한 그룹으로 묶어 옆으로 밀어서 숨김
            panelGroup = Ui.RT("PanelGroup", canvasRoot);
            Ui.Stretch(panelGroup);
            panelCG = panelGroup.gameObject.AddComponent<CanvasGroup>();
            var ct = panelGroup;

            var shadow = Ui.Img(ct, new Color(0f, 0f, 0f, 0.55f), Ui.Shadow, "PanelShadow");
            shadow.raycastTarget = false;
            var srt = shadow.rectTransform;
            srt.anchorMin = new Vector2(1, 0);
            srt.anchorMax = new Vector2(1, 1);
            srt.pivot = new Vector2(1, 0.5f);
            srt.sizeDelta = new Vector2(PanelWidth + 20, 20);
            srt.anchoredPosition = new Vector2(2, -6);

            var panel = Ui.Img(ct, Theme.Panel, Ui.Round, "Panel").rectTransform;
            panel.anchorMin = new Vector2(1, 0);
            panel.anchorMax = new Vector2(1, 1);
            panel.pivot = new Vector2(1, 0.5f);
            panel.sizeDelta = new Vector2(PanelWidth - 20, -20);
            panel.anchoredPosition = new Vector2(-10, 0);
            Ui.V(panel.gameObject, Ui.Pad(20, 20, 20, 18), 14);

            // 제목
            var title = Ui.Row(panel, 12);
            Ui.Icon(title, Ui.Cup, Theme.Gold, 34);
            var tt = Ui.Txt(title, "카페 타워 디펜스", 30, Theme.Gold, true);
            tt.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Shadowed(tt, 0.5f, 2f);

            // 상태 칩
            // 높이 절반 (70 → 35). 이름과 숫자를 한 줄에 나란히 놓아 글자 크기는 유지
            var chips = Ui.Row(panel, 8, 35, true);
            waveValue = Chip(chips, Ui.Bug, Theme.Dim, "웨이브", out _);
            livesValue = Chip(chips, Ui.Cake, Theme.Red, "디저트", out livesChip);
            goldValue = Chip(chips, Ui.Coin, Theme.Gold, "코인", out _);

            // 본문 (스크롤)
            bodyScroll = Ui.Scroll(panel, out body, Ui.Pad(0, 0, 2, 8), 12);
            Ui.LE(bodyScroll, flexH: 1, minH: 120);

            // 하단 버튼 (간략 버튼 열과 같은 행동 목록 사용)
            Ui.Divider(panel);

            // 바리스타 레벨업: 스크롤하지 않아도 늘 보이도록 고정 영역 맨 위에 크게 둠
            // 높이 78
            var lv = Ui.Button(panel, "", Theme.BtnGold, () => G.UpgradeBarista(),
                () => G.BaristaLevel < MenuDB.MaxBaristaLevel && G.Coins >= MenuDB.BaristaLevelCost(G.BaristaLevel) && !G.IsOver,
                26, 78);
            lv.DynLabel = () => G.BaristaLevel < MenuDB.MaxBaristaLevel
                ? $"바리스타 레벨업  ·  {MenuDB.BaristaLevelCost(G.BaristaLevel)}코인"
                : "바리스타 최고 레벨";

            // 정지·배속: 글자(21)가 들어가는 높이
            var speedRow = Ui.Row(panel, 8, 52, true);
            foreach (var a in new[] { Act.Pause, Act.Speed1, Act.Speed2, Act.Speed3 }) ActButton(speedRow, a, false, 52, 21).KeepFontSize();

            // 레시피·새 영업: 글자(22)가 들어가는 높이
            var row2 = Ui.Row(panel, 8, 54, true);
            ActButton(row2, Act.Recipes, false, 54, 22).KeepFontSize();
            ActButton(row2, Act.Ranking, false, 54, 22).KeepFontSize();
            ActButton(row2, Act.NewGame, false, 54, 22).KeepFontSize();
            ActButton(row2, Act.Music, false, 54, 22).KeepFontSize();

            // 축소·전체 보기·확대: 글자(19)가 들어가는 높이
            var row3 = Ui.Row(panel, 8, 50, true);
            foreach (var a in new[] { Act.ZoomOut, Act.ZoomFit, Act.ZoomIn }) ActButton(row3, a, false, 50, 19).KeepFontSize();

            // 패널 왼쪽 가장자리에 붙은 숨기기 탭
            var tab = ActButton(ct, Act.TogglePanel, false, 0, 24);
            var trt = (RectTransform)tab.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(1f, 0.5f);
            trt.pivot = new Vector2(1f, 0.5f);
            trt.sizeDelta = new Vector2(46f, 120f);
            trt.anchoredPosition = new Vector2(-(PanelWidth - 14f), 0f);
        }

        /// <summary>패널을 숨겼을 때 오른쪽에 남는 간략 버튼 열 (상태 정보 + 공통 행동). 내용이 길면 위아래로 스크롤.</summary>
        void BuildCompact(Transform ct)
        {
            var bg = Ui.Img(ct, Theme.Panel, Ui.Round, "CompactColumn");
            compactRT = bg.rectTransform;
            compactRT.anchorMin = new Vector2(1, 0);
            compactRT.anchorMax = new Vector2(1, 1);
            compactRT.pivot = new Vector2(1, 0.5f);
            compactRT.sizeDelta = new Vector2(CompactWidth - 16f, -20f);
            compactCG = bg.gameObject.AddComponent<CanvasGroup>();

            var scroll = Ui.Scroll(bg.transform, out var col, Ui.Pad(10, 10, 12, 12), 8);
            Ui.Stretch((RectTransform)scroll.transform);

            ActButton(col, Act.TogglePanel, true, 52, 19);

            // 상태 정보 상자
            var box = Ui.Card(col, Theme.Card, 10, 8).transform;
            StatusLine(box, Ui.Bug, Theme.Dim, () => $"{G.Wave}");
            StatusLine(box, Ui.Cake, Theme.Red, () => G.Desserts.ToString());
            StatusLine(box, Ui.Coin, Theme.Gold, () => $"{G.Coins:N0}");

            Ui.Divider(col);
            Ui.Button(col, "패스\n+1%", Theme.BtnTeal, () => G.PassRound(), () => G.CanPassRound, 17, 62);
            // 바리스타 레벨업: 좁은 열에 맞게 두 줄(이름 / 비용)로 표시
            var lv = Ui.Button(col, "", Theme.BtnGold, () => G.UpgradeBarista(),
                () => G.BaristaLevel < MenuDB.MaxBaristaLevel && G.Coins >= MenuDB.BaristaLevelCost(G.BaristaLevel) && !G.IsOver,
                17, 62);
            lv.DynLabel = () => G.BaristaLevel < MenuDB.MaxBaristaLevel
                ? $"레벨업\n{MenuDB.BaristaLevelCost(G.BaristaLevel)}코인"
                : "최고\n레벨";
            foreach (var a in new[] { Act.Pause, Act.Speed1, Act.Speed2, Act.Speed3 }) ActButton(col, a, true, 46, 19);
            Ui.Divider(col);
            foreach (var a in new[] { Act.ZoomIn, Act.ZoomFit, Act.ZoomOut }) ActButton(col, a, true, 46, 19);
            Ui.Divider(col);
            ActButton(col, Act.Recipes, true, 46, 18);
            ActButton(col, Act.Ranking, true, 46, 18);
            ActButton(col, Act.NewGame, true, 46, 17);
            ActButton(col, Act.Music, true, 46, 17);
        }

        void StatusLine(Transform parent, Sprite icon, Color color, System.Func<string> value)
        {
            var row = Ui.Row(parent, 8);
            Ui.Icon(row, icon, color, 22);
            var t = Ui.Txt(row, "", 20, Theme.Text, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiTicker.Attach(t, () => t.text = value());
        }

        bool NewGameArmed => Time.unscaledTime < newGameArmedUntil;

        /// <summary>공통 행동 버튼. compact = 간략 버튼 열용 짧은 라벨.</summary>
        ButtonFx ActButton(Transform parent, Act a, bool compact, float height, int font)
        {
            ButtonFx b;
            switch (a)
            {
                case Act.Pause:
                case Act.Speed1:
                case Act.Speed2:
                case Act.Speed3:
                {
                    int sp = a == Act.Pause ? 0 : CafeApp.SpeedLevels[a - Act.Speed1];
                    b = Ui.Button(parent, sp == 0 ? "정지" : $"{sp}x", Theme.BtnNeutral, () => app.SetSpeed(sp), null, font, height);
                    b.DynColor = () => app.Speed == sp ? (sp == 0 ? Theme.BtnRed : Theme.BtnBlue) : Theme.BtnNeutral;
                    break;
                }
                case Act.Recipes:
                    b = Ui.Button(parent, "레시피", Theme.BtnPurple, () =>
                    {
                        ShowRecipes = !ShowRecipes;
                        if (ShowRecipes) ShowRanking = false;
                    }, null, font, height);
                    b.DynLabel = compact
                        ? (System.Func<string>)(() => ShowRecipes ? "닫기" : "레시피")
                        : () => ShowRecipes ? "레시피 닫기" : "레시피 보기";
                    break;
                case Act.ZoomIn:
                    b = Ui.Button(parent, compact ? "+" : "+ 확대", Theme.BtnNeutral, app.ZoomIn, () => app.CanZoomIn, font, height);
                    break;
                case Act.ZoomFit:
                    b = Ui.Button(parent, compact ? "전체" : "전체 보기", Theme.BtnNeutral, app.ZoomFit, () => !app.AtFit, font, height);
                    break;
                case Act.ZoomOut:
                    b = Ui.Button(parent, compact ? "-" : "- 축소", Theme.BtnNeutral, app.ZoomOut, () => app.CanZoomOut, font, height);
                    break;
                case Act.NewGame:
                    b = Ui.Button(parent, "새 영업", Theme.BtnNeutral, () =>
                    {
                        if (NewGameArmed)
                        {
                            newGameArmedUntil = 0f;
                            StartNewGame();
                        }
                        else newGameArmedUntil = Time.unscaledTime + 3f;
                    }, null, font, height);
                    b.DynLabel = compact
                        ? (System.Func<string>)(() => NewGameArmed ? "한 번 더" : "새 영업")
                        : () => NewGameArmed ? "한 번 더 누르면 시작" : "새 영업";
                    b.DynColor = () => NewGameArmed ? Theme.BtnRed : Theme.BtnNeutral;
                    break;
                case Act.Ranking:
                    b = Ui.Button(parent, "랭킹", Theme.BtnGold, () =>
                    {
                        ShowRanking = !ShowRanking;
                        if (ShowRanking) ShowRecipes = false;
                    }, null, font, height);
                    b.DynLabel = () => ShowRanking ? "닫기" : "랭킹";
                    break;
                case Act.Music:
                    b = Ui.Button(parent, "음악", Theme.BtnNeutral, () => app.Music.Toggle(), null, font, height);
                    b.DynLabel = compact
                        ? (System.Func<string>)(() => app.Music.On ? "음악" : "음악끔")
                        : () => app.Music.On ? "음악 켬" : "음악 끔";
                    b.DynColor = () => app.Music.On ? Theme.BtnPurple : Theme.BtnNeutral;
                    break;
                default: // TogglePanel
                    b = Ui.Button(parent, compact ? "◀ 패널" : "▶", Theme.BtnTeal, TogglePanel, null, font, height);
                    break;
            }
            return b;
        }

        static Text Chip(Transform parent, Sprite icon, Color color, string caption, out Image bg)
        {
            bg = Ui.Img(parent, Theme.Card, Ui.Round, "Chip");
            bg.raycastTarget = false;
            // [아이콘] 이름 ······ 숫자 (한 줄)
            // 칩 영역 높이는 35(원래 70의 절반), 아이콘·글자 크기는 원래대로 (아이콘 30, 이름 15, 숫자 25)
            Ui.H(bg.gameObject, Ui.Pad(10, 10, 2, 2), 6, TextAnchor.MiddleLeft);
            Ui.LE(bg, flexW: 1);
            Ui.Icon(bg.transform, icon, color, 30);
            var cap = Ui.Txt(bg.transform, caption, 15, Theme.Dim, false, TextAnchor.MiddleLeft);
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            var v = Ui.Txt(bg.transform, "", 25, Theme.Text, true, TextAnchor.MiddleRight);
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            v.verticalOverflow = VerticalWrapMode.Overflow;
            Ui.LE(v, flexW: 1);
            return v;
        }

        void StartNewGame()
        {
            G.NewGame();
            ShowRecipes = false;
            ShowRanking = false;
            CloseConfirm();
            bodySig = null;
            lastGold = G.Coins;
            lastLives = G.Desserts;
            lastState = G.State;
            bannerAge = 99f;
            foreach (var f in floaters)
            {
                f.Text.gameObject.SetActive(false);
                floaterPool.Push(f);
            }
            floaters.Clear();
        }

        // =================================================================== 패널 본문

        void RebuildBody()
        {
            Ui.Clear(body);
            if (G.Selected.HasValue) SelectionCard(G.Selected.Value);
            StateCard();
            QualityCard();
            if (!G.Selected.HasValue)
            {
                var hint = Ui.Txt(body, "음료나 빈 컵을 눌러 자세한 정보를 확인하세요.\n휠·두 손가락 확대/축소  ·  드래그 이동  ·  Tab 패널 숨기기\nESC·우클릭 선택 해제  ·  Space 일시정지  ·  1~3 배속  ·  R 레시피",
                    17, Theme.Hint, false, TextAnchor.UpperCenter);
                hint.lineSpacing = 1.3f;
            }
        }

        // ------------------------------------------------------------------ 선택 카드

        void SelectionCard(Cell sel)
        {
            var card = Ui.Card(body, Theme.CardHi, 16, 10).transform;

            if (G.CellTypeAt(sel) == CellType.EmptyCup)
            {
                Header(card, Ui.Cup, MenuInfo.Argb(0xFFD9C7B0), "빈 컵", Theme.Text, "장애물 · 벌레의 길을 막아요");
                Ui.Button(card, $"빈 컵 치우기  ·  {Cafe.CupClearCost}코인", Theme.BtnNeutral, () => G.ClearCup(sel),
                    () => G.IsBuildState && G.Coins >= Cafe.CupClearCost);
                return;
            }

            var t = G.TowerAt(sel);
            if (t == null) return;
            Color col = t.Stats.Color;

            string sub;
            if (t.Signature.HasValue)
            {
                int tier = MenuDB.TierOf(t.Signature.Value);
                sub = tier == MenuDB.SetTier || tier == MenuDB.CourseTier
                    ? Theme.Paint($"{MenuDB.TierNames[tier]} 메뉴", Theme.Tier(tier))
                    : Theme.Paint($"시그니처 · {MenuDB.TierNames[tier]}", Theme.Tier(tier));
                if (t.SignatureLevel > 0) sub += $"  ·  강화 {t.SignatureLevel}/{MenuDB.MaxSignatureLevel}";
            }
            else
            {
                int qi = (int)t.Grade;
                sub = Theme.Paint(new string('★', qi + 1), Theme.GradeColor(t.Grade)) +
                      Theme.Paint(new string('★', 4 - qi), MenuInfo.Argb(0xFF3A4255)) +
                      "  " + Theme.Paint(t.Grade.Label(), Theme.GradeColor(t.Grade));
            }
            sub += t.Kept ? "  ·  서빙 중" : "  ·  이번 라운드";
            Header(card, t.Signature.HasValue ? Ui.Cake : Ui.Cup, col, t.Stats.Name, col, sub);

            // 행동 버튼
            if (t.Kept && t.Signature.HasValue && MenuDB.CanUpgrade(t.Signature.Value) && t.SignatureLevel < MenuDB.MaxSignatureLevel)
            {
                int cost = MenuDB.SignatureUpgradeCost(t.SignatureLevel);
                Ui.Button(card, $"메뉴 강화  ·  {cost}코인", Theme.BtnPurple, () => G.UpgradeSignature(t),
                    () => G.IsBuildState && G.Coins >= cost);
            }

            if (t.Kept && t.Signature.HasValue)
                foreach (var cr in MenuDB.CourseRecipes)
                {
                    if (G.FindCourseParts(t, cr) == null) continue;
                    var crr = cr;
                    Ui.Button(card, $"코스 메뉴 → {cr.Result.Label()}", Theme.BtnTeal, () => G.MakeCourse(t, crr), () => G.FindCourseParts(t, crr) != null);
                }

            if (G.CanDemolish(t))
                Ui.Button(card, "음료 버리기  ·  무료", Theme.BtnRed, () => AskDemolish(t), () => G.CanDemolish(t));

            if (G.State == GameState.BuildChoose && G.RoundDrinks.Contains(t))
            {
                Ui.Button(card, "이 음료 남기기", Theme.BtnGreen, () => G.Keep(t));
                for (int levels = 1; levels <= 2; levels++)
                {
                    if (!G.CanBlend(t, levels)) continue;
                    int lv = levels;
                    var q = (Grade)((int)t.Grade + levels);
                    string label = (levels == 1 ? "블렌딩 → " : "더블 블렌딩 → ") + $"{q.Label()} {t.Flavor.Label()}";
                    Ui.Button(card, label, Theme.BtnGold, () => G.Blend(t, lv));
                }
                foreach (var r in MenuDB.Recipes)
                {
                    if (G.FindRecipeParts(t, r) == null) continue;
                    var rr = r;
                    Ui.Button(card, $"시그니처 레시피 → {r.Result.Label()}", Theme.BtnPurple, () => G.MakeSignature(t, rr));
                }
                foreach (var sr in MenuDB.SetRecipes)
                {
                    if (G.FindSetDessert(t, sr) == null) continue;
                    var srr = sr;
                    Ui.Button(card, $"세트 메뉴 → {sr.Result.Label()}", Theme.BtnSet, () => G.MakeSet(t, srr));
                }
            }

            if (G.IsBuildState && t.Signature.HasValue)
            {
                if (G.MovingFrom == sel)
                {
                    var info = Ui.Card(card, Theme.Alpha(Theme.Teal, 0.14f), 12, 4).transform;
                    Ui.Txt(info, "옮길 칸을 누르세요", 19, Theme.Teal, true);
                    Ui.Txt(info, "빈 칸 → 이동  ·  음료/빈 컵 → 자리 교환  ·  메뉴당 1번", 16, Theme.Dim);
                    Ui.Button(card, "이동 취소", Theme.BtnRed, () => G.CancelMove());
                }
                else if (t.Moved)
                {
                    Ui.Button(card, "자리 옮기기 사용함", Theme.BtnNeutral, () => { }, () => false);
                }
                else
                {
                    Ui.Button(card, $"자리 옮기기  ·  {Cafe.MoveCost}코인", Theme.BtnTeal, () => G.StartMove(sel), () => G.CanStartMove(sel));
                }
            }

            // 능력치
            Ui.Gap(card, 2);
            Ui.Divider(card);
            StatRows(card, t.Stats);
            if (!string.IsNullOrEmpty(t.Stats.Desc))
            {
                var d = Ui.Txt(card, t.Stats.Desc, 17, Theme.Dim);
                d.fontStyle = FontStyle.Italic;
            }
            if (t.Kept)
            {
                Ui.Divider(card);
                var kills = Ui.KeyValue(card, "처치", "", Theme.Green);
                UiTicker.Attach(kills, () => kills.text = $"{t.Kills}");
                var dmg = Ui.KeyValue(card, "누적 피해", "", Theme.Green);
                UiTicker.Attach(dmg, () => dmg.text = $"{MenuInfo.Num(t.TotalDamage)}");
            }
            if (!t.Signature.HasValue)
            {
                var key = new DrinkKey(t.Flavor, t.Grade);
                var uses = MenuDB.RecipesUsing(key);
                if (uses.Count > 0) Ui.KeyValue(card, "재료로 쓰임", string.Join(", ", uses.Select(u => u.Result.Label())), Theme.Purple, 17);
                var sets = MenuDB.SetsUsingDrink(key);
                if (sets.Count > 0)
                    Ui.KeyValue(card, "세트 음료", string.Join(", ", sets.Select(s => $"{s.Result.Label()} ({s.Dessert.Label()}와 함께)")), Theme.Tier(MenuDB.SetTier), 17);
            }
            else if (MenuDB.SetUsingDessert(t.Signature.Value) is SetRecipe setR)
            {
                // 이 디저트로 만들 수 있는 세트 안내
                Ui.KeyValue(card, "세트 메뉴", $"{setR.Result.Label()} = 이 디저트 + {setR.Drink}", Theme.Tier(MenuDB.SetTier), 17);
            }
            else if (MenuDB.IsSet(t.Signature.Value))
            {
                var sr = MenuDB.SetFor(t.Signature.Value);
                Ui.KeyValue(card, "구성", $"{sr.Dessert.Label()} + {sr.Drink}", Theme.Tier(MenuDB.SetTier), 17);
            }
            if (t.Signature.HasValue)
            {
                var courses = MenuDB.CoursesUsing(t.Signature.Value);
                if (courses.Count > 0)
                    Ui.KeyValue(card, "코스 재료", string.Join(", ", courses.Select(c => c.Result.Label())), Theme.Tier(MenuDB.CourseTier), 17);
                if (MenuDB.IsCourse(t.Signature.Value))
                    Ui.KeyValue(card, "구성", string.Join(" + ", MenuDB.CourseFor(t.Signature.Value).Parts.Select(p => p.Label())), Theme.Tier(MenuDB.CourseTier), 17);
            }
        }

        static void Header(Transform card, Sprite icon, Color iconColor, string name, Color nameColor, string sub)
        {
            var row = Ui.Row(card, 14);
            var tile = Ui.Img(row, Theme.Inset, Ui.Round, "IconTile");
            Ui.LE(tile, prefW: 68, prefH: 68, minW: 68, minH: 68);
            var glow = Ui.Img(tile.transform, Theme.Alpha(iconColor, 0.18f), Ui.Circle, "Glow");
            Ui.Center(glow.rectTransform, new Vector2(60, 60));
            var ic = Ui.Img(tile.transform, iconColor, icon, "Icon");
            Ui.Center(ic.rectTransform, new Vector2(40, 40));
            Ui.Shadowed(ic, 0.5f, 2f);

            var col = Ui.Column(row, 4);
            Ui.LE(col, flexW: 1);
            Ui.Txt(col, name, 26, nameColor, true);
            Ui.Txt(col, sub, 17, Theme.Dim);
        }

        static void StatRows(Transform card, MenuStats s)
        {
            float dps = s.Damage * s.Targets / s.Cooldown * (1f + s.CritChance * (s.CritMult - 1f));
            Ui.KeyValue(card, "공격력", MenuInfo.Num(s.Damage), Theme.Text);
            Ui.KeyValue(card, "초당 피해", $"약 {MenuInfo.Num(dps)}" + (s.PoisonDps > 0 ? $" + 지속 {MenuInfo.Num(s.PoisonDps)}" : ""), Theme.Gold);
            Ui.KeyValue(card, "사거리", $"{s.Range:0.0}칸", Theme.Text);
            Ui.KeyValue(card, "공격 간격", $"{s.Cooldown:0.00}초", Theme.Text);
            if (s.SplashRadius > 0) Ui.KeyValue(card, "범위 피해", $"{s.SplashPct * 100:0}%  ·  반경 {s.SplashRadius:0.0}", Theme.Orange);
            if (s.SlowPct > 0) Ui.KeyValue(card, "둔화", $"{s.SlowPct * 100:0}%  ·  {s.SlowDur:0.0}초", Theme.Blue);
            if (s.PoisonDps > 0) Ui.KeyValue(card, "지속 피해", $"초당 {MenuInfo.Num(s.PoisonDps)}  ·  {s.PoisonDur:0}초", Theme.Green);
            if (s.Targets > 1) Ui.KeyValue(card, "동시 공격", $"{s.Targets}마리", Theme.Gold);
            if (s.CritChance > 0) Ui.KeyValue(card, "치명타", $"{s.CritChance * 100:0}%  ·  x{s.CritMult:0.0}", Theme.Red);
            if (s.AirMult > 1f) Ui.KeyValue(card, "파리 피해", $"x{s.AirMult:0.0}", Theme.Purple);
            if (s.ChainCount > 0) Ui.KeyValue(card, "연쇄", $"{s.ChainCount}회  ·  튕길 때 {s.ChainPct * 100:0}%", Theme.Gold);
            if (s.StunChance > 0) Ui.KeyValue(card, "기절", $"{s.StunChance * 100:0}%  ·  {s.StunDur:0.0}초 (대장 절반)", Theme.Teal);
            if (s.BountyBonus > 0) Ui.KeyValue(card, "추가 코인", $"+{s.BountyBonus}", Theme.Gold);
            if (s.ExecutePct > 0) Ui.KeyValue(card, "마무리", $"체력 {s.ExecutePct * 100:0}% 이하 (대장 제외)", Theme.Red);
        }

        // ------------------------------------------------------------------ 진행 상태 카드

        void StateCard()
        {
            var card = Ui.Card(body).transform;
            var head = Ui.Row(card, 10);
            string phase;
            Color pc;
            switch (G.State)
            {
                case GameState.BuildPlace: phase = "준비"; pc = Theme.Green; break;
                case GameState.BuildChoose: phase = "선택"; pc = Theme.Gold; break;
                case GameState.Wave: phase = "방어"; pc = Theme.Red; break;
                case GameState.GameOver: phase = "폐점"; pc = Theme.Hint; break;
                default: phase = "영업 성공"; pc = Theme.Gold; break;
            }
            Ui.Tag(head, phase, pc, Theme.Bg, 18);
            Ui.Spacer(head);
            var w = G.State == GameState.Wave ? G.CurrentWave : G.WaveInfoFor(G.Wave);
            var wt = Ui.Txt(head, (G.State == GameState.Wave ? "현재 웨이브: " : "다음 웨이브: ") + WaveLabel(w), 17, Theme.Dim, false, TextAnchor.MiddleRight);
            wt.horizontalOverflow = HorizontalWrapMode.Overflow;

            switch (G.State)
            {
                case GameState.BuildPlace:
                {
                    Ui.Txt(card, "빈 칸을 눌러 무작위 음료를 놓으세요. 음료와 빈 컵으로 동선을 꼬아 벌레의 길을 늘리세요.", 18, Theme.Dim);
                    var dots = Ui.Row(card, 8);
                    for (int i = 0; i < Cafe.DrinksPerRound; i++)
                        Ui.Icon(dots, Ui.Cup, i < G.RoundDrinks.Count ? Theme.Gold : MenuInfo.Argb(0xFF4A3628), 26);
                    Ui.Spacer(dots);
                    var cnt = Ui.Txt(dots, $"{G.RoundDrinks.Count} / {Cafe.DrinksPerRound}", 19, Theme.Text, true);
                    cnt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    if (G.CanPassRound)
                        Ui.Button(card, "배치 패스  ·  모든 음료 공격력 +1%", Theme.BtnTeal, () => G.PassRound(), () => G.CanPassRound, 20, 54);
                    if (G.PassCount > 0)
                        Ui.Txt(card, $"패스 {G.PassCount}회  ·  모든 음료 공격력 +{G.DamageBonusPercent:0.##}%", 16, Theme.Gold);
                    break;
                }
                case GameState.BuildChoose:
                    Ui.Txt(card, "빛나는 테두리의 이번 라운드 음료 중 한 잔을 남기세요. 나머지는 빈 컵이 돼요.", 18, Theme.Dim);
                    Ui.Txt(card, Theme.Paint("초록 테두리", Theme.Green) + " = 블렌딩 가능  (같은 음료 2잔 → 블렌딩, 4잔 → 더블 블렌딩)", 16, Theme.Dim);
                    break;
                case GameState.Wave:
                {
                    var fill = Ui.Bar(card, pc, 12);
                    var info = Ui.Txt(card, "", 18, Theme.Dim);
                    UiTicker.Attach(info, () =>
                    {
                        int total = Mathf.Max(1, G.CurrentWave.Count);
                        Ui.SetBar(fill, 1f - G.RemainingEnemies / (float)total);
                        info.text = $"남은 벌레 {G.RemainingEnemies} / {total}   ·   경로 {G.GroundPath.Count}칸";
                    });
                    break;
                }
                case GameState.GameOver:
                    Ui.Txt(card, $"웨이브 {G.Wave}에서 디저트를 모두 빼앗겼어요.", 18, Theme.Dim);
                    break;
                default:
                    Ui.Txt(card, "축하해요! 오늘 영업을 무사히 마쳤어요!", 18, Theme.Gold);
                    break;
            }
        }

        static string WaveLabel(WaveInfo w)
        {
            Color c = w.Boss ? Theme.Red : w.Flying ? Theme.Purple : w.Fast ? Theme.Orange : Theme.Text;
            return Theme.Paint(w.Label, c);
        }

        // ------------------------------------------------------------------ 품질 카드

        void QualityCard()
        {
            var card = Ui.Card(body).transform;
            var head = Ui.Row(card, 10);
            Ui.Txt(head, "바리스타 실력", 21, Theme.Text, true).horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Spacer(head);
            Ui.Tag(head, $"Lv {G.BaristaLevel} / {MenuDB.MaxBaristaLevel}", Theme.Inset, Theme.Gold, 16);

            ChanceBar(card, G.BaristaLevel, 18);
            Ui.Txt(card, ChanceLegend(G.BaristaLevel), 16, Theme.Dim);

            // 레벨업 버튼은 패널 아래쪽 고정 영역(확대 버튼 줄 위)에 있음
            if (G.BaristaLevel < MenuDB.MaxBaristaLevel)
                Ui.Txt(card, "다음 레벨  " + ChanceLegend(G.BaristaLevel + 1), 15, Theme.Hint);
        }

        static void ChanceBar(Transform parent, int level, float height)
        {
            var row = MenuDB.Chances(level);
            var bg = Ui.Img(parent, Theme.Inset, Ui.Pill, "ChanceBar");
            Ui.LE(bg, prefH: height, minH: height);
            bg.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var h = Ui.H(bg.gameObject, Ui.Pad(0), 2, TextAnchor.MiddleLeft, false);
            h.childForceExpandHeight = true;
            for (int q = 0; q < row.Length; q++)
            {
                if (row[q] <= 0) continue;
                var seg = Ui.Img(bg.transform, Theme.GradeColor((Grade)q), null, "Seg");
                seg.raycastTarget = false;
                Ui.LE(seg, prefW: 0, flexW: row[q]);
            }
        }

        static string ChanceLegend(int level)
        {
            var row = MenuDB.Chances(level);
            var parts = new List<string>();
            for (int q = 0; q < row.Length; q++)
                if (row[q] > 0) parts.Add(Theme.Paint("●", Theme.GradeColor((Grade)q)) + $" {((Grade)q).Label()} {row[q]}%");
            return string.Join("   ", parts);
        }

        // =================================================================== 보드 오버레이

        void BuildToast(Transform area)
        {
            var bg = Ui.Img(area, new Color(0.04f, 0.06f, 0.09f, 0.88f), Ui.Pill, "Toast");
            bg.raycastTarget = false;
            toastRT = bg.rectTransform;
            toastRT.anchorMin = toastRT.anchorMax = new Vector2(0.5f, 1);
            toastRT.pivot = new Vector2(0.5f, 1);
            Ui.H(bg.gameObject, Ui.Pad(22, 28, 12, 13), 12, TextAnchor.MiddleCenter);
            var fit = bg.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Ui.Icon(bg.transform, Ui.Cup, Theme.Gold, 18);
            toastText = Ui.Txt(bg.transform, "", 23, MenuInfo.Argb(0xFFFFF0A0), true, TextAnchor.MiddleCenter);
            toastText.horizontalOverflow = HorizontalWrapMode.Overflow;
            toastGroup = bg.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            toastGroup.alpha = 0f;
        }

        void BuildBanner(Transform area)
        {
            var band = Ui.Img(area, new Color(0f, 0f, 0f, 0.6f), null, "Banner");
            band.raycastTarget = false;
            var rt = band.rectTransform;
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.sizeDelta = new Vector2(0, 170);
            rt.anchoredPosition = new Vector2(0, 60);
            bannerTitle = Ui.Txt(band.transform, "", 66, Theme.Text, true, TextAnchor.MiddleCenter);
            bannerTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Center(bannerTitle.rectTransform, new Vector2(1200, 90), new Vector2(0, 22));
            Ui.Shadowed(bannerTitle, 0.6f, 3f);
            bannerSub = Ui.Txt(band.transform, "", 25, Theme.Dim, false, TextAnchor.MiddleCenter);
            bannerSub.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Center(bannerSub.rectTransform, new Vector2(1200, 40), new Vector2(0, -42));
            bannerGroup = band.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            bannerGroup.alpha = 0f;
        }

        void BuildPausePill(Transform area)
        {
            var t = Ui.Tag(area, "일시정지  ·  Space로 재개", Theme.BtnRed, Color.white, 20);
            var rt = (RectTransform)t.transform.parent;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(24, -24);
            var fit = rt.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            pausePill = rt.gameObject;
            pausePill.SetActive(false);
        }

        void BuildGameOver(Transform area)
        {
            var dim = Ui.Img(area, new Color(0f, 0f, 0f, 0.65f), null, "GameOver");
            Ui.Stretch(dim.rectTransform);
            overGroup = dim.gameObject.AddComponent<CanvasGroup>();

            var card = Ui.Img(dim.transform, Theme.Panel, Ui.Round, "Card");
            overCard = card.rectTransform;
            Ui.Center(overCard, new Vector2(620, 0));
            Ui.V(card.gameObject, Ui.Pad(40, 40, 36, 36), 14, TextAnchor.UpperCenter);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            overTitle = Ui.Txt(card.transform, "", 70, Theme.Text, true, TextAnchor.MiddleCenter);
            Ui.Shadowed(overTitle, 0.6f, 3f);
            overSub = Ui.Txt(card.transform, "", 23, Theme.Dim, false, TextAnchor.MiddleCenter);
            Ui.Gap(card.transform, 4);
            overStats = Ui.Column(card.transform, 8);
            Ui.Gap(card.transform, 8);
            var overButtons = Ui.Row(card.transform, 10, 68, true);
            Ui.Button(overButtons, "다시 영업하기", Theme.BtnGreen, StartNewGame, null, 24, 68);
            Ui.Button(overButtons, "기록 보기", Theme.BtnGold, OpenTitle, null, 24, 68);
            dim.gameObject.SetActive(false);
        }

        void BuildRecipes(Transform area)
        {
            var root = Ui.RT("Recipes", area);
            Ui.Stretch(root);
            recipeGroup = root.gameObject.AddComponent<CanvasGroup>();

            // 배경을 누르면 닫힘 (창은 형제 오브젝트라 창 안을 눌러도 닫히지 않음)
            var backdrop = Ui.Img(root, new Color(0f, 0f, 0f, 0.55f), null, "Backdrop");
            Ui.Stretch(backdrop.rectTransform);
            var close = backdrop.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(() => ShowRecipes = false);

            var card = Ui.Img(root, Theme.Panel, Ui.Round, "Window");
            recipeCard = card.rectTransform;
            Ui.Stretch(recipeCard, 28, 28, 28, 28);
            Ui.V(card.gameObject, Ui.Pad(28, 28, 24, 20), 12);

            var head = Ui.Row(card.transform, 12);
            Ui.Icon(head, Ui.Cake, Theme.Purple, 34);
            var title = Ui.Txt(head, $"레시피  ·  시그니처 {MenuDB.Recipes.Count} · 세트 {MenuDB.SetRecipes.Count} · 코스 {MenuDB.CourseRecipes.Count}", 31, Theme.Text, true);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Spacer(head);
            Ui.Tag(head, "● 매장에 있는 재료", MenuInfo.Argb(0xFF1C3A2A), Theme.Green, 16);
            var cb = Ui.Button(head, "닫기", Theme.BtnNeutral, () => ShowRecipes = false, null, 20, 48);
            Ui.LE(cb, prefW: 120, minW: 120);

            Ui.Txt(card.transform, "재료 음료가 매장에 모두 있고 그중 하나가 이번 라운드 음료면, 선택 단계에서 그 음료를 눌러 시그니처 메뉴를 만들 수 있어요. 이미 남긴 재료 음료는 빈 컵이 돼요. 세트 메뉴는 시그니처 디저트가 매장에 있을 때 짝이 되는 음료를 눌러 만들어요.", 18, Theme.Dim);

            recipeScroll = Ui.Scroll(card.transform, out recipeContent, Ui.Pad(0, 6, 4, 4), 10);
            Ui.LE(recipeScroll, flexH: 1);
            root.gameObject.SetActive(false);
        }

        void RebuildRecipes()
        {
            recipeVersion = G.Version;
            Ui.Clear(recipeContent);
            var p = recipeContent;

            for (int tier = 0; tier <= 2; tier++)
            {
                var list = MenuDB.Recipes.Where(r => r.Tier == tier).ToList();
                if (list.Count == 0) continue;
                string tierDesc = tier == 0 ? "진한 음료가 핵심 재료" : tier == 1 ? "특제 음료가 핵심 재료" : "명품 음료가 핵심 재료";
                if (tier > 0) Ui.Gap(p, 6);
                var th = Ui.Row(p, 10);
                Ui.Tag(th, MenuDB.TierNames[tier], Theme.Tier(tier), Theme.Bg, 18);
                Ui.Txt(th, tierDesc, 18, Theme.Dim).horizontalOverflow = HorizontalWrapMode.Overflow;

                foreach (var r in list)
                    RecipeCard(p, r.Result, r.Parts.Select(part =>
                        (part.ToString(), Ui.Cup, (Color)part.Flavor.Color(), G.HasDrinkInCafe(part))).ToArray());
            }

            // 세트 메뉴
            Ui.Gap(p, 6);
            var sh = Ui.Row(p, 10);
            Ui.Tag(sh, MenuDB.TierNames[MenuDB.SetTier], Theme.Tier(MenuDB.SetTier), Theme.Bg, 18);
            Ui.Txt(sh, "매장의 시그니처 디저트 + 이번 라운드 음료 1잔 · 디저트 자리가 세트로 바뀌고 강화 단계는 유지",
                18, Theme.Dim).horizontalOverflow = HorizontalWrapMode.Overflow;
            foreach (var sr in MenuDB.SetRecipes)
            {
                var dessertColor = (Color)MenuDB.SignatureStats(sr.Dessert).Color;
                RecipeCard(p, sr.Result, new[]
                {
                    (sr.Dessert.Label(), Ui.Cake, dessertColor, G.HasSignatureInCafe(sr.Dessert)),
                    (sr.Drink.ToString(), Ui.Cup, (Color)sr.Drink.Flavor.Color(), G.HasDrinkInCafe(sr.Drink)),
                });
            }

            // 코스 메뉴
            Ui.Gap(p, 6);
            var ch = Ui.Row(p, 10);
            Ui.Tag(ch, MenuDB.TierNames[MenuDB.CourseTier], Theme.Tier(MenuDB.CourseTier), Theme.Bg, 18);
            Ui.Txt(ch, "매장에 남긴 시그니처 메뉴 2~3개 · 준비 단계에 메뉴를 눌러 합치고, 강화 단계는 가장 높은 단계를 이어받음",
                18, Theme.Dim).horizontalOverflow = HorizontalWrapMode.Overflow;
            foreach (var cr in MenuDB.CourseRecipes)
                RecipeCard(p, cr.Result, cr.Parts.Select(part =>
                    (part.Label(), Ui.Cake, (Color)MenuDB.SignatureStats(part).Color, G.HasSignatureInCafe(part))).ToArray());
        }

        /// <summary>레시피 카드 한 장. parts = (재료 이름, 아이콘, 색, 지금 매장에 있는지)</summary>
        void RecipeCard(Transform parent, SignatureMenu result, (string label, Sprite icon, Color color, bool owned)[] parts)
        {
            var s = MenuDB.SignatureStats(result);
            Color sc = s.Color;
            var owned = parts.Select(x => x.owned).ToArray();
            bool ready = owned.All(o => o);

            var card = Ui.Img(parent, ready ? Theme.CardHi : Theme.Card, Ui.Round, "Recipe");
            var h = Ui.H(card.gameObject, Ui.Pad(12, 18, 14, 14), 16, TextAnchor.UpperLeft);
            h.childForceExpandHeight = true;
            var accent = Ui.Img(card.transform, sc, Ui.Pill, "Accent");
            accent.raycastTarget = false;
            Ui.LE(accent, prefW: 6, minW: 6);

            var col = Ui.Column(card.transform, 8);
            Ui.LE(col, flexW: 1);

            var nameRow = Ui.Row(col, 10);
            Ui.Icon(nameRow, Ui.Circle, sc, 22);
            Ui.Txt(nameRow, result.Label(), 24, sc, true).horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Spacer(nameRow);
            if (ready) Ui.Tag(nameRow, "재료 준비 완료", Theme.Green, Theme.Bg, 16);

            var chips = Ui.Row(col, 8);
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var chip = Ui.Img(chips, owned[i] ? MenuInfo.Argb(0xFF1C3A2A) : Theme.Inset, Ui.Pill, "Part");
                chip.raycastTarget = false;
                Ui.H(chip.gameObject, Ui.Pad(10, 14, 6, 7), 6, TextAnchor.MiddleLeft);
                Ui.Icon(chip.transform, part.icon, Theme.Alpha(part.color, owned[i] ? 1f : 0.45f), 18);
                var pt = Ui.Txt(chip.transform, part.label, 18, owned[i] ? Theme.Green : Theme.Dim, owned[i]);
                pt.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            Ui.Txt(col, s.Desc, 18, Theme.Text);
            Ui.Txt(col, $"공격력 {MenuInfo.Num(s.Damage)}  ·  사거리 {s.Range:0.0}  ·  간격 {s.Cooldown:0.00}초", 16, Theme.Hint);
        }

        // =================================================================== 이벤트 연출

        void OnKilled(Pest e, int gold)
        {
            SpawnFloater(CafeView.World(e.X, e.Y, e.Flying ? 1.6f : 1f), $"+{gold}", Theme.Gold, e.Boss ? 40 : 26);
        }

        void OnLeaked(Pest e)
        {
            vignetteT = 1f;
            livesFlash = 1f;
            SpawnFloater(CafeView.World(e.X, e.Y, 1f), $"-{e.LeakDamage} 디저트", Theme.Red, 30);
        }

        void SpawnFloater(Vector3 world, string text, Color color, int size)
        {
            var f = floaterPool.Count > 0 ? floaterPool.Pop() : null;
            if (f == null)
            {
                var t = Ui.Txt(floaterRoot, "", size, color, true, TextAnchor.MiddleCenter);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.rectTransform.sizeDelta = new Vector2(240, 60);
                var o = t.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.7f);
                o.effectDistance = new Vector2(1.5f, -1.5f);
                f = new Floater { Text = t };
            }
            f.Text.gameObject.SetActive(true);
            f.Text.text = text;
            f.Text.fontSize = size;
            f.Text.color = color;
            f.World = world;
            f.Age = 0f;
            f.Life = 0.9f;
            floaters.Add(f);
        }

        // =================================================================== 매 프레임

        public void Refresh(Camera cam)
        {
            float dt = Time.unscaledDeltaTime;
            UpdateHeader(dt);

            string sig = $"{G.Version}|{G.Selected}|{G.MovingFrom}|{G.State}";
            if (sig != bodySig)
            {
                bool selChanged = G.Selected != lastSelected;
                lastSelected = G.Selected;
                bodySig = sig;
                RebuildBody();
                if (selChanged && G.Selected.HasValue)
                {
                    bodyScroll.verticalNormalizedPosition = 1f;
                    // 음료·빈 컵을 고르면 그 정보와 행동 버튼은 패널에만 있으므로 패널을 다시 엶
                    PanelVisible = true;
                }
            }

            UpdatePanelSlide(dt);

            UpdateBanner(dt);
            UpdateToast(dt);
            bool paused = app.Speed == 0 && !G.IsOver;
            if (pausePill.activeSelf != paused) pausePill.SetActive(paused);
            UpdateGameOver(dt);
            UpdateRecipes(dt);
            UpdateRanking(dt);
            UpdateConfirm(dt);
            UpdateFloaters(cam, dt);
            UpdateWaypointLabels(cam);

            vignetteT = Mathf.Max(0f, vignetteT - dt * 1.6f);
            vignette.color = Theme.Alpha(Theme.Red, vignetteT * 0.75f);
        }

        /// <summary>패널 ↔ 간략 버튼 열 전환 애니메이션. 게임판 영역(boardArea)도 함께 넓어지거나 좁아짐.</summary>
        void UpdatePanelSlide(float dt)
        {
            panelT = Mathf.MoveTowards(panelT, PanelVisible ? 1f : 0f, dt / 0.22f);
            float e = Mathf.SmoothStep(0f, 1f, panelT);

            panelGroup.anchoredPosition = new Vector2((1f - e) * (PanelWidth + 60f), 0f);
            panelCG.alpha = Mathf.Clamp01(e * 1.5f);
            panelCG.blocksRaycasts = panelCG.interactable = e > 0.5f;

            compactRT.anchoredPosition = new Vector2(-8f + e * (CompactWidth + 40f), 0f);
            compactCG.alpha = Mathf.Clamp01((1f - e) * 1.5f);
            compactCG.blocksRaycasts = compactCG.interactable = e < 0.5f;

            boardArea.offsetMax = new Vector2(-RightInsetUnits, 0f);
        }

        void UpdateHeader(float dt)
        {
            if (G.Coins > lastGold) goldPunch = 1f;
            if (G.Desserts < lastLives) livesFlash = 1f;
            lastGold = G.Coins;
            lastLives = G.Desserts;
            goldPunch = Mathf.Max(0f, goldPunch - dt * 5f);
            livesFlash = Mathf.Max(0f, livesFlash - dt * 2f);

            waveValue.text = $"{G.Wave}";
            livesValue.text = G.Desserts.ToString();
            goldValue.text = $"{G.Coins:N0}";
            goldValue.rectTransform.localScale = Vector3.one * (1f + goldPunch * 0.18f);
            goldValue.color = Color.Lerp(Theme.Text, Theme.Gold, goldPunch);
            livesValue.color = Color.Lerp(G.Desserts <= 5 ? Theme.Red : Theme.Text, Theme.Red, livesFlash);
            livesChip.color = Color.Lerp(Theme.Card, MenuInfo.Argb(0xFF5A1E24), livesFlash);
        }

        void UpdateBanner(float dt)
        {
            if (G.State != lastState)
            {
                if (G.State == GameState.Wave)
                {
                    var w = G.CurrentWave;
                    ShowBanner(G.Wave >= Cafe.MaxWave ? "최종 보스 등장!" : w.Boss ? "생쥐 대장 등장!" : $"웨이브 {G.Wave}", w.Boss ? Theme.Red : Theme.Text,
                        $"{WaveLabel(w)}  ·  {w.Count}마리" + (w.Flying ? "  ·  동선을 무시하고 날아와요" : ""));
                }
                else if (lastState == GameState.Wave && G.State == GameState.BuildPlace)
                {
                    ShowBanner("웨이브 클리어!", Theme.Green, $"보너스 +{10 + (G.Wave - 1) * 2}코인  ·  음료 5잔을 놓으세요");
                }
                lastState = G.State;
            }

            bannerAge += dt;
            const float life = 2.2f;
            float a = bannerAge < 0.2f ? bannerAge / 0.2f : bannerAge > life - 0.45f ? (life - bannerAge) / 0.45f : 1f;
            bannerGroup.alpha = Mathf.Clamp01(a);
            float s = 1f + (1f - Ui.EaseOut(bannerAge / 0.35f)) * 0.25f;
            bannerTitle.rectTransform.localScale = new Vector3(s, s, 1f);
        }

        void ShowBanner(string title, Color color, string sub)
        {
            bannerTitle.text = title;
            bannerTitle.color = color;
            bannerSub.text = sub;
            bannerAge = 0f;
        }

        void UpdateToast(float dt)
        {
            bool has = G.MessageTimer > 0f && !string.IsNullOrEmpty(G.Message);
            if (has && G.Message != lastMessage)
            {
                lastMessage = G.Message;
                toastText.text = G.Message;
                toastAge = 0f;
            }
            if (!has) lastMessage = null;
            toastAge += dt;
            float e = Ui.EaseOut(toastAge / 0.3f);
            toastRT.anchoredPosition = new Vector2(0f, Mathf.Lerp(20f, -24f, e));
            float alpha = has ? Mathf.Min(e, Mathf.Clamp01(G.MessageTimer / 0.5f)) : 0f;
            if (bannerGroup.alpha > 0.05f) alpha = 0f; // 배너와 겹치지 않게
            toastGroup.alpha = alpha;
        }

        void UpdateGameOver(float dt)
        {
            var go = overGroup.gameObject;
            if (go.activeSelf != G.IsOver)
            {
                go.SetActive(G.IsOver);
                if (G.IsOver)
                {
                    overAge = 0f;
                    bool win = G.State == GameState.Victory;
                    overTitle.text = win ? "영업 성공!" : "오늘은 폐점";
                    overTitle.color = win ? Theme.Gold : Theme.Text;
                    overSub.text = win ? "모든 웨이브를 막아냈어요" : "디저트를 모두 빼앗겼어요";
                    Ui.Clear(overStats);
                    Ui.KeyValue(overStats, "도달 웨이브", $"{G.Wave}", Theme.Text, 22);
                    Ui.KeyValue(overStats, "남은 디저트", $"{G.Desserts}", G.Desserts > 0 ? Theme.Green : Theme.Red, 22);
                    Ui.KeyValue(overStats, "보유 코인", $"{G.Coins:N0}", Theme.Gold, 22);
                    Ui.KeyValue(overStats, "보스 피해", RunRecords.FormatNumber(G.BossDamage), Theme.Red, 22);
                    Ui.KeyValue(overStats, "보스 시간", RunRecords.FormatTime(G.BossStageTime), Theme.Text, 22);
                    Ui.KeyValue(overStats, "총 시간", RunRecords.FormatTime(G.TotalTime), Theme.Text, 22);
                    Ui.KeyValue(overStats, "이번 순위", app.LastRank > 0 ? $"{app.LastRank}위" : "순위 밖", app.LastRank > 0 ? Theme.Gold : Theme.Dim, 22);
                }
            }
            if (!G.IsOver) return;
            overAge += dt;
            overGroup.alpha = Ui.EaseOut(overAge / 0.3f);
            float s = Mathf.Lerp(0.85f, 1f, Ui.EaseOutBack(overAge / 0.4f));
            overCard.localScale = new Vector3(s, s, 1f);
        }

        void UpdateRecipes(float dt)
        {
            var go = recipeGroup.gameObject;
            if (go.activeSelf != ShowRecipes)
            {
                go.SetActive(ShowRecipes);
                if (ShowRecipes)
                {
                    recipeAge = 0f;
                    RebuildRecipes();
                    recipeScroll.verticalNormalizedPosition = 1f;
                }
            }
            if (!ShowRecipes) return;
            if (recipeVersion != G.Version) RebuildRecipes();
            recipeAge += dt;
            recipeGroup.alpha = Ui.EaseOut(recipeAge / 0.18f);
            float s = Mathf.Lerp(0.96f, 1f, Ui.EaseOut(recipeAge / 0.22f));
            recipeCard.localScale = new Vector3(s, s, 1f);
        }

        void UpdateFloaters(Camera cam, float dt)
        {
            float sf = fxCanvas.scaleFactor;
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                f.Age += dt;
                if (f.Age >= f.Life)
                {
                    f.Text.gameObject.SetActive(false);
                    floaterPool.Push(f);
                    floaters.RemoveAt(i);
                    continue;
                }
                float t = f.Age / f.Life;
                var p = cam.WorldToScreenPoint(f.World);
                p.y += Ui.EaseOut(t) * 70f * sf;
                p.z = 0f;
                f.Text.rectTransform.position = p;
                var c = f.Text.color;
                c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                f.Text.color = c;
                float s = t < 0.15f ? Mathf.Lerp(0.6f, 1.15f, t / 0.15f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - 0.15f) / 0.2f));
                f.Text.rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }

        void UpdateWaypointLabels(Camera cam)
        {
            var wps = Cafe.Waypoints;
            var a = cam.WorldToScreenPoint(CafeView.World(9, 9, 0));
            var b = cam.WorldToScreenPoint(CafeView.World(10, 9, 0));
            float cellUnits = Vector2.Distance(a, b) / fxCanvas.scaleFactor;
            for (int i = 0; i < wps.Length; i++)
            {
                var t = wpLabels[i];
                var p = cam.WorldToScreenPoint(CafeView.World(wps[i].X, wps[i].Y, 0.08f));
                p.z = 0f;
                t.rectTransform.position = p;
                t.fontSize = Mathf.Max(8, Mathf.RoundToInt(cellUnits * (t.text.Length > 1 ? 0.32f : 0.5f)));
            }
        }
    }
}

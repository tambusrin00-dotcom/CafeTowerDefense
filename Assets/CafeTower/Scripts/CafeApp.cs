using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.UI;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CafeTowerDefense
{
    /// <summary>
    /// 게임 진입점. 씬에 아무것도 배치할 필요 없이, 플레이하면 자동으로 생성되어
    /// 카메라·조명·후처리·UI·보드를 모두 코드로 구성함.
    /// </summary>
    public class CafeApp : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<CafeApp>() != null) return;
            new GameObject("CafeTowerDefense").AddComponent<CafeApp>();
        }

        const float CameraPitch = 58f;

        public Cafe Cafe { get; private set; }
        /// <summary>게임 배속. 0 = 일시정지</summary>
        public int Speed { get; private set; } = 1;
        int resumeSpeed = 1;

        /// <summary>배속 버튼과 숫자키 1~3에 대응하는 배속</summary>
        public static readonly int[] SpeedLevels = { 1, 3, 6 };

        public RunRecords Records { get; private set; }
        public CafeMusic Music { get; private set; }
        public SaveStore Saves { get; private set; }
        /// <summary>이어할 수 있는 저장된 영업 (없으면 null)</summary>
        public CafeSaveData SavedGame { get; private set; }
        /// <summary>방금 끝난 판의 순위 (10위 밖이면 0)</summary>
        public int LastRank { get; private set; }
        bool wasOver;

        Camera cam;
        CafeView view;
        CafeUI ui;

        Cell? flashCell;
        float flashTime;
        Cell? hoverCell;

        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        PointerEventData pointerData;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.orientation = ScreenOrientation.AutoRotation;

            EnhancedTouchSupport.Enable(); // 두 손가락 확대/축소용 멀티터치

            Cafe = new Cafe();
            Records = new RunRecords(System.IO.Path.Combine(Application.persistentDataPath, "records.json"));
            Saves = new SaveStore(System.IO.Path.Combine(Application.persistentDataPath, "save.json"));
            SavedGame = Saves.Load();
            SetupCamera();
            SetupLight();
            SetupPostProcessing();
            SetupEventSystem();
            view = new CafeView(Cafe, transform);
            Music = new CafeMusic(gameObject);
            ui = new CafeUI(this, transform);
        }

        public void SetSpeed(int s)
        {
            s = Mathf.Clamp(s, 0, SpeedLevels[SpeedLevels.Length - 1]);
            if (s == 0 && Speed != 0) resumeSpeed = Speed;
            Speed = s;
        }

        public void TogglePause() => SetSpeed(Speed == 0 ? resumeSpeed : 0);

        public void ClearLastRank() => LastRank = 0;

        // ------------------------------------------------------------------ 이어하기

        int savedVersion = -1;
        float nextSaveAt;

        /// <summary>판이 바뀔 때마다 (최대 1초에 한 번) 저장. 영업이 끝나면 저장을 지움. 시작 화면에서는 저장하지 않음.</summary>
        void AutoSave(bool force)
        {
            if (ui == null || ui.ShowTitle) return;
            if (Cafe.IsOver)
            {
                if (SavedGame != null)
                {
                    Saves.Delete();
                    SavedGame = null;
                }
                return;
            }
            if (!force && (Cafe.Version == savedVersion || Time.unscaledTime < nextSaveAt)) return;
            savedVersion = Cafe.Version;
            nextSaveAt = Time.unscaledTime + 1f;
            var d = Cafe.SaveData;
            if (d == null) return;
            Saves.Write(d);
            SavedGame = d;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) AutoSave(true);
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus) AutoSave(true);
        }

        void OnApplicationQuit() => AutoSave(true);

        /// <summary>저장된 영업을 불러옴. 실패하면 저장을 지우고 false.</summary>
        public bool ContinueSaved()
        {
            if (SavedGame == null) return false;
            if (Cafe.Restore(SavedGame))
            {
                savedVersion = -1;
                return true;
            }
            Saves.Delete();
            SavedGame = null;
            return false;
        }

        /// <summary>지금 판의 기록 (진행 중이면 지금까지의 값)</summary>
        public RunRecord CurrentRun() => new RunRecord
        {
            BossDamage = Cafe.BossDamage,
            BossTime = Cafe.BossStageTime,
            TotalTime = Cafe.TotalTime,
            Desserts = Cafe.Desserts,
            Coins = Cafe.Coins,
            Wave = Cafe.Wave,
            Victory = Cafe.State == GameState.Victory,
        };

        /// <summary>게임이 끝나는 순간 한 번만 기록을 저장. 중간에 새 영업을 누른 판은 저장하지 않음.</summary>
        void SaveRecordIfEnded()
        {
            if (Cafe.IsOver && !wasOver)
            {
                LastRank = Records.Add(CurrentRun());
            }
            wasOver = Cafe.IsOver;
        }

        // =================================================================== 초기 구성

        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.Bg;
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        static void SetupLight()
        {
            foreach (var l in FindObjectsByType<Light>())
            {
                if (l.type != LightType.Directional) continue;
                l.shadows = LightShadows.Soft;
                l.color = new Color(1f, 0.93f, 0.82f); // 따뜻한 카페 조명
                return;
            }
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.color = new Color(1f, 0.93f, 0.82f);
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        /// <summary>음료·투사체가 빛나 보이도록 블룸, 가장자리 비네트, 톤매핑</summary>
        static void SetupPostProcessing()
        {
            var go = new GameObject("Cafe Volume");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = profile;

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.65f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(8f);
            color.saturation.Override(12f);
        }

        static void SetupEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        // =================================================================== 루프

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            Music.Update();
            float gameDt = ui.ShowTitle ? 0f : dt * Speed;
            Cafe.Update(gameDt);
            SaveRecordIfEnded();
            AutoSave(false);
            Cafe.TickUi(dt);
            if (flashTime > 0f) flashTime -= dt;

            UpdateCameraRect();
            HandleInput(dt);
            ClampPan();
            ApplyCamera();
            view.Sync(cam, flashCell, flashTime, hoverCell, gameDt); // 연출도 배속·일시정지를 따름
            ui.Refresh(cam);
        }

        // =================================================================== 카메라 (확대/축소·이동)
        //
        // 직교 카메라라서 '배율(orthoSize)'과 '보는 중심(camTarget)' 두 값만 바꾸면 확대·이동이 되고
        // 게임판을 그리는 코드는 손대지 않아도 됨. (원래 설계의 cs = 칸 크기, ox/oy = 원점에 해당)
        //   칸 하나의 화면 크기(px) = cam.pixelHeight / (2 * orthoSize)
        //   배율 범위: 최대 축소 = 게임판 전체가 보이는 크기, 기본 = 34dp, 최대 확대 = 72dp
        //   (PC처럼 DPI가 낮아 72dp가 전체 보기보다 작으면 최대 확대를 전체 보기의 2.5배로 보정)

        const float CamDistance = 30f;
        const float BoardMargin = 0.9f;   // 게임판 가장자리 바깥으로 더 보여줄 여유(칸)
        const float DefaultCellDp = 34f;
        const float MaxCellDp = 72f;

        float orthoSize = 10f;
        Vector2 camTarget;                // 보는 중심 (월드 x, z)
        bool camReady;
        bool atFit = true;                // '전체 보기' 상태면 화면 크기가 바뀌어도 계속 전체를 보여줌

        static float PitchSin => Mathf.Sin(CameraPitch * Mathf.Deg2Rad);
        static float PitchCos => Mathf.Cos(CameraPitch * Mathf.Deg2Rad);
        static Vector2 BoardCenter => new Vector2((Cafe.W - 1) / 2f, (Cafe.H - 1) / 2f);
        static float BoardHalfW => Cafe.W / 2f + BoardMargin;
        static float BoardHalfD => Cafe.H / 2f + BoardMargin;

        static float DpToPx(float dp) => dp * (Screen.dpi > 0f ? Screen.dpi : 96f) / 160f;
        float SizeForCellPx(float px) => cam.pixelHeight / (2f * Mathf.Max(1f, px));
        float CellPx(float size) => cam.pixelHeight / (2f * size);

        /// <summary>게임판 전체가 보이는 배율 = 가장 많이 축소한 상태</summary>
        float FitSize => Mathf.Max(BoardHalfD * PitchSin + 0.8f * PitchCos, BoardHalfW / cam.aspect);
        float MaxSize => FitSize;
        float MinSize => Mathf.Min(FitSize, SizeForCellPx(Mathf.Max(DpToPx(MaxCellDp), CellPx(FitSize) * 2.5f)));
        float DefaultSize => Mathf.Clamp(SizeForCellPx(DpToPx(DefaultCellDp)), MinSize, MaxSize);

        public bool AtFit => atFit;
        public bool CanZoomIn => orthoSize > MinSize + 0.01f;
        public bool CanZoomOut => orthoSize < MaxSize - 0.01f;

        public void ZoomIn() => ZoomAt(cam.pixelRect.center, orthoSize / 1.3f);
        public void ZoomOut() => ZoomAt(cam.pixelRect.center, orthoSize * 1.3f);

        public void ZoomFit()
        {
            orthoSize = MaxSize;
            camTarget = BoardCenter;
            atFit = true;
            ClampPan();
            ApplyCamera();
        }

        /// <summary>패널을 뺀 영역만 카메라가 그림. 패널 토글 애니메이션 중에도 매 프레임 따라감.</summary>
        void UpdateCameraRect()
        {
            float wFrac = Mathf.Clamp(1f - ui.RightInsetPx / Mathf.Max(1, Screen.width), 0.3f, 1f);
            cam.rect = new Rect(0f, 0f, wFrac, 1f);
            if (!camReady)
            {
                camReady = true;
                orthoSize = DefaultSize;
                atFit = orthoSize >= MaxSize - 0.001f;
                camTarget = BoardCenter;
            }
        }

        /// <summary>
        /// 배율·이동 범위 제한. 화면이 게임판보다 넓은 방향은 가운데에 고정하고,
        /// 좁은 방향은 게임판 가장자리가 화면 안쪽으로 들어오지 않게 막음.
        /// 패널을 토글해 영역이 바뀌면 칸 크기는 그대로 두고 이 함수로 위치만 보정됨.
        /// </summary>
        void ClampPan()
        {
            if (atFit) orthoSize = MaxSize;
            orthoSize = Mathf.Clamp(orthoSize, MinSize, MaxSize);
            float visHalfW = orthoSize * cam.aspect;
            float visHalfD = orthoSize / PitchSin;
            var c = BoardCenter;
            camTarget.x = visHalfW >= BoardHalfW ? c.x : Mathf.Clamp(camTarget.x, c.x - BoardHalfW + visHalfW, c.x + BoardHalfW - visHalfW);
            camTarget.y = visHalfD >= BoardHalfD ? c.y : Mathf.Clamp(camTarget.y, c.y - BoardHalfD + visHalfD, c.y + BoardHalfD - visHalfD);
        }

        void ApplyCamera()
        {
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            var rot = Quaternion.Euler(CameraPitch, 0f, 0f);
            var target = new Vector3(camTarget.x, 0f, camTarget.y);
            cam.transform.SetPositionAndRotation(target - rot * Vector3.forward * CamDistance, rot);
        }

        bool ScreenToGround(Vector2 pos, out Vector2 ground)
        {
            var ray = cam.ScreenPointToRay(pos);
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter))
            {
                var p = ray.GetPoint(enter);
                ground = new Vector2(p.x, p.z);
                return true;
            }
            ground = default;
            return false;
        }

        /// <summary>화면의 한 점(손가락·마우스 위치) 아래 게임판 지점을 고정한 채 배율 변경</summary>
        void ZoomAt(Vector2 screenPos, float newSize)
        {
            bool ok = ScreenToGround(screenPos, out var before);
            orthoSize = Mathf.Clamp(newSize, MinSize, MaxSize);
            atFit = orthoSize >= MaxSize - 0.001f;
            ApplyCamera();
            if (ok && ScreenToGround(screenPos, out var after)) camTarget += before - after;
            ClampPan();
            ApplyCamera();
        }

        /// <summary>화면에서 from → to 로 끈 만큼 게임판을 따라 움직임</summary>
        void PanScreen(Vector2 from, Vector2 to)
        {
            float pxPerUnit = cam.pixelHeight / (2f * orthoSize);
            var d = to - from;
            camTarget.x -= d.x / pxPerUnit;
            camTarget.y -= d.y / (pxPerUnit * PitchSin);
            ClampPan();
            ApplyCamera();
        }

        // =================================================================== 입력

        float TapSlop => Mathf.Max(8f, DpToPx(10f));

        // 마우스
        bool mouseDown, mouseDownOverUI, mouseDragging;
        Vector2 mouseDownPos, mouseLastPos;

        // 터치
        int lastTouchCount;
        bool touchOverUI, touchGesture;
        Vector2 touchStartPos, lastMid, lastTapPos;
        float lastDist;
        readonly List<Vector2> touchPos = new List<Vector2>();

        void HandleInput(float dt)
        {
            if (ui.ShowTitle)
            {
                hoverCell = null;
                return;
            }
            if (ui.ShowConfirm)
            {
                hoverCell = null;
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ui.CloseConfirm();
                return;
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame) Deselect();
                if (kb.spaceKey.wasPressedThisFrame && !Cafe.IsOver) TogglePause();
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) SetSpeed(SpeedLevels[0]);
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) SetSpeed(SpeedLevels[1]);
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) SetSpeed(SpeedLevels[2]);
                if (kb.rKey.wasPressedThisFrame) ui.ShowRecipes = !ui.ShowRecipes;
                if (kb.tabKey.wasPressedThisFrame) ui.TogglePanel();
                if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) ZoomIn();
                if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) ZoomOut();
                if (kb.digit0Key.wasPressedThisFrame || kb.numpad0Key.wasPressedThisFrame) ZoomFit();
                // 방향키 이동
                var move = Vector2.zero;
                if (kb.leftArrowKey.isPressed) move.x -= 1f;
                if (kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.downArrowKey.isPressed) move.y -= 1f;
                if (move != Vector2.zero) PanScreen(Vector2.zero, -move * cam.pixelHeight * 0.8f * dt);
            }

            hoverCell = null;
            if (Touchscreen.current != null && (ETouch.activeTouches.Count > 0 || lastTouchCount > 0))
                HandleTouch();
            else
                HandleMouse();
        }

        /// <summary>마우스: 왼쪽 클릭 = 탭, 왼쪽/가운데 드래그 = 이동, 휠 = 커서 위치 기준 확대/축소, 오른쪽 = 선택 해제</summary>
        void HandleMouse()
        {
            var m = Mouse.current;
            if (m == null) return;
            var pos = m.position.ReadValue();
            bool overUI = IsOverUI(pos);

            if (m.rightButton.wasPressedThisFrame) Deselect();

            if (!overUI && !ui.ShowRecipes && !ui.ShowRanking)
            {
                float wheel = m.scroll.ReadValue().y;
                if (wheel != 0f)
                {
                    // 장치에 따라 한 칸이 120 또는 1로 들어옴
                    float steps = Mathf.Clamp(Mathf.Abs(wheel) > 10f ? wheel / 120f : wheel, -3f, 3f);
                    ZoomAt(pos, orthoSize * Mathf.Pow(0.85f, steps));
                }
            }

            if (m.leftButton.wasPressedThisFrame)
            {
                mouseDown = true;
                mouseDownOverUI = overUI;
                mouseDownPos = pos;
                mouseDragging = false;
            }
            if (mouseDown && m.leftButton.isPressed && !mouseDownOverUI)
            {
                if (!mouseDragging && (pos - mouseDownPos).magnitude > TapSlop) mouseDragging = true;
                if (mouseDragging) PanScreen(mouseLastPos, pos);
            }
            if (m.middleButton.isPressed && !m.middleButton.wasPressedThisFrame && !overUI) PanScreen(mouseLastPos, pos);

            if (m.leftButton.wasReleasedThisFrame && mouseDown)
            {
                mouseDown = false;
                if (!mouseDownOverUI && !mouseDragging) TapBoard(pos);
                mouseDragging = false;
            }
            mouseLastPos = pos;

            // 칸 미리보기 (드래그 중에는 숨김)
            if (!overUI && !ui.ShowRecipes && !mouseDragging) hoverCell = CellAt(pos);
        }

        /// <summary>
        /// 터치: 한 손가락 드래그 = 이동, 두 손가락 = 확대/축소 + 이동, 움직이지 않고 뗀 한 손가락 = 탭.
        /// 손가락 수가 바뀌는 순간에는 기준점만 다시 잡아서 판이 튀지 않게 함.
        /// UI 위에서 시작한 터치는 통째로 무시 (버튼 열 스크롤과 게임판 이동이 섞이지 않음).
        /// </summary>
        void HandleTouch()
        {
            touchPos.Clear();
            foreach (var t in ETouch.activeTouches)
            {
                var ph = t.phase;
                if (ph == UnityEngine.InputSystem.TouchPhase.Began || ph == UnityEngine.InputSystem.TouchPhase.Moved ||
                    ph == UnityEngine.InputSystem.TouchPhase.Stationary)
                    touchPos.Add(t.screenPosition);
                else lastTapPos = t.screenPosition; // 이번 프레임에 뗀 손가락 위치
            }
            int n = touchPos.Count;

            if (lastTouchCount == 0 && n > 0)
            {
                // 새 터치 묶음 시작
                touchOverUI = IsOverUI(touchPos[0]);
                touchGesture = false;
                touchStartPos = touchPos[0];
                ResetTouchRef();
            }
            else if (n > 0 && !touchOverUI)
            {
                if (n != lastTouchCount)
                {
                    ResetTouchRef(); // 손가락 수가 바뀜 → 기준만 다시 잡음
                }
                else
                {
                    var mid = Mid();
                    float dist = n >= 2 ? Vector2.Distance(touchPos[0], touchPos[1]) : 0f;
                    if (n == 1)
                    {
                        if (!touchGesture && (touchPos[0] - touchStartPos).magnitude > TapSlop) touchGesture = true;
                        if (touchGesture) PanScreen(lastMid, mid);
                    }
                    else
                    {
                        if (lastDist > 1f && dist > 1f) ZoomAt(mid, orthoSize * lastDist / dist);
                        PanScreen(lastMid, mid);
                    }
                    lastMid = mid;
                    lastDist = dist;
                }
                if (n >= 2) touchGesture = true;
            }

            if (n == 0 && lastTouchCount > 0 && !touchOverUI && !touchGesture) TapBoard(lastTapPos);
            lastTouchCount = n;
        }

        Vector2 Mid() => touchPos.Count >= 2 ? (touchPos[0] + touchPos[1]) * 0.5f : touchPos[0];

        void ResetTouchRef()
        {
            lastMid = Mid();
            lastDist = touchPos.Count >= 2 ? Vector2.Distance(touchPos[0], touchPos[1]) : 0f;
        }

        void Deselect()
        {
            ui.ShowRecipes = false;
            ui.ShowRanking = false;
            Cafe.CancelMove();
            Cafe.Selected = null;
        }

        bool IsOverUI(Vector2 pos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (pointerData == null) pointerData = new PointerEventData(es);
            pointerData.position = pos;
            uiHits.Clear();
            es.RaycastAll(pointerData, uiHits);
            return uiHits.Count > 0;
        }

        /// <summary>화면 좌표 → 보드 칸. 보드 밖이면 null.</summary>
        Cell? CellAt(Vector2 pos)
        {
            if (!cam.pixelRect.Contains(pos)) return null;
            // 음료 윗부분을 눌러도 그 칸이 잡히도록 약간 띄운 평면과 교차
            var ray = cam.ScreenPointToRay(pos);
            var plane = new Plane(Vector3.up, new Vector3(0f, 0.3f, 0f));
            if (!plane.Raycast(ray, out float enter)) return null;
            var w = ray.GetPoint(enter);
            var cell = new Cell(Mathf.RoundToInt(w.x), Cafe.H - 1 - Mathf.RoundToInt(w.z));
            return Cafe.InBounds(cell) ? cell : (Cell?)null;
        }

        void TapBoard(Vector2 pos)
        {
            if (ui.ShowRecipes)
            {
                ui.ShowRecipes = false;
                return;
            }
            var cell = CellAt(pos);
            if (!cell.HasValue)
            {
                Deselect();
                return;
            }
            if (Cafe.IsOver) return;
            if (!Cafe.TapCell(cell.Value))
            {
                flashCell = cell;
                flashTime = 0.6f;
            }
        }
    }
}

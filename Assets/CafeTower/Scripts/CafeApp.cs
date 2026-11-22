using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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

        Camera cam;
        CafeView view;
        CafeUI ui;

        Cell? flashCell;
        float flashTime;
        Cell? hoverCell;

        bool pressing;
        bool downOverUI;
        Vector2 downPos;
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        PointerEventData pointerData;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.orientation = ScreenOrientation.AutoRotation;

            Cafe = new Cafe();
            SetupCamera();
            SetupLight();
            SetupPostProcessing();
            SetupEventSystem();
            view = new CafeView(Cafe, transform);
            ui = new CafeUI(this, transform);
        }

        public void SetSpeed(int s)
        {
            s = Mathf.Clamp(s, 0, 3);
            if (s == 0 && Speed != 0) resumeSpeed = Speed;
            Speed = s;
        }

        public void TogglePause() => SetSpeed(Speed == 0 ? resumeSpeed : 0);

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
            Cafe.Update(dt * Speed);
            Cafe.TickUi(dt);
            if (flashTime > 0f) flashTime -= dt;

            HandleInput();
            FitCamera();
            view.Sync(cam, flashCell, flashTime, hoverCell, dt * Speed); // 연출도 배속·일시정지를 따름
            ui.Refresh(cam);
        }

        // =================================================================== 카메라

        /// <summary>오른쪽 패널을 뺀 영역에 보드 전체가 들어오도록 카메라 배치</summary>
        void FitCamera()
        {
            float panelPx = CafeUI.PanelWidth * ui.ScaleFactor;
            float wFrac = Mathf.Clamp(1f - panelPx / Mathf.Max(1, Screen.width), 0.3f, 1f);
            cam.rect = new Rect(0f, 0f, wFrac, 1f);

            const float halfV = 10.4f; // 보드를 기울여 봤을 때의 세로 절반 + 여유
            const float halfH = 10.6f;
            float t = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float dist = Mathf.Max(halfV / t, halfH / (t * cam.aspect));
            var target = new Vector3((Cafe.W - 1) / 2f, 0f, (Cafe.H - 1) / 2f - 0.3f);
            var rot = Quaternion.Euler(CameraPitch, 0f, 0f);
            cam.transform.SetPositionAndRotation(target - rot * Vector3.forward * dist, rot);
        }

        // =================================================================== 입력

        void HandleInput()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame) Deselect();
                if (kb.spaceKey.wasPressedThisFrame && !Cafe.IsOver) TogglePause();
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) SetSpeed(1);
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) SetSpeed(2);
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) SetSpeed(3);
                if (kb.rKey.wasPressedThisFrame) ui.ShowRecipes = !ui.ShowRecipes;
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) Deselect();

            var p = Pointer.current;
            hoverCell = null;
            if (p == null) return;
            var pos = p.position.ReadValue();

            // 마우스일 때만 칸 미리보기 (터치는 손을 떼면 의미 없음)
            if (p is Mouse && !ui.ShowRecipes && !IsOverUI(pos)) hoverCell = CellAt(pos);

            if (p.press.wasPressedThisFrame)
            {
                pressing = true;
                downPos = pos;
                downOverUI = IsOverUI(pos);
            }
            if (p.press.wasReleasedThisFrame && pressing)
            {
                pressing = false;
                float slop = Screen.height * 0.02f;
                if (!downOverUI && (pos - downPos).sqrMagnitude <= slop * slop) TapBoard(pos);
            }
        }

        void Deselect()
        {
            ui.ShowRecipes = false;
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

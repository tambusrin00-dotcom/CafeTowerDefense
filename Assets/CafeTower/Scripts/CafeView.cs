using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CafeTowerDefense
{
    /// <summary>
    /// 게임 상태를 3D 오브젝트로 그림. 로직(Cafe)은 건드리지 않고 매 프레임 읽기만 함.
    /// 게임 칸 (x, y) → 월드 (x, 높이, H-1-y). 화면 위쪽(y=0)이 월드 +Z(먼 쪽).
    /// </summary>
    public class CafeView
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        // 카페 바닥: 라떼/모카 체크 타일 + 원목 테두리
        static readonly Color TileA = MenuInfo.Argb(0xFFC2A07E);
        static readonly Color TileB = MenuInfo.Argb(0xFFA5825F);
        static readonly Color Grout = MenuInfo.Argb(0xFF7A5C44);
        static readonly Color WoodLight = MenuInfo.Argb(0xFF7A4E30);
        static readonly Color WoodDark = MenuInfo.Argb(0xFF4A2E1C);
        static readonly Color Cream = MenuInfo.Argb(0xFFF6EBDC);
        static readonly Color Kraft = MenuInfo.Argb(0xFFB98B5E);
        static readonly Color Gold = MenuInfo.Argb(0xFFF5BE6B);
        static readonly Color MoveColor = MenuInfo.Argb(0xFF7FE0D3);
        static readonly Color[] Sprinkles = { MenuInfo.Argb(0xFFFF8FB8), MenuInfo.Argb(0xFFFFE07A), MenuInfo.Argb(0xFF8EE3C8) };
        static readonly Color[] Crumbs = { MenuInfo.Argb(0xFFE8C99A), MenuInfo.Argb(0xFFB07A4C), MenuInfo.Argb(0xFFF6EBDC), MenuInfo.Argb(0xFF6B4329) };

        const float PathY = 0.03f;

        readonly Cafe game;
        readonly Transform root;
        readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();

        Shader litShader;
        Material lit, glossMat, unlit, unlitCircle;
        Texture2D circleTex;

        // 칸 오브젝트
        int lastVersion = -1;
        readonly GameObject[,] cupPiles = new GameObject[Cafe.W, Cafe.H];
        readonly Dictionary<MenuTower, MenuVis> menuVis = new Dictionary<MenuTower, MenuVis>();
        readonly HashSet<MenuTower> seenMenus = new HashSet<MenuTower>();
        readonly List<MenuTower> removeMenus = new List<MenuTower>();

        // 적
        readonly Dictionary<Pest, PestVis> pestVis = new Dictionary<Pest, PestVis>();
        readonly HashSet<Pest> seenPests = new HashSet<Pest>();
        readonly List<Pest> removePests = new List<Pest>();

        // 풀
        readonly Dictionary<Droplet, DropVis> dropVis = new Dictionary<Droplet, DropVis>();
        readonly Stack<DropVis> dropPool = new Stack<DropVis>();
        readonly HashSet<Droplet> seenDrops = new HashSet<Droplet>();
        readonly List<Droplet> removeDrops = new List<Droplet>();
        readonly List<LineRenderer[]> beamPool = new List<LineRenderer[]>();

        // 연출
        readonly List<Shard> shards = new List<Shard>();
        readonly Stack<Shard> shardPool = new Stack<Shard>();
        readonly List<Ring> rings = new List<Ring>();
        readonly Stack<Ring> ringPool = new Stack<Ring>();
        readonly List<Pop> pops = new List<Pop>();
        Renderer hoverQuad;
        LineRenderer hoverFrame;
        Cell? hoverCached;
        int hoverVersion = -1;
        bool hoverBuildable;

        class DropVis
        {
            public Renderer R;
            public TrailRenderer Trail;
        }

        class Shard
        {
            public Transform T;
            public Renderer R;
            public Vector3 Vel, Spin;
            public float Age, Life, Size;
        }

        class Ring
        {
            public LineRenderer L;
            public Vector3 Center;
            public float Age, Life, Radius;
            public Color Color;
        }

        struct Pop
        {
            public Transform T;
            public float Age;
        }
        readonly List<Transform> dotPool = new List<Transform>();

        // 경로
        LineRenderer pathLine;
        List<Cell> pathRef;
        readonly List<Vector3> pathPts = new List<Vector3>();
        readonly List<float> pathLen = new List<float>();

        // 선택 표시
        LineRenderer selFrame, rangeRing, moveFrame;
        Transform rangeDisc;
        Renderer moveQuad, flashQuad;

        class MenuVis
        {
            public GameObject Root;
            public Transform Item;
            public Transform[] Orbit;
            public Transform Outline;
            public Renderer OutlineR;
            public int Sig;
        }

        enum Bug { Ant, Roach, Fly, Mouse }

        class PestVis
        {
            public GameObject Root;
            public Bug Kind;
            public Transform Model, WingL, WingR, Stun, HpRoot, HpFill;
            public Renderer WingLR, WingRR, HpFillR;
            public readonly List<Renderer> Tinted = new List<Renderer>();
            public readonly List<Transform> Legs = new List<Transform>();
            public Color BaseColor;
            public float Radius, HpWidth, ModelY;
            public float Flash;
            public Vector3 LastPos;
            public bool HasLast;
        }

        public static Vector3 World(float x, float y, float h) => new Vector3(x, h, Cafe.H - 1 - y);

        public CafeView(Cafe game, Transform parent)
        {
            this.game = game;
            root = new GameObject("Board").transform;
            root.SetParent(parent, false);
            CreateMaterials();
            BuildBoard();
            BuildOverlays();

            game.Hurt += e =>
            {
                if (pestVis.TryGetValue(e, out var ev)) ev.Flash = 1f;
            };
            game.Killed += (e, gold) => Burst(e);
            game.Impact += (src, x, y) =>
            {
                if (src.Stats.SplashRadius > 0f) SpawnRing(World(x, y, 0.08f), src.Stats.SplashRadius, src.Stats.Color);
            };
        }

        // =================================================================== 재료

        void CreateMaterials()
        {
            // 기본 도형의 머티리얼 셰이더 = 현재 파이프라인(URP)의 Lit. 빌드에서도 확실히 포함됨.
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            litShader = tmp.GetComponent<Renderer>().sharedMaterial.shader;
            Object.DestroyImmediate(tmp);

            lit = new Material(litShader) { name = "Cafe_Lit" };
            lit.SetFloat("_Smoothness", 0.25f);

            glossMat = new Material(litShader) { name = "Cafe_Gem" };
            glossMat.SetFloat("_Smoothness", 0.9f);
            glossMat.EnableKeyword("_EMISSION");
            glossMat.SetColor(EmissionId, Color.black);
            glossMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            var sprite = Shader.Find("Sprites/Default"); // 기본 Always Included 셰이더
            unlit = new Material(sprite != null ? sprite : litShader) { name = "Cafe_Unlit" };

            circleTex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32));
                circleTex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(31f - d)));
            }
            circleTex.Apply();
            unlitCircle = new Material(unlit) { name = "Cafe_UnlitCircle", mainTexture = circleTex };
        }

        void Paint(Renderer r, Color c, float emission = 0f)
        {
            mpb.Clear();
            mpb.SetColor(BaseColorId, c);
            mpb.SetColor(ColorId, c);
            if (emission > 0f) mpb.SetColor(EmissionId, c * emission);
            r.SetPropertyBlock(mpb);
        }

        GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material m, Color c,
            float emission = 0f, bool shadows = true)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            Paint(r, c, emission);
            return go;
        }

        /// <summary>바닥에 눕힌 Quad (위에서 보임)</summary>
        GameObject FlatQuad(Transform parent, Vector3 pos, float size, Material m, Color c)
        {
            var go = Prim(PrimitiveType.Quad, parent, pos, new Vector3(size, size, 1f), m, c, 0f, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.GetComponent<Renderer>().receiveShadows = false;
            return go;
        }

        LineRenderer Line(string name, float width, Color c, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // TransformZ 정렬 → 바닥에 눕힘
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = unlit;
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.widthMultiplier = width;
            lr.startColor = lr.endColor = c;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.alignment = LineAlignment.TransformZ;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = 0;
            return lr;
        }

        // =================================================================== 정적 보드

        void BuildBoard()
        {
            float c = (Cafe.W - 1) / 2f;

            // 보드 아래 은은한 원형 그라데이션 바닥 (배경색으로 자연스럽게 사라짐)
            var bgTex = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(64, 64)) / 64f;
                bgTex.SetPixel(x, y, Color.Lerp(Theme.BgCenter, Theme.Bg, Mathf.SmoothStep(0f, 1f, d)));
            }
            bgTex.Apply();
            var bgMat = new Material(unlit) { name = "Cafe_Ground", mainTexture = bgTex };
            FlatQuad(root, new Vector3(c, -0.42f, c), 70f, bgMat, Color.white);

            // 원목 테두리 (두 겹으로 입체감)
            Prim(PrimitiveType.Cube, root, new Vector3(c, -0.25f, c), new Vector3(Cafe.W + 1.2f, 0.3f, Cafe.H + 1.2f), lit, WoodDark);
            // 윗면(-0.01)이 바닥 타일(0.001)보다 낮아야 타일과 선이 가려지지 않음
            Prim(PrimitiveType.Cube, root, new Vector3(c, -0.11f, c), new Vector3(Cafe.W + 0.7f, 0.2f, Cafe.H + 0.7f), lit, WoodLight);

            // 체크 타일 바닥. 타일마다 밝기를 살짝 달리해 손으로 깐 느낌
            const int px = 16;
            var tex = new Texture2D(Cafe.W * px, Cafe.H * px, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            for (int y = 0; y < tex.height; y++)
            for (int x = 0; x < tex.width; x++)
            {
                int tx = x / px, tz = y / px, lx = x % px, ly = y % px;
                bool edge = lx == 0 || ly == 0 || lx == px - 1 || ly == px - 1;
                var baseC = (tx + tz) % 2 == 0 ? TileA : TileB;
                float vary = ((tx * 37 + tz * 91) % 7 - 3) * 0.012f;
                float shine = (lx + ly) < 6 && !edge ? 0.03f : 0f; // 타일 모서리 하이라이트
                var col = edge ? Grout : new Color(baseC.r + vary + shine, baseC.g + vary + shine, baseC.b + vary + shine, 1f);
                tex.SetPixel(x, y, col);
            }
            tex.Apply();
            var boardMat = new Material(litShader) { name = "Cafe_Board", mainTexture = tex };
            boardMat.SetFloat("_Smoothness", 0.35f);
            var q = Prim(PrimitiveType.Quad, root, new Vector3(c, 0.001f, c), new Vector3(Cafe.W, Cafe.H, 1f), boardMat, Color.white, 0f, false);
            q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            BuildGrid();

            // 체크포인트: 입구 = 초록 발매트, 중간 = 캐러멜 코스터, 진열대 = 케이크 스탠드
            var wps = Cafe.Waypoints;
            for (int i = 0; i < wps.Length; i++)
            {
                var p = World(wps[i].X, wps[i].Y, 0f);
                if (i == 0)
                {
                    Prim(PrimitiveType.Cube, root, p + new Vector3(0f, 0.025f, 0f), new Vector3(0.92f, 0.05f, 0.92f), lit, MenuInfo.Argb(0xFF4E8A5E));
                    Prim(PrimitiveType.Cube, root, p + new Vector3(0f, 0.055f, 0f), new Vector3(0.72f, 0.01f, 0.72f), lit, MenuInfo.Argb(0xFF6DB07E));
                }
                else if (i == wps.Length - 1)
                {
                    Prim(PrimitiveType.Cylinder, root, p + new Vector3(0f, 0.1f, 0f), new Vector3(0.18f, 0.1f, 0.18f), lit, Cream);
                    Prim(PrimitiveType.Cylinder, root, p + new Vector3(0f, 0.21f, 0f), new Vector3(0.9f, 0.015f, 0.9f), glossMat, MenuInfo.Argb(0xFFFFB3CF), 0.15f);
                    Prim(PrimitiveType.Cylinder, root, p + new Vector3(0f, 0.31f, 0f), new Vector3(0.5f, 0.09f, 0.5f), lit, MenuInfo.Argb(0xFFF8E1C4));
                    Prim(PrimitiveType.Cylinder, root, p + new Vector3(0f, 0.4f, 0f), new Vector3(0.52f, 0.02f, 0.52f), lit, MenuInfo.Argb(0xFFFF9EC7));
                    Prim(PrimitiveType.Sphere, root, p + new Vector3(0f, 0.48f, 0f), Vector3.one * 0.13f, glossMat, MenuInfo.Argb(0xFFE0283C), 0.3f);
                }
                else
                {
                    Prim(PrimitiveType.Cylinder, root, p + new Vector3(0f, 0.02f, 0f), new Vector3(0.84f, 0.02f, 0.84f), lit, MenuInfo.Argb(0xFFC2843A));
                    Prim(PrimitiveType.Cylinder, root, p + new Vector3(0f, 0.035f, 0f), new Vector3(0.66f, 0.005f, 0.66f), lit, MenuInfo.Argb(0xFFD9A05A));
                }
            }

            pathLine = Line("Path", 0.14f, new Color(1f, 0.95f, 0.85f, 0.28f), false);
        }

        /// <summary>
        /// 칸 구분선. 텍스처 줄눈은 멀리서 흐려지므로 얇은 띠들을 하나의 메시로 바닥 위에 따로 그림.
        /// 굵기·색은 GridWidth / GridColor로 조절.
        /// </summary>
        void BuildGrid()
        {
            const float GridWidth = 0.06f;
            const float y = 0.012f;
            var gridColor = MenuInfo.Argb(0xFFFFF1DC); // 밝은 크림색 (불투명)

            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Strip(Vector3 a, Vector3 b)
            {
                var dir = (b - a).normalized;
                var side = new Vector3(-dir.z, 0f, dir.x) * (GridWidth / 2f);
                int i = verts.Count;
                verts.Add(a - side); verts.Add(a + side); verts.Add(b + side); verts.Add(b - side);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }

            // 칸 중심이 정수 좌표이므로 경계는 -0.5 ~ W-0.5
            float minX = -0.5f, maxX = Cafe.W - 0.5f;
            float minZ = -0.5f, maxZ = Cafe.H - 0.5f;
            for (int i = 0; i <= Cafe.W; i++) Strip(new Vector3(minX + i, y, minZ), new Vector3(minX + i, y, maxZ));
            for (int j = 0; j <= Cafe.H; j++) Strip(new Vector3(minX, y, minZ + j), new Vector3(maxX, y, minZ + j));

            var mesh = new Mesh { name = "CafeGrid" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            var up = new Vector3[verts.Count];
            for (int i = 0; i < up.Length; i++) up[i] = Vector3.up;
            mesh.normals = up;
            mesh.RecalculateBounds();

            // 바닥·컵과 같은 Lit 셰이더(확실히 그려짐)를 쓰고, 양면 렌더링으로 면 방향 문제도 없앰
            var gridMat = new Material(litShader) { name = "Cafe_Grid" };
            gridMat.SetFloat("_Cull", 0f);
            gridMat.SetFloat("_Smoothness", 0.1f);

            var go = new GameObject("Grid");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = gridMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            Paint(mr, gridColor);
        }

        void BuildOverlays()
        {
            rangeDisc = FlatQuad(root, Vector3.zero, 1f, unlitCircle, new Color(1, 1, 1, 0.1f)).transform;
            rangeRing = Line("RangeRing", 0.04f, new Color(1, 1, 1, 0.6f), true);
            rangeRing.positionCount = 64;
            selFrame = Line("SelFrame", 0.07f, Gold, true);
            selFrame.positionCount = 4;
            moveQuad = FlatQuad(root, Vector3.zero, 1f, unlit, MoveColor).GetComponent<Renderer>();
            moveFrame = Line("MoveFrame", 0.08f, MoveColor, true);
            moveFrame.positionCount = 4;
            flashQuad = FlatQuad(root, Vector3.zero, 1f, unlit, Color.red).GetComponent<Renderer>();
            hoverQuad = FlatQuad(root, Vector3.zero, 0.92f, unlit, Color.white).GetComponent<Renderer>();
            hoverFrame = Line("HoverFrame", 0.05f, Color.white, true);
            hoverFrame.positionCount = 4;
            HideOverlays();
        }

        void HideOverlays()
        {
            rangeDisc.gameObject.SetActive(false);
            rangeRing.enabled = false;
            selFrame.enabled = false;
            moveQuad.enabled = false;
            moveFrame.enabled = false;
            flashQuad.enabled = false;
            hoverQuad.enabled = false;
            hoverFrame.enabled = false;
        }

        // =================================================================== 매 프레임

        /// <param name="hover">마우스가 올라가 있는 칸 (터치 기기는 null)</param>
        /// <param name="dt">실제 경과 시간 (연출용)</param>
        public void Sync(Camera cam, Cell? flashCell, float flashTime, Cell? hover, float dt)
        {
            if (game.Version != lastVersion)
            {
                lastVersion = game.Version;
                SyncCells();
            }
            if (!ReferenceEquals(pathRef, game.GroundPath)) RebuildPath();

            AnimateMenus();
            SyncPests(cam, dt);
            SyncDroplets();
            SyncBeams();
            SyncPathDots();
            SyncSelection(flashCell, flashTime);
            SyncHover(hover);
            UpdateEffects(dt);
        }

        // ------------------------------------------------------------------ 마우스 올림 미리보기

        void SyncHover(Cell? hover)
        {
            bool show = hover.HasValue && !game.IsOver && hover != game.Selected;
            if (show)
            {
                var c = hover.Value;
                var ct = game.CellTypeAt(c);
                if (game.MovingFrom.HasValue)
                {
                    show = ct != CellType.Waypoint;
                    if (show) PaintHover(c, MoveColor, 0.22f);
                }
                else if (ct == CellType.Empty && game.State == GameState.BuildPlace)
                {
                    if (hoverCached != c || hoverVersion != game.Version)
                    {
                        hoverCached = c;
                        hoverVersion = game.Version;
                        hoverBuildable = game.CanBuild(c);
                    }
                    PaintHover(c, hoverBuildable ? MenuInfo.Argb(0xFF6BF08A) : MenuInfo.Argb(0xFFFF5050), 0.28f);
                }
                else if (ct == CellType.Menu || ct == CellType.EmptyCup)
                {
                    PaintHover(c, Color.white, 0.08f);
                }
                else show = false;
            }
            hoverQuad.enabled = show;
            hoverFrame.enabled = show;
        }

        void PaintHover(Cell c, Color color, float fillAlpha)
        {
            float pulse = Mathf.Sin(Time.unscaledTime * 6f) * 0.5f + 0.5f;
            hoverQuad.transform.localPosition = World(c.X, c.Y, 0.035f);
            Paint(hoverQuad, Theme.Alpha(color, fillAlpha * (0.7f + 0.3f * pulse)));
            SetSquare(hoverFrame, c, 0.05f, 0.47f);
            hoverFrame.startColor = hoverFrame.endColor = Theme.Alpha(color, 0.85f);
        }

        // ------------------------------------------------------------------ 파편 / 충격파 / 등장 애니메이션

        void Burst(Pest e)
        {
            int n = e.Boss ? 22 : 9;
            var center = World(e.X, e.Y, e.Flying ? 1.1f : (e.Boss ? 0.42f : 0.25f));
            for (int i = 0; i < n; i++)
            {
                var s = shardPool.Count > 0 ? shardPool.Pop() : null;
                if (s == null)
                {
                    var go = Prim(PrimitiveType.Cube, root, Vector3.zero, Vector3.one, glossMat, Color.white, 0f, false);
                    s = new Shard { T = go.transform, R = go.GetComponent<Renderer>() };
                }
                s.T.gameObject.SetActive(true);
                s.T.localPosition = center;
                s.T.localRotation = Random.rotation;
                var dir = Random.insideUnitSphere;
                dir.y = Mathf.Abs(dir.y) + 0.6f;
                s.Vel = dir.normalized * Random.Range(2.5f, 5f) * (e.Boss ? 1.4f : 1f);
                s.Spin = Random.insideUnitSphere * 720f;
                s.Age = 0f;
                s.Life = Random.Range(0.45f, 0.7f);
                s.Size = Random.Range(0.08f, 0.16f) * (e.Boss ? 1.5f : 1f);
                Paint(s.R, Crumbs[Random.Range(0, Crumbs.Length)], 0.3f); // 과자 부스러기
                shards.Add(s);
            }
            SpawnRing(World(e.X, e.Y, 0.06f), e.Boss ? 1.6f : 0.7f, Cream);
        }

        void SpawnRing(Vector3 center, float radius, Color color)
        {
            var r = ringPool.Count > 0 ? ringPool.Pop() : null;
            if (r == null)
            {
                r = new Ring { L = Line("Ring", 0.08f, Color.white, true) };
                r.L.positionCount = 40;
            }
            r.L.enabled = true;
            r.Center = center;
            r.Radius = radius;
            r.Color = color;
            r.Age = 0f;
            r.Life = 0.32f;
            rings.Add(r);
        }

        void AddPop(Transform t)
        {
            t.localScale = Vector3.zero;
            pops.Add(new Pop { T = t, Age = 0f });
        }

        void UpdateEffects(float dt)
        {
            for (int i = shards.Count - 1; i >= 0; i--)
            {
                var s = shards[i];
                s.Age += dt;
                if (s.Age >= s.Life)
                {
                    s.T.gameObject.SetActive(false);
                    shardPool.Push(s);
                    shards.RemoveAt(i);
                    continue;
                }
                s.Vel += Vector3.down * 14f * dt;
                var p = s.T.localPosition + s.Vel * dt;
                if (p.y < 0.05f)
                {
                    p.y = 0.05f;
                    s.Vel = new Vector3(s.Vel.x * 0.5f, -s.Vel.y * 0.35f, s.Vel.z * 0.5f);
                }
                s.T.localPosition = p;
                s.T.Rotate(s.Spin * dt, Space.Self);
                s.T.localScale = Vector3.one * (s.Size * (1f - s.Age / s.Life));
            }

            for (int i = rings.Count - 1; i >= 0; i--)
            {
                var r = rings[i];
                r.Age += dt;
                if (r.Age >= r.Life)
                {
                    r.L.enabled = false;
                    ringPool.Push(r);
                    rings.RemoveAt(i);
                    continue;
                }
                float t = r.Age / r.Life;
                float rad = r.Radius * Ui.EaseOut(t);
                for (int k = 0; k < 40; k++)
                {
                    float a = k / 40f * Mathf.PI * 2f;
                    r.L.SetPosition(k, r.Center + new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad));
                }
                r.L.widthMultiplier = Mathf.Lerp(0.14f, 0.02f, t);
                r.L.startColor = r.L.endColor = Theme.Alpha(r.Color, 1f - t);
            }

            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var p = pops[i];
                if (p.T == null)
                {
                    pops.RemoveAt(i);
                    continue;
                }
                p.Age += dt;
                p.T.localScale = Vector3.one * Ui.EaseOutBack(p.Age / 0.35f);
                if (p.Age >= 0.35f)
                {
                    p.T.localScale = Vector3.one;
                    pops.RemoveAt(i);
                }
                else pops[i] = p;
            }
        }

        // ------------------------------------------------------------------ 빈 컵 / 음료

        void SyncCells()
        {
            seenMenus.Clear();
            for (int x = 0; x < Cafe.W; x++)
            for (int y = 0; y < Cafe.H; y++)
            {
                var cell = new Cell(x, y);
                var ct = game.CellTypeAt(cell);
                if (ct == CellType.EmptyCup)
                {
                    if (cupPiles[x, y] == null) cupPiles[x, y] = CreateCupPile(x, y);
                }
                else if (cupPiles[x, y] != null)
                {
                    Object.Destroy(cupPiles[x, y]);
                    cupPiles[x, y] = null;
                }

                var t = ct == CellType.Menu ? game.TowerAt(cell) : null;
                if (t == null) continue;
                seenMenus.Add(t);
                int sig = MenuSig(t);
                if (menuVis.TryGetValue(t, out var tv) && tv.Sig != sig)
                {
                    Object.Destroy(tv.Root);
                    menuVis.Remove(t);
                    tv = null;
                }
                if (tv == null)
                {
                    tv = CreateMenu(t);
                    menuVis[t] = tv;
                }
                tv.Root.transform.localPosition = World(t.Cell.X, t.Cell.Y, 0f);
            }

            removeMenus.Clear();
            foreach (var kv in menuVis)
                if (!seenMenus.Contains(kv.Key)) removeMenus.Add(kv.Key);
            foreach (var t in removeMenus)
            {
                Object.Destroy(menuVis[t].Root);
                menuVis.Remove(t);
            }
        }

        static int MenuSig(MenuTower t) =>
            t.Signature.HasValue ? 10000 + (int)t.Signature.Value * 10 + t.SignatureLevel : (int)t.Flavor * 10 + (int)t.Grade;

        /// <summary>빈 컵 더미 (원본의 빈 컵): 겹쳐 쌓은 종이컵 + 옆에 쓰러진 컵</summary>
        GameObject CreateCupPile(int x, int y)
        {
            var go = new GameObject($"Cups {x},{y}");
            go.transform.SetParent(root, false);
            go.transform.localPosition = World(x, y, 0f);
            int seed = x * 73 + y * 151;
            go.transform.localRotation = Quaternion.Euler(0f, seed % 360, 0f);
            var tr = go.transform;
            var paper = MenuInfo.Argb(0xFFEDE2D2);

            int stack = 2 + seed % 2;
            for (int i = 0; i < stack; i++)
            {
                float yy = 0.1f + i * 0.09f;
                Prim(PrimitiveType.Cylinder, tr, new Vector3(-0.1f, yy, 0.06f), new Vector3(0.34f, 0.1f, 0.34f), lit, paper);
                Prim(PrimitiveType.Cylinder, tr, new Vector3(-0.1f, yy + 0.1f, 0.06f), new Vector3(0.38f, 0.012f, 0.38f), lit, Cream);
            }
            Prim(PrimitiveType.Cylinder, tr, new Vector3(-0.1f, 0.09f, 0.06f), new Vector3(0.355f, 0.035f, 0.355f), lit, Kraft);

            var fallen = Prim(PrimitiveType.Cylinder, tr, new Vector3(0.2f, 0.14f, -0.14f), new Vector3(0.28f, 0.14f, 0.28f), lit, paper);
            fallen.transform.localRotation = Quaternion.Euler(0f, 30f, 90f);
            var band = Prim(PrimitiveType.Cylinder, tr, new Vector3(0.2f, 0.14f, -0.14f), new Vector3(0.295f, 0.04f, 0.295f), lit, Kraft);
            band.transform.localRotation = Quaternion.Euler(0f, 30f, 90f);

            AddPop(go.transform);
            return go;
        }

        MenuVis CreateMenu(MenuTower t)
        {
            var tv = new MenuVis { Sig = MenuSig(t) };
            var go = new GameObject("Menu " + t.Stats.Name);
            go.transform.SetParent(root, false);
            tv.Root = go;
            var tr = go.transform;
            var col = (Color)t.Stats.Color;

            // 아직 남기지 않은 음료 표시 링 (코스터보다 살짝 커서 테두리만 보임)
            var outline = Prim(PrimitiveType.Cylinder, tr, new Vector3(0f, 0.02f, 0f), new Vector3(0.97f, 0.02f, 0.97f), glossMat, Color.white, 0.6f, false);
            tv.Outline = outline.transform;
            tv.OutlineR = outline.GetComponent<Renderer>();

            // 원목 코스터
            Prim(PrimitiveType.Cylinder, tr, new Vector3(0f, 0.04f, 0f), new Vector3(0.86f, 0.04f, 0.86f), lit, WoodLight);
            Prim(PrimitiveType.Cylinder, tr, new Vector3(0f, 0.081f, 0f), new Vector3(0.72f, 0.002f, 0.72f), lit, Color.Lerp(WoodLight, Kraft, 0.55f));

            if (!t.Signature.HasValue)
            {
                // 테이크아웃 컵: 투명 컵 속 음료 + 종이 슬리브 + 뚜껑 + 빨대. 품질이 높을수록 큰 컵
                int qi = (int)t.Grade;
                float s = 0.72f + 0.09f * qi;
                var cup = new GameObject("Cup").transform;
                cup.SetParent(tr, false);
                tv.Item = cup;
                var drink = Color.Lerp(col, Color.white, 0.1f);

                Prim(PrimitiveType.Cylinder, cup, new Vector3(0f, 0.25f * s, 0f), new Vector3(0.42f * s, 0.25f * s, 0.42f * s), glossMat, drink, 0.3f);
                Prim(PrimitiveType.Cylinder, cup, new Vector3(0f, 0.2f * s, 0f), new Vector3(0.445f * s, 0.075f * s, 0.445f * s), lit, Kraft);
                Prim(PrimitiveType.Cylinder, cup, new Vector3(0f, 0.505f * s, 0f), new Vector3(0.47f * s, 0.018f * s, 0.47f * s), lit, Cream);
                Prim(PrimitiveType.Sphere, cup, new Vector3(0f, 0.525f * s, 0f), new Vector3(0.4f * s, 0.12f * s, 0.4f * s), lit, Cream);
                var straw = Prim(PrimitiveType.Cylinder, cup, new Vector3(0.05f * s, 0.68f * s, 0f), new Vector3(0.05f, 0.16f * s, 0.05f), glossMat, Theme.Shade(col, 0.8f), 0.25f);
                straw.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);

                // 품질 = 코스터 앞쪽의 금빛 원두 개수 (앞쪽 = 카메라 쪽 = -Z)
                int n = qi + 1;
                const float gap = 0.12f;
                float start = -gap * (n - 1) / 2f;
                for (int i = 0; i < n; i++)
                    Prim(PrimitiveType.Sphere, tr, new Vector3(start + gap * i, 0.1f, -0.31f), new Vector3(0.075f, 0.045f, 0.1f), glossMat, Gold, 0.5f, false);
            }
            else
            {
                // 시그니처 메뉴: 접시 위 2단 케이크 + 체리, 주위를 도는 스프링클, 강화 수만큼 초
                Prim(PrimitiveType.Cylinder, tr, new Vector3(0f, 0.1f, 0f), new Vector3(0.8f, 0.015f, 0.8f), glossMat, MenuInfo.Argb(0xFFFFFAF2), 0.1f);
                var cake = new GameObject("Cake").transform;
                cake.SetParent(tr, false);
                cake.localPosition = new Vector3(0f, 0.11f, 0f);
                tv.Item = cake;
                var light = Color.Lerp(col, Color.white, 0.35f);
                Prim(PrimitiveType.Cylinder, cake, new Vector3(0f, 0.12f, 0f), new Vector3(0.58f, 0.12f, 0.58f), glossMat, col, 0.25f);
                Prim(PrimitiveType.Cylinder, cake, new Vector3(0f, 0.25f, 0f), new Vector3(0.61f, 0.02f, 0.61f), lit, Cream);
                Prim(PrimitiveType.Cylinder, cake, new Vector3(0f, 0.35f, 0f), new Vector3(0.42f, 0.09f, 0.42f), glossMat, light, 0.2f);
                Prim(PrimitiveType.Cylinder, cake, new Vector3(0f, 0.445f, 0f), new Vector3(0.44f, 0.015f, 0.44f), lit, Cream);
                Prim(PrimitiveType.Sphere, cake, new Vector3(0f, 0.53f, 0f), Vector3.one * 0.14f, glossMat, MenuInfo.Argb(0xFFE0283C), 0.4f);

                tv.Orbit = new Transform[3];
                for (int i = 0; i < 3; i++)
                    tv.Orbit[i] = Prim(PrimitiveType.Sphere, tr, Vector3.zero, Vector3.one * 0.1f, glossMat, Sprinkles[i], 1f, false).transform;

                for (int i = 0; i < t.SignatureLevel; i++)
                {
                    float a = i / (float)MenuDB.MaxSignatureLevel * Mathf.PI * 2f + 0.6f;
                    var p = new Vector3(Mathf.Cos(a) * 0.15f, 0f, Mathf.Sin(a) * 0.15f);
                    Prim(PrimitiveType.Cylinder, cake, p + new Vector3(0f, 0.5f, 0f), new Vector3(0.035f, 0.05f, 0.035f), lit, MenuInfo.Argb(0xFFFFB3CF), 0f, false);
                    Prim(PrimitiveType.Sphere, cake, p + new Vector3(0f, 0.58f, 0f), new Vector3(0.04f, 0.06f, 0.04f), glossMat, MenuInfo.Argb(0xFFFFC850), 2f, false);
                }
            }
            AddPop(tr);
            return tv;
        }

        void AnimateMenus()
        {
            float time = game.Time;
            bool choosing = game.State == GameState.BuildChoose;
            foreach (var kv in menuVis)
            {
                var t = kv.Key;
                var tv = kv.Value;
                if (!t.Signature.HasValue)
                {
                    // 남기기 전에는 통통 튀고, 남긴 뒤에는 살랑살랑 흔들림
                    float bob = t.Kept ? 0f : Mathf.Abs(Mathf.Sin(time * 3f + t.Cell.X)) * 0.08f;
                    tv.Item.localPosition = new Vector3(0f, 0.083f + bob, 0f);
                    tv.Item.localRotation = Quaternion.Euler(0f, Mathf.Sin(time * 1.3f + t.Cell.X * 0.7f) * 14f, 0f);
                }
                else
                {
                    tv.Item.localRotation = Quaternion.Euler(0f, time * 25f + t.Cell.X * 30f, 0f);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = time * 2.2f + i * 2.0944f;
                        tv.Orbit[i].localPosition = new Vector3(Mathf.Cos(a) * 0.42f, 0.42f + Mathf.Sin(a * 2f) * 0.06f, Mathf.Sin(a) * 0.42f);
                    }
                }

                bool showOutline = !t.Kept;
                if (tv.Outline.gameObject.activeSelf != showOutline) tv.Outline.gameObject.SetActive(showOutline);
                if (showOutline)
                {
                    bool combinable = choosing && game.CountSame(t) >= 2;
                    float pulse = choosing ? (Mathf.Sin(time * 5f) * 0.5f + 0.5f) * 0.07f : 0f;
                    tv.Outline.localScale = new Vector3(0.97f + pulse, 0.02f, 0.97f + pulse);
                    Paint(tv.OutlineR, combinable ? MenuInfo.Argb(0xFF7CF0A0) : Cream, combinable ? 1.2f : 0.5f);
                }
            }
        }

        // ------------------------------------------------------------------ 벌레

        void SyncPests(Camera cam, float dt)
        {
            seenPests.Clear();
            var camRot = cam.transform.rotation;
            float time = game.Time;
            float turn = 1f - Mathf.Exp(-dt * 14f);
            foreach (var e in game.Pests)
            {
                seenPests.Add(e);
                if (!pestVis.TryGetValue(e, out var ev))
                {
                    ev = CreatePest(e);
                    pestVis[e] = ev;
                }
                var rootT = ev.Root.transform;
                var pos = World(e.X, e.Y, 0f);
                if (ev.HasLast)
                {
                    var d = pos - ev.LastPos;
                    d.y = 0f;
                    if (d.sqrMagnitude > 1e-7f)
                        rootT.localRotation = Quaternion.Slerp(rootT.localRotation, Quaternion.LookRotation(d), turn);
                }
                ev.LastPos = pos;
                ev.HasLast = true;
                rootT.localPosition = pos;

                Color col = ev.BaseColor;
                if (e.Slowed) col = Color.Lerp(col, MenuInfo.Argb(0xFF9AD8FF), 0.5f);
                if (e.Poisoned) col = Color.Lerp(col, MenuInfo.Argb(0xFF8EDC6A), 0.45f);
                // 맞으면 잠깐 하얗게 번쩍이고 살짝 커짐
                ev.Flash = Mathf.Max(0f, ev.Flash - dt * 9f);
                col = Color.Lerp(col, Color.white, ev.Flash * 0.75f);
                foreach (var r in ev.Tinted) Paint(r, col, ev.Flash * 0.8f);

                bool moving = !e.Stunned && dt > 0f;
                float bob = ev.Kind == Bug.Fly
                    ? Mathf.Sin(time * 8f + e.Progress) * 0.06f
                    : (moving ? Mathf.Abs(Mathf.Sin(time * 18f + e.Progress * 3f)) * (ev.Kind == Bug.Mouse ? 0.04f : 0.015f) : 0f);
                ev.Model.localPosition = new Vector3(0f, ev.ModelY + bob, 0f);
                ev.Model.localScale = Vector3.one * (1f + ev.Flash * 0.12f);

                for (int i = 0; i < ev.Legs.Count; i++)
                    ev.Legs[i].localRotation = Quaternion.Euler(0f, moving ? Mathf.Sin(time * 24f + i * 2.1f) * 18f : 0f, 0f);

                if (ev.Kind == Bug.Fly)
                {
                    float flap = Mathf.Sin(time * 40f + e.Progress) * 30f;
                    ev.WingL.localRotation = Quaternion.Euler(0f, 15f, -flap);
                    ev.WingR.localRotation = Quaternion.Euler(0f, -15f, flap);
                }

                if (ev.Stun.gameObject.activeSelf != e.Stunned) ev.Stun.gameObject.SetActive(e.Stunned);

                ev.HpRoot.rotation = camRot;
                float f = Mathf.Clamp01(e.Hp / e.MaxHp);
                float w = ev.HpWidth;
                ev.HpFill.localScale = new Vector3(Mathf.Max(0.001f, w * f), 0.09f, 1f);
                ev.HpFill.localPosition = new Vector3(-w / 2f + w * f / 2f, 0f, -0.01f);
                Paint(ev.HpFillR, Color.Lerp(MenuInfo.Argb(0xFFFF6B5E), MenuInfo.Argb(0xFF8EE08A), f));
            }

            removePests.Clear();
            foreach (var kv in pestVis)
                if (!seenPests.Contains(kv.Key)) removePests.Add(kv.Key);
            foreach (var e in removePests)
            {
                Object.Destroy(pestVis[e].Root);
                pestVis.Remove(e);
            }
        }

        PestVis CreatePest(Pest e)
        {
            var kind = e.Boss ? Bug.Mouse : e.Flying ? Bug.Fly : e.Speed > 2f ? Bug.Roach : Bug.Ant;
            float r = e.Boss ? 0.42f : 0.25f;
            var ev = new PestVis { Kind = kind, Radius = r, HpWidth = r * 2.4f };
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(root, false);
            ev.Root = go;
            var tr = go.transform;
            var model = new GameObject("Model").transform;
            model.SetParent(tr, false);
            ev.Model = model;
            Color hue = e.Color;

            // 몸 색을 따르는 부위(Tinted)와 고정 색 부위를 구분해서 만듦
            GameObject Part(PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler = default, Color? fixedColor = null, float em = 0f, Material m = null)
            {
                var p = Prim(type, model, pos, scale, m ?? lit, fixedColor ?? ev.BaseColor, em);
                p.transform.localRotation = Quaternion.Euler(euler);
                if (fixedColor == null) ev.Tinted.Add(p.GetComponent<Renderer>());
                return p;
            }

            void Legs(float y, float span, params float[] zs)
            {
                foreach (var z in zs)
                    ev.Legs.Add(Part(PrimitiveType.Cube, new Vector3(0f, y, z), new Vector3(span, 0.022f, 0.022f)).transform);
            }

            float top;
            switch (kind)
            {
                case Bug.Ant:
                    ev.BaseColor = Color.Lerp(MenuInfo.Argb(0xFF2A1810), hue, 0.28f);
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.15f, -0.16f), new Vector3(0.24f, 0.2f, 0.3f));
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.14f, 0.02f), new Vector3(0.13f, 0.12f, 0.15f));
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.16f, 0.16f), new Vector3(0.16f, 0.15f, 0.15f));
                    Legs(0.08f, 0.42f, -0.04f, 0.02f, 0.08f);
                    Part(PrimitiveType.Cube, new Vector3(-0.04f, 0.24f, 0.25f), new Vector3(0.015f, 0.015f, 0.14f), new Vector3(-35f, -20f, 0f));
                    Part(PrimitiveType.Cube, new Vector3(0.04f, 0.24f, 0.25f), new Vector3(0.015f, 0.015f, 0.14f), new Vector3(-35f, 20f, 0f));
                    top = 0.3f;
                    break;

                case Bug.Roach:
                    ev.BaseColor = Color.Lerp(MenuInfo.Argb(0xFF5A2E16), hue, 0.2f);
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.09f, -0.02f), new Vector3(0.3f, 0.12f, 0.48f), default, null, 0f, glossMat);
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.24f), new Vector3(0.14f, 0.1f, 0.1f));
                    Legs(0.05f, 0.48f, -0.1f, 0.0f, 0.1f);
                    Part(PrimitiveType.Cube, new Vector3(-0.05f, 0.1f, 0.42f), new Vector3(0.012f, 0.012f, 0.3f), new Vector3(-8f, -18f, 0f));
                    Part(PrimitiveType.Cube, new Vector3(0.05f, 0.1f, 0.42f), new Vector3(0.012f, 0.012f, 0.3f), new Vector3(-8f, 18f, 0f));
                    top = 0.2f;
                    break;

                case Bug.Fly:
                    ev.BaseColor = Color.Lerp(MenuInfo.Argb(0xFF262A33), hue, 0.3f);
                    ev.ModelY = 1.1f;
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0f, -0.03f), new Vector3(0.22f, 0.2f, 0.3f));
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0.15f), new Vector3(0.17f, 0.16f, 0.14f));
                    var eye = MenuInfo.Argb(0xFFD8303A);
                    Part(PrimitiveType.Sphere, new Vector3(-0.06f, 0.05f, 0.19f), Vector3.one * 0.09f, default, eye, 0.4f, glossMat);
                    Part(PrimitiveType.Sphere, new Vector3(0.06f, 0.05f, 0.19f), Vector3.one * 0.09f, default, eye, 0.4f, glossMat);
                    var wingC = MenuInfo.Argb(0xFFD4E6F2);
                    var wl = Part(PrimitiveType.Sphere, new Vector3(-0.17f, 0.1f, -0.05f), new Vector3(0.32f, 0.02f, 0.16f), default, wingC, 0.2f, glossMat);
                    var wr = Part(PrimitiveType.Sphere, new Vector3(0.17f, 0.1f, -0.05f), new Vector3(0.32f, 0.02f, 0.16f), default, wingC, 0.2f, glossMat);
                    ev.WingL = wl.transform;
                    ev.WingR = wr.transform;
                    ev.WingLR = wl.GetComponent<Renderer>();
                    ev.WingRR = wr.GetComponent<Renderer>();
                    top = 0.15f;
                    break;

                default: // 생쥐 대장
                    ev.BaseColor = Color.Lerp(MenuInfo.Argb(0xFF8E8580), hue, 0.1f);
                    var pink = MenuInfo.Argb(0xFFFF9FB2);
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.3f, -0.05f), new Vector3(0.66f, 0.55f, 0.9f));
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.36f, 0.42f), new Vector3(0.42f, 0.4f, 0.46f));
                    Part(PrimitiveType.Sphere, new Vector3(-0.17f, 0.6f, 0.36f), new Vector3(0.26f, 0.26f, 0.06f));
                    Part(PrimitiveType.Sphere, new Vector3(0.17f, 0.6f, 0.36f), new Vector3(0.26f, 0.26f, 0.06f));
                    Part(PrimitiveType.Sphere, new Vector3(-0.17f, 0.6f, 0.39f), new Vector3(0.16f, 0.16f, 0.02f), default, pink);
                    Part(PrimitiveType.Sphere, new Vector3(0.17f, 0.6f, 0.39f), new Vector3(0.16f, 0.16f, 0.02f), default, pink);
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.36f, 0.66f), Vector3.one * 0.09f, default, pink, 0.2f, glossMat);
                    Part(PrimitiveType.Sphere, new Vector3(-0.1f, 0.45f, 0.6f), Vector3.one * 0.07f, default, Color.black, 0f, glossMat);
                    Part(PrimitiveType.Sphere, new Vector3(0.1f, 0.45f, 0.6f), Vector3.one * 0.07f, default, Color.black, 0f, glossMat);
                    Part(PrimitiveType.Cylinder, new Vector3(0f, 0.18f, -0.62f), new Vector3(0.045f, 0.3f, 0.045f), new Vector3(70f, 0f, 0f), MenuInfo.Argb(0xFFD8A8A8));
                    foreach (var fx in new[] { -0.2f, 0.2f })
                    foreach (var fz in new[] { -0.25f, 0.22f })
                        Part(PrimitiveType.Sphere, new Vector3(fx, 0.06f, fz), Vector3.one * 0.14f, default, pink);
                    top = 0.68f;
                    break;
            }
            foreach (var rend in ev.Tinted) Paint(rend, ev.BaseColor);

            // 기절: 바닥에 빛나는 고리
            var stun = Prim(PrimitiveType.Cylinder, tr, new Vector3(0f, 0.04f, 0f),
                new Vector3(r * 2.8f, 0.02f, r * 2.8f), glossMat, MenuInfo.Argb(0xFFFFE6A0), 1.5f, false);
            ev.Stun = stun.transform;
            stun.SetActive(false);

            // 체력바 (항상 카메라를 향함)
            var hp = new GameObject("Hp").transform;
            hp.SetParent(tr, false);
            hp.localPosition = new Vector3(0f, ev.ModelY + top + 0.22f, 0f);
            ev.HpRoot = hp;
            Prim(PrimitiveType.Quad, hp, Vector3.zero, new Vector3(ev.HpWidth + 0.05f, 0.13f, 1f), unlit, MenuInfo.Argb(0xFF20140E), 0f, false);
            var fill = Prim(PrimitiveType.Quad, hp, new Vector3(0f, 0f, -0.01f), new Vector3(ev.HpWidth, 0.09f, 1f), unlit, Color.green, 0f, false);
            ev.HpFill = fill.transform;
            ev.HpFillR = fill.GetComponent<Renderer>();
            return ev;
        }

        // ------------------------------------------------------------------ 투사체 / 번개

        void SyncDroplets()
        {
            seenDrops.Clear();
            foreach (var p in game.Droplets)
            {
                seenDrops.Add(p);
                var pos = World(p.X, p.Y, 0.6f);
                if (!dropVis.TryGetValue(p, out var pv))
                {
                    pv = dropPool.Count > 0 ? dropPool.Pop() : CreateDroplet();
                    pv.R.transform.localPosition = pos;
                    pv.R.gameObject.SetActive(true);
                    pv.Trail.Clear();
                    Color c = p.Crit ? Color.white : (Color)p.Src.Stats.Color;
                    pv.R.transform.localScale = Vector3.one * (p.Crit ? 0.26f : 0.17f);
                    Paint(pv.R, c, p.Crit ? 2.5f : 1.6f);
                    pv.Trail.widthMultiplier = p.Crit ? 0.2f : 0.13f;
                    pv.Trail.startColor = Theme.Alpha(c, 0.85f);
                    pv.Trail.endColor = Theme.Alpha(c, 0f);
                    dropVis[p] = pv;
                }
                pv.R.transform.localPosition = pos;
            }

            removeDrops.Clear();
            foreach (var kv in dropVis)
                if (!seenDrops.Contains(kv.Key)) removeDrops.Add(kv.Key);
            foreach (var p in removeDrops)
            {
                var pv = dropVis[p];
                pv.R.gameObject.SetActive(false);
                dropPool.Push(pv);
                dropVis.Remove(p);
            }
        }

        DropVis CreateDroplet()
        {
            var go = Prim(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 0.17f, glossMat, Color.white, 1.5f, false);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = unlit;
            trail.time = 0.12f;
            trail.minVertexDistance = 0.04f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.numCapVertices = 2;
            return new DropVis { R = go.GetComponent<MeshRenderer>(), Trail = trail };
        }

        void SyncBeams()
        {
            var list = game.Beams;
            while (beamPool.Count < list.Count)
                beamPool.Add(new[] { Line("Beam", 0.09f, Color.white, false), Line("BeamCore", 0.035f, Color.white, false) });
            for (int i = 0; i < beamPool.Count; i++)
            {
                var pair = beamPool[i];
                bool on = i < list.Count;
                pair[0].enabled = on;
                pair[1].enabled = on;
                if (!on) continue;
                var b = list[i];
                float a = Mathf.Clamp01(b.Ttl / Beam.Duration);
                var p1 = World(b.X1, b.Y1, 0.5f);
                var p2 = World(b.X2, b.Y2, 0.5f);
                Color c = b.Color;
                c.a = a;
                foreach (var lr in pair)
                {
                    lr.positionCount = 2;
                    lr.SetPosition(0, p1);
                    lr.SetPosition(1, p2);
                }
                pair[0].startColor = pair[0].endColor = c;
                pair[1].startColor = pair[1].endColor = new Color(1, 1, 1, a);
            }
        }

        // ------------------------------------------------------------------ 경로

        void RebuildPath()
        {
            pathRef = game.GroundPath;
            pathPts.Clear();
            pathLen.Clear();
            float acc = 0f;
            for (int i = 0; i < pathRef.Count; i++)
            {
                var p = World(pathRef[i].X, pathRef[i].Y, PathY);
                if (i > 0) acc += Vector3.Distance(pathPts[i - 1], p);
                pathPts.Add(p);
                pathLen.Add(acc);
            }
            pathLine.positionCount = pathPts.Count;
            pathLine.SetPositions(pathPts.ToArray());
        }

        /// <summary>경로를 따라 흐르는 점 (원본의 움직이는 점선)</summary>
        void SyncPathDots()
        {
            const float spacing = 0.6f;
            float total = pathLen.Count > 0 ? pathLen[pathLen.Count - 1] : 0f;
            int n = total > 0f ? Mathf.FloorToInt(total / spacing) : 0;
            while (dotPool.Count < n)
                dotPool.Add(FlatQuad(root, Vector3.zero, 0.15f, unlitCircle, new Color(1f, 0.96f, 0.88f, 0.85f)).transform);

            float offset = (game.Time * 2f) % spacing;
            int seg = 1;
            for (int i = 0; i < dotPool.Count; i++)
            {
                var d = dotPool[i];
                bool on = i < n;
                if (d.gameObject.activeSelf != on) d.gameObject.SetActive(on);
                if (!on) continue;
                float s = offset + i * spacing;
                while (seg < pathLen.Count - 1 && pathLen[seg] < s) seg++;
                if (seg >= pathLen.Count) seg = pathLen.Count - 1;
                float l0 = pathLen[seg - 1], l1 = pathLen[seg];
                float t = l1 > l0 ? (s - l0) / (l1 - l0) : 0f;
                d.localPosition = Vector3.Lerp(pathPts[seg - 1], pathPts[seg], Mathf.Clamp01(t)) + Vector3.up * 0.005f;
            }
        }

        // ------------------------------------------------------------------ 선택 / 깜빡임

        static void SetSquare(LineRenderer lr, Cell c, float y, float half)
        {
            var p = World(c.X, c.Y, y);
            lr.SetPosition(0, p + new Vector3(-half, 0f, -half));
            lr.SetPosition(1, p + new Vector3(-half, 0f, half));
            lr.SetPosition(2, p + new Vector3(half, 0f, half));
            lr.SetPosition(3, p + new Vector3(half, 0f, -half));
        }

        void SyncSelection(Cell? flashCell, float flashTime)
        {
            var sel = game.Selected;
            if (sel.HasValue)
            {
                var ct = game.CellTypeAt(sel.Value);
                if (ct != CellType.Menu && ct != CellType.EmptyCup)
                {
                    game.Selected = null;
                    sel = null;
                }
            }

            selFrame.enabled = sel.HasValue;
            var tower = sel.HasValue ? game.TowerAt(sel.Value) : null;
            rangeRing.enabled = tower != null;
            rangeDisc.gameObject.SetActive(tower != null);
            if (sel.HasValue) SetSquare(selFrame, sel.Value, 0.04f, 0.5f);
            if (tower != null)
            {
                float range = tower.Stats.Range;
                var center = World(sel.Value.X, sel.Value.Y, 0.02f);
                for (int i = 0; i < 64; i++)
                {
                    float a = i / 64f * Mathf.PI * 2f;
                    rangeRing.SetPosition(i, center + new Vector3(Mathf.Cos(a) * range, 0f, Mathf.Sin(a) * range));
                }
                rangeDisc.localPosition = center;
                rangeDisc.localScale = new Vector3(range * 2f, range * 2f, 1f);
            }

            var mv = game.MovingFrom;
            moveQuad.enabled = mv.HasValue;
            moveFrame.enabled = mv.HasValue;
            if (mv.HasValue)
            {
                float pulse = Mathf.Sin(game.Time * 6f) * 0.5f + 0.5f;
                moveQuad.transform.localPosition = World(mv.Value.X, mv.Value.Y, 0.05f);
                var c = MoveColor;
                c.a = (60f + pulse * 70f) / 255f;
                Paint(moveQuad, c);
                SetSquare(moveFrame, mv.Value, 0.06f, 0.5f);
            }

            bool flash = flashCell.HasValue && flashTime > 0f;
            flashQuad.enabled = flash;
            if (flash)
            {
                flashQuad.transform.localPosition = World(flashCell.Value.X, flashCell.Value.Y, 0.04f);
                Paint(flashQuad, new Color(1f, 0.16f, 0.16f, Mathf.Clamp01(flashTime / 0.6f) * 0.63f));
            }
        }
    }
}

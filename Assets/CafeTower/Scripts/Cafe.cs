using System;
using System.Collections.Generic;
using UnityEngine;

namespace CafeTowerDefense
{
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X;
        public readonly int Y;

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Cell operator +(Cell a, Cell b) => new Cell(a.X + b.X, a.Y + b.Y);
        public static bool operator ==(Cell a, Cell b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Cell a, Cell b) => !(a == b);
        public bool Equals(Cell o) => this == o;
        public override bool Equals(object obj) => obj is Cell c && this == c;
        public override int GetHashCode() => X * 397 ^ Y;
        public override string ToString() => $"({X},{Y})";
    }

    public enum CellType { Empty, Waypoint, EmptyCup, Menu }

    public enum GameState { BuildPlace, BuildChoose, Wave, GameOver, Victory }

    public class WaveInfo
    {
        public int Count;
        public double Hp;
        public float Speed;
        public float Interval;
        public int Bounty;
        public int Leak;
        public bool Flying;
        public bool Boss;
        public bool Fast;
        public string Label;
    }

    public class MenuTower
    {
        public Cell Cell { get; internal set; }
        public Flavor Flavor { get; private set; }
        public Grade Grade { get; private set; }
        public SignatureMenu? Signature { get; private set; }
        public int SignatureLevel { get; internal set; }
        public MenuStats Stats { get; private set; }
        /// <summary>라운드 종료 후 남겨진 음료만 공격함</summary>
        public bool Kept { get; internal set; }
        public int Kills { get; internal set; }
        public float TotalDamage { get; internal set; }
        /// <summary>위치 바꾸기를 이미 썼는지 (시그니처 메뉴당 1번)</summary>
        public bool Moved { get; internal set; }
        internal float Cooldown;
        /// <summary>인스타 감성 티의 공격력 (매장의 다른 메뉴 공격력 합계)</summary>
        internal float InstaDamage;
        /// <summary>인스타 감성 티에 영구히 더해지는 공격력 (자기 음료 + 함께 나온 라운드 음료)</summary>
        internal float InstaBonus;
        /// <summary>배치 패스로 쌓인 공격력 배율</summary>
        internal float DamageMult = 1f;

        public MenuTower(Cell cell, Flavor type, Grade quality)
        {
            Cell = cell;
            Flavor = type;
            Grade = quality;
            Stats = MenuDB.FlavorStats(type, quality);
        }

        internal void SetMenu(Flavor t, Grade q, SignatureMenu? sp)
        {
            Flavor = t;
            Grade = q;
            Signature = sp;
            SignatureLevel = 0;
            Refresh();
        }

        internal void Refresh()
        {
            Stats = Signature.HasValue ? MenuDB.SignatureStats(Signature.Value, SignatureLevel) : MenuDB.FlavorStats(Flavor, Grade);
            if (Signature == SignatureMenu.InstaTea) { Stats.Damage = InstaDamage; return; }
            Stats.Damage *= DamageMult;
            Stats.PoisonDps *= DamageMult;
        }
    }

    public class Pest
    {
        public readonly double MaxHp;
        public readonly float Speed;
        public readonly bool Flying;
        public readonly bool Boss;
        public readonly int Bounty;
        public readonly int LeakDamage;
        public readonly Color32 Color;
        readonly List<Vector2> path;

        public float X { get; private set; }
        public float Y { get; private set; }
        public double Hp { get; internal set; }
        /// <summary>이동한 거리 (타워 타겟 우선순위)</summary>
        public float Progress { get; private set; }
        public bool Dead { get; internal set; }
        int idx = 1;

        internal float SlowPct;
        internal float SlowTimer;
        internal float PoisonDps;
        internal float PoisonTimer;
        internal MenuTower PoisonSrc;
        internal float StunTimer;

        public bool Slowed => SlowTimer > 0f;
        public bool Poisoned => PoisonTimer > 0f;
        public bool Stunned => StunTimer > 0f;

        public Pest(double maxHp, float speed, bool flying, bool boss, int bounty, int leakDamage, List<Vector2> path, Color32 color)
        {
            MaxHp = maxHp;
            Speed = speed;
            Flying = flying;
            Boss = boss;
            Bounty = bounty;
            LeakDamage = leakDamage;
            this.path = path;
            Color = color;
            Hp = maxHp;
            if (path.Count > 0)
            {
                X = path[0].x;
                Y = path[0].y;
            }
        }

        /// <returns>도착 지점에 도달하면 true</returns>
        internal bool Move(float dt)
        {
            if (StunTimer > 0f)
            {
                StunTimer -= dt;
                return idx >= path.Count;
            }
            float sp = Speed;
            if (SlowTimer > 0f)
            {
                SlowTimer -= dt;
                sp *= 1f - SlowPct;
            }
            else SlowPct = 0f;

            float mv = sp * dt;
            while (mv > 0f && idx < path.Count)
            {
                var t = path[idx];
                float dx = t.x - X;
                float dy = t.y - Y;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= mv)
                {
                    X = t.x; Y = t.y;
                    mv -= d; Progress += d; idx++;
                }
                else
                {
                    X += dx / d * mv; Y += dy / d * mv;
                    Progress += mv; mv = 0f;
                }
            }
            return idx >= path.Count;
        }

        internal void ApplySlow(float pct, float dur)
        {
            if (SlowTimer <= 0f || pct >= SlowPct) SlowPct = pct;
            SlowTimer = Mathf.Max(SlowTimer, dur);
        }

        internal void ApplyPoison(float dps, float dur, MenuTower src)
        {
            if (PoisonTimer <= 0f || dps >= PoisonDps)
            {
                PoisonDps = dps;
                PoisonSrc = src;
            }
            PoisonTimer = Mathf.Max(PoisonTimer, dur);
        }
    }

    /// <summary>연쇄 번개 등 짧게 보이는 선 효과</summary>
    public class Beam
    {
        public const float Duration = 0.18f;
        public readonly float X1, Y1, X2, Y2;
        public readonly Color32 Color;
        public float Ttl { get; internal set; } = Duration;

        public Beam(float x1, float y1, float x2, float y2, Color32 color)
        {
            X1 = x1; Y1 = y1; X2 = x2; Y2 = y2;
            Color = color;
        }
    }

    public class Droplet
    {
        public readonly MenuTower Src;
        public readonly Pest Target;
        public float X, Y;
        public readonly float Damage;
        public readonly bool Crit;
        public float AimX { get; internal set; }
        public float AimY { get; internal set; }
        public bool Done { get; internal set; }

        public Droplet(MenuTower src, Pest target, float x, float y, float damage, bool crit)
        {
            Src = src;
            Target = target;
            X = x;
            Y = y;
            Damage = damage;
            Crit = crit;
            AimX = target.X;
            AimY = target.Y;
        }
    }

    /// <summary>
    /// 음료 타워 디펜스 게임 로직 (Unity 렌더링 의존성 없음).
    /// 흐름: 음료 5개 배치 → 1개 선택(나머지는 빈 컵) → 웨이브 → 반복
    /// 좌표 단위는 칸(cell). 칸 (x, y)의 중심이 (x, y). y는 화면 위쪽이 0.
    /// </summary>
    public class Cafe
    {
        public const int W = 19;
        public const int H = 19;
        public const int DrinksPerRound = 5;
        /// <summary>최종 보스가 나오는 마지막 웨이브. 화면에는 표시하지 않음.</summary>
        public const int MaxWave = 201;
        public const int StartDesserts = 20;
        public const int CupClearCost = 10;
        public const int MoveCost = 15;
        public const float DropletSpeed = 14f;
        const float MaxStep = 0.02f;

        /// <summary>적이 순서대로 거쳐가는 지점 (출발 → 체크포인트 → 도착)</summary>
        public static readonly Cell[] Waypoints =
        {
            new Cell(0, 18), new Cell(2, 9), new Cell(16, 9), new Cell(16, 16), new Cell(9, 16), new Cell(9, 2), new Cell(18, 0),
        };

        static readonly Cell[] Dirs = { new Cell(0, -1), new Cell(1, 0), new Cell(0, 1), new Cell(-1, 0) };

        readonly System.Random rng;
        readonly CellType[,] cells = new CellType[W, H];
        readonly MenuTower[,] towers = new MenuTower[W, H];

        public readonly List<Pest> Pests = new List<Pest>();
        public readonly List<Droplet> Droplets = new List<Droplet>();
        public readonly List<Beam> Beams = new List<Beam>();
        public readonly List<MenuTower> RoundDrinks = new List<MenuTower>();

        public List<Cell> GroundPath { get; private set; } = new List<Cell>();
        List<Vector2> groundPos = new List<Vector2>();
        readonly List<Vector2> airPos = new List<Vector2>();

        /// <summary>보드 구조/상태가 바뀔 때마다 증가 (화면 갱신용)</summary>
        public int Version { get; private set; }

        public GameState State { get; private set; } = GameState.BuildPlace;
        public int Wave { get; private set; } = 1;
        public int Desserts { get; private set; } = StartDesserts;
        public int Coins { get; private set; }
        public int BaristaLevel { get; private set; }
        public Cell? Selected;
        /// <summary>위치 바꾸기 모드에서 옮길 음료의 칸</summary>
        public Cell? MovingFrom { get; private set; }
        public string Message { get; private set; } = "";
        public float MessageTimer { get; private set; }
        public WaveInfo CurrentWave { get; private set; }
        /// <summary>애니메이션용 누적 게임 시간</summary>
        public float Time { get; private set; }

        // 기록용. 모두 게임 시간 기준이라 배속·일시정지의 영향을 받지 않음
        public double BossDamage { get; private set; }
        public float BossStageTime { get; private set; }
        public float TotalTime { get; private set; }

        /// <summary>이번 판에서 음료 배치를 패스한 횟수</summary>
        public int PassCount { get; private set; }
        /// <summary>패스 1회마다 모든 음료 공격력 +1% (합연산)</summary>
        public const float PassDamageBonus = 0.01f;
        public float DamageMult => 1f + PassDamageBonus * PassCount;
        public float DamageBonusPercent => PassDamageBonus * PassCount * 100f;

        int toSpawn;
        float spawnTimer;
        readonly List<Pest> targetBuf = new List<Pest>();
        readonly List<Pest> hitBuf = new List<Pest>();

        // 화면 연출용 이벤트 (로직에는 영향 없음)
        /// <summary>적 처치 (적, 받은 코인)</summary>
        public event Action<Pest, int> Killed;
        /// <summary>적이 도착 지점에 도달해 디저트이 줄어듦</summary>
        public event Action<Pest> Leaked;
        /// <summary>음료 공격에 직접 맞음 (독 피해 제외)</summary>
        public event Action<Pest> Hurt;
        /// <summary>투사체가 도착한 위치 (범위 피해 연출용)</summary>
        public event Action<MenuTower, float, float> Impact;
        /// <summary>떠오르는 피해 숫자용: (맞은 벌레, 실제 피해, 치명타 여부). 직접 맞은 공격만 (범위·지속 피해 제외)</summary>
        public event Action<Pest, float, bool> DamageShown;

        public int RemainingEnemies => Pests.Count + toSpawn;
        public bool IsBuildState => State == GameState.BuildPlace || State == GameState.BuildChoose;
        public bool IsOver => State == GameState.GameOver || State == GameState.Victory;

        public Cafe(System.Random rng = null)
        {
            this.rng = rng ?? new System.Random();
            foreach (var wp in Waypoints) airPos.Add(new Vector2(wp.X, wp.Y));
            CurrentWave = WaveInfoFor(1);
            NewGame();
        }

        public CellType CellTypeAt(Cell c) => cells[c.X, c.Y];
        public MenuTower TowerAt(Cell c) => towers[c.X, c.Y];
        public static bool InBounds(Cell c) => c.X >= 0 && c.X < W && c.Y >= 0 && c.Y < H;

        void Touch()
        {
            RefreshInstaTea();
            Version++;
        }

        /// <summary>인스타 감성 티의 공격력을 현재 매장의 다른 메뉴 공격력 합계로 맞춤. 티끼리는 서로 더하지 않음.</summary>
        void RefreshInstaTea()
        {
            float sum = 0f;
            bool any = false;
            foreach (var t in towers)
            {
                if (t == null) continue;
                if (t.Signature == SignatureMenu.InstaTea) any = true;
                else sum += t.Stats.Damage;
            }
            if (!any) return;
            foreach (var t in towers)
            {
                if (t == null || t.Signature != SignatureMenu.InstaTea) continue;
                t.InstaDamage = t.InstaBonus * DamageMult + sum;
                t.Refresh();
            }
        }

        public void NewGame()
        {
            for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                cells[x, y] = CellType.Empty;
                towers[x, y] = null;
            }
            foreach (var wp in Waypoints) cells[wp.X, wp.Y] = CellType.Waypoint;
            Pests.Clear();
            Droplets.Clear();
            Beams.Clear();
            RoundDrinks.Clear();
            Wave = 1;
            Desserts = StartDesserts;
            Coins = 0;
            BaristaLevel = 0;
            BossDamage = 0;
            BossStageTime = 0f;
            TotalTime = 0f;
            PassCount = 0;
            toSpawn = 0;
            Selected = null;
            MovingFrom = null;
            State = GameState.BuildPlace;
            RecalcPath();
            ShowMessage("빈 칸을 눌러 음료 5잔을 놓으세요");
        }

        // ------------------------------------------------------------------ 이어하기 (저장 / 불러오기)

        /// <summary>웨이브 시작 직전 상태 (웨이브 도중에는 이것을 저장)</summary>
        CafeSaveData waveStartSave;

        /// <summary>지금 저장할 상태. 영업이 끝났으면 null (저장 지움).</summary>
        public CafeSaveData SaveData => IsOver ? null : State == GameState.Wave ? waveStartSave : Capture(State);

        CafeSaveData Capture(GameState state)
        {
            var d = new CafeSaveData
            {
                State = state.ToString(),
                Wave = Wave,
                Desserts = Desserts,
                Coins = Coins,
                BaristaLevel = BaristaLevel,
                PassCount = PassCount,
                BossDamage = BossDamage,
                BossStageTime = BossStageTime,
                TotalTime = TotalTime,
                Cells = new int[W * H],
            };
            for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                d.Cells[x * H + y] = (int)cells[x, y];
                var t = towers[x, y];
                if (t == null) continue;
                d.Towers.Add(new TowerSave
                {
                    X = x,
                    Y = y,
                    Flavor = t.Flavor.ToString(),
                    Grade = t.Grade.ToString(),
                    Signature = t.Signature.HasValue ? t.Signature.Value.ToString() : "",
                    SignatureLevel = t.SignatureLevel,
                    Kept = t.Kept,
                    Kills = t.Kills,
                    TotalDamage = t.TotalDamage,
                    Moved = t.Moved,
                    InstaBonus = t.InstaBonus,
                    RoundIndex = RoundDrinks.IndexOf(t),
                });
            }
            return d;
        }

        /// <summary>저장된 상태로 되돌림. 웨이브 도중에 저장된 판은 그 웨이브를 처음부터 다시 시작.</summary>
        public bool Restore(CafeSaveData d)
        {
            if (d == null || d.Cells == null || d.Cells.Length != W * H || d.Wave < 1) return false;
            if (!Enum.TryParse(d.State, out GameState st) || st == GameState.GameOver || st == GameState.Victory) return false;
            NewGame();
            for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                int v = d.Cells[x * H + y];
                cells[x, y] = v == (int)CellType.EmptyCup || v == (int)CellType.Menu ? (CellType)v : CellType.Empty;
            }
            foreach (var wp in Waypoints) cells[wp.X, wp.Y] = CellType.Waypoint;

            Wave = Mathf.Clamp(d.Wave, 1, MaxWave);
            Desserts = Mathf.Max(1, d.Desserts);
            Coins = Mathf.Max(0, d.Coins);
            BaristaLevel = Mathf.Clamp(d.BaristaLevel, 0, MenuDB.MaxBaristaLevel);
            PassCount = Mathf.Max(0, d.PassCount);
            BossDamage = d.BossDamage;
            BossStageTime = d.BossStageTime;
            TotalTime = d.TotalTime;

            var round = new SortedList<int, MenuTower>();
            if (d.Towers != null)
                foreach (var s in d.Towers)
                {
                    var c = new Cell(s.X, s.Y);
                    if (!InBounds(c) || cells[c.X, c.Y] != CellType.Menu) continue;
                    if (!Enum.TryParse(s.Flavor, out Flavor f) || !Enum.TryParse(s.Grade, out Grade g)) continue;
                    var t = new MenuTower(c, f, g) { DamageMult = DamageMult };
                    if (!string.IsNullOrEmpty(s.Signature) && Enum.TryParse(s.Signature, out SignatureMenu sig))
                    {
                        t.SetMenu(f, g, sig);
                        t.SignatureLevel = Mathf.Clamp(s.SignatureLevel, 0, MenuDB.MaxSignatureLevel);
                        t.InstaBonus = s.InstaBonus;
                    }
                    t.Kept = s.Kept;
                    t.Kills = s.Kills;
                    t.TotalDamage = s.TotalDamage;
                    t.Moved = s.Moved;
                    t.Refresh();
                    towers[c.X, c.Y] = t;
                    if (s.RoundIndex >= 0 && !s.Kept && !round.ContainsKey(s.RoundIndex)) round.Add(s.RoundIndex, t);
                }
            // 메뉴 정보를 읽지 못한 칸은 빈 컵으로
            for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                if (cells[x, y] == CellType.Menu && towers[x, y] == null) cells[x, y] = CellType.EmptyCup;
            RoundDrinks.AddRange(round.Values);

            CurrentWave = WaveInfoFor(Wave);
            RecalcPath();
            if (st == GameState.Wave)
            {
                StartWave();
                ShowMessage($"이어서 영업해요 · 웨이브 {Wave}를 처음부터 다시 막아요");
            }
            else
            {
                State = st == GameState.BuildChoose && RoundDrinks.Count >= DrinksPerRound ? GameState.BuildChoose : GameState.BuildPlace;
                ShowMessage(State == GameState.BuildChoose ? "이어서 영업해요 · 남길 음료 한 잔을 골라주세요" : "이어서 영업해요 · 음료를 놓으세요");
                Touch();
            }
            return true;
        }

        // ------------------------------------------------------------------ 길찾기

        bool Passable(Cell c) => cells[c.X, c.Y] == CellType.Empty || cells[c.X, c.Y] == CellType.Waypoint;

        List<Cell> FindPath(Cell a, Cell b)
        {
            var prev = new Cell?[W, H];
            var seen = new bool[W, H];
            var q = new Queue<Cell>();
            q.Enqueue(a);
            seen[a.X, a.Y] = true;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c == b) break;
                foreach (var d in Dirs)
                {
                    var n = c + d;
                    if (!InBounds(n) || seen[n.X, n.Y] || !Passable(n)) continue;
                    seen[n.X, n.Y] = true;
                    prev[n.X, n.Y] = c;
                    q.Enqueue(n);
                }
            }
            if (!seen[b.X, b.Y]) return null;
            var path = new List<Cell>();
            var cur = b;
            while (cur != a)
            {
                path.Add(cur);
                cur = prev[cur.X, cur.Y].Value;
            }
            path.Add(a);
            path.Reverse();
            return path;
        }

        public List<Cell> ComputeFullPath()
        {
            var full = new List<Cell>();
            for (int i = 0; i < Waypoints.Length - 1; i++)
            {
                var seg = FindPath(Waypoints[i], Waypoints[i + 1]);
                if (seg == null) return null;
                if (i > 0) seg.RemoveAt(0);
                full.AddRange(seg);
            }
            return full;
        }

        public bool CanBuild(Cell c)
        {
            if (!InBounds(c) || cells[c.X, c.Y] != CellType.Empty) return false;
            cells[c.X, c.Y] = CellType.EmptyCup;
            bool ok = ComputeFullPath() != null;
            cells[c.X, c.Y] = CellType.Empty;
            return ok;
        }

        void RecalcPath()
        {
            GroundPath = ComputeFullPath() ?? new List<Cell>();
            var pos = new List<Vector2>(GroundPath.Count);
            foreach (var c in GroundPath) pos.Add(new Vector2(c.X, c.Y));
            groundPos = pos; // 이미 이동 중인 적은 기존 경로 리스트를 계속 사용
            Touch();
        }

        // ------------------------------------------------------------------ 입력

        /// <summary>칸을 눌렀을 때. 배치/이동이 거부되면 false.</summary>
        public bool TapCell(Cell c)
        {
            if (MovingFrom.HasValue)
            {
                if (!InBounds(c))
                {
                    CancelMove();
                    return true;
                }
                return MoveTo(c);
            }
            if (!InBounds(c))
            {
                Selected = null;
                return true;
            }
            var ct = cells[c.X, c.Y];
            if (State == GameState.BuildPlace && ct == CellType.Empty)
            {
                if (!CanBuild(c))
                {
                    ShowMessage("벌레의 길을 완전히 막을 수는 없어요!");
                    return false;
                }
                PlaceDrink(c);
                return true;
            }
            Selected = ct == CellType.Menu || ct == CellType.EmptyCup ? c : (Cell?)null;
            return true;
        }

        // ------------------------------------------------------------------ 위치 바꾸기

        /// <summary>시그니처 메뉴만, 음료마다 1번만, 건설 단계에서 코인가 있을 때</summary>
        public bool CanStartMove(Cell c)
        {
            if (!IsBuildState || !InBounds(c) || Coins < MoveCost) return false;
            var t = towers[c.X, c.Y];
            return t != null && t.Signature.HasValue && !t.Moved;
        }

        public void StartMove(Cell c)
        {
            if (!CanStartMove(c)) return;
            MovingFrom = c;
            Selected = c;
            ShowMessage("옮길 칸을 누르세요 · 음료/빈 컵을 누르면 자리를 바꿔요");
            Touch();
        }

        public void CancelMove()
        {
            if (!MovingFrom.HasValue) return;
            MovingFrom = null;
            Touch();
        }

        /// <summary>
        /// MovingFrom의 음료을 target으로 옮김. 빈 칸이면 이동, 음료·빈 컵면 서로 자리를 바꿈.
        /// </summary>
        /// <returns>이동에 실패하면 false (코인 차감 없음)</returns>
        bool MoveTo(Cell target)
        {
            if (!MovingFrom.HasValue) return false;
            var from = MovingFrom.Value;
            var t = towers[from.X, from.Y];
            if (!IsBuildState || t == null || !t.Signature.HasValue || t.Moved)
            {
                MovingFrom = null;
                Touch();
                return false;
            }
            if (target == from)
            {
                CancelMove();
                ShowMessage("자리 옮기기를 취소했어요");
                return true;
            }
            if (Coins < MoveCost)
            {
                MovingFrom = null;
                ShowMessage("코인이 부족해요");
                Touch();
                return false;
            }
            switch (cells[target.X, target.Y])
            {
                case CellType.Waypoint:
                    ShowMessage("체크포인트에는 놓을 수 없어요");
                    return false;
                case CellType.Empty:
                    cells[from.X, from.Y] = CellType.Empty;
                    cells[target.X, target.Y] = CellType.Menu;
                    if (ComputeFullPath() == null)
                    {
                        cells[from.X, from.Y] = CellType.Menu;
                        cells[target.X, target.Y] = CellType.Empty;
                        ShowMessage("벌레의 길을 완전히 막을 수는 없어요!");
                        return false;
                    }
                    towers[from.X, from.Y] = null;
                    towers[target.X, target.Y] = t;
                    break;
                case CellType.Menu:
                {
                    var other = towers[target.X, target.Y];
                    // 상대가 시그니처 메뉴이면 그 음료도 이동한 것으로 침 (시그니처 메뉴은 1번만 이동)
                    if (other.Signature.HasValue && other.Moved)
                    {
                        ShowMessage($"{other.Stats.Name}은(는) 이미 옮겨서 자리를 바꿀 수 없어요");
                        return false;
                    }
                    towers[target.X, target.Y] = t;
                    towers[from.X, from.Y] = other;
                    other.Cell = from;
                    if (other.Signature.HasValue) other.Moved = true;
                    break;
                }
                case CellType.EmptyCup:
                    cells[target.X, target.Y] = CellType.Menu;
                    cells[from.X, from.Y] = CellType.EmptyCup;
                    towers[target.X, target.Y] = t;
                    towers[from.X, from.Y] = null;
                    break;
            }
            t.Cell = target;
            t.Moved = true;
            Coins -= MoveCost;
            MovingFrom = null;
            Selected = target;
            RecalcPath();
            ShowMessage($"자리를 옮겼어요 (-{MoveCost}코인)");
            return true;
        }

        // ------------------------------------------------------------------ 음료 / 빈 컵

        void SetEmptyCup(Cell c)
        {
            towers[c.X, c.Y] = null;
            cells[c.X, c.Y] = CellType.EmptyCup;
        }

        MenuTower PlaceDrink(Cell c)
        {
            var type = (Flavor)rng.Next(Enum.GetValues(typeof(Flavor)).Length);
            var quality = MenuDB.RollQuality(BaristaLevel, rng);
            var t = new MenuTower(c, type, quality) { DamageMult = DamageMult };
            t.Refresh();
            bool insta = rng.NextDouble() < MenuDB.InstaTeaChance;
            if (insta)
            {
                t.InstaBonus = MenuDB.FlavorStats(type, quality).Damage; // 패스 배율은 RefreshInstaTea에서 적용. 티가 된 음료 자신의 공격력도 5잔 합계에 포함
                t.SetMenu(type, quality, SignatureMenu.InstaTea);
            }
            cells[c.X, c.Y] = CellType.Menu;
            towers[c.X, c.Y] = t;
            RoundDrinks.Add(t);
            if (RoundDrinks.Count >= DrinksPerRound)
            {
                State = GameState.BuildChoose;
                Selected = null;
                ShowMessage("남길 음료 한 잔을 골라주세요");
            }
            if (insta) ShowMessage("전설 메뉴 등장! 인스타 감성 티를 꼭 남기세요");
            RecalcPath();
            return t;
        }

        /// <summary>남겨 둔 음료는 준비·선택 단계에 무료로 버릴 수 있음</summary>
        public bool CanDemolish(MenuTower t) =>
            t != null && t.Kept && IsBuildState && InBounds(t.Cell) && towers[t.Cell.X, t.Cell.Y] == t;

        /// <summary>남겨 둔 음료를 버림. 자리는 빈 칸이 됨 (코인 없음).</summary>
        public void Demolish(Cell c)
        {
            if (!InBounds(c)) return;
            var t = towers[c.X, c.Y];
            if (!CanDemolish(t)) return;
            towers[c.X, c.Y] = null;
            cells[c.X, c.Y] = CellType.Empty;
            if (Selected == c) Selected = null;
            if (MovingFrom == c) MovingFrom = null;
            ShowMessage($"{t.Stats.Name}을(를) 버렸어요");
            RecalcPath();
        }

        public void ClearCup(Cell c)
        {
            if (!IsBuildState || cells[c.X, c.Y] != CellType.EmptyCup || Coins < CupClearCost) return;
            Coins -= CupClearCost;
            cells[c.X, c.Y] = CellType.Empty;
            if (Selected == c) Selected = null;
            RecalcPath();
        }

        /// <summary>필드(남긴 음료 + 이번 라운드 음료)에 이 음료이 있는지. 조합법 창 표시용.</summary>
        public bool HasDrinkInCafe(DrinkKey key)
        {
            for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                var t = towers[x, y];
                if (t != null && !t.Signature.HasValue && t.Flavor == key.Flavor && t.Grade == key.Grade) return true;
            }
            return false;
        }

        public int CountSame(MenuTower t)
        {
            int n = 0;
            foreach (var g in RoundDrinks)
                if (!g.Signature.HasValue && g.Flavor == t.Flavor && g.Grade == t.Grade) n++;
            return n;
        }

        public bool CanBlend(MenuTower t, int levels) =>
            State == GameState.BuildChoose && RoundDrinks.Contains(t) && !t.Signature.HasValue &&
            (int)t.Grade + levels <= (int)Grade.Masterpiece &&
            CountSame(t) >= (levels >= 2 ? 4 : 2);

        /// <summary>조합법 재료를 찾음. t는 재료 중 하나여야 하며, 나머지는 필드의 다른 음료에서 찾음.</summary>
        public List<MenuTower> FindRecipeParts(MenuTower t, Recipe r)
        {
            if (t.Signature.HasValue) return null;
            var used = new List<MenuTower>();
            bool tUsed = false;
            foreach (var part in r.Parts)
            {
                if (!tUsed && t.Flavor == part.Flavor && t.Grade == part.Grade)
                {
                    used.Add(t);
                    tUsed = true;
                    continue;
                }
                MenuTower found = null;
                for (int x = 0; x < W && found == null; x++)
                for (int y = 0; y < H; y++)
                {
                    var o = towers[x, y];
                    if (o == null || o == t || o.Signature.HasValue || used.Contains(o)) continue;
                    if (o.Flavor == part.Flavor && o.Grade == part.Grade)
                    {
                        found = o;
                        break;
                    }
                }
                if (found == null) return null;
                used.Add(found);
            }
            return tUsed ? used : null;
        }

        public void Keep(MenuTower t) => FinishRound(t);

        public void Blend(MenuTower t, int levels)
        {
            if (!CanBlend(t, levels)) return;
            t.SetMenu(t.Flavor, (Grade)((int)t.Grade + levels), null);
            FinishRound(t);
        }

        public void MakeSignature(MenuTower t, Recipe r)
        {
            if (State != GameState.BuildChoose || !RoundDrinks.Contains(t)) return;
            var parts = FindRecipeParts(t, r);
            if (parts == null) return;
            foreach (var p in parts)
                if (p != t && !RoundDrinks.Contains(p)) SetEmptyCup(p.Cell); // 이미 남긴 재료는 빈 컵이 됨
            t.SetMenu(t.Flavor, t.Grade, r.Result);
            ShowMessage($"시그니처 메뉴 완성: {t.Stats.Name}");
            FinishRound(t);
        }

        // ------------------------------------------------------------------ 세트 메뉴

        /// <summary>매장에 이 시그니처 메뉴가 있는지. 레시피 창 표시용.</summary>
        public bool HasSignatureInCafe(SignatureMenu k)
        {
            for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                if (towers[x, y] != null && towers[x, y].Signature == k) return true;
            return false;
        }

        /// <summary>
        /// 세트 메뉴의 디저트를 찾음. drink는 이번 라운드의 재료 음료여야 하고,
        /// 매장에 레시피의 시그니처 디저트가 있어야 함. 없으면 null.
        /// </summary>
        public MenuTower FindSetDessert(MenuTower drink, SetRecipe r)
        {
            if (State != GameState.BuildChoose || !RoundDrinks.Contains(drink) || drink.Signature.HasValue) return null;
            if (drink.Flavor != r.Drink.Flavor || drink.Grade != r.Drink.Grade) return null;
            for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                var o = towers[x, y];
                if (o != null && o.Kept && o.Signature == r.Dessert) return o;
            }
            return null;
        }

        /// <summary>
        /// 디저트 자리를 세트 메뉴로 바꿈 (강화 단계 유지). 음료는 디저트에 곁들여져 사라지고
        /// 이번 라운드의 다른 음료는 평소처럼 빈 컵이 됨. 그다음 웨이브 시작.
        /// </summary>
        public void MakeSet(MenuTower drink, SetRecipe r)
        {
            var dessert = FindSetDessert(drink, r);
            if (dessert == null) return;
            MovingFrom = null;
            foreach (var g in RoundDrinks) SetEmptyCup(g.Cell);
            RoundDrinks.Clear();
            int level = dessert.SignatureLevel;
            dessert.SetMenu(dessert.Flavor, dessert.Grade, r.Result);
            dessert.SignatureLevel = level;
            dessert.Refresh();
            Selected = dessert.Cell;
            ShowMessage($"세트 메뉴 완성: {dessert.Stats.Name}");
            RecalcPath();
            StartWave();
        }

        // ------------------------------------------------------------------ 코스 메뉴

        /// <summary>
        /// 코스 메뉴 재료를 찾음. t는 매장에 남긴 재료 시그니처 중 하나여야 하고,
        /// 나머지는 매장에 남긴 다른 시그니처에서 찾음. 없으면 null. (준비·선택 단계에서만)
        /// </summary>
        public List<MenuTower> FindCourseParts(MenuTower t, CourseRecipe r)
        {
            if (t == null || !t.Kept || !IsBuildState || !t.Signature.HasValue || Array.IndexOf(r.Parts, t.Signature.Value) < 0) return null;
            var used = new List<MenuTower> { t };
            bool tUsed = false;
            foreach (var part in r.Parts)
            {
                if (!tUsed && t.Signature == part)
                {
                    tUsed = true;
                    continue;
                }
                MenuTower found = null;
                foreach (var o in towers)
                {
                    if (o == null || !o.Kept || used.Contains(o) || o.Signature != part) continue;
                    found = o;
                    break;
                }
                if (found == null) return null;
                used.Add(found);
            }
            return used;
        }

        /// <summary>t 자리를 코스 메뉴로 바꾸고 나머지 재료는 빈 컵이 됨. 강화 단계는 재료 중 가장 높은 단계.</summary>
        public void MakeCourse(MenuTower t, CourseRecipe r)
        {
            var parts = FindCourseParts(t, r);
            if (parts == null) return;
            int level = 0;
            foreach (var p in parts) level = Math.Max(level, p.SignatureLevel);
            foreach (var p in parts)
                if (p != t) SetEmptyCup(p.Cell);
            MovingFrom = null;
            t.SetMenu(t.Flavor, t.Grade, r.Result);
            t.SignatureLevel = level;
            t.Refresh();
            Selected = t.Cell;
            ShowMessage($"코스 메뉴 완성: {t.Stats.Name}");
            RecalcPath();
        }

        void FinishRound(MenuTower chosen)
        {
            if (State != GameState.BuildChoose || !RoundDrinks.Contains(chosen)) return;
            MovingFrom = null;
            if (chosen.Signature == SignatureMenu.InstaTea)
            {
                // 나머지 음료는 빈 컵이 되므로 사라지기 전에 공격력을 티에 옮겨 둠
                foreach (var g in RoundDrinks)
                {
                    if (g == chosen) continue;
                    chosen.InstaBonus += MenuDB.FlavorStats(g.Flavor, g.Grade).Damage;
                }
            }
            foreach (var g in RoundDrinks)
                if (g != chosen) SetEmptyCup(g.Cell);
            RoundDrinks.Clear();
            chosen.Kept = true;
            Selected = chosen.Cell;
            RecalcPath();
            StartWave();
        }

        /// <summary>이번 라운드에 아직 음료를 놓지 않았고 공격할 음료가 있으면 패스 가능</summary>
        public bool CanPassRound
        {
            get
            {
                if (State != GameState.BuildPlace || RoundDrinks.Count > 0) return false;
                foreach (var t in towers)
                    if (t != null && t.Kept) return true;
                return false;
            }
        }

        /// <summary>이번 라운드 음료 5잔을 놓지 않는 대신 모든 음료 공격력을 영구히 1% 올리고 웨이브 시작</summary>
        public void PassRound()
        {
            if (!CanPassRound) return;
            PassCount++;
            foreach (var t in towers)
            {
                if (t == null) continue;
                t.DamageMult = DamageMult;
                t.Refresh();
            }
            MovingFrom = null;
            Selected = null;
            StartWave();
            ShowMessage($"배치 패스! 모든 음료 공격력 +{DamageBonusPercent:0.##}%");
        }

        public void UpgradeSignature(MenuTower t)
        {
            if (!t.Signature.HasValue || !MenuDB.CanUpgrade(t.Signature.Value) || !t.Kept || !IsBuildState || t.SignatureLevel >= MenuDB.MaxSignatureLevel) return;
            int cost = MenuDB.SignatureUpgradeCost(t.SignatureLevel);
            if (Coins < cost) return;
            Coins -= cost;
            t.SignatureLevel++;
            t.Refresh();
            Touch();
        }

        public void UpgradeBarista()
        {
            if (BaristaLevel >= MenuDB.MaxBaristaLevel || IsOver) return;
            int cost = MenuDB.BaristaLevelCost(BaristaLevel);
            if (Coins < cost) return;
            Coins -= cost;
            BaristaLevel++;
            ShowMessage($"바리스타 레벨 {BaristaLevel}");
            Touch();
        }

        // ------------------------------------------------------------------ 웨이브

        /// <summary>1~200웨이브에 나오는 모든 몹의 체력 합계 = 최종 보스 체력</summary>
        double FinalBossHp()
        {
            double sum = 0;
            for (int w = 1; w < MaxWave; w++)
            {
                var info = WaveInfoFor(w);
                sum += info.Hp * info.Count;
            }
            return sum;
        }

        public WaveInfo WaveInfoFor(int w)
        {
            // 최종 보스는 도착하면 바로 영업 종료 (놓치고 승리하는 일이 없도록)
            if (w >= MaxWave)
                return new WaveInfo { Count = 1, Hp = FinalBossHp(), Speed = 1.1f, Interval = 1f, Bounty = 20 + w, Leak = 9999, Boss = true, Label = "최종 보스 생쥐 대왕" };

            // 체력은 라운드마다 21%씩 오르고, 5의 배수 라운드에서는 상승폭이 2배(42%)가 됨
            int doubled = w / 5;
            double hp = 15.0 * System.Math.Pow(1.21, w - 1 - doubled) * System.Math.Pow(1.42, doubled) + 4.0 * w;
            int bounty = 1 + w / 4;
            if (w % 10 == 0)
                return new WaveInfo { Count = 1, Hp = hp * 8, Speed = 1.1f, Interval = 1f, Bounty = 20 + w, Leak = 5, Boss = true, Label = "생쥐 대장" };
            if (w % 4 == 0)
                return new WaveInfo { Count = 10, Hp = hp * 0.8, Speed = 1.6f, Interval = 1f, Bounty = bounty, Leak = 1, Flying = true, Label = "파리 떼 (동선 무시)" };
            if (w % 5 == 3)
                return new WaveInfo { Count = 10, Hp = hp * 0.7, Speed = 1.6f * 1.7f, Interval = 0.6f, Bounty = bounty, Leak = 1, Fast = true, Label = "바퀴벌레" };
            return new WaveInfo { Count = 10, Hp = hp, Speed = 1.6f, Interval = 1f, Bounty = bounty, Leak = 1, Label = "개미 떼" };
        }

        void StartWave()
        {
            waveStartSave = Capture(GameState.Wave);
            CurrentWave = WaveInfoFor(Wave);
            toSpawn = CurrentWave.Count;
            spawnTimer = 0.3f;
            State = GameState.Wave;
            ShowMessage($"웨이브 {Wave} - {CurrentWave.Label}");
            Touch();
        }

        void SpawnEnemy()
        {
            var w = CurrentWave;
            float hue = (Wave * 0.137f) % 1f;
            Color32 color = w.Boss ? MenuInfo.Argb(0xFFA60D1F) : (Color32)UnityEngine.Color.HSVToRGB(hue, 0.65f, 0.95f);
            Pests.Add(new Pest(w.Hp, w.Speed, w.Flying, w.Boss, w.Bounty, w.Leak, w.Flying ? airPos : groundPos, color));
        }

        void EndWave()
        {
            int bonus = 10 + Wave * 2;
            Coins += bonus;
            if (Wave >= MaxWave)
            {
                State = GameState.Victory;
                ShowMessage("모든 벌레를 막아냈어요! 카페 영업 성공!");
                Touch();
                return;
            }
            Wave++;
            State = GameState.BuildPlace;
            ShowMessage($"웨이브 클리어! +{bonus}코인 · 음료 5잔을 놓으세요");
            Touch();
        }

        void Leak(Pest e)
        {
            e.Dead = true;
            if (State != GameState.Wave) return;
            Desserts -= e.LeakDamage;
            Leaked?.Invoke(e);
            if (Desserts <= 0)
            {
                Desserts = 0;
                State = GameState.GameOver;
                ShowMessage("디저트를 모두 빼앗겼어요");
                Touch();
            }
        }

        // ------------------------------------------------------------------ 전투

        /// <summary>게임 시간 dt(초)만큼 진행 (배속이 이미 적용된 값)</summary>
        public void Update(float dt)
        {
            float remaining = dt;
            while (remaining > 0f)
            {
                float s = Mathf.Min(remaining, MaxStep);
                Step(s);
                remaining -= s;
            }
        }

        /// <summary>실제 시간 기준 UI 타이머</summary>
        public void TickUi(float dt)
        {
            if (MessageTimer > 0f) MessageTimer -= dt;
        }

        void Step(float dt)
        {
            Time += dt;
            if (!IsOver) TotalTime += dt;
            if (State == GameState.Wave && Wave >= MaxWave) BossStageTime += dt; // 최종 보스 웨이브만

            if (State == GameState.Wave && toSpawn > 0)
            {
                spawnTimer -= dt;
                if (spawnTimer <= 0f)
                {
                    SpawnEnemy();
                    toSpawn--;
                    spawnTimer = CurrentWave.Interval;
                }
            }

            if (State != GameState.GameOver)
            {
                for (int i = 0; i < Pests.Count; i++)
                {
                    var e = Pests[i];
                    if (e.Dead) continue;
                    if (e.PoisonTimer > 0f)
                    {
                        e.PoisonTimer -= dt;
                        Damage(e, e.PoisonDps * dt, e.PoisonSrc);
                        if (e.PoisonTimer <= 0f) e.PoisonDps = 0f;
                        if (e.Dead) continue;
                    }
                    if (e.Move(dt)) Leak(e);
                }
            }

            if (State == GameState.Wave)
            {
                for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    var t = towers[x, y];
                    if (t != null && t.Kept) UpdateTower(t, dt);
                }
            }

            for (int i = 0; i < Droplets.Count; i++)
            {
                var p = Droplets[i];
                if (p.Done) continue;
                if (!p.Target.Dead)
                {
                    p.AimX = p.Target.X;
                    p.AimY = p.Target.Y;
                }
                float dx = p.AimX - p.X;
                float dy = p.AimY - p.Y;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float mv = DropletSpeed * dt;
                if (d <= mv)
                {
                    p.X = p.AimX; p.Y = p.AimY;
                    p.Done = true;
                    ApplyHit(p.Src, p.Target, p.AimX, p.AimY, p.Damage, p.Crit);
                }
                else
                {
                    p.X += dx / d * mv;
                    p.Y += dy / d * mv;
                }
            }

            foreach (var b in Beams) b.Ttl -= dt;

            Pests.RemoveAll(e => e.Dead);
            Droplets.RemoveAll(p => p.Done);
            Beams.RemoveAll(b => b.Ttl <= 0f);

            if (State == GameState.Wave && toSpawn == 0 && Pests.Count == 0) EndWave();
        }

        void UpdateTower(MenuTower t, float dt)
        {
            t.Cooldown -= dt;
            if (t.Cooldown > 0f) return;
            var s = t.Stats;
            float r2 = s.Range * s.Range;
            float tx = t.Cell.X;
            float ty = t.Cell.Y;
            targetBuf.Clear();
            foreach (var e in Pests)
            {
                if (e.Dead) continue;
                float dx = e.X - tx, dy = e.Y - ty;
                if (dx * dx + dy * dy <= r2) targetBuf.Add(e);
            }
            if (targetBuf.Count == 0) return;
            targetBuf.Sort((a, b) => b.Progress.CompareTo(a.Progress)); // 가장 멀리 간 적 우선
            int n = Mathf.Min(s.Targets, targetBuf.Count);
            for (int i = 0; i < n; i++)
            {
                float dmg = s.Damage;
                bool crit = false;
                if (s.CritChance > 0f && rng.NextDouble() < s.CritChance)
                {
                    dmg *= s.CritMult;
                    crit = true;
                }
                Droplets.Add(new Droplet(t, targetBuf[i], tx, ty, dmg, crit));
            }
            t.Cooldown = s.Cooldown;
        }

        void ApplyHit(MenuTower src, Pest target, float x, float y, float dmg, bool crit = false)
        {
            if (!target.Dead)
            {
                DamageShown?.Invoke(target, dmg * (target.Flying ? src.Stats.AirMult : 1f), crit);
                Hit(src, target, dmg);
            }
            var s = src.Stats;
            Impact?.Invoke(src, x, y);
            if (s.SplashRadius > 0f)
            {
                float r2 = s.SplashRadius * s.SplashRadius;
                foreach (var e in Pests)
                {
                    if (e.Dead || e == target) continue;
                    float dx = e.X - x, dy = e.Y - y;
                    if (dx * dx + dy * dy <= r2) Hit(src, e, dmg * s.SplashPct);
                }
            }
            if (s.ChainCount > 0) Chain(src, target, x, y, dmg);
        }

        /// <summary>연쇄 번개: 마지막으로 맞은 위치에서 가장 가까운, 아직 안 맞은 적에게 튕김</summary>
        void Chain(MenuTower src, Pest first, float x, float y, float dmg)
        {
            var s = src.Stats;
            hitBuf.Clear();
            hitBuf.Add(first);
            float r2 = s.ChainRange * s.ChainRange;
            float cx = x, cy = y, d = dmg;
            for (int k = 0; k < s.ChainCount; k++)
            {
                Pest best = null;
                float bestD = r2;
                foreach (var e in Pests)
                {
                    if (e.Dead || hitBuf.Contains(e)) continue;
                    float dx = e.X - cx, dy = e.Y - cy;
                    float dd = dx * dx + dy * dy;
                    if (dd <= bestD)
                    {
                        bestD = dd;
                        best = e;
                    }
                }
                if (best == null) return;
                d *= s.ChainPct;
                Beams.Add(new Beam(cx, cy, best.X, best.Y, s.Color));
                Hit(src, best, d);
                hitBuf.Add(best);
                cx = best.X;
                cy = best.Y;
            }
        }

        void Hit(MenuTower src, Pest e, float dmg)
        {
            var s = src.Stats;
            if (s.SlowPct > 0f) e.ApplySlow(s.SlowPct, s.SlowDur);
            if (s.PoisonDps > 0f) e.ApplyPoison(s.PoisonDps, s.PoisonDur, src);
            if (s.StunChance > 0f && rng.NextDouble() < s.StunChance)
            {
                float dur = e.Boss ? s.StunDur * 0.5f : s.StunDur;
                e.StunTimer = Mathf.Max(e.StunTimer, dur);
            }
            Damage(e, dmg * (e.Flying ? s.AirMult : 1f), src);
            Hurt?.Invoke(e);
            if (s.ExecutePct > 0f && !e.Dead && !e.Boss && e.Hp <= e.MaxHp * s.ExecutePct) Damage(e, e.Hp + 1.0, src);
        }

        void Damage(Pest e, double dmg, MenuTower src)
        {
            if (e.Dead || dmg <= 0f) return;
            double applied = System.Math.Min(e.Hp, dmg);
            if (e.Boss && State == GameState.Wave && Wave >= MaxWave) BossDamage += applied; // 최종 보스에게 준 피해만
            e.Hp -= dmg;
            if (src != null) src.TotalDamage += (float)applied;
            if (e.Hp <= 0f)
            {
                e.Dead = true;
                if (src != null) src.Kills++;
                int gold = e.Bounty + (src != null ? src.Stats.BountyBonus : 0);
                Coins += gold;
                Killed?.Invoke(e, gold);
            }
        }

        void ShowMessage(string m)
        {
            Message = m;
            MessageTimer = 3.5f;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CafeTowerDefense
{
    public enum Flavor { Strawberry, Ice, Matcha, Lemon, Blueberry, Milk }

    public enum Grade { Light, Regular, Strong, Premium, Masterpiece }

    public enum SignatureMenu
    {
        // 하급
        IceFrappe, MatchaLatte, HotChocoBomb, EspressoShot, SparklingLemonade, HoneyBread, JellyPudding, RedVelvetCake,
        // 중급
        MacaronCannon, MatchaBingsu, Affogato,
        // 상급
        SignatureParfait, RainbowCake,
        // 세트 메뉴 (시그니처 디저트 + 음료)
        HoneyBrunchSet, PuddingIceSet, RedVelvetTeaSet, MacaronPartySet, MatchaDessertSet, ParfaitSpecialSet, RainbowPartySet,
    }

    public static class MenuInfo
    {
        public static Color32 Argb(uint argb) =>
            new Color32((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));

        static readonly string[] TypeLabels = { "딸기", "얼음", "말차", "레몬", "블루베리", "우유" };
        static readonly uint[] TypeColors = { 0xFFFF4D6D, 0xFF7FD6FF, 0xFF7CC46A, 0xFFFFE04D, 0xFF8E6BFF, 0xFFFFF6E8 };
        static readonly string[] QualityLabels = { "연한", "보통", "진한", "특제", "명품" };
        static readonly float[] QualityDamage = { 8f, 18f, 40f, 90f, 200f };
        static readonly string[] SpecialLabels =
        {
            "아이스 프라페", "말차 라떼", "핫초코 폭탄", "에스프레소 샷", "탄산 레모네이드", "허니 브레드", "탱글 푸딩", "레드벨벳 케이크",
            "마카롱 대포", "말차 빙수", "아포가토", "시그니처 파르페", "레인보우 케이크",
            "허니 브런치 세트", "푸딩 아이스 세트", "레드벨벳 티타임 세트", "마카롱 파티 세트", "말차 디저트 세트",
            "파르페 스페셜 세트", "레인보우 파티 세트",
        };

        public static string Label(this Flavor t) => TypeLabels[(int)t];
        public static Color32 Color(this Flavor t) => Argb(TypeColors[(int)t]);
        public static string Label(this Grade q) => QualityLabels[(int)q];
        public static float BaseDamage(this Grade q) => QualityDamage[(int)q];
        public static string Label(this SignatureMenu k) => SpecialLabels[(int)k];
    }

    public class MenuStats
    {
        public string Name;
        public Color32 Color;
        public float Damage;
        public float Range = 3f;
        public float Cooldown = 1f;
        public float SplashRadius;
        public float SplashPct;
        public float SlowPct;
        public float SlowDur;
        public float PoisonDps;
        public float PoisonDur;
        public int Targets = 1;
        public float CritChance;
        public float CritMult = 1f;
        public float AirMult = 1f;
        /// <summary>연쇄 번개: 맞은 적에서 가까운 적에게 튕기는 횟수</summary>
        public int ChainCount;
        public float ChainRange;
        /// <summary>튕길 때마다 피해 배율</summary>
        public float ChainPct;
        public float StunChance;
        public float StunDur;
        /// <summary>이 음료이 처치한 적이 주는 추가 코인</summary>
        public int BountyBonus;
        /// <summary>체력이 이 비율 이하로 떨어진 적을 즉시 처치 (보스 제외)</summary>
        public float ExecutePct;
        public string Desc = "";

        public MenuStats Clone() => (MenuStats)MemberwiseClone();
    }

    public readonly struct DrinkKey : IEquatable<DrinkKey>
    {
        public readonly Flavor Flavor;
        public readonly Grade Grade;

        public DrinkKey(Flavor t, Grade q)
        {
            Flavor = t;
            Grade = q;
        }

        public bool Equals(DrinkKey o) => Flavor == o.Flavor && Grade == o.Grade;
        public override bool Equals(object obj) => obj is DrinkKey k && Equals(k);
        public override int GetHashCode() => (int)Flavor * 16 + (int)Grade;
        public override string ToString() => $"{Grade.Label()} {Flavor.Label()}";
    }

    public class Recipe
    {
        public readonly SignatureMenu Result;
        public readonly DrinkKey[] Parts;

        public Recipe(SignatureMenu result, params DrinkKey[] parts)
        {
            Result = result;
            Parts = parts;
        }

        /// <summary>0 = 하급, 1 = 중급, 2 = 상급 (가장 높은 재료 등급 기준)</summary>
        public int Tier
        {
            get
            {
                var max = Parts.Max(p => p.Grade);
                return max == Grade.Masterpiece ? 2 : max == Grade.Premium ? 1 : 0;
            }
        }
    }

    /// <summary>
    /// 세트 메뉴 레시피: 매장에 있는 시그니처 디저트(Dessert) + 이번 라운드 음료(Drink) 1잔.
    /// 디저트 자리가 세트 메뉴로 바뀌고 음료는 디저트에 곁들여져 사라짐.
    /// </summary>
    public class SetRecipe
    {
        public readonly SignatureMenu Result;
        public readonly SignatureMenu Dessert;
        public readonly DrinkKey Drink;

        public SetRecipe(SignatureMenu result, SignatureMenu dessert, DrinkKey drink)
        {
            Result = result;
            Dessert = dessert;
            Drink = drink;
        }
    }

    /// <summary>음료 수치 / 확률표 / 조합법. 밸런스 조절은 여기서.</summary>
    public static class MenuDB
    {
        // [레벨][깨진, 흠있는, 일반, 무결점, 완벽한] (%)
        static readonly int[][] ChanceTable =
        {
            new[] { 100, 0, 0, 0, 0 },
            new[] { 70, 30, 0, 0, 0 },
            new[] { 60, 30, 10, 0, 0 },
            new[] { 50, 30, 20, 0, 0 },
            new[] { 40, 30, 20, 10, 0 },
            new[] { 30, 30, 30, 10, 0 },
            new[] { 20, 30, 30, 20, 0 },
            new[] { 10, 30, 30, 30, 0 },
            new[] { 0, 30, 30, 30, 10 },
        };

        public static int MaxBaristaLevel => ChanceTable.Length - 1;

        /// <summary>해당 품질 레벨의 [깨진..완벽한] 확률(%) 복사본</summary>
        public static int[] Chances(int level) => (int[])ChanceTable[Mathf.Clamp(level, 0, MaxBaristaLevel)].Clone();
        public static int BaristaLevelCost(int level) => 20 + 20 * level;

        public const int MaxSignatureLevel = 5;
        public static int SignatureUpgradeCost(int level) => 60 + 60 * level;

        public static readonly string[] TierNames = { "기본", "스페셜", "마스터", "세트" };
        public const int SetTier = 3;

        static DrinkKey K(Flavor t, Grade q) => new DrinkKey(t, q);
        const Grade C = Grade.Light;
        const Grade F = Grade.Regular;
        const Grade N = Grade.Strong;
        const Grade FL = Grade.Premium;
        const Grade P = Grade.Masterpiece;

        public static readonly List<Recipe> Recipes = new List<Recipe>
        {
            // ---- 하급: 일반 + 흠있는/깨진 ----
            new Recipe(SignatureMenu.IceFrappe, K(Flavor.Ice, N), K(Flavor.Milk, F), K(Flavor.Blueberry, C)),
            new Recipe(SignatureMenu.MatchaLatte, K(Flavor.Matcha, N), K(Flavor.Strawberry, F), K(Flavor.Lemon, C)),
            new Recipe(SignatureMenu.HotChocoBomb, K(Flavor.Strawberry, N), K(Flavor.Lemon, F), K(Flavor.Milk, C)),
            new Recipe(SignatureMenu.EspressoShot, K(Flavor.Blueberry, N), K(Flavor.Ice, F), K(Flavor.Matcha, F)),
            new Recipe(SignatureMenu.SparklingLemonade, K(Flavor.Lemon, N), K(Flavor.Ice, F), K(Flavor.Strawberry, C)),
            new Recipe(SignatureMenu.HoneyBread, K(Flavor.Milk, N), K(Flavor.Lemon, C), K(Flavor.Matcha, C)),
            new Recipe(SignatureMenu.JellyPudding, K(Flavor.Ice, N), K(Flavor.Blueberry, F), K(Flavor.Lemon, F)),
            new Recipe(SignatureMenu.RedVelvetCake, K(Flavor.Strawberry, N), K(Flavor.Matcha, F), K(Flavor.Blueberry, C)),
            // ---- 중급: 무결점 + 일반 ----
            new Recipe(SignatureMenu.MacaronCannon, K(Flavor.Blueberry, FL), K(Flavor.Milk, N), K(Flavor.Ice, N)),
            new Recipe(SignatureMenu.MatchaBingsu, K(Flavor.Matcha, FL), K(Flavor.Lemon, N), K(Flavor.Strawberry, N)),
            new Recipe(SignatureMenu.Affogato, K(Flavor.Milk, FL), K(Flavor.Lemon, N), K(Flavor.Ice, N)),
            // ---- 상급: 완벽한 + 무결점 ----
            new Recipe(SignatureMenu.SignatureParfait, K(Flavor.Milk, P), K(Flavor.Strawberry, FL), K(Flavor.Ice, FL)),
            new Recipe(SignatureMenu.RainbowCake, K(Flavor.Blueberry, P), K(Flavor.Matcha, FL), K(Flavor.Lemon, FL)),
        };

        public static Recipe RecipeFor(SignatureMenu k) => Recipes.First(r => r.Result == k);

        /// <summary>이 음료가 재료로 들어가는 조합법</summary>
        public static List<Recipe> RecipesUsing(DrinkKey key) => Recipes.Where(r => r.Parts.Contains(key)).ToList();

        // ---- 세트 메뉴: 디저트 단계에 맞춰 진한(기본) → 특제(스페셜) → 명품(마스터) 음료가 필요
        public static readonly List<SetRecipe> SetRecipes = new List<SetRecipe>
        {
            new SetRecipe(SignatureMenu.HoneyBrunchSet, SignatureMenu.HoneyBread, K(Flavor.Milk, N)),
            new SetRecipe(SignatureMenu.PuddingIceSet, SignatureMenu.JellyPudding, K(Flavor.Ice, N)),
            new SetRecipe(SignatureMenu.RedVelvetTeaSet, SignatureMenu.RedVelvetCake, K(Flavor.Strawberry, N)),
            new SetRecipe(SignatureMenu.MacaronPartySet, SignatureMenu.MacaronCannon, K(Flavor.Blueberry, FL)),
            new SetRecipe(SignatureMenu.MatchaDessertSet, SignatureMenu.MatchaBingsu, K(Flavor.Matcha, FL)),
            new SetRecipe(SignatureMenu.ParfaitSpecialSet, SignatureMenu.SignatureParfait, K(Flavor.Lemon, P)),
            new SetRecipe(SignatureMenu.RainbowPartySet, SignatureMenu.RainbowCake, K(Flavor.Strawberry, P)),
        };

        public static bool IsSet(SignatureMenu k) => k >= SignatureMenu.HoneyBrunchSet;
        public static SetRecipe SetFor(SignatureMenu k) => SetRecipes.First(r => r.Result == k);

        /// <summary>이 음료가 곁들여지는 세트 메뉴</summary>
        public static List<SetRecipe> SetsUsingDrink(DrinkKey key) => SetRecipes.Where(r => r.Drink.Equals(key)).ToList();

        /// <summary>이 디저트로 만들 수 있는 세트 메뉴 (없으면 null)</summary>
        public static SetRecipe SetUsingDessert(SignatureMenu dessert) => SetRecipes.FirstOrDefault(r => r.Dessert == dessert);

        /// <summary>0 기본 · 1 스페셜 · 2 마스터 · 3 세트</summary>
        public static int TierOf(SignatureMenu k) => IsSet(k) ? SetTier : RecipeFor(k).Tier;

        public static Grade RollQuality(int level, System.Random rng)
        {
            var row = ChanceTable[Mathf.Clamp(level, 0, MaxBaristaLevel)];
            int r = rng.Next(100);
            int acc = 0;
            for (int i = 0; i < row.Length; i++)
            {
                acc += row[i];
                if (r < acc) return (Grade)i;
            }
            return Grade.Light;
        }

        public static string ChanceText(int level)
        {
            var row = ChanceTable[Mathf.Clamp(level, 0, MaxBaristaLevel)];
            return string.Join("  ", Enumerable.Range(0, row.Length).Where(i => row[i] > 0)
                .Select(i => $"{((Grade)i).Label()} {row[i]}%"));
        }

        public static MenuStats FlavorStats(Flavor t, Grade q)
        {
            int qi = (int)q;
            float bd = q.BaseDamage();
            var s = new MenuStats { Name = $"{q.Label()} {t.Label()}", Color = t.Color(), Damage = bd };
            switch (t)
            {
                case Flavor.Strawberry:
                    s.SplashRadius = 1.2f + 0.1f * qi; s.SplashPct = 0.5f;
                    break;
                case Flavor.Ice:
                    s.Damage = bd * 0.7f; s.Range = 3.2f; s.SlowPct = 0.2f + 0.06f * qi; s.SlowDur = 2f;
                    break;
                case Flavor.Matcha:
                    s.Damage = bd * 0.6f; s.Cooldown = 0.9f; s.PoisonDps = bd * 0.5f; s.PoisonDur = 4f;
                    break;
                case Flavor.Lemon:
                    s.Damage = bd * 0.55f; s.Range = 3.2f; s.Targets = 2 + qi / 2;
                    break;
                case Flavor.Blueberry:
                    s.Damage = bd * 0.9f; s.Range = 4.5f + 0.1f * qi; s.Cooldown = 1.3f; s.AirMult = 2f;
                    break;
                case Flavor.Milk:
                    s.Damage = bd * 1.3f; s.Cooldown = 0.85f; s.CritChance = 0.2f + 0.04f * qi; s.CritMult = 2.5f;
                    break;
            }
            return s;
        }

        public static MenuStats SignatureStats(SignatureMenu k, int level = 0)
        {
            string n = k.Label();
            MenuStats b;
            switch (k)
            {
                case SignatureMenu.IceFrappe:
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFF99EBFF), Damage = 70f, Range = 4f, Cooldown = 0.8f,
                        SlowPct = 0.5f, SlowDur = 2.5f, SplashRadius = 1.5f, SplashPct = 0.4f,
                        Desc = "주변 벌레를 얼려 크게 둔화시켜요." };
                    break;
                case SignatureMenu.MatchaLatte:
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFF9BE07A), Damage = 45f, Range = 3.8f, Cooldown = 0.7f,
                        PoisonDps = 60f, PoisonDur = 4f, Targets = 3,
                        Desc = "여러 벌레에게 진한 말차를 끼얹어 계속 피해를 줘요." };
                    break;
                case SignatureMenu.HotChocoBomb:
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFFD9824A), Damage = 150f, Range = 3.5f, Cooldown = 1.1f,
                        SplashRadius = 2f, SplashPct = 0.7f,
                        Desc = "뜨거운 초콜릿이 넓게 터져요." };
                    break;
                case SignatureMenu.EspressoShot:
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFFC08552), Damage = 260f, Range = 5.5f, Cooldown = 1.4f,
                        AirMult = 2.5f, CritChance = 0.2f, CritMult = 2f,
                        Desc = "멀리 있는 벌레를 강하게 저격해요." };
                    break;
                case SignatureMenu.SparklingLemonade:
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFFFFF07A), Damage = 55f, Range = 3.8f, Cooldown = 0.9f,
                        ChainCount = 4, ChainRange = 2.2f, ChainPct = 0.8f,
                        Desc = "톡 쏘는 탄산이 주변 벌레에게 4번 튕겨요." };
                    break;
                case SignatureMenu.HoneyBread:
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFFFFB31A), Damage = 40f, Range = 3.5f, Cooldown = 0.8f,
                        Targets = 2, BountyBonus = 2,
                        Desc = "이 메뉴로 잡은 벌레는 코인을 2 더 줘요." };
                    break;
                case SignatureMenu.JellyPudding:
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFFFFD27F), Damage = 60f, Range = 3.5f, Cooldown = 1f,
                        StunChance = 0.25f, StunDur = 1f, SlowPct = 0.3f, SlowDur = 2f,
                        Desc = "벌레를 푸딩에 가둬 기절시켜요." };
                    break;
                case SignatureMenu.RedVelvetCake:
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFFC4163A), Damage = 90f, Range = 3.2f, Cooldown = 0.9f,
                        SplashRadius = 1.3f, SplashPct = 0.5f, ExecutePct = 0.15f,
                        Desc = "빈사 상태의 벌레를 즉시 처치해요." };
                    break;
                case SignatureMenu.MacaronCannon:
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFFFF9EC7), Damage = 380f, Range = 6f, Cooldown = 1.3f,
                        SplashRadius = 1.8f, SplashPct = 0.6f, AirMult = 2.5f,
                        Desc = "먼 거리를 마카롱으로 광역 포격해요. 파리에 강해요." };
                    break;
                case SignatureMenu.MatchaBingsu:
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFF5FBF6A), Damage = 160f, Range = 4.2f, Cooldown = 0.8f,
                        PoisonDps = 200f, PoisonDur = 4f, Targets = 4, SlowPct = 0.2f, SlowDur = 2f,
                        Desc = "네 마리에게 아주 진한 말차를 퍼뜨려요." };
                    break;
                case SignatureMenu.Affogato:
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFFE8D2B0), Damage = 420f, Range = 4f, Cooldown = 1.2f,
                        ChainCount = 3, ChainRange = 2.5f, ChainPct = 0.75f, StunChance = 0.35f, StunDur = 1.2f,
                        CritChance = 0.25f, CritMult = 2f,
                        Desc = "튀는 에스프레소로 여러 벌레를 기절시켜요." };
                    break;
                case SignatureMenu.SignatureParfait:
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFFFFF2B3), Damage = 1500f, Range = 5f, Cooldown = 1f,
                        SplashRadius = 2.2f, SplashPct = 0.6f, CritChance = 0.3f, CritMult = 3f,
                        Desc = "카페 최고의 메뉴. 모든 벌레를 녹여버려요." };
                    break;
                case SignatureMenu.RainbowCake:
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFFE040FB), Damage = 900f, Range = 5.5f, Cooldown = 0.7f,
                        ChainCount = 6, ChainRange = 3f, ChainPct = 0.85f, PoisonDps = 400f, PoisonDur = 4f, AirMult = 2f,
                        Desc = "무지개 크림이 6번 튕기며 계속 피해를 줘요." };
                    break;

                // ---- 세트 메뉴: 디저트 능력을 크게 키우고 곁들인 음료의 특징을 더함
                case SignatureMenu.HoneyBrunchSet: // 허니 브레드 + 진한 우유(치명타)
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFFFFC14D), Damage = 220f, Range = 4f, Cooldown = 0.7f,
                        Targets = 3, BountyBonus = 5, CritChance = 0.25f, CritMult = 2.5f,
                        Desc = "꿀과 우유의 브런치. 잡은 벌레마다 코인을 5 더 줘요." };
                    break;
                case SignatureMenu.PuddingIceSet: // 탱글 푸딩 + 진한 얼음(둔화)
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFFBDE8FF), Damage = 260f, Range = 4.2f, Cooldown = 0.9f,
                        StunChance = 0.35f, StunDur = 1.3f, SlowPct = 0.45f, SlowDur = 2.5f, SplashRadius = 1.6f, SplashPct = 0.5f,
                        Desc = "차가운 푸딩에 갇힌 벌레가 꼼짝 못 해요." };
                    break;
                case SignatureMenu.RedVelvetTeaSet: // 레드벨벳 케이크 + 진한 딸기(범위)
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFFE0465E), Damage = 480f, Range = 3.8f, Cooldown = 0.85f,
                        SplashRadius = 1.8f, SplashPct = 0.6f, ExecutePct = 0.25f,
                        Desc = "빈사 상태의 벌레를 넓은 범위에서 처치해요." };
                    break;
                case SignatureMenu.MacaronPartySet: // 마카롱 대포 + 특제 블루베리(파리)
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFFFFB3DA), Damage = 900f, Range = 6.8f, Cooldown = 1.2f,
                        SplashRadius = 2.2f, SplashPct = 0.65f, AirMult = 3f,
                        Desc = "하늘까지 닿는 마카롱 폭격. 파리에 아주 강해요." };
                    break;
                case SignatureMenu.MatchaDessertSet: // 말차 빙수 + 특제 말차(지속 피해)
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFF6FCF7A), Damage = 380f, Range = 4.6f, Cooldown = 0.75f,
                        PoisonDps = 650f, PoisonDur = 4f, Targets = 5, SlowPct = 0.25f, SlowDur = 2f,
                        Desc = "다섯 마리에게 아주 진한 말차를 퍼부어요." };
                    break;
                case SignatureMenu.ParfaitSpecialSet: // 시그니처 파르페 + 명품 레몬(동시 공격)
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFFFFF0B8), Damage = 4200f, Range = 5.5f, Cooldown = 0.95f,
                        SplashRadius = 2.5f, SplashPct = 0.65f, CritChance = 0.35f, CritMult = 3f, Targets = 2,
                        Desc = "상큼한 레몬 파르페. 두 마리를 동시에 녹여버려요." };
                    break;
                default: // RainbowPartySet: 레인보우 케이크 + 명품 딸기
                    b = new MenuStats { Name = n, Color = MenuInfo.Argb(0xFFF07AFF), Damage = 2600f, Range = 6f, Cooldown = 0.65f,
                        ChainCount = 8, ChainRange = 3.2f, ChainPct = 0.88f, PoisonDps = 1100f, PoisonDur = 4f, AirMult = 2.5f,
                        Desc = "무지개 크림이 8번 튕기며 모두를 물들여요." };
                    break;
            }
            if (level <= 0) return b;
            float m = Mathf.Pow(1.7f, level);
            var s = b.Clone();
            s.Name = $"{b.Name} +{level}";
            s.Damage = b.Damage * m;
            s.PoisonDps = b.PoisonDps * m;
            s.Range = b.Range + 0.2f * level;
            return s;
        }

        public static string RecipeText(Recipe r) => $"{r.Result.Label()} = " + string.Join(" + ", r.Parts);

        public static List<string> Describe(MenuStats s)
        {
            var l = new List<string>
            {
                $"공격력 {s.Damage:0} · 사거리 {s.Range:0.0} · 간격 {s.Cooldown:0.00}초",
            };
            if (s.SplashRadius > 0) l.Add($"범위 피해 {s.SplashPct * 100:0}% (반경 {s.SplashRadius:0.0})");
            if (s.SlowPct > 0) l.Add($"둔화 {s.SlowPct * 100:0}% ({s.SlowDur:0.0}초)");
            if (s.PoisonDps > 0) l.Add($"지속 피해 초당 {s.PoisonDps:0} ({s.PoisonDur:0}초)");
            if (s.Targets > 1) l.Add($"동시 공격 {s.Targets}명");
            if (s.CritChance > 0) l.Add($"치명타 {s.CritChance * 100:0}% (x{s.CritMult:0.0})");
            if (s.AirMult > 1f) l.Add($"파리 피해 x{s.AirMult:0.0}");
            if (s.ChainCount > 0) l.Add($"연쇄 {s.ChainCount}회 (튕길 때마다 {s.ChainPct * 100:0}%)");
            if (s.StunChance > 0) l.Add($"기절 {s.StunChance * 100:0}% 확률 ({s.StunDur:0.0}초, 대장은 절반)");
            if (s.BountyBonus > 0) l.Add($"처치 시 추가 코인 +{s.BountyBonus}");
            if (s.ExecutePct > 0) l.Add($"체력 {s.ExecutePct * 100:0}% 이하 즉시 처치 (대장 제외)");
            if (!string.IsNullOrEmpty(s.Desc)) l.Add(s.Desc);
            return l;
        }
    }
}

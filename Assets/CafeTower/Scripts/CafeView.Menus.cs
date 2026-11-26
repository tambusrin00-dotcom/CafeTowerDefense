using UnityEngine;

namespace CafeTowerDefense
{
    /// <summary>
    /// 메뉴별 생김새. 실제 카페 메뉴의 대표적인 모습을 기본 도형으로 단순화함.
    ///   기본 음료: 같은 컵에 맛마다 다른 장식 (딸기·얼음·말차 잎·레몬 휠·블루베리·젖소 무늬)
    ///   시그니처: 메뉴마다 고유한 모양 (머그, 유리잔, 빙수 그릇, 허니 브레드, 푸딩, 마카롱 탑 등)
    ///   세트: 시그니처 디저트 모양 + 짝이 되는 음료 컵
    /// </summary>
    public partial class CafeView
    {
        static readonly Color Ceramic = MenuInfo.Argb(0xFFFAF6EF);
        static readonly Color GlassTint = MenuInfo.Argb(0xFFDDEFF5);
        static readonly Color Vanilla = MenuInfo.Argb(0xFFFFF4D6);
        static readonly Color Whip = MenuInfo.Argb(0xFFFFF8EE);
        static readonly Color Cherry = MenuInfo.Argb(0xFFE0283C);
        static readonly Color Leaf = MenuInfo.Argb(0xFF4CA03A);
        static readonly Color Espresso = MenuInfo.Argb(0xFF4A2A16);
        static readonly Color Chocolate = MenuInfo.Argb(0xFF5A2E18);
        static readonly Color Honey = MenuInfo.Argb(0xFFFFB31A);
        static readonly Color Berry = MenuInfo.Argb(0xFF3A3F9E);
        static readonly Color LemonC = MenuInfo.Argb(0xFFFFE04D);

        /// <summary>장식용 도형. 그림자는 몸통만 드리우도록 기본은 끔.</summary>
        GameObject P(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color c,
            float em = 0f, Vector3 euler = default, bool gloss = true, bool shadow = false)
        {
            var g = Prim(type, parent, pos, scale, gloss ? glossMat : lit, c, em, shadow);
            if (euler != default) g.transform.localRotation = Quaternion.Euler(euler);
            return g;
        }

        // ================================================================== 기본 음료 장식

        /// <summary>컵 위에 맛에 맞는 장식을 붙임. s = 컵 크기 배율, 컵 테두리 높이 = 0.5s, 반지름 = 0.21s.</summary>
        void FlavorGarnish(Transform cup, Flavor f, float s, bool lidded, bool dome)
        {
            float rim = 0.5f * s, r = 0.21f * s;
            float topY = dome ? 0.6f * s : lidded ? 0.545f * s : 0.51f * s;
            switch (f)
            {
                case Flavor.Strawberry: // 테두리에 꽂은 딸기
                    P(cup, PrimitiveType.Sphere, new Vector3(r * 0.75f, rim + 0.05f * s, -r * 0.75f), new Vector3(0.13f, 0.16f, 0.13f) * s, Cherry, 0.3f);
                    P(cup, PrimitiveType.Sphere, new Vector3(r * 0.75f, rim + 0.135f * s, -r * 0.75f), new Vector3(0.11f, 0.025f, 0.11f) * s, Leaf, 0.1f, default, false);
                    break;

                case Flavor.Ice: // 컵 옆으로 비치는 얼음 조각
                    for (int i = 0; i < 3; i++)
                    {
                        float a = Mathf.PI * (1.15f + 0.35f * i);
                        var p = new Vector3(Mathf.Sin(a) * r * 0.95f, (0.12f + 0.15f * i) * s, Mathf.Cos(a) * r * 0.95f);
                        P(cup, PrimitiveType.Cube, p, Vector3.one * 0.11f * s, MenuInfo.Argb(0xFFE6F7FF), 0.5f, new Vector3(20f * i, 35f + 25f * i, 15f));
                    }
                    break;

                case Flavor.Matcha: // 녹차 잎 두 장 (+ 뚜껑이 없으면 하트 라떼 아트)
                    P(cup, PrimitiveType.Sphere, new Vector3(r * 0.8f, rim + 0.06f * s, -r * 0.5f), new Vector3(0.05f, 0.16f, 0.02f) * s, Leaf, 0.15f, new Vector3(0f, 30f, -20f));
                    P(cup, PrimitiveType.Sphere, new Vector3(r * 0.95f, rim + 0.04f * s, -r * 0.3f), new Vector3(0.05f, 0.13f, 0.02f) * s, Theme.Shade(Leaf, 0.85f), 0.15f, new Vector3(0f, 30f, 25f));
                    if (!lidded) LatteHeart(cup, rim + 0.012f * s, 0.6f * s);
                    break;

                case Flavor.Lemon: // 테두리에 끼운 레몬 휠
                    P(cup, PrimitiveType.Cylinder, new Vector3(r, rim, -0.04f * s), new Vector3(0.2f, 0.012f, 0.2f) * s, LemonC, 0.3f, new Vector3(90f, 0f, 0f));
                    P(cup, PrimitiveType.Cylinder, new Vector3(r, rim, -0.053f * s), new Vector3(0.16f, 0.004f, 0.16f) * s, MenuInfo.Argb(0xFFFFF6C2), 0.3f, new Vector3(90f, 0f, 0f));
                    break;

                case Flavor.Blueberry: // 앞쪽에 올린 블루베리 세 알
                    foreach (var o in new[] { new Vector2(-0.07f, -0.12f), new Vector2(0.03f, -0.15f), new Vector2(-0.02f, -0.06f) })
                        P(cup, PrimitiveType.Sphere, new Vector3(o.x * s, topY + 0.03f * s, o.y * s), Vector3.one * 0.075f * s, Berry, 0.25f);
                    break;

                default: // 우유: 컵에 그린 젖소 무늬
                    foreach (var (deg, y, w) in new[] { (200f, 0.38f, 0.11f), (160f, 0.09f, 0.09f), (240f, 0.1f, 0.07f), (135f, 0.4f, 0.07f) })
                    {
                        float a = deg * Mathf.Deg2Rad;
                        P(cup, PrimitiveType.Sphere, new Vector3(Mathf.Sin(a) * r, y * s, Mathf.Cos(a) * r), new Vector3(w, w * 0.8f, 0.02f) * s,
                            MenuInfo.Argb(0xFF2A2522), 0f, new Vector3(0f, deg, 0f), false);
                    }
                    break;
            }
        }

        /// <summary>음료 표면 위 하트 모양 라떼 아트 (size = 하트 너비)</summary>
        void LatteHeart(Transform parent, float y, float size)
        {
            float k = size * 0.12f;
            P(parent, PrimitiveType.Sphere, new Vector3(-k * 0.5f, y, k * 0.3f), new Vector3(k * 1.1f, 0.01f, k * 1.1f), Whip, 0.2f, default, false);
            P(parent, PrimitiveType.Sphere, new Vector3(k * 0.5f, y, k * 0.3f), new Vector3(k * 1.1f, 0.01f, k * 1.1f), Whip, 0.2f, default, false);
            P(parent, PrimitiveType.Cube, new Vector3(0f, y, -k * 0.25f), new Vector3(k * 0.85f, 0.01f, k * 0.85f), Whip, 0.2f, new Vector3(0f, 45f, 0f), false);
        }

        // ================================================================== 시그니처 메뉴

        /// <summary>접시 위에 메뉴 모양을 만들고 꼭대기 높이를 돌려줌 (강화 초를 꽂을 높이)</summary>
        float BuildSignature(Transform item, SignatureMenu k, Color col)
        {
            if (MenuDB.IsCourse(k)) return BuildCourse(item, MenuDB.CourseFor(k));
            var light = Color.Lerp(col, Color.white, 0.35f);
            switch (k)
            {
                case SignatureMenu.IceFrappe: // 돔 뚜껑 아이스 컵 + 휘핑크림 + 굵은 빨대
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.21f, 0f), new Vector3(0.34f, 0.21f, 0.34f), light, 0.3f, default, true, true);
                    for (int i = 0; i < 3; i++)
                        P(item, PrimitiveType.Cube, new Vector3(Mathf.Sin(3.4f + i) * 0.165f, 0.1f + 0.11f * i, Mathf.Cos(3.4f + i) * 0.165f), Vector3.one * 0.08f, MenuInfo.Argb(0xFFF0FBFF), 0.6f, new Vector3(15f * i, 30f * i, 20f));
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.43f, 0f), new Vector3(0.36f, 0.14f, 0.36f), MenuInfo.Argb(0xFFEAF8FF), 0.3f);
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.5f, 0f), new Vector3(0.24f, 0.1f, 0.24f), Whip, 0.3f);
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.56f, 0f), new Vector3(0.13f, 0.08f, 0.13f), Whip, 0.3f);
                    P(item, PrimitiveType.Cylinder, new Vector3(0.07f, 0.6f, 0f), new Vector3(0.06f, 0.14f, 0.06f), Theme.Shade(col, 0.7f), 0.2f, new Vector3(0f, 0f, -12f));
                    return 0.66f;

                case SignatureMenu.MatchaLatte: // 받침 + 도자기 머그 + 하트 라떼 아트
                    Saucer(item, 0.27f);
                    Mug(item, 0.2f, 0.25f, Ceramic, col);
                    LatteHeart(item, 0.262f, 0.32f);
                    P(item, PrimitiveType.Sphere, new Vector3(-0.17f, 0.035f, -0.15f), new Vector3(0.05f, 0.012f, 0.12f), Leaf, 0.1f, new Vector3(0f, 40f, 0f), false);
                    return 0.3f;

                case SignatureMenu.HotChocoBomb: // 머그 위에 올린 반짝이는 초콜릿 구 + 드리즐 + 마시멜로
                    Saucer(item, 0.27f);
                    Mug(item, 0.2f, 0.23f, MenuInfo.Argb(0xFFC0503A), Chocolate);
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.33f, 0f), Vector3.one * 0.26f, Chocolate, 0.15f, default, true, true);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.36f, 0f), new Vector3(0.24f, 0.006f, 0.24f), MenuInfo.Argb(0xFFFF9EC7), 0.4f, new Vector3(18f, 0f, 10f));
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.31f, 0f), new Vector3(0.265f, 0.006f, 0.265f), Whip, 0.3f, new Vector3(-12f, 0f, -16f));
                    for (int i = 0; i < 3; i++)
                        P(item, PrimitiveType.Cube, new Vector3(Mathf.Cos(i * 2.1f) * 0.15f, 0.25f, Mathf.Sin(i * 2.1f) * 0.15f), Vector3.one * 0.055f, Whip, 0.2f, new Vector3(10f, 30f * i, 0f), false);
                    return 0.47f;

                case SignatureMenu.EspressoShot: // 받침 + 작은 데미타세 잔 + 크레마 + 원두
                    Saucer(item, 0.25f);
                    Mug(item, 0.13f, 0.18f, Ceramic, MenuInfo.Argb(0xFFC8945A));
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.183f, 0f), new Vector3(0.12f, 0.008f, 0.12f), Espresso, 0.1f, default, true);
                    foreach (var o in new[] { new Vector2(-0.17f, -0.1f), new Vector2(-0.12f, -0.17f), new Vector2(0.15f, -0.15f) })
                        P(item, PrimitiveType.Sphere, new Vector3(o.x, 0.04f, o.y), new Vector3(0.06f, 0.035f, 0.08f), MenuInfo.Argb(0xFF3A1F10), 0.1f, new Vector3(0f, o.x * 300f, 0f));
                    return 0.24f;

                case SignatureMenu.SparklingLemonade: // 키 큰 유리잔 + 레몬 휠 + 기포 + 줄무늬 빨대
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.22f, 0f), new Vector3(0.32f, 0.22f, 0.32f), Color.Lerp(LemonC, Color.white, 0.35f), 0.35f, default, true, true);
                    P(item, PrimitiveType.Cylinder, new Vector3(0.16f, 0.43f, -0.04f), new Vector3(0.2f, 0.012f, 0.2f), LemonC, 0.3f, new Vector3(90f, 0f, 0f));
                    P(item, PrimitiveType.Cylinder, new Vector3(0.16f, 0.43f, -0.053f), new Vector3(0.16f, 0.004f, 0.16f), MenuInfo.Argb(0xFFFFF6C2), 0.3f, new Vector3(90f, 0f, 0f));
                    foreach (var b in new[] { new Vector3(-0.06f, 0.47f, -0.03f), new Vector3(0.02f, 0.52f, 0.05f), new Vector3(-0.02f, 0.58f, -0.06f), new Vector3(0.05f, 0.47f, -0.07f) })
                        P(item, PrimitiveType.Sphere, b, Vector3.one * 0.04f, Color.white, 0.8f);
                    P(item, PrimitiveType.Sphere, new Vector3(-0.08f, 0.45f, 0.06f), new Vector3(0.1f, 0.02f, 0.06f), Leaf, 0.15f, new Vector3(0f, 30f, 0f));
                    P(item, PrimitiveType.Cylinder, new Vector3(-0.05f, 0.56f, 0.02f), new Vector3(0.05f, 0.16f, 0.05f), Color.white, 0.2f, new Vector3(0f, 0f, 10f));
                    P(item, PrimitiveType.Cylinder, new Vector3(-0.055f, 0.6f, 0.02f), new Vector3(0.052f, 0.03f, 0.052f), LemonC, 0.2f, new Vector3(0f, 0f, 10f));
                    return 0.72f;

                case SignatureMenu.LemonIcedTea: // 호박색 아이스티 유리잔 + 위로 솟은 얼음 + 레몬 휠 + 빨대
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.2f, 0f), new Vector3(0.32f, 0.2f, 0.32f), col, 0.35f, default, true, true);
                    for (int i = 0; i < 3; i++)
                        P(item, PrimitiveType.Cube, new Vector3(-0.06f + 0.06f * i, 0.41f + 0.02f * (i % 2), 0.04f * (i - 1)), Vector3.one * 0.09f,
                            MenuInfo.Argb(0xFFF0FBFF), 0.6f, new Vector3(15f * i, 25f + 30f * i, 10f));
                    for (int i = 0; i < 2; i++)
                        P(item, PrimitiveType.Cube, new Vector3(Mathf.Sin(3.6f + i) * 0.155f, 0.15f + 0.12f * i, Mathf.Cos(3.6f + i) * 0.155f), Vector3.one * 0.08f,
                            MenuInfo.Argb(0xFFF0FBFF), 0.5f, new Vector3(20f, 40f * i, 15f));
                    P(item, PrimitiveType.Cylinder, new Vector3(0.16f, 0.39f, -0.04f), new Vector3(0.2f, 0.012f, 0.2f), LemonC, 0.3f, new Vector3(90f, 0f, 0f));
                    P(item, PrimitiveType.Cylinder, new Vector3(0.16f, 0.39f, -0.053f), new Vector3(0.16f, 0.004f, 0.16f), MenuInfo.Argb(0xFFFFF6C2), 0.3f, new Vector3(90f, 0f, 0f));
                    P(item, PrimitiveType.Cylinder, new Vector3(-0.07f, 0.52f, 0.03f), new Vector3(0.05f, 0.15f, 0.05f), Color.white, 0.2f, new Vector3(0f, 0f, 10f));
                    return 0.62f;

                case SignatureMenu.HoneyBread: // 칼집 낸 식빵 큐브 + 바닐라 아이스크림 + 꿀
                    P(item, PrimitiveType.Cube, new Vector3(0f, 0.16f, 0f), new Vector3(0.38f, 0.3f, 0.38f), MenuInfo.Argb(0xFFC98A3A), 0.1f, default, false, true);
                    P(item, PrimitiveType.Cube, new Vector3(0f, 0.312f, 0f), new Vector3(0.35f, 0.01f, 0.35f), MenuInfo.Argb(0xFFE8B866), 0.15f, default, false);
                    foreach (var o in new[] { -0.06f, 0.06f })
                    {
                        P(item, PrimitiveType.Cube, new Vector3(o, 0.318f, 0f), new Vector3(0.012f, 0.008f, 0.35f), MenuInfo.Argb(0xFF8A5320), 0f, default, false);
                        P(item, PrimitiveType.Cube, new Vector3(0f, 0.318f, o), new Vector3(0.35f, 0.008f, 0.012f), MenuInfo.Argb(0xFF8A5320), 0f, default, false);
                    }
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.41f, 0f), Vector3.one * 0.2f, Vanilla, 0.25f);
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.48f, 0f), new Vector3(0.15f, 0.05f, 0.15f), Honey, 0.5f);
                    foreach (var d in new[] { new Vector3(0.191f, 0.25f, -0.06f), new Vector3(-0.08f, 0.24f, -0.191f), new Vector3(0.05f, 0.27f, -0.191f) })
                        P(item, PrimitiveType.Cube, d, new Vector3(Mathf.Abs(d.x) > 0.15f ? 0.01f : 0.035f, 0.1f, Mathf.Abs(d.z) > 0.15f ? 0.01f : 0.035f), Honey, 0.5f);
                    P(item, PrimitiveType.Sphere, new Vector3(0.05f, 0.52f, -0.03f), new Vector3(0.07f, 0.015f, 0.04f), Leaf, 0.1f, new Vector3(0f, 30f, 0f), false);
                    return 0.54f;

                case SignatureMenu.JellyPudding: // 아래가 넓은 커스터드 푸딩 + 캐러멜 + 크림 + 체리
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), new Vector3(0.38f, 0.05f, 0.38f), col, 0.2f, default, true, true);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.14f, 0f), new Vector3(0.34f, 0.045f, 0.34f), col, 0.2f, default, true, true);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.22f, 0f), new Vector3(0.3f, 0.04f, 0.3f), col, 0.2f, default, true, true);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.265f, 0f), new Vector3(0.305f, 0.012f, 0.305f), MenuInfo.Argb(0xFF8B4513), 0.2f);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 1.2566f;
                        P(item, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.15f, 0.24f, Mathf.Sin(a) * 0.15f), new Vector3(0.04f, 0.07f, 0.04f), MenuInfo.Argb(0xFF8B4513), 0.2f);
                    }
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.31f, 0f), new Vector3(0.12f, 0.08f, 0.12f), Whip, 0.3f);
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.37f, 0f), Vector3.one * 0.075f, Cherry, 0.4f);
                    return 0.41f;

                case SignatureMenu.RedVelvetCake: // 붉은 시트와 흰 크림이 번갈아 쌓인 케이크 + 크럼 + 라즈베리
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.07f, 0f), new Vector3(0.5f, 0.07f, 0.5f), col, 0.2f, default, false, true);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.155f, 0f), new Vector3(0.505f, 0.015f, 0.505f), Whip, 0.1f, default, false);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.24f, 0f), new Vector3(0.5f, 0.07f, 0.5f), col, 0.2f, default, false, true);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.318f, 0f), new Vector3(0.515f, 0.012f, 0.515f), Whip, 0.1f, default, false);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        P(item, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.2f, 0.345f, Mathf.Sin(a) * 0.2f), Vector3.one * 0.07f, Whip, 0.2f);
                        P(item, PrimitiveType.Cube, new Vector3(Mathf.Cos(a + 0.5f) * 0.1f, 0.335f, Mathf.Sin(a + 0.5f) * 0.1f), Vector3.one * 0.025f, Theme.Shade(col, 0.8f), 0f, new Vector3(30f, a * 60f, 0f), false);
                    }
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.37f, 0f), Vector3.one * 0.09f, MenuInfo.Argb(0xFFD0204A), 0.4f);
                    return 0.42f;

                case SignatureMenu.MacaronCannon: // 색색의 마카롱 탑
                {
                    Color[] shells = { col, MenuInfo.Argb(0xFF9FE3C8), MenuInfo.Argb(0xFFFFE07A) };
                    for (int i = 0; i < 3; i++)
                    {
                        var m = new GameObject("Macaron").transform;
                        m.SetParent(item, false);
                        m.localPosition = new Vector3(0f, 0.06f + i * 0.14f, 0f);
                        m.localRotation = Quaternion.Euler(i == 1 ? 6f : -4f, i * 40f, i == 2 ? -6f : 3f);
                        float w = 0.34f - i * 0.04f;
                        P(m, PrimitiveType.Sphere, new Vector3(0f, -0.025f, 0f), new Vector3(w, 0.07f, w), shells[i], 0.25f, default, true, i == 0);
                        P(m, PrimitiveType.Cylinder, Vector3.zero, new Vector3(w * 0.9f, 0.016f, w * 0.9f), Whip, 0.2f, default, false);
                        P(m, PrimitiveType.Sphere, new Vector3(0f, 0.03f, 0f), new Vector3(w, 0.08f, w), shells[i], 0.25f);
                    }
                    return 0.46f;
                }

                case SignatureMenu.MatchaBingsu: // 그릇 + 말차 가루 덮인 눈꽃 얼음 + 팥 + 떡
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(0.52f, 0.06f, 0.52f), Ceramic, 0.1f, default, true, true);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.122f, 0f), new Vector3(0.54f, 0.008f, 0.54f), MenuInfo.Argb(0xFF5A8A4A), 0.1f, default, false);
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.19f, 0f), new Vector3(0.48f, 0.3f, 0.48f), MenuInfo.Argb(0xFFBFE3A8), 0.3f, default, false);
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.27f, 0f), new Vector3(0.36f, 0.16f, 0.36f), col, 0.2f, default, false);
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.36f, 0f), new Vector3(0.15f, 0.08f, 0.15f), MenuInfo.Argb(0xFF6B1F2A), 0.15f);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * 2.094f + 0.5f;
                        P(item, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.15f, 0.29f, Mathf.Sin(a) * 0.15f), Vector3.one * 0.075f, Ceramic, 0.2f);
                    }
                    return 0.42f;

                case SignatureMenu.Affogato: // 낮은 유리잔 + 바닐라 젤라또 위로 흐르는 에스프레소 + 작은 샷잔
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.1f, 0f), new Vector3(0.34f, 0.1f, 0.34f), GlassTint, 0.15f, default, true, true);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.19f, 0f), new Vector3(0.3f, 0.008f, 0.3f), Espresso, 0.1f);
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.27f, 0f), Vector3.one * 0.22f, Vanilla, 0.25f);
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.33f, 0f), new Vector3(0.18f, 0.08f, 0.18f), MenuInfo.Argb(0xFF6A3A1C), 0.15f);
                    P(item, PrimitiveType.Cube, new Vector3(0.09f, 0.26f, -0.06f), new Vector3(0.025f, 0.1f, 0.025f), MenuInfo.Argb(0xFF6A3A1C), 0.15f, new Vector3(0f, 0f, 15f));
                    P(item, PrimitiveType.Cylinder, new Vector3(0.22f, 0.06f, -0.12f), new Vector3(0.11f, 0.06f, 0.11f), Ceramic, 0.1f);
                    P(item, PrimitiveType.Cylinder, new Vector3(0.22f, 0.115f, -0.12f), new Vector3(0.09f, 0.005f, 0.09f), Espresso, 0.1f);
                    foreach (var n in new[] { new Vector3(-0.04f, 0.37f, -0.02f), new Vector3(0.03f, 0.365f, 0.04f) })
                        P(item, PrimitiveType.Cube, n, Vector3.one * 0.025f, MenuInfo.Argb(0xFFC89A5A), 0f, new Vector3(20f, 40f, 0f), false);
                    return 0.4f;

                case SignatureMenu.SignatureParfait: // 다리 달린 파르페 잔 + 크림·딸기·그래놀라 층 + 웨이퍼 + 체리
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.012f, 0f), new Vector3(0.26f, 0.012f, 0.26f), GlassTint, 0.2f, default, true, true);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), new Vector3(0.05f, 0.06f, 0.05f), GlassTint, 0.2f);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.175f, 0f), new Vector3(0.24f, 0.04f, 0.24f), Whip, 0.2f, default, true, true);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.245f, 0f), new Vector3(0.28f, 0.03f, 0.28f), Cherry, 0.25f);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.305f, 0f), new Vector3(0.3f, 0.03f, 0.3f), MenuInfo.Argb(0xFFC89A5A), 0.1f, default, false);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.365f, 0f), new Vector3(0.32f, 0.03f, 0.32f), col, 0.25f);
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.42f, 0f), new Vector3(0.3f, 0.12f, 0.3f), Whip, 0.3f);
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.49f, 0f), new Vector3(0.18f, 0.09f, 0.18f), Whip, 0.3f);
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.56f, 0f), Vector3.one * 0.085f, Cherry, 0.4f);
                    P(item, PrimitiveType.Cube, new Vector3(0.08f, 0.53f, 0.02f), new Vector3(0.045f, 0.22f, 0.045f), MenuInfo.Argb(0xFFE0B070), 0.15f, new Vector3(0f, 0f, -18f), false);
                    P(item, PrimitiveType.Sphere, new Vector3(-0.12f, 0.41f, -0.07f), new Vector3(0.09f, 0.1f, 0.06f), Cherry, 0.3f, new Vector3(0f, 0f, 20f));
                    return 0.62f;

                case SignatureMenu.RainbowCake: // 여섯 빛깔 시트를 쌓은 케이크 + 스프링클
                {
                    uint[] layers = { 0xFFFF5A6E, 0xFFFF9F45, 0xFFFFE04D, 0xFF6ED46A, 0xFF4FA8FF, 0xFFA070FF };
                    for (int i = 0; i < layers.Length; i++)
                        P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.03f + i * 0.06f, 0f), new Vector3(0.5f, 0.03f, 0.5f), MenuInfo.Argb(layers[i]), 0.25f, default, false, i == 0);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.37f, 0f), new Vector3(0.515f, 0.012f, 0.515f), Whip, 0.15f, default, false);
                    for (int i = 0; i < 9; i++)
                    {
                        float a = i * 0.698f, rr = 0.06f + (i % 3) * 0.06f;
                        P(item, PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * rr, 0.385f, Mathf.Sin(a) * rr), new Vector3(0.05f, 0.015f, 0.015f), MenuInfo.Argb(layers[i % layers.Length]), 0.4f, new Vector3(0f, a * 80f, 0f), false);
                    }
                    return 0.4f;
                }

                case SignatureMenu.AuroraFloat: // 파랑→보라→분홍으로 번지는 유리잔 + 떠 있는 아이스크림 + 빛나는 별
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.11f, 0f), new Vector3(0.34f, 0.11f, 0.34f), MenuInfo.Argb(0xFF4FA8FF), 0.5f, default, true, true);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.29f, 0f), new Vector3(0.34f, 0.07f, 0.34f), MenuInfo.Argb(0xFFA070FF), 0.5f);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.395f, 0f), new Vector3(0.34f, 0.035f, 0.34f), MenuInfo.Argb(0xFFFF9ED8), 0.5f);
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.47f, 0f), Vector3.one * 0.22f, MenuInfo.Argb(0xFFEFFBFF), 0.4f);
                    foreach (var p in new[] { new Vector3(-0.06f, 0.57f, -0.03f), new Vector3(0.05f, 0.56f, 0.04f), new Vector3(0.0f, 0.6f, -0.07f) })
                        P(item, PrimitiveType.Cube, p, Vector3.one * 0.04f, MenuInfo.Argb(0xFFFFF27A), 1.6f, new Vector3(45f, 0f, 45f));
                    P(item, PrimitiveType.Cylinder, new Vector3(0.08f, 0.58f, 0.03f), new Vector3(0.05f, 0.15f, 0.05f), MenuInfo.Argb(0xFF8FE9FF), 0.4f, new Vector3(0f, 0f, -12f));
                    return 0.66f;

                case SignatureMenu.MatchaTiramisu: // 네모난 그릇 + 크림·말차 층 + 말차 가루 + 딸기·블루베리
                    P(item, PrimitiveType.Cube, new Vector3(0f, 0.035f, 0f), new Vector3(0.52f, 0.07f, 0.52f), Ceramic, 0.1f, default, true, true);
                    P(item, PrimitiveType.Cube, new Vector3(0f, 0.115f, 0f), new Vector3(0.44f, 0.09f, 0.44f), MenuInfo.Argb(0xFFF3E6CC), 0.15f, default, false);
                    P(item, PrimitiveType.Cube, new Vector3(0f, 0.195f, 0f), new Vector3(0.44f, 0.07f, 0.44f), Theme.Shade(col, 0.85f), 0.15f, default, false);
                    P(item, PrimitiveType.Cube, new Vector3(0f, 0.265f, 0f), new Vector3(0.44f, 0.07f, 0.44f), MenuInfo.Argb(0xFFF3E6CC), 0.15f, default, false);
                    P(item, PrimitiveType.Cube, new Vector3(0f, 0.305f, 0f), new Vector3(0.44f, 0.012f, 0.44f), col, 0.2f, default, false);
                    P(item, PrimitiveType.Sphere, new Vector3(-0.1f, 0.35f, -0.08f), new Vector3(0.09f, 0.11f, 0.09f), Cherry, 0.3f);
                    P(item, PrimitiveType.Sphere, new Vector3(0.08f, 0.335f, 0.06f), Vector3.one * 0.065f, Berry, 0.25f);
                    P(item, PrimitiveType.Sphere, new Vector3(0.12f, 0.33f, -0.06f), Vector3.one * 0.06f, Berry, 0.25f);
                    P(item, PrimitiveType.Sphere, new Vector3(-0.02f, 0.33f, 0.1f), new Vector3(0.08f, 0.015f, 0.05f), Leaf, 0.1f, new Vector3(0f, 30f, 0f), false);
                    return 0.4f;

                case SignatureMenu.InstaTea: // 금테 두른 분홍 유리 찻잔 + 꽃잎 + 반짝이
                    Saucer(item, 0.28f, MenuInfo.Argb(0xFFFFD6E8));
                    Mug(item, 0.19f, 0.21f, MenuInfo.Argb(0xFFFFB3D9), MenuInfo.Argb(0xFFFF7FB0), 0.5f);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.212f, 0f), new Vector3(0.395f, 0.01f, 0.395f), Gold, 1f, default, true);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 1.2566f;
                        P(item, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.065f, 0.225f, Mathf.Sin(a) * 0.065f), new Vector3(0.1f, 0.015f, 0.06f), Color.white, 0.6f, new Vector3(0f, -a * Mathf.Rad2Deg, 0f));
                    }
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.232f, 0f), Vector3.one * 0.045f, LemonC, 1f);
                    foreach (var p in new[] { new Vector3(-0.2f, 0.36f, -0.05f), new Vector3(0.18f, 0.42f, 0.04f), new Vector3(0.02f, 0.46f, -0.12f) })
                        P(item, PrimitiveType.Cube, p, Vector3.one * 0.04f, Gold, 2f, new Vector3(45f, 0f, 45f));
                    return 0.3f;

                default: // 혹시 모양이 없는 메뉴가 생기면 기본 2단 케이크
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.58f, 0.12f, 0.58f), col, 0.25f, default, true, true);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0f), new Vector3(0.61f, 0.02f, 0.61f), Cream, 0f, default, false);
                    P(item, PrimitiveType.Cylinder, new Vector3(0f, 0.35f, 0f), new Vector3(0.42f, 0.09f, 0.42f), light, 0.2f);
                    P(item, PrimitiveType.Sphere, new Vector3(0f, 0.53f, 0f), Vector3.one * 0.14f, Cherry, 0.4f);
                    return 0.6f;
            }
        }

        /// <summary>코스 메뉴: 원목 쟁반 위에 재료 메뉴들을 작게 나란히 올림</summary>
        float BuildCourse(Transform item, CourseRecipe r)
        {
            var tray = MenuInfo.Argb(0xFFC08A55);
            var edge = MenuInfo.Argb(0xFF8A5A33);
            P(item, PrimitiveType.Cube, new Vector3(0f, 0.025f, 0f), new Vector3(0.8f, 0.03f, 0.62f), tray, 0.1f, default, false, true);
            foreach (var z in new[] { -0.3f, 0.3f })
                P(item, PrimitiveType.Cube, new Vector3(0f, 0.055f, z), new Vector3(0.8f, 0.035f, 0.025f), edge, 0.05f, default, false);
            foreach (var x in new[] { -0.39f, 0.39f })
                P(item, PrimitiveType.Cube, new Vector3(x, 0.055f, 0f), new Vector3(0.025f, 0.035f, 0.62f), edge, 0.05f, default, false);

            int n = r.Parts.Length;
            Vector3[] spots = n == 2
                ? new[] { new Vector3(-0.19f, 0f, 0f), new Vector3(0.19f, 0f, 0f) }
                : new[] { new Vector3(-0.2f, 0f, -0.1f), new Vector3(0.2f, 0f, -0.1f), new Vector3(0f, 0f, 0.14f) };
            float scale = n == 2 ? 0.5f : 0.44f;
            float top = 0.1f;
            for (int i = 0; i < n && i < spots.Length; i++)
            {
                var part = r.Parts[i];
                var mini = new GameObject(part.ToString()).transform;
                mini.SetParent(item, false);
                mini.localPosition = spots[i] + new Vector3(0f, 0.04f, 0f);
                mini.localScale = Vector3.one * scale;
                float h = BuildSignature(mini, part, MenuDB.SignatureStats(part).Color);
                top = Mathf.Max(top, 0.04f + h * scale);
            }
            return top;
        }

        // ================================================================== 강화 꽃

        static readonly Color PetalOn = MenuInfo.Argb(0xFFFF8FB8);
        static readonly Color PetalOff = MenuInfo.Argb(0xFF5A4644);
        /// <summary>강화 꽃 크기 배율</summary>
        const float FlowerScale = 1.5f;

        /// <summary>
        /// 접시 앞쪽(카메라 쪽)에 카메라를 향해 살짝 세운 꽃. 꽃잎 max장 중 level장이 분홍으로 채워지고
        /// 나머지는 어두운 빈 자리로 남음. 최대 강화면 꽃잎과 가운데가 금빛으로 빛남. 메뉴와 함께 돌지 않음.
        /// </summary>
        void UpgradeFlower(Transform parent, int level, int max)
        {
            if (max <= 0) return;
            bool full = level >= max;
            var flower = new GameObject("UpgradeFlower").transform;
            flower.SetParent(parent, false);
            flower.localPosition = new Vector3(0f, 0.25f, -0.38f); // 커진 만큼 살짝 올려 접시에 묻히지 않게
            flower.localRotation = Quaternion.Euler(35f, 0f, 0f); // 뒤로 살짝 눕혀 위에서 내려다보는 카메라에도 잘 보이게
            flower.localScale = Vector3.one * FlowerScale;

            for (int i = 0; i < max; i++)
            {
                float deg = 90f - i * 360f / max; // 12시 방향부터 시계 방향으로 채움
                float a = deg * Mathf.Deg2Rad;
                bool on = i < level;
                P(flower, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.06f, Mathf.Sin(a) * 0.06f, 0f),
                    on ? new Vector3(0.065f, 0.1f, 0.025f) : new Vector3(0.05f, 0.08f, 0.015f),
                    on ? (full ? Color.Lerp(PetalOn, Gold, 0.35f) : PetalOn) : PetalOff,
                    on ? (full ? 1.4f : 0.7f) : 0f, new Vector3(0f, 0f, deg - 90f));
            }
            P(flower, PrimitiveType.Sphere, new Vector3(0f, 0f, -0.01f), new Vector3(0.05f, 0.05f, 0.03f),
                full ? Gold : MenuInfo.Argb(0xFFFFD24D), full ? 2f : 0.6f);
        }

        /// <summary>찻잔 받침</summary>
        void Saucer(Transform parent, float radius, Color? color = null)
        {
            P(parent, PrimitiveType.Cylinder, new Vector3(0f, 0.012f, 0f), new Vector3(radius * 2f, 0.012f, radius * 2f), color ?? Ceramic, 0.1f, default, true, true);
        }

        /// <summary>손잡이 달린 머그(찻잔). 음료 표면은 높이 h.</summary>
        void Mug(Transform parent, float r, float h, Color body, Color liquid, float bodyGlow = 0.15f)
        {
            P(parent, PrimitiveType.Cylinder, new Vector3(0f, h / 2f + 0.02f, 0f), new Vector3(r * 2f, h / 2f, r * 2f), body, bodyGlow, default, true, true);
            P(parent, PrimitiveType.Cylinder, new Vector3(0f, h + 0.02f, 0f), new Vector3(r * 1.84f, 0.006f, r * 1.84f), liquid, 0.25f);
            // 손잡이: 세운 고리 대신 납작한 원판 두 겹 (가운데를 어둡게 해 구멍처럼 보이게)
            P(parent, PrimitiveType.Cylinder, new Vector3(r + h * 0.16f, h * 0.55f + 0.02f, 0f), new Vector3(h * 0.6f, 0.02f, h * 0.6f), body, bodyGlow, new Vector3(90f, 0f, 0f));
            P(parent, PrimitiveType.Cylinder, new Vector3(r + h * 0.16f, h * 0.55f + 0.02f, 0f), new Vector3(h * 0.3f, 0.022f, h * 0.3f), Theme.Shade(body, 0.55f), 0f, new Vector3(90f, 0f, 0f), false);
        }
    }
}

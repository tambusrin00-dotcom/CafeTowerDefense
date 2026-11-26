using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CafeTowerDefense
{
    /// <summary>
    /// 벌레 위로 떠오르는 피해 숫자 (k · m 단위). 치명타는 크고 붉게, "치명타!" 글자와 함께 튀어 오름.
    /// 월드 공간 캔버스 하나에 글자를 모아 두고 재사용 (휴대폰에서도 확실히 그려지는 UI 셰이더 사용).
    /// </summary>
    public partial class CafeView
    {
        const int MaxDamageTexts = 48;
        const int MaxNormalPerFrame = 6; // 치명타가 아닌 숫자는 한 프레임에 이만큼만 (배속 시 화면이 숫자로 덮이지 않게)
        const float CanvasScale = 0.01f;

        static readonly Color NormalColor = new Color(1f, 0.98f, 0.92f);
        static readonly Color CritColor = new Color(1f, 0.32f, 0.18f);
        static readonly Color CritLabelColor = new Color(1f, 0.85f, 0.25f);

        class DamageText
        {
            public Text T;
            public Outline O;
            public string Num;
            public Vector3 Pos;
            public float Age, Life, Drift;
            public bool Crit, Active;
        }

        readonly List<DamageText> damageTexts = new List<DamageText>();
        Transform damageCanvas;
        int normalThisFrame;

        void InitDamageText()
        {
            Ui.Init();
            var go = new GameObject("DamageNumbers", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;
            go.transform.localScale = Vector3.one * CanvasScale;
            damageCanvas = go.transform;
            game.DamageShown += SpawnDamageText;
        }

        void SpawnDamageText(Pest e, float dmg, bool crit)
        {
            if (dmg < 0.5f) return;
            if (!crit && normalThisFrame >= MaxNormalPerFrame) return;
            if (!crit) normalThisFrame++;

            var d = TakeDamageText();
            Vector3 pos = pestVis.TryGetValue(e, out var pv) && pv.HpRoot != null
                ? pv.HpRoot.position + Vector3.up * 0.3f
                : World(e.X, e.Y, e.Flying ? 2.2f : 1.5f);
            d.Pos = pos + new Vector3(Random.Range(-0.18f, 0.18f), 0f, Random.Range(-0.1f, 0.1f));
            d.Age = 0f;
            d.Crit = crit;
            d.Life = crit ? 1.1f : 0.75f;
            d.Drift = Random.Range(-0.25f, 0.25f);
            d.Active = true;

            string num = MenuInfo.Num(dmg);
            if (crit)
            {
                d.Num = num;
                d.T.text = CritText(num, 1f);
                d.T.fontSize = 58;
                d.T.color = CritColor;
                d.O.effectColor = new Color(0.25f, 0f, 0f, 0.95f);
                d.O.effectDistance = new Vector2(3f, -3f);
                d.T.transform.SetAsLastSibling(); // 치명타가 다른 숫자 위에
            }
            else
            {
                d.T.text = num;
                d.T.fontSize = 36;
                d.T.color = NormalColor;
                d.O.effectColor = new Color(0.15f, 0.08f, 0.05f, 0.9f);
                d.O.effectDistance = new Vector2(2f, -2f);
            }
            d.T.gameObject.SetActive(true);
        }

        static string CritText(string num, float alpha)
        {
            var lc = CritLabelColor;
            lc.a = alpha;
            return $"<size=30><color=#{ColorUtility.ToHtmlStringRGBA(lc)}>치명타!</color></size>\n{num}";
        }

        DamageText TakeDamageText()
        {
            DamageText oldest = null;
            foreach (var d in damageTexts)
            {
                if (!d.Active) return d;
                if (oldest == null || d.Age / d.Life > oldest.Age / oldest.Life) oldest = d;
            }
            if (damageTexts.Count >= MaxDamageTexts) return oldest;

            var go = new GameObject("Dmg", typeof(RectTransform));
            go.transform.SetParent(damageCanvas, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(400f, 160f);
            var t = go.AddComponent<Text>();
            t.font = Ui.Font;
            t.alignment = TextAnchor.LowerCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.fontStyle = FontStyle.Bold;
            t.raycastTarget = false;
            var o = go.AddComponent<Outline>();
            var nd = new DamageText { T = t, O = o };
            damageTexts.Add(nd);
            return nd;
        }

        /// <summary>Sync에서 호출. 게임이 멈추면 숫자도 멈추고, 배속과 관계없이 실제 시간으로 떠오름.</summary>
        void SyncDamageText(Camera cam, float gameDt)
        {
            normalThisFrame = 0;
            float dt = gameDt > 0f ? Mathf.Min(Time.unscaledDeltaTime, 0.1f) : 0f;
            var rot = cam != null ? cam.transform.rotation : Quaternion.identity;
            foreach (var d in damageTexts)
            {
                if (!d.Active) continue;
                d.Age += dt;
                if (d.Age >= d.Life)
                {
                    d.Active = false;
                    d.T.gameObject.SetActive(false);
                    continue;
                }
                float k = d.Age / d.Life;
                // 위로 떠오르며 감속, 살짝 옆으로
                float rise = (d.Crit ? 0.9f : 0.7f) * (1f - (1f - k) * (1f - k));
                var tr = d.T.transform;
                tr.position = d.Pos + new Vector3(d.Drift * k, rise, 0f);
                tr.rotation = rot;
                // 치명타: 처음에 크게 튀었다가 제자리
                float pop = d.Crit ? 1f + 0.8f * Mathf.Max(0f, 1f - d.Age / 0.18f) : 1f + 0.25f * Mathf.Max(0f, 1f - d.Age / 0.1f);
                tr.localScale = Vector3.one * pop;
                float a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                var c = d.T.color;
                c.a = a;
                d.T.color = c;
                var oc = d.O.effectColor;
                oc.a = a * 0.9f;
                d.O.effectColor = oc;
                if (d.Crit && k >= 0.6f) d.T.text = CritText(d.Num, a);
            }
        }
    }
}

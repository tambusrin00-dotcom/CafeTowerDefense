using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CafeTowerDefense
{
    /// <summary>한 판의 결과. 시간은 게임 시간(초)이라 배속이나 일시정지로 유리해지지 않음.</summary>
    [Serializable]
    public class RunRecord
    {
        /// <summary>최종 보스(마지막 웨이브)에게 준 피해</summary>
        public double BossDamage;
        /// <summary>최종 보스 웨이브에 쓴 시간</summary>
        public float BossTime;
        public float TotalTime;
        public int Desserts;
        public int Coins;
        public int Wave;
        public bool Victory;
    }

    /// <summary>상위 10개 기록을 기기 안의 JSON 파일에 저장함.</summary>
    public class RunRecords
    {
        public const int MaxEntries = 10;

        [Serializable]
        class SaveData
        {
            public List<RunRecord> Items = new List<RunRecord>();
        }

        readonly string path;
        readonly List<RunRecord> items = new List<RunRecord>();

        public IReadOnlyList<RunRecord> Items => items;

        public RunRecords(string path)
        {
            this.path = path;
            Load();
        }

        /// <summary>앞선 순위면 음수. 최종 보스 피해 → 도달 웨이브 → 보스 시간 → 총 시간 → 남은 디저트 → 남은 코인 순으로 비교.</summary>
        public static int Compare(RunRecord a, RunRecord b)
        {
            int c = b.BossDamage.CompareTo(a.BossDamage);
            if (c == 0) c = b.Wave.CompareTo(a.Wave); // 최종 보스까지 못 간 판끼리는 더 멀리 간 판이 앞
            if (c == 0) c = a.BossTime.CompareTo(b.BossTime);
            if (c == 0) c = a.TotalTime.CompareTo(b.TotalTime);
            if (c == 0) c = b.Desserts.CompareTo(a.Desserts);
            if (c == 0) c = b.Coins.CompareTo(a.Coins);
            return c;
        }

        /// <summary>이 기록을 추가하면 몇 위가 되는지 (1~10). 10위 밖이면 0. 실제로 추가하지는 않음.</summary>
        public int RankOf(RunRecord r)
        {
            int at = items.Count;
            for (int i = 0; i < items.Count; i++)
                if (Compare(r, items[i]) < 0)
                {
                    at = i;
                    break;
                }
            return at < MaxEntries ? at + 1 : 0;
        }

        /// <summary>기록을 추가하고 순위(1~10)를 돌려줌. 10위 밖이면 0.</summary>
        public int Add(RunRecord r)
        {
            int at = items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                // 완전히 같은 기록은 먼저 세운 쪽이 앞
                if (Compare(r, items[i]) < 0)
                {
                    at = i;
                    break;
                }
            }
            if (at >= MaxEntries) return 0;
            items.Insert(at, r);
            if (items.Count > MaxEntries) items.RemoveRange(MaxEntries, items.Count - MaxEntries);
            Save();
            return at + 1;
        }

        void Load()
        {
            items.Clear();
            try
            {
                if (!File.Exists(path)) return;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (data?.Items == null) return;
                foreach (var r in data.Items)
                    if (r != null && !double.IsNaN(r.BossDamage)) items.Add(r);
                items.Sort(Compare);
                if (items.Count > MaxEntries) items.RemoveRange(MaxEntries, items.Count - MaxEntries);
            }
            catch (Exception e)
            {
                // 깨진 파일은 다음 저장에 덮어쓰이기 전에 복사해 둠
                Debug.LogWarning("[RunRecords] 기록을 읽지 못했어요: " + e.Message);
                items.Clear();
                try { File.Copy(path, path + ".broken", true); } catch (Exception) { }
            }
        }

        void Save()
        {
            try
            {
                // 저장 중 앱이 꺼져도 기존 기록이 망가지지 않게 임시 파일에 먼저 씀
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(new SaveData { Items = items }));
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RunRecords] 기록을 저장하지 못했어요: " + e.Message);
            }
        }


        /// <summary>큰 수를 줄임. 예: 12.3k, 1.2m</summary>
        public static string FormatNumber(double v) => MenuInfo.Num(v);

        public static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
        }
    }
}

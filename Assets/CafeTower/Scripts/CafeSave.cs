using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CafeTowerDefense
{
    /// <summary>
    /// 이어하기용 진행 상태. 준비 단계는 놓은 음료까지 그대로, 웨이브 도중이면 그 웨이브 시작 직전 상태를 저장함
    /// (다시 열면 그 웨이브를 처음부터 다시 막음). 메뉴 종류는 이름(문자열)으로 저장해서 나중에
    /// 메뉴가 추가되어 순서가 바뀌어도 저장이 깨지지 않음.
    /// </summary>
    [Serializable]
    public class CafeSaveData
    {
        public int Format = 1;
        public string AppVersion;
        public string SavedAt;
        public string State;
        public int Wave;
        public int Desserts;
        public int Coins;
        public int BaristaLevel;
        public int PassCount;
        public double BossDamage;
        public float BossStageTime;
        public float TotalTime;
        /// <summary>칸 종류 W*H (x*H + y)</summary>
        public int[] Cells;
        public List<TowerSave> Towers = new List<TowerSave>();
    }

    [Serializable]
    public class TowerSave
    {
        public int X, Y;
        public string Flavor;
        public string Grade;
        /// <summary>시그니처 메뉴 이름, 없으면 빈 문자열</summary>
        public string Signature;
        public int SignatureLevel;
        public bool Kept;
        public int Kills;
        public float TotalDamage;
        public bool Moved;
        public float InstaBonus;
        /// <summary>이번 라운드에 놓은 음료의 순서 (남긴 음료는 -1)</summary>
        public int RoundIndex = -1;
    }

    /// <summary>저장 파일 읽기/쓰기 (임시 파일에 먼저 써서 도중에 꺼져도 깨지지 않게)</summary>
    public class SaveStore
    {
        readonly string path;

        public SaveStore(string path) { this.path = path; }

        public bool Exists => File.Exists(path);

        public CafeSaveData Load()
        {
            try
            {
                if (!File.Exists(path)) return null;
                var d = JsonUtility.FromJson<CafeSaveData>(File.ReadAllText(path));
                return d != null && d.Cells != null && d.Wave > 0 ? d : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveStore] 저장된 영업을 읽지 못했어요: " + e.Message);
                return null;
            }
        }

        public void Write(CafeSaveData d)
        {
            if (d == null) return;
            try
            {
                d.AppVersion = Application.version;
                d.SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(d));
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveStore] 영업을 저장하지 못했어요: " + e.Message);
            }
        }

        public void Delete()
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveStore] 저장을 지우지 못했어요: " + e.Message);
            }
        }
    }
}

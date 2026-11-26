using System;
using UnityEngine;

namespace CafeTowerDefense
{
    /// <summary>기록을 다운로드 링크와 함께 공유. Android는 시스템 공유창, 그 외에는 클립보드 복사.</summary>
    public static class ScoreShare
    {
        public const string StoreUrl = "https://play.google.com/store/apps/details?id=com.tambusrin.cafetowerdefense";

        public static string BuildText(RunRecord r)
        {
            string progress = r.Victory ? "최종 보스 격파!" : $"{r.Wave} 웨이브 도달";
            return "☕ 카페 타워 디펜스 내 1등 기록\n\n" +
                   $"🏆 {progress}\n" +
                   $"⚔️ 보스 피해 {RunRecords.FormatNumber(r.BossDamage)}\n" +
                   $"⏱️ 보스 시간 {RunRecords.FormatTime(r.BossTime)} · 총 시간 {RunRecords.FormatTime(r.TotalTime)}\n" +
                   $"🍰 남은 디저트 {r.Desserts} · 🪙 코인 {RunRecords.FormatNumber(r.Coins)}\n\n" +
                   "이 기록, 깰 수 있어요?\n" +
                   $"👉 {StoreUrl}";
        }

        /// <returns>공유창을 열었으면 true, 대신 클립보드에 복사했으면 false</returns>
        public static bool Share(RunRecord r)
        {
            string text = BuildText(r);
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                OpenAndroidShare(text);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ScoreShare] 공유창을 열지 못했어요: " + e.Message);
            }
#endif
            GUIUtility.systemCopyBuffer = text;
            return false;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static void OpenAndroidShare(string text)
        {
            using (var intentClass = new AndroidJavaClass("android.content.Intent"))
            using (var intent = new AndroidJavaObject("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND")).Dispose();
                intent.Call<AndroidJavaObject>("setType", "text/plain").Dispose();
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), "카페 타워 디펜스 1등 기록").Dispose();
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text).Dispose();
                using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "1등 기록 공유하기"))
                    UnityEngine.Android.AndroidApplication.currentActivity.Call("startActivity", chooser);
            }
        }
#endif
    }
}

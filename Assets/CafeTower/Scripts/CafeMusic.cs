using UnityEngine;

namespace CafeTowerDefense
{
    /// <summary>
    /// 배경 음악: Resources/Audio/CafeBgm (관현악 카페 음악, 이음새 없이 반복).
    /// 켬/끔 상태는 PlayerPrefs에 저장. 게임 배속·정지와 무관하게 계속 흐름.
    /// </summary>
    public class CafeMusic
    {
        const string PrefKey = "music_on";
        const float Volume = 0.45f;
        const float FadeTime = 1.2f;

        readonly AudioSource src;
        float target;

        public bool On { get; private set; }

        public CafeMusic(GameObject host)
        {
            if (Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
            src = host.AddComponent<AudioSource>();
            src.clip = Resources.Load<AudioClip>("Audio/CafeBgm");
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.ignoreListenerPause = true;
            src.volume = 0f;
            On = PlayerPrefs.GetInt(PrefKey, 1) == 1;
            target = On ? Volume : 0f;
            if (src.clip != null && On) src.Play();
        }

        public void Toggle()
        {
            On = !On;
            PlayerPrefs.SetInt(PrefKey, On ? 1 : 0);
            PlayerPrefs.Save();
            target = On ? Volume : 0f;
            if (On && src.clip != null && !src.isPlaying) src.Play();
        }

        /// <summary>CafeApp.Update에서 호출 (실제 시간 기준 페이드)</summary>
        public void Update()
        {
            if (src.clip == null) return;
            src.volume = Mathf.MoveTowards(src.volume, target, Volume / FadeTime * Time.unscaledDeltaTime);
            if (!On && src.volume <= 0f && src.isPlaying) src.Pause();
        }
    }
}

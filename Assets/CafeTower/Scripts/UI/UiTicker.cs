using System;
using UnityEngine;

namespace CafeTowerDefense
{
    /// <summary>매 프레임 갱신이 필요한 UI 요소(숫자, 진행 막대 등)에 붙이는 작은 컴포넌트</summary>
    public class UiTicker : MonoBehaviour
    {
        public Action Tick;

        void Update() => Tick?.Invoke();

        public static void Attach(Component c, Action tick)
        {
            var t = c.gameObject.AddComponent<UiTicker>();
            t.Tick = tick;
            tick();
        }
    }
}

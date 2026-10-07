using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Runtime.Audio
{
    /// <summary>
    /// 연출용 BGM 임시 감쇠(E1 첫 보스 조우). 사용자 볼륨(Mixer 노출 파라미터)·세이브와 <b>별개의 계수</b>다 —
    /// BGM AudioSource 의 volume 에만 곱한다. 설정 창이 볼륨을 바꾸는 중에도 서로 덮어쓰지 않는다.
    ///
    /// <list type="bullet">
    /// <item>소유자별로 걸고 소유자별로 푼다. 여럿이 걸면 가장 낮은 값이 이긴다. 남의 감쇠를 풀지 않는다.</item>
    /// <item>정지(timeScale 0) 위에서 걸리므로 실시간으로 옮겨 간다.</item>
    /// <item>소유자가 파괴됐는데 풀지 않았으면 그 감쇠는 버린다(영구 감쇠 방지).</item>
    /// </list>
    /// </summary>
    public sealed partial class AudioManager
    {
        private const float BGM_DUCK_RATE_PER_SECOND = 2.5f;

        private readonly Dictionary<object, float> bgmDuckOwners = new();
        private readonly List<object> bgmDuckReleaseBuffer = new();
        private float bgmDuckCurrent = 1f;

        /// <summary><paramref name="owner"/> 의 BGM 감쇠 계수(0~1)를 건다. 같은 소유자가 다시 걸면 값을 바꾼다.</summary>
        public void SetBgmDuck(object owner, float multiplier)
        {
            if (owner == null) return;
            bgmDuckOwners[owner] = Mathf.Clamp01(multiplier);
        }

        /// <summary><paramref name="owner"/> 의 감쇠를 푼다(멱등). 다른 소유자의 감쇠는 남는다.</summary>
        public void ReleaseBgmDuck(object owner)
        {
            if (owner == null) return;
            bgmDuckOwners.Remove(owner);
        }

        private void Update()
        {
            if (bgmSource == null) return;

            float target = ResolveBgmDuckTarget();
            if (Mathf.Approximately(bgmDuckCurrent, target)) return;

            bgmDuckCurrent = Mathf.MoveTowards(bgmDuckCurrent, target, BGM_DUCK_RATE_PER_SECOND * Time.unscaledDeltaTime);
            bgmSource.volume = bgmDuckCurrent;
        }

        private float ResolveBgmDuckTarget()
        {
            if (bgmDuckOwners.Count == 0) return 1f;

            float target = 1f;
            bgmDuckReleaseBuffer.Clear();
            foreach (var pair in bgmDuckOwners)
            {
                if (pair.Key is Object unityOwner && unityOwner == null)
                {
                    bgmDuckReleaseBuffer.Add(pair.Key);
                    continue;
                }
                target = Mathf.Min(target, pair.Value);
            }

            for (int i = 0; i < bgmDuckReleaseBuffer.Count; i++) bgmDuckOwners.Remove(bgmDuckReleaseBuffer[i]);
            bgmDuckReleaseBuffer.Clear();
            return target;
        }
    }
}

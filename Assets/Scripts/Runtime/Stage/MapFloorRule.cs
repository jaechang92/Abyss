using System;
using System.Collections.Generic;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 맵 방의 「걸어서 서는 바닥」 판정 규칙. MonoBehaviour 가 아니다 — EditMode 에서 규칙만 고정하려는 것이다.
    ///
    /// 🔑 위에서 아래로 쏜 광선이 맞힌 면 중 <b>다른 충돌체 안에 묻히지 않은 가장 낮은 윗면</b>이 바닥이다.
    /// <list type="bullet">
    /// <item>공중 발판만 있는 지형(Stage2/3) — 가장 낮은 면(공용 지면)이 그대로 바닥이다. 예전 「가장 낮은 면」 규칙과 결과가 같다.</item>
    /// <item>지면 위에 올린 테라스(Stage1) — 지면 윗면은 테라스 안에 묻히므로 건너뛰고 테라스 윗면이 바닥이 된다.
    /// 예전 규칙이면 적·문이 테라스 속(지면 높이)에 섰다.</item>
    /// </list>
    /// </summary>
    public static class MapFloorRule
    {
        /// <summary>윗면 바로 위를 찔러 볼 높이. 이만큼 위가 다른 충돌체 안이면 그 면은 묻힌 것이다.</summary>
        public const float FREE_PROBE_HEIGHT = 0.05f;

        /// <summary>
        /// <paramref name="tops"/>(한 세로선에서 맞힌 윗면 높이들) 중 바닥을 고른다.
        /// <paramref name="isBlocked"/>(높이 y 의 점이 어떤 충돌체 안인가)가 윗면 바로 위에서 false 인 것 중 가장 낮은 것.
        /// 고를 것이 없으면 false.
        /// </summary>
        public static bool TryPickFloor(IReadOnlyList<float> tops, Func<float, bool> isBlocked, out float floor)
        {
            floor = 0f;
            bool isFound = false;
            if (tops == null || isBlocked == null) return false;

            for (int i = 0; i < tops.Count; i++)
            {
                float top = tops[i];
                if (isFound && top >= floor) continue;
                if (isBlocked(top + FREE_PROBE_HEIGHT)) continue;
                floor = top;
                isFound = true;
            }
            return isFound;
        }
    }
}

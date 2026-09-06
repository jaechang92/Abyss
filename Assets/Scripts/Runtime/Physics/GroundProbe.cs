using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Runtime.Physics
{
    /// <summary>
    /// 발밑 접지 판정용 원형 오버랩 질의의 단일 출처(SoT).
    ///
    /// 🔑 <b>같은 질의가 두 곳에 있었다</b> — 런의 <c>PlayerCharacter.UpdateGrounded</c>와
    /// 로비의 <c>LobbyPlayerController.UpdateGrounded</c>. 물리 질의 자체는 완전히 같고
    /// <b>결과를 거르는 규칙만 다르다</b>(런은 적 콜라이더도 제외, 로비는 자기 자신만).
    /// 그래서 질의만 여기로 모으고 거르기는 호출부에 남긴다.
    ///
    /// 🔴 <b>Unity 6에서 <c>Physics2D.OverlapCircleNonAlloc</c>은 deprecated(CS0618)다.</b>
    /// 대체 API는 <see cref="ContactFilter2D"/>와 <c>List</c>를 받는 오버로드인데, 옮기는 과정에
    /// <b>조용히 동작이 바뀔 자리가 하나 있다</b> — 트리거 포함 여부다(<see cref="Overlap"/> 주석).
    /// 그 판단을 두 곳에 각각 적으면 언젠가 한쪽만 바뀐다.
    /// </summary>
    public static class GroundProbe
    {
        /// <summary>
        /// <paramref name="point"/> 중심 반경 <paramref name="radius"/> 안에서
        /// <paramref name="mask"/> 레이어의 콜라이더를 찾아 <paramref name="results"/>에 채운다.
        /// 반환값은 채워진 개수다.
        ///
        /// ⚠️ <b>트리거를 포함할지는 프로젝트 설정에서 읽는다</b>(<c>Physics2D.queriesHitTriggers</c>).
        /// 옛 <c>OverlapCircleNonAlloc</c>이 그 설정을 따랐기 때문이다 — 여기에 <c>true</c>나
        /// <c>false</c>를 적어 두면 설정을 바꾼 순간 접지 판정이 <b>오류 없이</b> 달라진다.
        /// (현재 프로젝트 설정은 <c>m_QueriesHitTriggers: 1</c>.)
        ///
        /// 📌 <paramref name="results"/>는 호출부가 <b>재사용</b>해야 GC가 없다. 리스트는 한 번
        /// 커진 뒤로는 용량을 유지하므로, static 버퍼로 두면 프레임마다 할당이 생기지 않는다
        /// (옛 NonAlloc 배열이 하던 역할).
        /// </summary>
        public static int Overlap(Vector2 point, float radius, LayerMask mask, List<Collider2D> results)
        {
            if (results == null) return 0;

            var filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
            filter.SetLayerMask(mask);   // useLayerMask 도 함께 켠다

            return Physics2D.OverlapCircle(point, radius, filter, results);
        }
    }
}

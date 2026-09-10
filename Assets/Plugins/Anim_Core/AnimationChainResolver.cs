using System.Collections.Generic;

namespace Anim.Core
{
    /// <summary>
    /// <b>대체 순서를 받아 재생할 것을 고른다.</b> 사슬의 <i>내용</i>은 게임이 정하고
    /// (어느 상태가 무엇으로 물러나는지는 그 게임의 연출 판단이다),
    /// 여기서는 <i>도는 방법</i>만 안다.
    ///
    /// 🔑 <b>Unity 타입을 하나도 쓰지 않는다.</b> 그래서 가짜 드라이버 하나로
    /// EditMode에서 전부 검증된다 — <c>Animator</c>가 끼면 재생 판정은 플레이 모드 없이 못 본다.
    /// 이 프로젝트에서 애니메이션 결손이 <b>오류가 아니라 화면으로만</b> 드러나는 종류라,
    /// 테스트로 잡을 수 있는 부분을 최대한 이쪽으로 옮겨 둔다.
    /// </summary>
    public static class AnimationChainResolver
    {
        /// <summary>
        /// <paramref name="chain"/> 앞에서부터 재생 가능한 첫 번째를 돌려준다.
        /// <b>사슬이 전부 비었으면 <c>null</c>이다</b> — 그림이 하나도 없는 대상이 실제로 있으므로
        /// (프로토 단계의 적처럼) 여기서 임의로 무언가를 고르지 않고 호출자에게 판단을 넘긴다.
        /// </summary>
        public static string Resolve(IReadOnlyList<string> chain, IAnimationDriver driver)
        {
            if (chain == null || driver == null) return null;

            for (int i = 0; i < chain.Count; i++)
            {
                string animationId = chain[i];
                if (string.IsNullOrEmpty(animationId)) continue;
                if (driver.HasClip(animationId)) return animationId;
            }

            return null;
        }
    }
}

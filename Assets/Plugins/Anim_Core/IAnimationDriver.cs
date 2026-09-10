using System;

namespace Anim.Core
{
    /// <summary>
    /// <b>클립 재생기의 계약.</b> "지금 무슨 애니메이션이어야 하는가"는 <b>여기서 정하지 않는다</b> —
    /// 그것은 게임의 상태 기계가 판단하고, 이 계약은 그 결론을 화면에 옮기기만 한다.
    ///
    /// 🔑 <b>판단자가 둘이 되지 않게 하는 것이 이 인터페이스의 존재 이유다.</b>
    /// Unity의 <c>Animator</c>는 그 자체가 상태 기계라, 컨트롤러에 전이를 그려 넣으면
    /// 게임 FSM과 <b>각자 다른 "현재 상태"를 갖는다.</b> 어느 쪽이 옳은지 코드로는 알 수 없고,
    /// 어긋나도 오류가 아니라 <b>화면만 이상해진다.</b>
    /// 그래서 재생기는 전이를 모르는 채로 두고 <see cref="Play"/>로만 움직인다.
    ///
    /// 📌 구현을 갈아끼울 수 있게 인터페이스로 둔다 — Animator 대신 Playables나
    /// <c>SpriteRenderer</c> 직접 구동으로 가더라도 부르는 쪽은 그대로다.
    /// 적처럼 요구가 가벼운 대상은 다른 구현을 쓰게 될 수 있다.
    /// </summary>
    public interface IAnimationDriver
    {
        /// <summary>마지막으로 <see cref="Play"/>된 애니메이션 이름. 아직 없으면 빈 문자열.</summary>
        string CurrentAnimationId { get; }

        /// <summary>
        /// <b>재생 가능한 클립이 실제로 있는가.</b> 폴백 사슬은 이 답으로 돈다.
        ///
        /// 🔴 <b>"상태가 있는가"와 같은 질문이 아니다.</b> 오버라이드 방식에서는
        /// 상태는 있는데 클립만 비어 있는 조합이 정상적으로 존재한다(그 폼은 아직 안 그려진 것).
        /// 상태만 보고 true를 돌려주면 사슬이 한 번도 안 돌고
        /// <b>화면은 직전 클립에 얼어붙는다</b> — 오류로 안 잡히는 종류의 결손이다.
        /// </summary>
        bool HasClip(string animationId);

        /// <summary>
        /// 그 애니메이션을 재생한다.
        /// </summary>
        /// <param name="restart">
        /// 이미 같은 것을 재생 중이어도 <b>처음부터 다시</b>. 연속 공격이 이것으로 산다 —
        /// 두 번째 입력에 이름이 안 바뀌므로, 끄면 그림이 멈춰 보인다.
        /// </param>
        /// <param name="immediate">
        /// <b>이번 프레임 안에</b> 화면에 반영한다. 끄면 다음 갱신까지 한 프레임 늦는다.
        /// 게임 FSM이 즉시 전이로 얻은 즉시성을 재생기에서 도로 잃지 않게 하는 장치다.
        /// </param>
        void Play(string animationId, bool restart, bool immediate);

        /// <summary>
        /// <b>재생 가능한 클립 구성이 바뀌었다</b>(폼 교체 등). 구독자는 현재 상태를 <b>다시 판정해야</b> 한다 —
        /// 달리던 중에 교체된 폼에 달리기 클립이 없으면 사슬을 다시 타고 내려가야 하기 때문이다.
        /// </summary>
        event Action OnClipsChanged;
    }
}

using System.Collections.Generic;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// 적 애니메이터 상태 이름 SoT + <see cref="EnemyStateIds"/>에서 애니메이션으로 가는 <b>폴백 사슬</b>.
    ///
    /// 🔑 <b>플레이어(<c>PlayerAnimationIds</c>)와 같은 계약이고 내용만 적다.</b> FSM 5상태를 그림 4장으로 받는다 —
    /// 순찰과 추적은 둘 다 「기어 온다」라 같은 클립이다(2026-09-18 근접 병사).
    ///
    /// 📌 서 있는 그림(Idle)은 없다. 적은 멈춰 있을 때도 움직임 클립을 튼다 —
    /// 추적 중 사거리 안에서 쿨다운을 기다리는 동안 제자리에서 기는 것처럼 보이는 것은 알고 둔 한계다.
    /// </summary>
    public static class EnemyAnimationIds
    {
        public const string Move = "Move";
        public const string Attack = "Attack";
        public const string Hit = "Hit";
        public const string Dead = "Dead";

        /// <summary>base 컨트롤러가 갖출 상태. 첫 항목이 Animator 기본 상태가 된다.</summary>
        public static readonly IReadOnlyList<string> All = new[] { Move, Attack, Hit, Dead };

        private static readonly string[] moveChain = { Move };
        private static readonly string[] attackChain = { Attack, Move };
        private static readonly string[] hitChain = { Hit, Move };

        // 🔴 사망이 Hit 를 거치는 것은 플레이어와 같은 이유다 — 죽었는데 기어 다니는 그림은 버그로 읽힌다.
        private static readonly string[] deadChain = { Dead, Hit, Move };

        public static IReadOnlyList<string> FallbackChain(string enemyStateId)
        {
            return enemyStateId switch
            {
                EnemyStateIds.Attack => attackChain,
                EnemyStateIds.Stagger => hitChain,
                EnemyStateIds.Dead => deadChain,
                _ => moveChain,
            };
        }

        /// <summary>한 번 재생하고 멈추는가. 클립 생성기와 재생기가 같은 답을 써야 해서 여기 둔다.</summary>
        public static bool IsOneShot(string animationStateId)
        {
            return animationStateId is Attack or Hit or Dead;
        }

        /// <summary>같은 상태로 다시 들어왔을 때 처음부터 — 경직 중 또 맞으면 젖혀지는 동작이 다시 나와야 한다.</summary>
        public static bool RestartsOnReenter(string animationStateId) => IsOneShot(animationStateId);
    }
}

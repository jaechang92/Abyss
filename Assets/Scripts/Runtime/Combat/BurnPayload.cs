namespace Abyss.Runtime.Combat
{
    /// <summary>
    /// 연소 부여 명세 — "얼마나 태우는가"를 부여 시점에 확정해 실어 나르는 값 객체.
    ///
    /// <b>스택당 초당 피해를 여기에 담는 이유</b>: 연소 강화(Passive, 연소 피해 +50%) 같은
    /// 플레이어 측 보정을 적이 tick마다 되묻게 하면 적이 플레이어의 스킬 구성을 알아야 한다
    /// (의존 방향 역전). 부여 시점에 배율을 곱해 확정해 두면 적은 "초당 N 피해, T초"만 알면 된다.
    /// 부수적으로 "버프 중에 건 불은 버프가 끝나도 그대로 탄다"는 자연스러운 규칙이 따라온다.
    ///
    /// 적 발사체도 같은 <see cref="Projectile"/>을 쓰므로 기본값(연소 없음)이 그대로 통과해야 한다.
    /// </summary>
    public readonly struct BurnPayload
    {
        /// <summary>부여할 스택 수. 0이면 연소를 걸지 않는다.</summary>
        public readonly int Stacks;

        /// <summary>지속시간(초).</summary>
        public readonly float Duration;

        /// <summary>스택 1개가 1초에 주는 피해(부여자 보정이 이미 반영된 확정값).</summary>
        public readonly float DamagePerStackPerSecond;

        public BurnPayload(int stacks, float duration, float damagePerStackPerSecond)
        {
            Stacks = stacks;
            Duration = duration;
            DamagePerStackPerSecond = damagePerStackPerSecond;
        }

        /// <summary>실제로 태울 내용이 있는지. 셋 중 하나라도 0이면 연소가 성립하지 않는다.</summary>
        public bool HasBurn => Stacks > 0 && Duration > 0f && DamagePerStackPerSecond > 0f;

        /// <summary>스택당 피해에 배율을 먹인 사본(연소 강화 등 부여자 측 보정).</summary>
        public BurnPayload Scaled(float multiplier)
            => new(Stacks, Duration, DamagePerStackPerSecond * multiplier);
    }
}

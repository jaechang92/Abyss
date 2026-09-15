using UnityEngine;

namespace Abyss.Runtime.Weapon
{
    /// <summary>
    /// 무기 하나. <c>Resources/Data/Weapons/</c> 에 놓으면 <see cref="WeaponCatalog"/>가 자동으로 편입한다
    /// (<c>14-weapon-equipment-system.md</c> §9-1).
    ///
    /// 🔑 <b>무기는 폼 전용이다.</b> <see cref="formBound"/> 가 비면 어느 폼도 못 쓰므로 필수다 —
    /// 「검사는 검만 든다」가 전제이고, 그 덕에 자루 위치 편차가 작아 앵커 규약이 성립한다(§6).
    ///
    /// 🔴 <b>그림 세 칸이 한 벌이다.</b> <see cref="sprite"/> 하나만 채우면 무기가 안 돌고,
    /// <see cref="angleSprites"/> 만 채우면 그 폴백이 없다. 방패처럼 안 도는 무기는
    /// <see cref="bracedUpright"/> 를 켜고 <see cref="sprite"/> 만 채우면 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponData", menuName = "Abyss/Data/Weapon Data")]
    public sealed class WeaponData : ScriptableObject
    {
        [Header("식별자")]
        [Tooltip("로깅·중복 판정에 쓰이는 고유 ID. 예: rusted_blade")]
        public string weaponId;

        [Tooltip("🔴 필수. 이 무기를 쓸 수 있는 폼 ID(FormData.formId 와 같아야 한다).")]
        public string formBound;

        public WeaponRarity rarity = WeaponRarity.Common;

        [Header("표시 정보")]
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;

        [Header("손에 얹힐 그림")]
        [Tooltip("기본 그림. 🔑 피벗이 곧 손이 쥐는 지점이다(weapon-attachment.md §2).")]
        public Sprite sprite;

        [Tooltip("각도별로 미리 돌려 구운 그림. 0번이 그려진 그대로이고 시계방향으로 360/N 도씩. " +
                 "비우면 sprite 를 Transform 으로 돌린다(픽셀이 뭉갠다).")]
        public Sprite[] angleSprites;

        [Tooltip("켜면 앵커 각도를 무시하고 세워 든다. 방패처럼 버티는 무기. " +
                 "이때 angleSprites 는 0번만 쓰이므로 안 채워도 된다.")]
        public bool bracedUpright;

        [Header("전투")]
        [Tooltip("기본 공격 배율. PlayerCharacter 의 버프·메타 배율과 곱해진다.")]
        [Min(0f)] public float attackMultiplier = 1f;

        [Tooltip("중복 획득 1회당 늘어나는 배율(§7 중복=강화).")]
        [Min(0f)] public float upgradeMultiplierStep = 0.1f;

        /// <summary>
        /// 강화 단계를 반영한 배율. <paramref name="upgradeLevel"/> 0 이 기본이다.
        ///
        /// 🔑 <b>배율 계산을 여기 두는 이유</b> — 「기본 + 단계 x 증가분」이라는 규약이
        /// 전투·상점·툴팁 세 곳에 흩어지면 갈린다. 이 프로젝트가 화폐 표기·정렬 순위에서
        /// 이미 겪은 파편화라, 식은 데이터 옆에 하나만 둔다.
        /// </summary>
        public float MultiplierAt(int upgradeLevel)
            => attackMultiplier + Mathf.Max(0, upgradeLevel) * upgradeMultiplierStep;
    }
}

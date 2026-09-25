using Abyss.Runtime.Localization;
using GAS.Core;
using UnityEngine;

namespace Abyss.Runtime.Form
{
    /// <summary>
    /// 폼(직업) 정의 ScriptableObject. 프로토 최소 필드.
    /// 확장 필드(점프 횟수·대시 스타일·패시브·근/원거리 카테고리 등)는 P-14에서 추가.
    /// </summary>
    [CreateAssetMenu(fileName = "FormData", menuName = "Abyss/Data/Form Data")]
    public sealed class FormData : ScriptableObject
    {
        [Header("식별자")]
        [Tooltip("로깅·세이브에 쓰이는 고유 ID. 예: dark_blade, void_archer")]
        public string formId;

        [Tooltip("제단에서 해금해야 쓸 수 있는가. 기본 false = 처음부터 열려 있다.")]
        public bool requiresMetaUnlock;

        [Header("표시 정보")]
        public string displayName;

        [Tooltip("이름 StringKey (GameText.csv). 비우면 displayName을 그대로 쓴다.")]
        public string nameKey;

        /// <summary>
        /// 화면에 찍을 폼 이름 — 폼 이름을 보여주는 모든 화면이 이것 하나를 쓴다.
        /// nameKey가 있으면 현재 언어, 없으면 displayName, 그것도 없으면 formId로 물러난다.
        /// </summary>
        public string LocalizedName =>
            !string.IsNullOrEmpty(nameKey) ? Loc.Get(nameKey)
            : string.IsNullOrEmpty(displayName) ? formId : displayName;

        [Tooltip("게임 화면에 그려질 몸통 스프라이트. 비우면 기존 흰 사각형 + castColor 틴트로 폴백.")]
        public Sprite bodySprite;

        [Tooltip("이 폼의 애니메이션 한 벌(AnimatorOverrideController). 비우면 bodySprite 정지 그림으로 폴백.")]
        public RuntimeAnimatorController animatorController;

        [Tooltip("이 폼의 프레임별 무기 배치. 비우면 무기가 화면에서 숨는다 — 틀린 자리에 박히는 것보다 낫다.")]
        public Player.WeaponAnchorSet weaponAnchors;

        [Tooltip("🔴 이 폼을 얻으면 딸려 오는 기본 무기. 비우면 맨손으로 시작한다. " +
                 "WeaponData.formBound 가 이 폼의 formId 와 같아야 한다.")]
        public Weapon.WeaponData defaultWeapon;
        [TextArea(2, 4)] public string description;
        public Sprite icon;

        [Header("스탯 보정 (RunConfig.baseHp·moveSpeed 대비 배율)")]
        [Range(0.5f, 2f)] public float hpMultiplier = 1f;
        [Range(0.5f, 2f)] public float moveSpeedMultiplier = 1f;
        [Tooltip("폼별 다단 점프 횟수. FormA(암흑검사)=2, FormB(공허궁수)=3 (기획 02-form-change-system.md)")]
        [Min(1)] public int jumpCount = 2;

        [Header("기본 공격 방식")]
        [Tooltip("Melee = 몸 앞 판정 박스 · Ranged = 발사체. 비워 둔 기존 에셋은 Melee 로 읽힌다.")]
        public FormAttackStyle attackStyle = FormAttackStyle.Melee;

        [Tooltip("Ranged 일 때 약공격. 빠른 단발")]
        public RangedAttackSpec rangedLight;

        [Tooltip("Ranged 일 때 강공격. 관통")]
        public RangedAttackSpec rangedHeavy;

        [Tooltip("켜면 강공격 키 = 가드, 강공격은 가드 성공 시 자동 반격으로만 나간다(방패병)")]
        public FormGuardSpec guard;

        [Header("폼 전용 어빌리티 (P-14에서 채움)")]
        public AbilityData primaryAction;
        public AbilityData secondaryAction;

        [Header("폼 스킬 개시 연출")]
        [Tooltip("이 폼의 formBound 전용 스킬 발동 시 본체 개시 플래시 색상. " +
                 "기본값은 공용 청록 — 폼별로 지정하면 발동한 폼을 색으로 구분한다.")]
        public Color castColor = new(0.6f, 0.9f, 1f, 1f);
    }
}

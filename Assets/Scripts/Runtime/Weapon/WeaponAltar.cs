using Abyss.Runtime.Events;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Run;
using UnityEngine;

namespace Abyss.Runtime.Weapon
{
    /// <summary>
    /// 런 도중 배치되는 무기 보상 제단. 획득 3창구 중 첫째다(§3-B).
    /// <c>FormAltar</c>와 <b>형태는 같고 결말이 다르다</b> — 근접 감지·발동은 <c>PlayerInteractor</c>가
    /// 맡고(<c>IInteractable</c>), 1회성이며 소비 후 흐려진다.
    ///
    /// 🔴 <b>모달이 없다. 상호작용이 곧 획득이다</b>(「확정 지급」 — §3-B).
    /// 폼 제단은 <b>어느 슬롯에 넣을지</b>를 물어야 해서 모달이 필요했지만, 무기는 폼당 한 자루라
    /// 물을 것이 없다. 물을 것이 없는데 창을 띄우면 [확인]만 누르는 절차가 된다.
    ///
    /// 🔑 <b>무엇을 얻는지는 프롬프트가 말한다.</b> 모달을 안 띄우는 대신
    /// <see cref="InteractionPrompt"/>가 무기 이름과 <b>신규/강화</b>를 미리 보여준다 —
    /// 안 그러면 플레이어는 뭘 주웠는지 모른 채 방을 넘어간다.
    /// (「표시가 로직보다 앞서면 결손이 가려진다」의 반대편 실패다.)
    /// </summary>
    public sealed class WeaponAltar : MonoBehaviour, IInteractable
    {
        [Tooltip("이 제단이 주는 무기. StageDirector가 룸 클리어 시 Configure로 꽂는다.")]
        [SerializeField] private WeaponData rewardWeapon;

        [Tooltip("프롬프트 StringKey(비우면 무기 이름으로 만든 기본 문구).")]
        [SerializeField] private string promptKey;

        [Tooltip("소비 시 흐려질 스프라이트(비우면 자기 SpriteRenderer 자동 사용).")]
        [SerializeField] private SpriteRenderer visual;

        [Tooltip("소비 후 스프라이트 알파 배율(시각적 비활성 표시).")]
        [SerializeField] private float consumedAlphaScale = 0.35f;

        private bool consumed;
        private float baseAlpha = 1f;

        /// <summary>
        /// 무엇을 얻는지 + 이미 가진 것인지. 🔑 <b>「강화」로 읽혀야 중복이 꽝으로 안 보인다.</b>
        /// </summary>
        public string InteractionPrompt
        {
            get
            {
                if (!string.IsNullOrEmpty(promptKey)) return Loc.Get(promptKey);
                if (rewardWeapon == null) return "무기 획득 (G)";

                string label = string.IsNullOrEmpty(rewardWeapon.displayName)
                    ? rewardWeapon.weaponId
                    : rewardWeapon.displayName;

                var inventory = ResolveInventory();
                bool owned = inventory != null && inventory.Owns(rewardWeapon);
                return owned ? $"{label} 강화 (G)" : $"{label} 획득 (G)";
            }
        }

        // 이미 소비했거나 보상이 비어 있으면 상호작용 불가 — PlayerInteractor의 후보 선정에서 자동 제외된다.
        public bool CanInteract => !consumed && rewardWeapon != null;

        private void Awake()
        {
            if (visual == null) visual = GetComponent<SpriteRenderer>();
            if (visual != null) baseAlpha = visual.color.a;
        }

        /// <summary>
        /// 런타임 재설정. 보상 무기를 갈아끼우고 재장전(소비 해제·시각 복원)한 뒤 활성화한다.
        /// 보상 룸 클리어 시 <c>StageDirector</c>가 호출 — 제단 하나를 룸마다 재사용한다(폼 제단과 같다).
        /// </summary>
        public void Configure(WeaponData reward)
        {
            // 비활성 상태면 먼저 켜서 Awake(visual/baseAlpha 확보)를 유발한다.
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            rewardWeapon = reward;
            consumed = false;
            if (visual != null)
            {
                var c = visual.color;
                c.a = baseAlpha;
                visual.color = c;
            }
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract) return;

            consumed = true;

            // 🔴 <b>먼저 담고 나서 알린다.</b> 순서가 뒤집히면 게이트가 풀려 방이 넘어가는 동안
            //    아직 인벤토리에 안 들어간 상태로 폼이 다시 비칠 수 있다.
            ResolveInventory()?.Grant(rewardWeapon);

            ApplyConsumedVisual();
            GameEvents.RaiseWeaponRewardResolved();
        }

        /// <summary>
        /// 이번 런의 무기 보유 상태. 런이 없으면 <c>null</c>이다.
        /// ⚠️ <c>HasInstance</c>로 묻는다 — <c>Instance</c>는 없으면 만들어 버린다.
        /// </summary>
        private static WeaponInventory ResolveInventory()
            => RunManager.HasInstance ? RunManager.Instance.Weapons : null;

        // 파괴/비활성 대신 흐리게 남겨 "이미 받은 제단"임을 알린다. 재상호작용은 CanInteract가 차단한다.
        private void ApplyConsumedVisual()
        {
            if (visual == null) return;
            var c = visual.color;
            c.a *= consumedAlphaScale;
            visual.color = c;
        }
    }
}

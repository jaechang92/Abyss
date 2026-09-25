using Abyss.Runtime.Form;
using Abyss.Runtime.Run;
using Abyss.Runtime.Weapon;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 룸 클리어 보상 제단(폼·무기) 담당. 본체(<c>StageDirector.cs</c>)에서 떼어 낸 이유는
    /// <b>500줄 규약</b>이고, 경계를 여기로 잡은 이유는 <b>보상이 늘어나는 축</b>이기 때문이다 —
    /// 획득 3창구 중 상점·가차가 뒤따라오면 이 파일만 자란다(§3-B).
    ///
    /// 🔴 <b>한 방은 제단을 하나만 연다.</b> 폼 보상이 먼저 잡히고, 그 방에서 무기 보상은 안 열린다.
    /// 둘 다 열면 제단 두 개가 같은 자리에 겹치고 게이트 해제가 한쪽만 와서 방이 영영 안 넘어간다.
    /// (<c>RoomData</c>의 툴팁이 같은 말을 데이터 쪽에서 한다.)
    /// </summary>
    public sealed partial class StageDirector
    {
        [Tooltip("무기 보상 룸에서 활성화할 제단. 비우면 무기 보상 룸이 있어도 게이트 없이 진행.")]
        [SerializeField] private WeaponAltar weaponAltar;

        /// <summary>
        /// 이 방의 보상 제단을 연다. <b>게이트를 걸었으면 <c>true</c></b> — 호출자는 자동 진행을 멈춘다.
        ///
        /// 제단이 미배선이거나 제시할 것이 없으면 <b>게이트를 걸지 않고</b> <c>false</c>를 준다(스톨 방지).
        /// 보상이 안 나오는 것은 방이 안 넘어가는 것보다 훨씬 가벼운 고장이다.
        /// </summary>
        private bool TryOpenRewardAltar(RoomData room)
        {
            // 폼 보상 룸: 제단을 활성화하고 상호작용(획득/거절)까지 자동 진행을 보류한다.
            if (room.IsFormRewardRoom && formAltar != null)
            {
                var reward = ResolveRewardForm(room);
                if (reward != null)
                {
                    formAltar.Configure(reward);
                    PlaceRewardAltar(formAltar.transform);
                    isRoomGateHeld = true;
                    Debug.Log($"[StageDirector] 폼 보상 룸 — 제단 활성화({reward.formId}), 상호작용까지 진행 보류");
                    return true;
                }

                Debug.LogWarning($"[StageDirector] 폼 보상 룸({room.roomId})이나 제시할 폼이 없어 게이트 없이 진행 — FormCatalog 비어 있음 여부 확인");
                return false;
            }

            // 무기 보상 룸: 확정 지급이라 모달이 없다. 상호작용 자체가 획득이다(WeaponAltar).
            if (room.IsWeaponRewardRoom && weaponAltar != null)
            {
                var weapon = ResolveRewardWeapon(room);
                if (weapon != null)
                {
                    weaponAltar.Configure(weapon);
                    PlaceRewardAltar(weaponAltar.transform);
                    isRoomGateHeld = true;
                    Debug.Log($"[StageDirector] 무기 보상 룸 — 제단 활성화({weapon.weaponId}), 상호작용까지 진행 보류");
                    return true;
                }

                Debug.LogWarning($"[StageDirector] 무기 보상 룸({room.roomId})이나 제시할 무기가 없어 게이트 없이 진행 — " +
                                 "현재 폼의 formId 와 맞는 WeaponData.formBound 가 Resources/Data/Weapons 에 있는지 확인");
            }

            return false;
        }

        /// <summary>
        /// 보상 룸이 제시할 폼을 결정한다.
        /// 1) 룸에 고정 폼이 지정되어 있으면 그대로(레거시·의도적 고정 보상).
        /// 2) 아니면 FormCatalog에서 플레이어 미보유 폼을 우선 추첨.
        /// 3) 미보유가 없으면(전 폼 보유) 카탈로그 전체에서 현재 슬롯 밖 폼을 추첨, 그마저 없으면 null(게이트 스킵).
        /// </summary>
        private FormData ResolveRewardForm(RoomData room)
        {
            if (room.formReward != null) return room.formReward;

            var catalog = FormCatalog.All;
            if (catalog == null || catalog.Length == 0) return null;

            var form = ResolveFormController();
            var owned = form != null ? form.GetOwnedForms() : null;

            var candidates = FormCatalog.GetExcluding(owned);
            if (candidates.Count > 0)
            {
                return candidates[UnityEngine.Random.Range(0, candidates.Count)];
            }

            // 전 폼 보유 상태 — 중복이라도 제시해 보상 룸이 빈손이 되지 않게 한다.
            // GetExcluding과 동일하게 null 엔트리를 배제한 뒤 뽑는다(카탈로그에 깨진 참조가 섞여도 안전).
            var fallback = FormCatalog.GetExcluding(null);
            return fallback.Count > 0 ? fallback[UnityEngine.Random.Range(0, fallback.Count)] : null;
        }

        /// <summary>
        /// 보상 룸이 제시할 무기를 결정한다. <b>추첨 규칙 자체는 <see cref="WeaponReward"/>가 갖는다</b> —
        /// 여기는 씬에서 값을 모아 넘기는 일만 한다(그래야 규칙이 씬 없이 테스트된다).
        ///
        /// 🔴 <b>등급 가중치는 <c>RunConfig</c>에서 온다.</b> 여기서 배열을 짜 넣으면
        /// 밸런스가 코드에 묻히고, <c>WeaponDraw</c>가 가중치를 일부러 안 갖는 이유가 무너진다.
        /// 설정이 없으면 뽑지 않는다 — 임의의 기본값으로 조용히 굴러가는 쪽이 더 나쁘다.
        /// </summary>
        private WeaponData ResolveRewardWeapon(RoomData room)
        {
            if (room.weaponReward != null) return room.weaponReward;

            var run = RunManager.HasInstance ? RunManager.Instance : null;
            float[] weights = run != null && run.Config != null ? run.Config.weaponRarityWeights : null;
            if (weights == null || weights.Length == 0)
            {
                Debug.LogWarning("[StageDirector] RunConfig.weaponRarityWeights 가 비어 무기 보상을 뽑지 못했다");
                return null;
            }

            var form = ResolveFormController();
            string formId = form != null && form.CurrentForm != null ? form.CurrentForm.formId : null;

            return WeaponReward.Resolve(null, WeaponCatalog.All, formId, weights);
        }

        /// <summary>
        /// 씬의 FormController를 지연 조회해 캐시한다(런 씬에서 플레이어는 1명).
        /// StageDirector는 플레이어 생성 순서에 의존하지 않으므로 직렬화 참조 대신 필요 시점에 찾는다.
        /// </summary>
        private FormController ResolveFormController()
        {
            if (cachedFormController == null)
            {
                cachedFormController = FindAnyObjectByType<FormController>();
            }
            return cachedFormController;
        }

        // 폼 보상 모달이 닫히면(획득/거절) 제단을 숨기고 게이트를 푼다.
        private void HandleFormRewardResolved()
        {
            if (!isRoomGateHeld) return;

            if (formAltar != null) formAltar.gameObject.SetActive(false);
            ReleaseRoomGate("폼 보상 해결");
        }

        // 무기 제단을 집으면(획득은 제단이 이미 끝냈다) 제단을 숨기고 게이트를 푼다.
        private void HandleWeaponRewardResolved()
        {
            if (!isRoomGateHeld) return;

            if (weaponAltar != null) weaponAltar.gameObject.SetActive(false);
            ReleaseRoomGate("무기 보상 해결");
        }
    }
}

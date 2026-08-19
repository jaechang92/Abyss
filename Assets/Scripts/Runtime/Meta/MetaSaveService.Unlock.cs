using UnityEngine;

namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// <see cref="MetaSaveService"/>의 <b>메타 해금</b> 부분(완주 루프 계획 3-2).
    /// (본체와 같은 partial 클래스라 EnsureLoaded·current·Save를 공유한다.)
    ///
    /// 🔑 <b>잠금은 옵트인이다.</b> <c>requiresMetaUnlock</c>이 true인 에셋만 잠기고,
    /// 그 값이 false면 해금 목록을 <b>보지도 않는다</b>. 그래서 목록이 비어 있어도
    /// 아무것도 안 잠긴다 — <b>빈 목록의 기본 답이 false가 아니라 true다.</b>
    ///
    /// 이 규약이 2026-08-14의 함정을 막는다. 그때는 <c>UnlockForm</c> 호출부가 0곳이라
    /// 목록이 항상 비어 있었고, 그것을 게이트로 쓴 각인사 내력이 <b>어떤 조건으로도 안 열렸다</b>
    /// (오류는 안 났다). 지금은 <c>TryPurchaseUpgrade</c>가 유일한 기록자다.
    ///
    /// ⚠️ "실제로 써 본 폼"을 묻는 것이라면 여기가 아니라 <c>IsFormDiscovered</c>(도감 발견)다.
    /// </summary>
    public sealed partial class MetaSaveService
    {
        /// <summary>
        /// 폼 해제. 중복 ID·빈 문자열 무시.
        /// </summary>
        public bool UnlockForm(string formId, bool autoSave = true)
        {
            if (string.IsNullOrEmpty(formId)) return false;
            EnsureLoaded();
            if (current.unlockedFormIds.Contains(formId)) return false;
            current.unlockedFormIds.Add(formId);
            if (autoSave) Save();
            return true;
        }

        /// <summary>
        /// 이 폼을 쓸 수 있는가. <b>기본은 열려 있다</b> — <see cref="Form.FormData.requiresMetaUnlock"/>이
        /// true인 것만 잠기고, 그 값이 false면 이 목록을 보지도 않는다.
        ///
        /// 🔴 <b>2026-08-14의 함정을 이 규약이 막는다.</b> 그때는 <c>UnlockForm</c> 호출부가 0곳이라
        /// 목록이 항상 비어 있었고, 그것을 게이트로 쓴 각인사 내력이 <b>어떤 조건으로도 안 열렸다</b>
        /// (오류는 안 났다). 그래서 한동안 조회 API 자체를 두지 않았다.
        /// 이제는 <b>"잠금은 옵트인"</b>이라 목록이 비어 있어도 아무것도 안 잠긴다 —
        /// 빈 목록의 기본 답이 false가 아니라 <b>true</b>다.
        ///
        /// ⚠️ "실제로 써 본 폼"을 묻는 것이라면 이게 아니라 <see cref="IsFormDiscovered"/>다.
        /// </summary>
        public bool IsFormUnlocked(Form.FormData form)
        {
            if (form == null) return false;
            if (!form.requiresMetaUnlock) return true;
            EnsureLoaded();
            return current.unlockedFormIds.Contains(form.formId);
        }

        /// <summary>
        /// 이 스킬이 드래프트 풀에 드는가. 폼과 같은 규약 — <b>기본은 풀에 있다.</b>
        /// </summary>
        public bool IsSkillUnlocked(Draft.SkillData skill)
        {
            if (skill == null) return false;
            if (!skill.requiresMetaUnlock) return true;
            EnsureLoaded();
            return current.unlockedSkillIds.Contains(skill.skillId);
        }

        /// <summary>
        /// 스킬 해제. 중복 ID·빈 문자열 무시.
        /// </summary>
        public bool UnlockSkill(string skillId, bool autoSave = true)
        {
            if (string.IsNullOrEmpty(skillId)) return false;
            EnsureLoaded();
            if (current.unlockedSkillIds.Contains(skillId)) return false;
            current.unlockedSkillIds.Add(skillId);
            if (autoSave) Save();
            return true;
        }

        /// <summary>
        /// 해금 항목이면 대상 id를 해금 목록에 넣는다. 대상 id가 비어 있으면 아무것도 안 한다 —
        /// <b>그 경우 조용히 넘어가지 않고 경고를 남긴다.</b> 조각만 빠져나가고 해금이 안 되는 것은
        /// 플레이어에게 손해인데 오류로는 안 드러난다.
        /// </summary>
        private void ApplyUnlockIfAny(MetaUpgradeData data)
        {
            if (!data.IsUnlock) return;

            if (string.IsNullOrEmpty(data.unlockTargetId))
            {
                Debug.LogWarning(
                    $"[MetaSaveService] 해금 대상이 비어 있다 — upgradeId: \"{data.upgradeId}\". " +
                    "조각만 차감되고 아무것도 안 열린다. 에셋의 unlockTargetId를 확인할 것.");
                return;
            }

            if (data.type == MetaUpgradeType.UnlockForm) UnlockForm(data.unlockTargetId, autoSave: false);
            else UnlockSkill(data.unlockTargetId, autoSave: false);
        }
    }
}

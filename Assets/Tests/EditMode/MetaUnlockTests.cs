using Abyss.Runtime.Draft;
using Abyss.Runtime.Form;
using Abyss.Runtime.Meta;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 메타 해금(완주 루프 계획 3-2)의 불변식. 핵심은 하나다 ―
    /// <b>잠금은 옵트인이고, 빈 해금 목록의 기본 답은 true다.</b>
    ///
    /// 이 규약이 없으면 2026-08-14의 함정이 재현된다. 그때 <c>unlockedFormIds</c>는
    /// <c>UnlockForm</c> 호출부가 0곳이라 <b>항상 비어 있었고</b>, 그것을 게이트로 쓴 각인사 내력이
    /// <b>어떤 조건으로도 안 열렸다</b> — 그리고 오류는 안 났다.
    ///
    /// 지금은 콘텐츠를 하나도 안 잠갔으므로(기존 폼 4종·스킬 18종 전부 <c>requiresMetaUnlock=false</c>)
    /// <b>이 테스트가 "아무것도 안 잠겼다"를 지키는 역할도 한다.</b>
    /// </summary>
    public sealed class MetaUnlockTests
    {
        private MetaSaveService service;
        private FormData openForm;
        private FormData lockedForm;
        private SkillData openSkill;
        private SkillData lockedSkill;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("[Test] MetaSaveService");
            service = go.AddComponent<MetaSaveService>();
            service.ResetAll(autoSave: false);

            openForm = ScriptableObject.CreateInstance<FormData>();
            openForm.formId = "open_form";
            openForm.requiresMetaUnlock = false;

            lockedForm = ScriptableObject.CreateInstance<FormData>();
            lockedForm.formId = "locked_form";
            lockedForm.requiresMetaUnlock = true;

            openSkill = ScriptableObject.CreateInstance<SkillData>();
            openSkill.skillId = "open_skill";
            openSkill.requiresMetaUnlock = false;

            lockedSkill = ScriptableObject.CreateInstance<SkillData>();
            lockedSkill.skillId = "locked_skill";
            lockedSkill.requiresMetaUnlock = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (service != null) Object.DestroyImmediate(service.gameObject);
            foreach (var so in new Object[] { openForm, lockedForm, openSkill, lockedSkill })
            {
                if (so != null) Object.DestroyImmediate(so);
            }
        }

        // ───────────────────────── 옵트인 규약 ─────────────────────────

        /// <summary>🔴 이 테스트가 이 설계의 이유다.</summary>
        [Test]
        public void 해금_목록이_비어도_잠기지_않은_것은_열려_있다()
        {
            Assert.IsEmpty(service.Current.unlockedFormIds, "전제: 해금 목록이 비어 있다.");

            Assert.IsTrue(service.IsFormUnlocked(openForm),
                "requiresMetaUnlock=false인 폼은 목록을 보지도 않아야 한다.");
            Assert.IsTrue(service.IsSkillUnlocked(openSkill),
                "requiresMetaUnlock=false인 스킬은 목록을 보지도 않아야 한다.");
        }

        [Test]
        public void 잠긴_것은_해금_전까지_닫혀_있다()
        {
            Assert.IsFalse(service.IsFormUnlocked(lockedForm));
            Assert.IsFalse(service.IsSkillUnlocked(lockedSkill));
        }

        [Test]
        public void 해금하면_열린다()
        {
            service.UnlockForm(lockedForm.formId, autoSave: false);
            service.UnlockSkill(lockedSkill.skillId, autoSave: false);

            Assert.IsTrue(service.IsFormUnlocked(lockedForm));
            Assert.IsTrue(service.IsSkillUnlocked(lockedSkill));
        }

        [Test]
        public void null은_열리지_않는다()
        {
            Assert.IsFalse(service.IsFormUnlocked(null));
            Assert.IsFalse(service.IsSkillUnlocked(null));
        }

        // ───────────────────────── 구매 경로 ─────────────────────────

        /// <summary>
        /// 🔴 구매가 <c>unlockedFormIds</c>의 <b>유일한 기록자</b>다.
        /// 이 연결이 끊기면 조각만 빠져나가고 아무것도 안 열리는데 오류는 안 난다.
        /// </summary>
        [Test]
        public void 해금_항목을_사면_실제로_해금된다()
        {
            var data = UnlockUpgrade(MetaUpgradeType.UnlockForm, "locked_form", cost: 50);
            service.AddAbyssShards(100, autoSave: false);

            Assert.IsTrue(service.TryPurchaseUpgrade(data, autoSave: false));
            Assert.IsTrue(service.IsFormUnlocked(lockedForm), "구매했는데 해금이 안 됐다.");
            Assert.AreEqual(50, service.Current.abyssShardsTotal, "비용이 정확히 차감돼야 한다.");

            Object.DestroyImmediate(data);
        }

        [Test]
        public void 잔액이_모자라면_해금도_차감도_없다()
        {
            var data = UnlockUpgrade(MetaUpgradeType.UnlockForm, "locked_form", cost: 50);
            service.AddAbyssShards(10, autoSave: false);

            Assert.IsFalse(service.TryPurchaseUpgrade(data, autoSave: false));
            Assert.IsFalse(service.IsFormUnlocked(lockedForm));
            Assert.AreEqual(10, service.Current.abyssShardsTotal);

            Object.DestroyImmediate(data);
        }

        /// <summary>해금은 1회성이다 — costLadder 길이가 1이라 두 번째 구매는 최대 레벨에서 막힌다.</summary>
        [Test]
        public void 해금은_두_번_사지지_않는다()
        {
            var data = UnlockUpgrade(MetaUpgradeType.UnlockForm, "locked_form", cost: 50);
            service.AddAbyssShards(200, autoSave: false);

            Assert.IsTrue(service.TryPurchaseUpgrade(data, autoSave: false));
            Assert.IsFalse(service.TryPurchaseUpgrade(data, autoSave: false), "두 번째 구매가 통과했다.");
            Assert.AreEqual(150, service.Current.abyssShardsTotal, "두 번 차감되면 안 된다.");

            Object.DestroyImmediate(data);
        }

        // ───────────────────────── 현행 콘텐츠 보호 ─────────────────────────

        /// <summary>
        /// 🔵 3-2는 <b>"시스템은 짓되 기존 콘텐츠는 안 뺏는다"</b>로 결정됐다(2026-08-19).
        /// 2026-10-06 메타 해금 1단계에서 <b>신규 스킬만</b> 잠그기 시작했다 — 잠겨도 되는 것은 아래 목록뿐이다.
        /// 기존 폼·스킬이 잠기거나, 목록 밖 스킬이 잠기면 여기서 걸린다.
        /// </summary>
        private static readonly string[] LockableSkillIds =
        {
            SkillIds.DEATHS_PROMISE,
            SkillIds.CRIMSON_RADIANCE,
            SkillIds.FATES_FAVOR,
        };

        [Test]
        public void 기존_폼과_스킬은_잠기지_않고_잠금은_허용_목록뿐이다()
        {
            FormCatalog.Reload();
            foreach (var form in FormCatalog.All)
            {
                if (form == null) continue;
                Assert.IsFalse(form.requiresMetaUnlock,
                    $"{form.formId}가 잠겼다 — 기존 콘텐츠를 잠그는 것은 3-2의 결정 범위 밖이다.");
            }

            SkillCatalog.Reload();
            foreach (var skill in SkillCatalog.All)
            {
                if (skill == null || !skill.requiresMetaUnlock) continue;
                CollectionAssert.Contains(LockableSkillIds, skill.skillId,
                    $"{skill.skillId}가 잠겼다 — 메타 해금 허용 목록 밖이다(기존 콘텐츠는 안 뺏는다).");
            }
        }

        /// <summary>
        /// 잠긴 스킬마다 제단에 그것을 여는 항목이 있어야 한다. 없으면 그 스킬은 <b>오류 없이 영영 안 나온다</b>
        /// (2026-08-14 각인사 내력이 같은 모양으로 안 열렸다).
        /// </summary>
        [Test]
        public void 잠긴_스킬마다_제단_해금_항목이_있다()
        {
            SkillCatalog.Reload();
            MetaUpgrades.Reload();
            var upgrades = MetaUpgrades.All;

            foreach (var skill in SkillCatalog.All)
            {
                if (skill == null || !skill.requiresMetaUnlock) continue;

                bool hasUnlock = false;
                foreach (var upgrade in upgrades)
                {
                    if (upgrade != null && upgrade.type == MetaUpgradeType.UnlockSkill && upgrade.unlockTargetId == skill.skillId)
                    {
                        hasUnlock = true;
                        break;
                    }
                }
                Assert.IsTrue(hasUnlock, $"{skill.skillId}가 잠겼는데 제단 해금 항목이 없다 — 영영 드래프트에 안 나온다.");
            }
        }

        private static MetaUpgradeData UnlockUpgrade(MetaUpgradeType type, string targetId, int cost)
        {
            var data = ScriptableObject.CreateInstance<MetaUpgradeData>();
            data.upgradeId = "unlock_" + targetId;
            data.type = type;
            data.unlockTargetId = targetId;
            data.costLadder = new[] { cost };   // 길이 1 = 1회성 구매
            return data;
        }
    }
}

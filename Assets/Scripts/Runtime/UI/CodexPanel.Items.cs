using System.Collections.Generic;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Form;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Stage;
using UnityEngine;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// <see cref="CodexPanel"/>의 항목 수집·표시 변환 파트.
    /// 진입점·UI 조립은 CodexPanel.cs가 담당한다(파일 500줄 규약 대비 분할 — HudBuilder.Modals 선례).
    ///
    /// 카탈로그(폼·스킬·적)에서 읽은 SO를 화면이 그대로 쓸 수 있는 <see cref="CodexItem"/>으로 바꾼다.
    /// 표시 규약은 새로 만들지 않고 기존 SoT를 재사용한다 —
    /// 축은 <see cref="SynergyAxis"/>, 희귀도·카테고리는 <see cref="SkillDisplay"/>, 적 분류 색은 <see cref="RoomTypeDisplay"/>.
    /// </summary>
    public sealed partial class CodexPanel
    {
        /// <summary>도감 탭. 순서가 곧 화면의 탭 버튼 순서다.</summary>
        public enum CodexTab
        {
            Form,
            Skill,
            Enemy,
            Boss,
            Records
        }

        private const string UNKNOWN_NAME = "???";
        private const string UNKNOWN_GLYPH = "?";
        private const string EMPTY_VALUE = "—";

        /// <summary>미발견 항목의 실루엣 색. 형태만 남기고 색을 지운다.</summary>
        private static readonly Color SilhouetteColor = new(0.09f, 0.09f, 0.12f, 1f);

        /// <summary>미발견 타일 배경. 발견 항목의 배경색과 확실히 갈리도록 균일한 어두운 색 하나로 통일한다.</summary>
        private static readonly Color LockedTileColor = new(0.13f, 0.13f, 0.16f, 1f);

        private static readonly Color NeutralBadgeColor = new(0.78f, 0.80f, 0.86f);

        /// <summary>
        /// 도감 항목 1건의 표시 데이터. SO를 직접 들고 다니지 않는 이유는 탭마다 원본 타입이
        /// 다르기 때문이다(FormData·SkillData·EnemyData) — 화면은 같은 타일 하나로 그린다.
        /// </summary>
        private readonly struct CodexItem
        {
            public readonly string Name;
            public readonly Sprite Icon;
            public readonly Color IconTint;

            /// <summary>아이콘이 없을 때 대신 찍는 한 글자. 폼 4종·Passive 스킬은 아이콘 에셋이 아직 없다.</summary>
            public readonly string Glyph;

            public readonly Color TileColor;
            public readonly string Badge;
            public readonly Color BadgeColor;
            public readonly string Description;
            public readonly string Stats;
            public readonly bool IsDiscovered;

            public CodexItem(string name, Sprite icon, Color iconTint, string glyph, Color tileColor,
                             string badge, Color badgeColor, string description, string stats, bool isDiscovered)
            {
                Name = name;
                Icon = icon;
                IconTint = iconTint;
                Glyph = glyph;
                TileColor = tileColor;
                Badge = badge;
                BadgeColor = badgeColor;
                Description = description;
                Stats = stats;
                IsDiscovered = isDiscovered;
            }
        }

        // ───────────────────────── 탭별 수집 ─────────────────────────

        /// <summary>지정 탭의 항목을 카탈로그 순서대로 모은다. 기록 탭은 항목이 없다(빈 목록).</summary>
        private static List<CodexItem> Collect(CodexTab tab) => tab switch
        {
            CodexTab.Form => CollectForms(),
            CodexTab.Skill => CollectSkills(),
            CodexTab.Enemy => CollectEnemies(isBoss: false),
            CodexTab.Boss => CollectEnemies(isBoss: true),
            _ => new List<CodexItem>()
        };

        private static List<CodexItem> CollectForms()
        {
            var meta = MetaSaveService.Instance;
            var catalog = FormCatalog.All;
            var result = new List<CodexItem>(catalog.Length);

            for (int i = 0; i < catalog.Length; i++)
            {
                var form = catalog[i];
                if (form == null) continue;

                bool found = meta.IsFormDiscovered(form.formId);
                string name = Resolve(form.displayName, form.formId);

                // 폼 에셋에는 아직 아이콘이 없다(4종 전부). 대신 폼별로 이미 지정된 castColor를
                // 타일 색으로 쓴다 — 폼 스킬 개시 연출이 쓰는 것과 같은 색이라 화면 사이에서 폼 정체성이 이어진다.
                result.Add(new CodexItem(
                    name: found ? name : UNKNOWN_NAME,
                    icon: form.icon,
                    iconTint: found ? Color.white : SilhouetteColor,
                    glyph: found ? FirstGlyph(name) : UNKNOWN_GLYPH,
                    tileColor: found ? Dim(form.castColor) : LockedTileColor,
                    badge: found ? FormBadge(form) : string.Empty,
                    badgeColor: found ? form.castColor : NeutralBadgeColor,
                    description: found ? form.description : string.Empty,
                    stats: found ? FormStats(form) : string.Empty,
                    isDiscovered: found));
            }
            return result;
        }

        private static List<CodexItem> CollectSkills()
        {
            var meta = MetaSaveService.Instance;
            var catalog = SkillCatalog.All;
            var result = new List<CodexItem>(catalog.Length);

            for (int i = 0; i < catalog.Length; i++)
            {
                var skill = catalog[i];
                if (skill == null) continue;

                bool found = meta.IsSkillDiscovered(skill.skillId);
                string name = Resolve(skill.displayName, skill.skillId);

                result.Add(new CodexItem(
                    name: found ? name : UNKNOWN_NAME,
                    icon: skill.icon,
                    iconTint: found ? Color.white : SilhouetteColor,
                    glyph: found ? FirstGlyph(name) : UNKNOWN_GLYPH,
                    tileColor: found ? SkillDisplay.RarityBackground(skill.rarity) : LockedTileColor,
                    badge: found ? SkillBadge(skill) : string.Empty,
                    badgeColor: found ? SynergyAxis.GetColor(skill.synergyTag) : NeutralBadgeColor,
                    description: found ? skill.description : string.Empty,
                    stats: found ? SkillStats(skill) : string.Empty,
                    isDiscovered: found));
            }
            return result;
        }

        private static List<CodexItem> CollectEnemies(bool isBoss)
        {
            var meta = MetaSaveService.Instance;
            var catalog = EnemyCatalog.GetByBossFlag(isBoss);
            var result = new List<CodexItem>(catalog.Count);

            for (int i = 0; i < catalog.Count; i++)
            {
                var enemy = catalog[i];
                bool found = meta.IsEnemyDiscovered(enemy.enemyId);
                string name = Resolve(enemy.displayName, enemy.enemyId);
                var portrait = ResolvePortrait(enemy);

                result.Add(new CodexItem(
                    name: found ? name : UNKNOWN_NAME,
                    icon: portrait.sprite,
                    iconTint: found ? portrait.tint : SilhouetteColor,
                    glyph: found ? FirstGlyph(name) : UNKNOWN_GLYPH,
                    tileColor: found ? Dim(RoomTypeDisplay.Color(EnemyRoomType(enemy))) : LockedTileColor,
                    badge: found ? EnemyBadge(enemy) : string.Empty,
                    badgeColor: found ? RoomTypeDisplay.Color(EnemyRoomType(enemy)) : NeutralBadgeColor,
                    description: string.Empty,   // EnemyData에는 설명 필드가 없다 — 내용은 수치가 대신한다
                    stats: found ? EnemyStats(enemy) : string.Empty,
                    isDiscovered: found));
            }
            return result;
        }

        // ───────────────────────── 항목 → 표시 문구 ─────────────────────────

        private static string FormBadge(FormData form) =>
            $"HP ×{form.hpMultiplier:0.##} · 이동 ×{form.moveSpeedMultiplier:0.##} · {form.jumpCount}단 점프";

        private static string FormStats(FormData form)
        {
            var lines = new List<string> { $"ID: {form.formId}" };
            AppendAbility(lines, "주 공격", form.primaryAction);
            AppendAbility(lines, "보조", form.secondaryAction);
            return string.Join("\n", lines);
        }

        private static void AppendAbility(List<string> lines, string label, GAS.Core.AbilityData ability)
        {
            if (ability == null) return;
            string name = string.IsNullOrEmpty(ability.abilityName) ? ability.name : ability.abilityName;
            lines.Add($"{label}: {name}  (쿨 {ability.cooldownDuration:0.##}s)");
        }

        private static string SkillBadge(SkillData skill) => SkillDisplay.Headline(skill);

        private static string SkillStats(SkillData skill)
        {
            var lines = new List<string>();

            // 폼 귀속은 플레이어가 가장 먼저 확인하는 조건이라 맨 위에 둔다(이 폼이 아니면 아예 안 나온다).
            if (!string.IsNullOrEmpty(skill.formBound))
            {
                var form = FormCatalog.GetById(skill.formBound);
                string formName = form != null ? Resolve(form.displayName, form.formId) : skill.formBound;
                lines.Add($"전용 폼: {formName}");
            }
            else
            {
                lines.Add("전용 폼: 없음 (전 폼 공용)");
            }

            if (!string.IsNullOrEmpty(skill.formulaDescription)) lines.Add($"공식: {skill.formulaDescription}");
            if (skill.relatedAbility != null) lines.Add($"쿨다운: {skill.relatedAbility.cooldownDuration:0.##}s");
            if (skill.category == SkillCategory.Synergy)
            {
                lines.Add($"발동 조건: 같은 축 {SynergyAxis.ACTIVATION_THRESHOLD}개 이상 보유");
            }
            return string.Join("\n", lines);
        }

        private static string EnemyBadge(EnemyData enemy)
        {
            if (enemy.isBoss) return "보스";
            string tier = enemy.isElite ? "엘리트" : "일반";
            string range = enemy.isRanged ? "원거리" : "근접";
            return $"{tier} · {range}";
        }

        private static string EnemyStats(EnemyData enemy)
        {
            var lines = new List<string>
            {
                $"체력: {enemy.baseHp}",
                $"공격력: {enemy.baseDamage}",
                $"이동 속도: {enemy.moveSpeed:0.##}",
                $"감지 거리: {enemy.detectionRange:0.##}",
                $"공격 사거리: {enemy.attackRange:0.##}  (간격 {enemy.attackCooldown:0.##}s)",
                $"보상: EXP {enemy.expReward} · 골드 {enemy.goldReward}"
            };
            if (enemy.isRanged) lines.Add($"발사체 속도: {enemy.projectileSpeed:0.##}");
            return string.Join("\n", lines);
        }

        /// <summary>
        /// 적 분류를 방 타입으로 환산해 <see cref="RoomTypeDisplay"/>의 색을 빌린다.
        /// 색 규약을 새로 만들지 않는 이유: 플레이어는 노드 맵에서 이미 이 색으로 보스·엘리트를 배웠다.
        /// </summary>
        private static RoomType EnemyRoomType(EnemyData enemy)
        {
            if (enemy.isBoss) return RoomType.Boss;
            return enemy.isElite ? RoomType.Elite : RoomType.Combat;
        }

        /// <summary>
        /// 적 초상화. EnemyData에는 스프라이트 필드가 없어 스폰 프리팹의 SpriteRenderer에서 역참조한다 —
        /// 필드를 새로 만들면 적 에셋 9종을 다시 채우고 빌더를 재실행해야 하는데, 프리팹에 이미 있는 정보다.
        /// 비활성 자식까지 훑는다(연출용 오브젝트가 꺼진 채 붙어 있을 수 있다).
        /// </summary>
        private static (Sprite sprite, Color tint) ResolvePortrait(EnemyData enemy)
        {
            if (enemy.spawnPrefab == null) return (null, Color.white);

            var renderer = enemy.spawnPrefab.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null) return (null, Color.white);

            // 프리팹의 색을 그대로 쓴다 — 적 구분이 스프라이트가 아니라 색으로 되어 있는 경우가 있다.
            // 알파는 1로 올린다(월드에서 반투명이어도 도감에서는 또렷해야 한다).
            var tint = renderer.color;
            tint.a = 1f;
            return (renderer.sprite, tint);
        }

        // ───────────────────────── 기록 탭 ─────────────────────────

        /// <summary>
        /// 기록 탭 <b>왼쪽 열</b> — 직전 런 8줄.
        ///
        /// 왼쪽에 둔 이유: 도감을 여는 가장 흔한 순간이 방금 런을 끝낸 직후이고, 그때 알고 싶은 것은
        /// 누적 통계가 아니라 "방금 그 런이 어땠나"다. 읽기 시작하는 자리에 그것을 둔다.
        ///
        /// 문구는 결과·엔딩 화면과 같은 <see cref="RunSummaryText"/>를 쓴다 —
        /// 여기서 따로 조립하면 같은 런이 화면에 따라 다르게 보인다.
        /// </summary>
        private static string BuildLastRunText()
        {
            var summary = MetaSaveService.Instance.Current.lastRunSummary;

            var lines = new List<string> { "■ 직전 런" };

            if (summary != null && summary.hasRecord)
            {
                lines.AddRange(RunSummaryText.AllLines(summary));
            }
            else
            {
                // 아직 런을 끝낸 적이 없다. 8줄을 0으로 채워 보여주면 "0킬로 끝난 런"과 구분되지 않는다.
                lines.Add("아직 기록이 없다. 한 번 내려가 보라.");
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// 기록 탭 <b>오른쪽 열</b> — 역대 최고 기록·메타·도감 발견 수.
        /// 직전 런(<see cref="BuildLastRunText"/>)과 나눈 기준은 <b>수명</b>이다:
        /// 이쪽은 갱신될 때만 바뀌고, 저쪽은 매 런 덮인다.
        /// </summary>
        private static string BuildRecordsText()
        {
            var save = MetaSaveService.Instance.Current;
            var r = save.records;

            var lines = new List<string>
            {
                "■ 런 기록",
                $"누적 런: {r.totalRunCount}회",
                $"보스 격파: {r.totalBossKillCount}회",
                $"최고 도달: {r.bestReach.Describe(EMPTY_VALUE)}",
                $"최장 런: {FormatDuration(r.bestRunDurationSeconds)}",
                $"최고 골드 파편: {r.bestGoldShards}",
                $"엔딩: {(r.hasSeenEnding ? "도달 ✦ 심연 탈출" : "미도달")}",
                string.Empty,
                "■ 메타",
                $"심연 조각 누적: {save.abyssShardsTotal}",
                string.Empty,
                "■ 도감 발견",
                $"폼: {DiscoveredCount(CodexTab.Form)}",
                $"스킬: {DiscoveredCount(CodexTab.Skill)}",
                $"적: {DiscoveredCount(CodexTab.Enemy)}",
                $"보스: {DiscoveredCount(CodexTab.Boss)}"
            };
            return string.Join("\n", lines);
        }

        /// <summary>
        /// "발견/전체" 표기. 탭 헤더와 기록 탭이 같은 문구를 쓴다.
        /// 이미 수집한 목록이 있으면 <see cref="CountText"/>를 직접 쓸 것 — 여기는 다른 탭의 수를
        /// 물어보는 경로(기록 탭)라서 수집을 한 번 더 한다.
        /// </summary>
        private static string DiscoveredCount(CodexTab tab) => CountText(Collect(tab));

        private static string CountText(List<CodexItem> list)
        {
            int found = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].IsDiscovered) found += 1;
            }
            return $"{found} / {list.Count}";
        }

        private static string FormatDuration(float seconds)
        {
            if (seconds <= 0f) return EMPTY_VALUE;
            int mins = Mathf.FloorToInt(seconds / 60f);
            int secs = Mathf.FloorToInt(seconds % 60f);
            return $"{mins:D2}:{secs:D2}";
        }

        // ───────────────────────── 소소한 헬퍼 ─────────────────────────

        private static string Resolve(string value, string fallback) =>
            string.IsNullOrEmpty(value) ? fallback : value;

        /// <summary>아이콘 대신 찍을 한 글자. 이름 첫 글자를 쓴다(한글 이름이라 대개 충분히 구분된다).</summary>
        private static string FirstGlyph(string name) =>
            string.IsNullOrEmpty(name) ? UNKNOWN_GLYPH : name.Substring(0, 1);

        /// <summary>표시용 색을 타일 배경으로 쓸 수 있게 어둡게 낮춘다(위에 흰 글씨를 올리기 위함).</summary>
        private static Color Dim(Color color) =>
            new(color.r * 0.30f, color.g * 0.30f, color.b * 0.30f, 1f);
    }
}

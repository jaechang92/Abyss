using System.Collections.Generic;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Player;
using Abyss.Runtime.Run;
using Abyss.Runtime.Stage;
using GAS.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.Cheats
{
    /// <summary>
    /// <see cref="CheatMenu"/>의 영속 세이브 파트(500줄 규칙 분할).
    /// 스토리 진행도·도감 발견·언어 설정 — 런이 끝나도 남고, 한 번 차면 정상 플레이로는
    /// 되돌릴 수 없는 것들이라 되돌리는 수단이 특히 필요한 영역이다.
    /// </summary>
    public sealed partial class CheatMenu
    {
        // ====== 스토리(화자별) ======

        private void DrawStorySection()

        {

            GUILayout.Label("■ 스토리", headerStyle);

            var meta = MetaSaveService.Instance;

            if (meta == null)

            {

                GUILayout.Label("(MetaSaveService 없음)");

                GUILayout.Space(6);

                return;

            }



            DrawStorySpeaker(meta, "기록자", StorySpeakerIds.Chronicler);

            DrawStorySpeaker(meta, "각인사", StorySpeakerIds.Engraver);

            // 프롤로그는 세이브당 1회라, 이 버튼이 없으면 확인할 때마다 세이브를 지워야 한다.

            GUILayout.BeginHorizontal();

            GUILayout.Label($"프롤로그: {(meta.HasSeenPrologue ? "시청함" : "미시청")}");

            if (GUILayout.Button("다시 보기")) meta.ResetPrologueSeen();

            GUILayout.EndHorizontal();

            GUILayout.Label("※ 설정 시 스냅샷 0 리셋 — 다음 대화에서 해당 다음 챕터 열람 가능(누적 기준)");

            GUILayout.Label("※ 각인사 내력은 폼 해금이 조건이라 단계 +1만으로는 안 열린다(폼 해금 병행)");

            GUILayout.Space(6);

        }



        /// <summary>

        /// 화자 1명의 진행도 줄. 시청 기록이 집합이라 ±1은 "가장 높은 단계 하나를 넣고/빼기"로 옮긴다 —

        /// 순차 화자(기록자)에서는 옛 단계 ±1과 같고, 각인사에서는 리셋이 실질적인 도구다.

        /// </summary>

        private void DrawStorySpeaker(MetaSaveService meta, string label, string speakerId)

        {

            var save = meta.Current;

            meta.GetStorySnapshots(speakerId, out int runSnapshot, out int bossSnapshot);

            int runDelta = save.records.totalRunCount - runSnapshot;

            int bossDelta = save.records.totalBossKillCount - bossSnapshot;



            var viewed = meta.GetViewedChapterStages(speakerId);

            int highest = 0;

            foreach (int stage in viewed)

            {

                if (stage > highest) highest = stage;

            }



            string viewedText = viewed.Count > 0 ? string.Join(",", viewed) : "없음";

            GUILayout.Label($"{label}: 시청 [{viewedText}]  (런델타 {runDelta} / 보스델타 {bossDelta})");



            GUILayout.BeginHorizontal();

            if (GUILayout.Button("리셋")) meta.DebugSetViewedChapters(speakerId, null);

            if (GUILayout.Button("단계 -1")) meta.DebugSetViewedChapters(speakerId, StageRange(highest - 1));

            if (GUILayout.Button("단계 +1")) meta.DebugSetViewedChapters(speakerId, StageRange(highest + 1));

            GUILayout.EndHorizontal();

        }



        private static IEnumerable<int> StageRange(int highest)

        {

            for (int stage = 1; stage <= highest; stage++) yield return stage;

        }



        // ====== 도감 ======

        // 발견 상태는 영속 메타라 한 번 차면 되돌릴 방법이 없어, 실루엣·??? 표시를 다시 보려면

        // 초기화 수단이 반드시 필요하다. MetaSaveService가 아니라 여기에 두는 이유는

        // 카탈로그 3종(폼·스킬·적)을 훑어야 하고 그 의존을 저장 게이트웨이에 들일 이유가 없기 때문이다.

        private void DrawCodexSection()

        {

            GUILayout.Label("■ 도감", headerStyle);

            var meta = MetaSaveService.Instance;

            if (meta == null)

            {

                GUILayout.Label("(MetaSaveService 없음)");

                GUILayout.Space(6);

                return;

            }



            var save = meta.Current;

            GUILayout.Label($"발견 — 폼 {save.discoveredFormIds.Count} · 스킬 {save.discoveredSkillIds.Count} · 적/보스 {save.discoveredEnemyIds.Count}");



            GUILayout.BeginHorizontal();

            if (GUILayout.Button("전체 발견"))

            {

                foreach (var form in FormCatalog.All)

                {

                    if (form != null) meta.DiscoverForm(form.formId, autoSave: false);

                }

                foreach (var skill in SkillCatalog.All)

                {

                    if (skill != null) meta.DiscoverSkill(skill.skillId, autoSave: false);

                }

                foreach (var enemy in EnemyCatalog.All)

                {

                    if (enemy != null) meta.DiscoverEnemy(enemy.enemyId, autoSave: false);

                }

                meta.Save();

            }

            if (GUILayout.Button("발견 초기화"))

            {

                save.discoveredFormIds.Clear();

                save.discoveredSkillIds.Clear();

                save.discoveredEnemyIds.Clear();

                meta.Save();

            }

            GUILayout.EndHorizontal();

            GUILayout.Label("※ 도감은 타이틀·로비 메뉴에서 열 수 있다(런 중에는 진입점 없음)");

            GUILayout.Space(6);

        }

        // ====== 언어 ======
        // 언어 저장처가 PlayerPrefs에서 MetaSave로 옮겨졌다(2026-09-03). 아직 게임 안에
        // 언어 UI가 없어서, 이 섹션이 전환·영속·레거시 이관을 확인하는 유일한 경로다.
        private void DrawLocalizationSection()
        {
            GUILayout.Label("■ 언어", headerStyle);

            var loc = LocalizationManager.GetInstanceSafe();
            if (loc == null)
            {
                GUILayout.Label("(LocalizationManager 없음)");
                GUILayout.Space(6);
                return;
            }

            var meta = MetaSaveService.GetInstanceSafe();
            string savedText = meta != null && meta.Current != null
                ? Describe(meta.Current.settings.language)
                : "(MetaSaveService 없음)";
            string legacy = PlayerPrefs.GetString(LocalizationManager.LegacyLanguagePrefKey, string.Empty);

            GUILayout.Label($"현재 = {loc.CurrentLanguage}  /  세이브 = {savedText}");
            GUILayout.Label($"레거시 PlayerPrefs = {Describe(legacy)}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("한국어")) loc.SetLanguage(LocalizationLanguage.Korean);
            if (GUILayout.Button("English")) loc.SetLanguage(LocalizationLanguage.English);
            if (GUILayout.Button("日本語")) loc.SetLanguage(LocalizationLanguage.Japanese);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            // 저장값을 비우면 "미설정"이 되어 다음 결정이 시스템 언어로 떨어진다.
            if (GUILayout.Button("세이브 값 비우기") && meta != null) meta.UpdateLanguage(null);

            // 이관 경로는 옛 키가 있어야만 도는데, 새로 깐 환경에는 그 키가 없다.
            // 재현 수단이 없으면 이 코드는 실제 사용자의 기기에서 처음 실행된다.
            if (GUILayout.Button("레거시 키 심기(日本語)"))
            {
                PlayerPrefs.SetString(LocalizationManager.LegacyLanguagePrefKey,
                    LocalizationLanguage.Japanese.ToString());
                PlayerPrefs.Save();
            }
            GUILayout.EndHorizontal();

            // 초기 결정을 다시 돌린다 — 재시작하지 않고도 세이브 → 레거시 이관 → 시스템 언어
            // 순서를 그대로 밟아 볼 수 있다.
            if (GUILayout.Button("초기 언어 결정 다시 실행")) loc.ReloadLanguageFromSave();

            GUILayout.Space(6);
        }

        private static string Describe(string value) =>
            string.IsNullOrEmpty(value) ? "(미설정)" : value;
    }
}

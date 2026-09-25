using System.Collections.Generic;
using System.Linq;
using Abyss.Runtime.Form;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Run;
using UnityEngine;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 런 결과 통계 문구의 단일 소스. 완주 루프 계획 2-2.
    ///
    /// 같은 8줄을 세 화면이 쓴다 — <see cref="ResultPanelPresenter"/>(사망),
    /// <see cref="EndingSequencePanel"/>(완주), 도감 기록 탭(직전 런). 라벨·단위·"기록 없음" 표기가
    /// 갈리면 같은 런이 화면에 따라 다르게 보이므로 여기로 모았다.
    ///
    /// 입력은 <see cref="RunSummary"/>다 — 진행 중인 <see cref="RunStats"/>가 아니라 <b>끝난 런의
    /// 확정값</b>을 받는다. 도감이 세 번째 소비자가 되면서 "저장된 기록도 같은 문구로 보여야 한다"가
    /// 요구사항이 됐고, 그러려면 입력이 직렬화 가능한 값 타입이어야 했다.
    ///
    /// 표시 문구만 담당하고 집계는 <see cref="RunStats"/>가 소유한다.
    /// </summary>
    public static class RunSummaryText
    {
        /// <summary>값이 없을 때 공통으로 쓰는 표기.</summary>
        private const string EMPTY = "—";

        public static string Kills(RunSummary s) => Loc.GetFormat(StringKey.RunSummary_KillsFormat, s.enemiesKilled);

        public static string Combo(RunSummary s) => Loc.GetFormat(StringKey.RunSummary_ComboFormat, s.maxCombo);

        public static string DominantForm(RunSummary s)
        {
            if (string.IsNullOrEmpty(s.dominantFormId)) return Loc.GetFormat(StringKey.RunSummary_DominantFormFormat, EMPTY);
            return Loc.GetFormat(StringKey.RunSummary_DominantFormRatioFormat, FormName(s.dominantFormId), s.dominantFormRatio);
        }

        public static string FormsUsed(RunSummary s) =>
            Loc.GetFormat(StringKey.RunSummary_FormsUsedFormat,
                s.formsUsed == null || s.formsUsed.Count == 0 ? EMPTY : string.Join(", ", s.formsUsed.Select(FormName)));

        // 스킬은 아직 이름 키가 없어 skillId를 그대로 찍는다(콘텐츠 이름 현지화는 별도 범위).
        public static string Skills(RunSummary s) =>
            s.draftedSkillIds == null || s.draftedSkillIds.Count == 0
                ? Loc.GetFormat(StringKey.RunSummary_SkillsNoneFormat, EMPTY)
                : Loc.GetFormat(StringKey.RunSummary_SkillsFormat, s.draftedSkillIds.Count, string.Join(", ", s.draftedSkillIds));

        /// <summary>
        /// 도달 지점 표기(현재 언어). 도감 기록 탭의 「최고 도달」도 이것을 쓴다 — 두 화면이 같은 기록을
        /// 다르게 부르지 않게. 판정은 <see cref="StageReach.Describe(string, string, string)"/>가 한다.
        /// </summary>
        public static string Reach(StageReach reach, string emptyValue) =>
            reach.Describe(emptyValue, Loc.Get(StringKey.Run_StageNumberFormat), Loc.Get(StringKey.Run_ReachStepFormat));

        /// <summary>formId → 현재 언어 폼 이름. 카탈로그에 없는 옛 기록은 id 그대로.</summary>
        private static string FormName(string formId)
        {
            var form = FormCatalog.GetById(formId);
            return form != null ? form.LocalizedName : formId;
        }

        /// <summary>
        /// 도달 지점. <see cref="RunSummary.reached"/>가 있으면 스테이지 이름으로,
        /// 없으면 roomId로 되돌아간다 — 이 필드가 생기기 전에 저장된 요약도 계속 읽히게 하기 위함이고,
        /// 그래서 이 화면 하나 때문에 세이브를 변환할 필요가 없다.
        /// </summary>
        public static string Stage(RunSummary s)
        {
            string reached = s.reached.HasRecord ? Reach(s.reached, EMPTY)
                : string.IsNullOrEmpty(s.stageReached) ? EMPTY : s.stageReached;
            return Loc.GetFormat(StringKey.RunSummary_StageFormat, reached);
        }

        public static string Elapsed(RunSummary s)
        {
            int mins = Mathf.FloorToInt(s.elapsedSeconds / 60f);
            int secs = Mathf.FloorToInt(s.elapsedSeconds % 60f);
            return Loc.GetFormat(StringKey.RunSummary_ElapsedFormat, $"{mins:D2}:{secs:D2}");
        }

        /// <summary>
        /// 메타 정산 라인. 살아 있는 <see cref="RunManager"/>·세이브를 조회하지 않고
        /// <b>요약에 굳어 있는 값</b>을 읽는다 — 저장된 기록을 나중에 다시 보여줄 때
        /// 그때의 누적치가 아니라 지금의 누적치가 찍히면 기록이 아니게 된다.
        /// </summary>
        public static string AbyssEarned(RunSummary s) =>
            Loc.GetFormat(StringKey.RunSummary_AbyssEarnedFormat, s.abyssEarned, s.abyssTotal);

        /// <summary>결과 화면 표시 순서 그대로의 전체 줄. 한 덩어리로 보여주는 화면(엔딩·도감)이 쓴다.</summary>
        public static IEnumerable<string> AllLines(RunSummary s)
        {
            yield return Kills(s);
            yield return Combo(s);
            yield return DominantForm(s);
            yield return FormsUsed(s);
            yield return Skills(s);
            yield return Stage(s);
            yield return Elapsed(s);
            yield return AbyssEarned(s);
        }
    }
}

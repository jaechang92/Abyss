using System.Collections.Generic;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using UnityEngine;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 런 결과 통계 문구의 단일 소스. 완주 루프 계획 2-2.
    ///
    /// 같은 8줄을 두 화면이 쓴다 — <see cref="ResultPanelPresenter"/>(사망)와
    /// <see cref="EndingSequencePanel"/>(완주). 라벨·단위·"기록 없음" 표기가 갈리면
    /// 같은 런이 화면에 따라 다르게 보이므로 여기로 모았다.
    ///
    /// 표시 문구만 담당하고 집계는 <see cref="RunStats"/>가 소유한다.
    /// </summary>
    public static class RunSummaryText
    {
        /// <summary>값이 없을 때 공통으로 쓰는 표기.</summary>
        private const string EMPTY = "—";

        public static string Kills(RunStats s) => $"처치 수: {s.enemiesKilled}";

        public static string Combo(RunStats s) => $"최장 콤보: {s.maxCombo}";

        public static string DominantForm(RunStats s)
        {
            string id = s.GetDominantFormId();
            if (string.IsNullOrEmpty(id)) return $"주 사용 폼: {EMPTY}";
            return $"주 사용 폼: {id} ({s.GetFormRatio(id):P0})";
        }

        public static string FormsUsed(RunStats s) =>
            s.formsUsed.Count == 0 ? $"사용 폼: {EMPTY}" : "사용 폼: " + string.Join(", ", s.formsUsed);

        public static string Skills(RunStats s) =>
            s.draftedSkillIds.Count == 0
                ? $"드래프트 스킬: {EMPTY}"
                : $"드래프트 스킬 {s.draftedSkillIds.Count}개: " + string.Join(", ", s.draftedSkillIds);

        public static string Stage(RunStats s) =>
            string.IsNullOrEmpty(s.stageReached) ? $"도달: {EMPTY}" : $"도달: {s.stageReached}";

        public static string Elapsed(RunStats s)
        {
            int mins = Mathf.FloorToInt(s.totalElapsedSeconds / 60f);
            int secs = Mathf.FloorToInt(s.totalElapsedSeconds % 60f);
            return $"경과: {mins:D2}:{secs:D2}";
        }

        /// <summary>
        /// 메타 정산 라인. <see cref="RunStats"/>가 아니라 런 종료 시점의 정산 결과를 읽는다.
        /// </summary>
        public static string AbyssEarned()
        {
            int earned = RunManager.HasInstance ? RunManager.Instance.LastRunAbyssShardsEarned : 0;
            int total = MetaSaveService.Instance.Current.abyssShardsTotal;
            return $"Abyss 획득: +{earned}  (누적 {total})";
        }

        /// <summary>결과 화면 표시 순서 그대로의 전체 줄. 한 덩어리로 보여주는 화면(엔딩)이 쓴다.</summary>
        public static IEnumerable<string> AllLines(RunStats s)
        {
            yield return Kills(s);
            yield return Combo(s);
            yield return DominantForm(s);
            yield return FormsUsed(s);
            yield return Skills(s);
            yield return Stage(s);
            yield return Elapsed(s);
            yield return AbyssEarned();
        }
    }
}

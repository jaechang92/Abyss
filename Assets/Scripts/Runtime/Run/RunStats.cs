using System;
using System.Collections.Generic;

namespace Abyss.Runtime.Run
{
    /// <summary>
    /// 런 1회의 통계 집계. RunManager가 GameEvents 구독으로 축적.
    /// ResultPanel에서 MF-6 요구 7항목 표시용.
    /// </summary>
    [Serializable]
    public sealed class RunStats
    {
        public int enemiesKilled;
        public int maxCombo;
        public float totalElapsedSeconds;
        public string stageReached = string.Empty;
        public List<string> formsUsed = new();
        public List<string> draftedSkillIds = new();
        public Dictionary<string, float> formPlaytimeSeconds = new();
        public int totalDraftCount;
        public int formExclusiveDraftCount;

        public float FormExclusiveDraftRatio => totalDraftCount <= 0 ? 0f : (float)formExclusiveDraftCount / totalDraftCount;

        public void Reset()
        {
            enemiesKilled = 0;
            maxCombo = 0;
            totalElapsedSeconds = 0f;
            stageReached = string.Empty;
            formsUsed.Clear();
            draftedSkillIds.Clear();
            formPlaytimeSeconds.Clear();
            totalDraftCount = 0;
            formExclusiveDraftCount = 0;
        }

        public string GetDominantFormId()
        {
            string dominant = string.Empty;
            float max = 0f;
            foreach (var kv in formPlaytimeSeconds)
            {
                if (kv.Value > max)
                {
                    max = kv.Value;
                    dominant = kv.Key;
                }
            }
            return dominant;
        }

        /// <summary>
        /// 런 총 경과시간(totalElapsedSeconds) 대비 해당 폼의 사용 비율. 결과 화면·애널리틱스 지표용.
        /// 분모가 unscaled 시간이라 모달·일시정지 구간을 포함하며, 전 폼 비율의 합은 1보다 작을 수 있다.
        /// </summary>
        public float GetFormRatio(string formId)
        {
            if (totalElapsedSeconds <= 0f || string.IsNullOrEmpty(formId)) return 0f;
            return formPlaytimeSeconds.TryGetValue(formId, out var t) ? t / totalElapsedSeconds : 0f;
        }

        /// <summary>기록된 전 폼 플레이타임의 합(초).</summary>
        public float FormPlaytimeTotalSeconds
        {
            get
            {
                float sum = 0f;
                foreach (var kv in formPlaytimeSeconds) sum += kv.Value;
                return sum;
            }
        }

        /// <summary>
        /// 폼 플레이타임 총합 대비 해당 폼의 점유 비율(편향 판정 전용).
        /// 분모·분자가 같은 시간축(FormController가 누적하는 scaled deltaTime)이라 모달·정지 구간이
        /// 양쪽에서 동일하게 빠진다 — 정지 시간이 길어져도 비율이 왜곡되지 않는다.
        /// 전 폼 비율의 합이 1이 되므로 formBiasThreshold(기본 0.6) 판정의 올바른 분모다.
        /// </summary>
        public float GetFormPlaytimeRatio(string formId)
        {
            if (string.IsNullOrEmpty(formId)) return 0f;
            float total = FormPlaytimeTotalSeconds;
            if (total <= 0f) return 0f;
            return formPlaytimeSeconds.TryGetValue(formId, out var t) ? t / total : 0f;
        }
    }
}

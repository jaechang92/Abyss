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

        public void Reset()
        {
            enemiesKilled = 0;
            maxCombo = 0;
            totalElapsedSeconds = 0f;
            stageReached = string.Empty;
            formsUsed.Clear();
            draftedSkillIds.Clear();
            formPlaytimeSeconds.Clear();
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

        public float GetFormRatio(string formId)
        {
            if (totalElapsedSeconds <= 0f || string.IsNullOrEmpty(formId)) return 0f;
            return formPlaytimeSeconds.TryGetValue(formId, out var t) ? t / totalElapsedSeconds : 0f;
        }
    }
}

using System;
using System.IO;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
using Abyss.Runtime.Run;
using Newtonsoft.Json;
using Singleton_Core;
using UnityEngine;

namespace Abyss.Runtime.Analytics
{
    /// <summary>
    /// P0 5종 이벤트 JSON Lines 로거 (stage-d-analyst §2, P-20).
    /// persistentDataPath/Abyss/analytics/{sessionId}.jsonl 에 한 줄당 1 이벤트 기록.
    /// 이벤트: run_start / run_end / death / skill_drafted / form_swap.
    /// </summary>
    public sealed class AnalyticsLogger : SingletonManager<AnalyticsLogger>
    {
        private string sessionId;
        private string runId;
        private string logPath;
        private StreamWriter writer;
        private int draftIndex;

        public string SessionId => sessionId;
        public string LogPath => logPath;

        protected override void Awake()
        {
            base.Awake();
            InitializeSession();
        }

        private void InitializeSession()
        {
            sessionId = Guid.NewGuid().ToString("N");
            string dir = Path.Combine(Application.persistentDataPath, "Abyss", "analytics");
            Directory.CreateDirectory(dir);
            logPath = Path.Combine(dir, $"{sessionId}.jsonl");

            try
            {
                writer = new StreamWriter(logPath, append: true);
                Debug.Log($"[Analytics] 세션 로그 시작: {logPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Analytics] 로그 파일 열기 실패: {e.Message}");
            }
        }

        private void OnEnable()
        {
            GameEvents.OnRunStarted += HandleRunStarted;
            GameEvents.OnRunEnded += HandleRunEnded;
            GameEvents.OnPlayerDead += HandlePlayerDead;
            GameEvents.OnSkillDrafted += HandleSkillDrafted;
            GameEvents.OnFormSwapped += HandleFormSwap;
        }

        private void OnDisable()
        {
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnRunEnded -= HandleRunEnded;
            GameEvents.OnPlayerDead -= HandlePlayerDead;
            GameEvents.OnSkillDrafted -= HandleSkillDrafted;
            GameEvents.OnFormSwapped -= HandleFormSwap;

            writer?.Flush();
        }

        private void OnApplicationQuit()
        {
            writer?.Flush();
            writer?.Dispose();
            writer = null;
        }

        /// <summary>
        /// 커스텀 이벤트를 외부에서 기록할 때 사용.
        /// </summary>
        public void Log(string eventName, object payload = null)
        {
            if (writer == null) return;

            var record = new
            {
                @event = eventName,
                timestamp = DateTime.UtcNow.ToString("o"),
                session_id = sessionId,
                run_id = runId,
                payload
            };

            try
            {
                string json = JsonConvert.SerializeObject(record);
                writer.WriteLine(json);
                writer.Flush();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] 직렬화 실패 ({eventName}): {e.Message}");
            }
        }

        private void HandleRunStarted()
        {
            runId = Guid.NewGuid().ToString("N");
            draftIndex = 0;
            Log("run_start", new
            {
                starting_form = GetCurrentFormId()
            });
        }

        private void HandleRunEnded()
        {
            var stats = RunManager.HasInstance ? RunManager.Instance.Stats : null;
            if (stats == null)
            {
                Log("run_end", new { note = "stats unavailable" });
                runId = null;
                return;
            }

            string dominantFormId = stats.GetDominantFormId();
            Log("run_end", new
            {
                kills = stats.enemiesKilled,
                duration_sec = stats.totalElapsedSeconds,
                gold_shards_earned = RunManager.HasInstance ? RunManager.Instance.GoldShards : 0,
                abyss_shards_earned = 0,
                stage_reached = stats.stageReached,
                dominant_form = dominantFormId,
                dominant_form_ratio = stats.GetFormRatio(dominantFormId),
                form_exclusive_draft_ratio = stats.FormExclusiveDraftRatio,
                total_drafts = stats.totalDraftCount
            });

            runId = null;
        }

        private void HandlePlayerDead()
        {
            Log("death", new
            {
                current_form = GetCurrentFormId(),
                stage_id = RunManager.HasInstance ? RunManager.Instance.Stats.stageReached : string.Empty,
                elapsed_sec = RunManager.HasInstance ? RunManager.Instance.Stats.totalElapsedSeconds : 0f
            });
        }

        private void HandleSkillDrafted(SkillData skill, DraftTriggerReason reason)
        {
            if (skill == null) return;

            Log("skill_drafted", new
            {
                skill_id = skill.skillId,
                rarity = skill.rarity.ToString(),
                category = skill.category.ToString(),
                synergy_tag = skill.synergyTag,
                form_bound = skill.formBound,
                draft_trigger_reason = reason.ToString(),
                draft_index = draftIndex++,
                current_form = GetCurrentFormId()
            });
        }

        private void HandleFormSwap(FormData previous, FormData next)
        {
            Log("form_swap", new
            {
                from_form = previous != null ? previous.formId : string.Empty,
                to_form = next != null ? next.formId : string.Empty,
                stage_id = RunManager.HasInstance ? RunManager.Instance.Stats.stageReached : string.Empty
            });
        }

        private static string GetCurrentFormId()
        {
            var form = FindAnyObjectByType<FormController>();
            return form != null && form.CurrentForm != null ? form.CurrentForm.formId : string.Empty;
        }
    }
}

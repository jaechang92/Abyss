using System;
using System.IO;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
using Abyss.Runtime.Run;
using Abyss.Runtime.Stage;
using GAS.Core;
using Newtonsoft.Json;
using Singleton_Core;
using UnityEngine;

namespace Abyss.Runtime.Analytics
{
    /// <summary>
    /// 이벤트 JSON Lines 로거 (stage-d-analyst §2, P-20).
    /// persistentDataPath/Abyss/analytics/{sessionId}.jsonl 에 한 줄당 1 이벤트 기록.
    ///
    /// <b>P0 5종</b>: run_start / run_end / death / skill_drafted / form_swap.
    /// <b>P1 3종</b>(2026-08-13): enemy_defeated / ability_used / room_cleared.
    /// <b>P2 1종</b>(2026-08-26): draft_offered — <b>제시된 카드 전부</b>.
    /// 획득 로그(<c>skill_drafted</c>)만으로는 <b>인기 있는 카드와 자주 나오는 카드가
    /// 구분되지 않아</b> 풀 분포를 볼 수 없었다. 이것도 기존 채널
    /// (<c>OnDraftOptionsReady</c>)만 쓴다 — 아래 규칙 그대로다.
    ///
    /// <b>새 GameEvents 채널을 만들지 않는다.</b> 계측은 관찰자이고, 관찰하려고 채널을 늘리면
    /// EventBus 리팩터 임계(30채널)를 <b>기능이 아니라 계측이</b> 앞당긴다. P1 3종은 전부
    /// 기존 채널(<c>OnEnemyKilled</c>·<c>OnRoomEntered/Cleared</c>)과 <c>AbilitySystem</c>의
    /// 자체 이벤트만으로 만들어진다.
    ///
    /// P1 3종(적 처치·방 체류·어빌리티 사용)의 추적 상태와 핸들러는 <c>AnalyticsLogger.Gameplay.cs</c>에 있다(500줄 규칙).
    /// </summary>
    public sealed partial class AnalyticsLogger : SingletonManager<AnalyticsLogger>
    {
        private string sessionId;
        private string runId;

        /// <summary>
        /// 직전 런 ID. <see cref="runId"/>를 비운 뒤에도 <b>그 런에 속하는 것이 분명한</b> 이벤트가
        /// 도착할 수 있어 남겨 둔다(사망 — <see cref="HandlePlayerDead"/>).
        /// </summary>
        private string lastRunId;

        private string logPath;
        private StreamWriter writer;
        private int draftIndex;

        /// <summary>
        /// 제시 순번. <see cref="draftIndex"/>와 <b>따로 센다.</b>
        ///
        /// 🔑 둘을 공유하면 스킵된 제시를 구분할 수 없다 — <c>draftIndex</c>는 획득할 때만 오르므로
        /// 스킵한 제시와 그다음 제시가 같은 번호를 달게 된다.
        /// 제시와 획득을 짝지을 때는 두 번호를 함께 본다.
        /// </summary>
        private int draftOfferIndex;

        /// <summary>
        /// 직전에 기록한 제시의 지문. 같은 제시가 다시 오면 세지 않는다.
        ///
        /// 🔴 <see cref="Draft.DraftSessionController.CancelReplacement"/>가
        /// <c>OnDraftOptionsReady</c>를 <b>재발행</b>한다(교체 모달을 닫고 패널을 되돌리기 위해서).
        /// 그건 새로 뽑은 제시가 아니라 같은 화면을 다시 그리는 것이라, 세면 제시 횟수가 부풀고
        /// 이 계측의 목적인 <b>풀 분포</b>가 "교체를 많이 취소한 런" 쪽으로 기운다.
        ///
        /// 📌 드래프트 쪽에 새 이벤트를 만들지 않고 여기서 거른 이유 —
        /// 재발행은 UI 복구를 위한 정상 동작이고, 그걸 아는 것은 계측 쪽 사정이다.
        /// 발행자를 고치면 <c>DraftPanelPresenter</c>가 의존하는 복구 경로까지 건드리게 된다.
        /// </summary>
        private string lastOfferKey;

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
            GameEvents.OnDraftOptionsReady += HandleDraftOptionsReady;
            GameEvents.OnSkillDrafted += HandleSkillDrafted;
            GameEvents.OnFormSwapped += HandleFormSwap;
            GameEvents.OnEnemyKilled += HandleEnemyKilled;
            GameEvents.OnRoomEntered += HandleRoomEntered;
            GameEvents.OnRoomCleared += HandleRoomCleared;
        }

        private void OnDisable()
        {
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnRunEnded -= HandleRunEnded;
            GameEvents.OnPlayerDead -= HandlePlayerDead;
            GameEvents.OnDraftOptionsReady -= HandleDraftOptionsReady;
            GameEvents.OnSkillDrafted -= HandleSkillDrafted;
            GameEvents.OnFormSwapped -= HandleFormSwap;
            GameEvents.OnEnemyKilled -= HandleEnemyKilled;
            GameEvents.OnRoomEntered -= HandleRoomEntered;
            GameEvents.OnRoomCleared -= HandleRoomCleared;
            UnhookAbilitySystem();

            writer?.Flush();
        }

        /// <summary>
        /// 종료 시 로그 파일을 닫는다.
        ///
        /// 🔴 <b>`override` 가 빠져 있었다(CS0114) — 부모를 가리고 있었다.</b>
        /// <c>SingletonManager&lt;T&gt;.OnApplicationQuit</c>은 정적 <c>applicationIsQuitting</c>을
        /// 세우고, 그 값이 <c>Instance</c>·<c>HasInstance</c>의 <b>종료 중 재생성 차단</b>을 담당한다.
        /// 가려 두면 그 플래그가 끝내 false로 남아, 종료 도중 <c>AnalyticsLogger.Instance</c>에
        /// 닿는 코드가 <b>파괴 중인 씬에 새 인스턴스를 만들려 든다</b>.
        ///
        /// 그래서 <c>base</c> 호출이 이 메서드의 본론이고 <see cref="CloseLog"/>가 덤이다.
        /// 순서도 base 가 먼저 — 파일을 닫는 동안에는 이미 종료 중으로 표시돼 있어야 한다.
        /// </summary>
        protected override void OnApplicationQuit()
        {
            base.OnApplicationQuit();
            CloseLog();
        }

        /// <summary>
        /// 로그 파일을 닫는다. 종료 경로와 테스트 정리가 공유한다 —
        /// 파일이 열려 있는 동안에는 Windows에서 삭제가 막히고, 테스트가 남긴 줄을 치우지 못하면
        /// <b>플레이테스트 분석기(`_playtest_analyzer.py`)가 그 줄까지 집계</b>한다.
        /// 닫은 뒤에는 이 세션에 더 기록하지 않는다.
        /// </summary>
        public void CloseLog()
        {
            writer?.Flush();
            writer?.Dispose();
            writer = null;
        }

        /// <summary>
        /// 커스텀 이벤트를 외부에서 기록할 때 사용. 현재 런에 귀속시킨다.
        /// </summary>
        public void Log(string eventName, object payload = null) => LogAs(runId, eventName, payload);

        /// <summary>
        /// 귀속시킬 런을 지정해 기록한다. 런이 이미 끝난 뒤에 도착하지만
        /// <b>그 런에 속하는 것이 분명한</b> 이벤트(사망)를 위한 경로다 — <see cref="HandlePlayerDead"/> 참조.
        /// </summary>
        private void LogAs(string effectiveRunId, string eventName, object payload)
        {
            if (writer == null) return;

            var record = new
            {
                @event = eventName,
                timestamp = DateTime.UtcNow.ToString("o"),
                session_id = sessionId,
                run_id = effectiveRunId,
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
            draftOfferIndex = 0;
            // 런이 바뀌면 지문도 비운다. 안 비우면 새 런의 첫 제시가
            // 직전 런의 마지막 제시와 같을 때 조용히 빠진다.
            lastOfferKey = null;
            ResetRoomTracking();
            HookAbilitySystem();
            Log("run_start", new
            {
                starting_form = GetCurrentFormId()
            });
        }

        private void HandleRunEnded()
        {
            // 마지막 방을 먼저 마감한다 — run_end 뒤에 오면 분석기가 런 밖 이벤트로 본다.
            FlushRoom();

            var stats = RunManager.HasInstance ? RunManager.Instance.Stats : null;
            if (stats == null)
            {
                Log("run_end", new { note = "stats unavailable" });
                CloseRun();
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

            CloseRun();
        }

        /// <summary>
        /// 런 귀속을 닫는다. ID를 버리지 않고 <see cref="lastRunId"/>로 옮기는 이유는,
        /// 런 종료 처리가 먼저 돌아 이 뒤에 도착하는 사망 이벤트가 여전히 그 런의 것이기 때문이다.
        /// </summary>
        private void CloseRun()
        {
            lastRunId = runId;
            runId = null;
        }

        /// <summary>
        /// 사망 기록. <b>run_id를 직전 런으로 되살려 붙인다</b>(Bug-035).
        ///
        /// 같은 <c>OnPlayerDead</c>를 RunManager도 듣는데, 부트스트랩이 RunManager를 먼저 만들어
        /// <b>구독 순서가 항상 RunManager 우선</b>이다. 그래서 여기 도달할 때는 이미
        /// <c>EndRun → run_end</c>가 지나가 <see cref="runId"/>가 비어 있다. 그대로 기록하면
        /// <c>run_id: null</c>이 되고, 분석기는 런에 속하지 않는 이벤트를 버리므로
        /// <b>사망 분석이 통째로 사라진다</b>(실제로 기존 로그의 death 35건이 전부 그랬다).
        ///
        /// 구독 순서를 바꾸는 대신 귀속을 명시하는 쪽을 골랐다 — 순서에 기대는 수정은
        /// 부트스트랩이 한 번 바뀌면 같은 방식으로 조용히 다시 깨진다.
        /// </summary>
        private void HandlePlayerDead()
        {
            LogAs(runId ?? lastRunId, "death", new
            {
                current_form = GetCurrentFormId(),
                stage_id = RunManager.HasInstance ? RunManager.Instance.Stats.stageReached : string.Empty,
                elapsed_sec = RunManager.HasInstance ? RunManager.Instance.Stats.totalElapsedSeconds : 0f
            });
        }

        /// <summary>
        /// 제시된 카드를 <b>전부</b> 남긴다.
        ///
        /// 🔑 <c>skill_drafted</c>는 <b>고른 것</b>만 남긴다. 그것만으로는
        /// "같은 카드가 반복해서 나오는가"를 볼 수 없다 — 고르지 않은 카드가 로그에 없으면
        /// 무엇이 자주 제시됐는지 알 방법이 자체가 없다. 획득 로그만 보면
        /// <b>인기 있는 카드와 자주 나오는 카드가 구분되지 않는다.</b>
        ///
        /// 📌 이 계측이 있어야 스킬 15→18 확장의 근거를 사후에 검증할 수 있고,
        /// 드래프트 발동 횟수(G2 게이트)가 모자란 것이 풀 문제인지 트리거 문제인지 갈린다.
        /// </summary>
        private void HandleDraftOptionsReady(DraftOptions options)
        {
            if (options?.Cards == null || options.Cards.Count == 0) return;

            int count = options.Cards.Count;
            var ids = new string[count];
            var rarities = new string[count];
            var tags = new string[count];
            for (int i = 0; i < count; i++)
            {
                var card = options.Cards[i];
                ids[i] = card != null ? card.skillId : string.Empty;
                rarities[i] = card != null ? card.rarity.ToString() : string.Empty;
                tags[i] = card != null ? card.synergyTag : string.Empty;
            }

            // 같은 제시가 다시 오면 세지 않는다 — 사유는 lastOfferKey 주석 참조.
            string key = $"{options.Reason}|{options.RerollIndex}|{string.Join(",", ids)}";
            if (key == lastOfferKey) return;
            lastOfferKey = key;

            Log("draft_offered", new
            {
                offer_index = draftOfferIndex++,
                card_ids = ids,
                rarities,
                synergy_tags = tags,
                card_count = count,
                draft_trigger_reason = options.Reason.ToString(),
                reroll_index = options.RerollIndex,
                current_form = GetCurrentFormId()
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

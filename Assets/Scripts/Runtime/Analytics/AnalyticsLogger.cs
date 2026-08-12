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
    ///
    /// <b>새 GameEvents 채널을 만들지 않는다.</b> 계측은 관찰자이고, 관찰하려고 채널을 늘리면
    /// EventBus 리팩터 임계(30채널)를 <b>기능이 아니라 계측이</b> 앞당긴다. P1 3종은 전부
    /// 기존 채널(<c>OnEnemyKilled</c>·<c>OnRoomEntered/Cleared</c>)과 <c>AbilitySystem</c>의
    /// 자체 이벤트만으로 만들어진다.
    /// </summary>
    public sealed class AnalyticsLogger : SingletonManager<AnalyticsLogger>
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

        /// <summary>진입 시각을 못 잡은 상태. 0으로 두면 "0초 클리어"와 구분되지 않는다.</summary>
        private const float NOT_MEASURED = -1f;

        // 추적 중인 방. room_cleared는 방을 <b>떠날 때</b> 기록하므로 진입 시점부터 들고 있어야 한다.
        // 완주 플레이테스트의 관심사가 "런이 긴가"가 아니라 "어느 방이 무거운가"이기 때문이다
        // (방 수를 줄일지 방당 시간을 줄일지는 이 값이 없으면 못 정한다).
        private float roomEnteredAt = NOT_MEASURED;
        private string roomEnteredId;
        private string roomTypeName;

        // 클리어 판정 시각. 전투가 끝난 뒤 모달·지연에서 보낸 시간을 분리해 보기 위해 따로 잡는다.
        private float roomClearedAt = NOT_MEASURED;

        // 방 단위 집계. 처치 수는 enemy_defeated를 세어도 되지만, 분석기가 방 이벤트만 보고도
        // 밀도를 알 수 있게 room_cleared에 함께 싣는다.
        private int killsInRoom;

        /// <summary>
        /// 어빌리티 사용 계측이 <see cref="AbilitySystem"/>에 붙었는지. 구독 시점이 런 시작인 이유는
        /// <see cref="OnEnable"/> 시점에 그 싱글톤이 아직 없을 수 있기 때문이다(부트스트랩 순서).
        /// </summary>
        private bool isAbilityHooked;

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
            GameEvents.OnEnemyKilled += HandleEnemyKilled;
            GameEvents.OnRoomEntered += HandleRoomEntered;
            GameEvents.OnRoomCleared += HandleRoomCleared;
        }

        private void OnDisable()
        {
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnRunEnded -= HandleRunEnded;
            GameEvents.OnPlayerDead -= HandlePlayerDead;
            GameEvents.OnSkillDrafted -= HandleSkillDrafted;
            GameEvents.OnFormSwapped -= HandleFormSwap;
            GameEvents.OnEnemyKilled -= HandleEnemyKilled;
            GameEvents.OnRoomEntered -= HandleRoomEntered;
            GameEvents.OnRoomCleared -= HandleRoomCleared;
            UnhookAbilitySystem();

            writer?.Flush();
        }

        private void OnApplicationQuit() => CloseLog();

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

        // ───────────────────────── P1 (2026-08-13) ─────────────────────────

        /// <summary>
        /// 적 처치 1건. 런당 60~100회로 기존 이벤트보다 훨씬 잦지만 <b>줄마다 flush를 유지</b>한다 —
        /// 크래시로 로그가 날아가면 그 플레이테스트 런이 통째로 무의미해지고, 그 손해가
        /// 쓰기 비용보다 크다. (체감 hitch가 생기면 그때 버퍼링을 검토할 것.)
        /// </summary>
        private void HandleEnemyKilled(EnemyData data, Vector3 _)
        {
            if (data == null) return;

            killsInRoom += 1;
            Log("enemy_defeated", new
            {
                enemy_id = data.enemyId,
                is_boss = data.isBoss,
                is_elite = data.isElite,
                room_id = roomEnteredId,
                current_form = GetCurrentFormId(),
                elapsed_sec = ElapsedRunSeconds()
            });
        }

        /// <summary>
        /// 새 방에 들어가면 <b>직전 방을 먼저 마감</b>하고 추적을 새로 시작한다.
        /// </summary>
        private void HandleRoomEntered(RoomData room)
        {
            if (room == null) return;

            FlushRoom();

            roomEnteredId = room.roomId;
            roomTypeName = room.roomType.ToString();
            roomEnteredAt = Time.unscaledTime;
            roomClearedAt = NOT_MEASURED;
            killsInRoom = 0;
        }

        /// <summary>
        /// 클리어 판정 시각만 잡고 <b>기록하지는 않는다</b>.
        ///
        /// ⚠️ 여기서 바로 기록하면 <b>비전투 방이 전부 0초로 남는다.</b> StageDirector는 적이 0마리인
        /// 방(이벤트·휴식·상점)을 <b>진입과 동시에</b> 클리어 처리하고(빈 방 즉시 클리어), 모달에서
        /// 보내는 시간은 그 뒤에 흐르기 때문이다. 실제로 첫 계측에서 23개 방 중 10개가 0.0초로 찍혔고,
        /// 그대로면 "런이 길다"의 원인 분석이 전투 쪽으로 기운다 — 계측하려던 바로 그 질문이 왜곡된다.
        /// </summary>
        private void HandleRoomCleared(RoomData room)
        {
            if (room == null || roomEnteredAt < 0f) return;
            roomClearedAt = Time.unscaledTime;
        }

        /// <summary>
        /// 추적 중인 방을 마감해 <c>room_cleared</c> 한 줄을 남긴다. 방을 <b>떠나는 시점</b>
        /// (다음 방 진입 · 런 종료)에 호출된다.
        ///
        /// <b>클리어된 방만</b> 기록한다 — 클리어 못 한 채 죽은 방까지 <c>room_cleared</c>로 남기면
        /// 이름이 데이터와 어긋난다(그 방의 위치는 <c>death.stage_id</c>가 이미 말한다).
        ///
        /// 시간을 둘로 나눠 싣는다:
        /// <list type="bullet">
        /// <item><c>combat_sec</c> — 진입 → 클리어 판정. 전투에 쓴 시간.</item>
        /// <item><c>duration_sec</c> — 진입 → 퇴장. 모달·진행 지연까지 포함한 <b>실제 체류</b>.</item>
        /// </list>
        /// 둘을 나란히 두면 "런이 길다"의 원인이 전투인지 그 밖인지 가려진다.
        ///
        /// 시간축은 <c>unscaledTime</c>이다. 정지 구간이 포함되지만 그것이 플레이어가 실제로 앉아
        /// 있던 시간이고, <c>RunStats.totalElapsedSeconds</c>와 같은 축이라 합을 런 시간과 견줄 수 있다.
        /// </summary>
        private void FlushRoom()
        {
            if (roomEnteredAt < 0f || roomClearedAt < 0f)
            {
                ResetRoomTracking();
                return;
            }

            Log("room_cleared", new
            {
                room_id = roomEnteredId,
                room_type = roomTypeName,
                duration_sec = Time.unscaledTime - roomEnteredAt,
                combat_sec = roomClearedAt - roomEnteredAt,
                kills = killsInRoom,
                current_form = GetCurrentFormId(),
                elapsed_sec = ElapsedRunSeconds()
            });

            ResetRoomTracking();
        }

        /// <summary>
        /// 어빌리티 발동 1건. <see cref="AbilitySystem.OnAbilityStarted"/>를 쓴다 —
        /// <c>OnAbilityExecuted</c>는 효과가 <b>끝난 뒤</b> 발화하는 비동기 완료 신호라
        /// 런이 끝난 뒤에 도착할 수도 있다. 계측하려는 것은 "언제 눌렀는가"이므로 개시 시점이 맞다.
        /// </summary>
        private void HandleAbilityStarted(string registeredName)
        {
            if (string.IsNullOrEmpty(registeredName)) return;

            SplitSlotKey(registeredName, out int slot, out string abilityName);

            Log("ability_used", new
            {
                ability_name = abilityName,
                slot,
                current_form = GetCurrentFormId(),
                room_id = roomEnteredId,
                elapsed_sec = ElapsedRunSeconds()
            });
        }

        /// <summary>
        /// <c>slot0:flame_burst</c> → (0, "flame_burst").
        ///
        /// 등록 키에 슬롯이 섞여 있는 것은 <see cref="PlayerCharacter"/>의 의도다(같은 어빌리티가
        /// 두 슬롯에 와도 충돌하지 않게). 하지만 그대로 집계하면 <b>같은 스킬이 슬롯마다 다른 항목</b>이
        /// 되어 "어느 스킬이 안 쓰이는가"라는 질문에 답할 수 없다. 그래서 둘로 나눠 싣는다 —
        /// 스킬은 <c>ability_name</c>, 슬롯 선호는 <c>slot</c>으로 각각 세면 된다.
        ///
        /// 형식이 다르면 원문을 그대로 두고 슬롯은 -1(미상)로 남긴다. 파싱 규약이 바뀌었을 때
        /// 이름을 훼손하지 않기 위해서다.
        /// </summary>
        private static void SplitSlotKey(string registeredName, out int slot, out string abilityName)
        {
            slot = -1;
            abilityName = registeredName;

            int colon = registeredName.IndexOf(':');
            if (colon <= 0) return;

            string prefix = registeredName[..colon];
            if (!prefix.StartsWith("slot", StringComparison.Ordinal)) return;
            if (!int.TryParse(prefix[4..], out int parsed)) return;

            slot = parsed;
            abilityName = registeredName[(colon + 1)..];
        }

        /// <summary>
        /// <see cref="AbilitySystem"/>은 부트스트랩에서 이 로거보다 늦게 설 수 있어 OnEnable에서 잡지 않는다.
        /// 런 시작 시점에는 반드시 존재하고(어빌리티는 런 안에서만 쓰인다), <c>-=</c> 후 <c>+=</c>라
        /// 여러 런에 걸쳐 호출돼도 중복 구독되지 않는다.
        /// </summary>
        private void HookAbilitySystem()
        {
            if (!AbilitySystem.HasInstance)
            {
                Debug.LogWarning("[Analytics] AbilitySystem 미생성 — ability_used 계측이 이번 런에서 빠진다.");
                return;
            }

            AbilitySystem.Instance.OnAbilityStarted -= HandleAbilityStarted;
            AbilitySystem.Instance.OnAbilityStarted += HandleAbilityStarted;
            isAbilityHooked = true;
        }

        private void UnhookAbilitySystem()
        {
            if (!isAbilityHooked || !AbilitySystem.HasInstance) return;
            AbilitySystem.Instance.OnAbilityStarted -= HandleAbilityStarted;
            isAbilityHooked = false;
        }

        private void ResetRoomTracking()
        {
            roomEnteredId = string.Empty;
            roomTypeName = string.Empty;
            roomEnteredAt = NOT_MEASURED;
            roomClearedAt = NOT_MEASURED;
            killsInRoom = 0;
        }

        /// <summary>런 시작 이후 경과(초). 이벤트를 시간축 위에 놓아 "언제 무너졌나"를 볼 수 있게 한다.</summary>
        private static float ElapsedRunSeconds() =>
            RunManager.HasInstance ? RunManager.Instance.Stats.totalElapsedSeconds : 0f;

        private static string GetCurrentFormId()
        {
            var form = FindAnyObjectByType<FormController>();
            return form != null && form.CurrentForm != null ? form.CurrentForm.formId : string.Empty;
        }
    }
}

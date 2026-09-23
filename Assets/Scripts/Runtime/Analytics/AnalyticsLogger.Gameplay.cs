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
    /// <see cref="AnalyticsLogger"/>의 P1 계측(2026-08-13) — <c>enemy_defeated</c> · <c>room_cleared</c> · <c>ability_used</c>.
    /// 방 추적 상태와 어빌리티 구독이 이 셋에만 쓰이므로 함께 둔다.
    /// </summary>
    public sealed partial class AnalyticsLogger
    {
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
                is_boss = data.IsBoss,
                is_elite = data.IsElite,
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
        /// (다음 방 진입 · 런 종료 · 런 포기)에 호출된다.
        ///
        /// 포기도 같은 규칙이다 — <c>OnRoomCleared</c>를 받은 방만 남는다. 싸우던 중 타이틀로 나간 방은
        /// 클리어 시각이 없어 기록 없이 버려진다. 클리어 후 모달·대기 중에 나간 방은 실제로 클리어된
        /// 방이므로 기록되며, <c>duration_sec</c>에는 포기 시점까지의 체류가 들어간다.
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
    }
}

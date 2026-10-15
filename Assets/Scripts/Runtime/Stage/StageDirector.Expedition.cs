using Abyss.Runtime.Flow;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using Abyss.Runtime.Story;
using Abyss.Runtime.UI;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 탐사 발견 — Stage1 형체 보상방의 「남겨진 통행 기록」(26-expedition-discovery E3).
    ///
    /// 방 클리어 직후(보상 제단·이벤트 분기 전에) <see cref="PassageDiscovery.IsRecordRoom"/>이면 방 하나에 한 번 세운다.
    /// 🔴 <b>진행과 분리한다.</b> 룸 게이트·보상·이벤트 해결 신호(<c>OnFormRewardResolved</c>·<c>OnEventResolved</c>)를 보내지 않고,
    /// 출구 개방 조건도 바꾸지 않는다 — 읽지 않고 지나가도 진행은 같다. 읽기는 발견 저장 한 번 말고는 아무것도 주지 않는다.
    ///
    /// 위치(배치 규칙): 폼 제단은 맵 가운데 x = <see cref="PASSAGE_ALTAR_X"/>에 선다(<see cref="PlaceAltarOnFloor"/>).
    /// 출구 문은 오른쪽 끝부터 왼쪽으로 늘어서므로(<see cref="TryOpenExitDoors"/>) 제단에 가장 가까운 문은
    /// <c>half − DOOR_RIGHT_INSET − (문 수 − 1) × DOOR_SPACING</c>이다. 기록은 그 둘의 <b>가운데</b>에 두고,
    /// 양쪽 상호작용 범위와 겹치지 않게 제단·문에서 각각 최소 간격을 지킨다. 높이는 그 x 의 실제 바닥(<see cref="ProbeFloorY"/>).
    /// 간격을 지킬 공간이 없으면 세우지 않는다(겹친 상호작용보다 기록이 없는 쪽이 낫다).
    /// </summary>
    public sealed partial class StageDirector
    {
        private const float PASSAGE_ALTAR_X = 0f;               // PlaceAltarOnFloor 와 같은 맵 가운데
        private const float PASSAGE_ALTAR_CLEARANCE = 2.5f;     // 제단 판정 반폭 + 기록 판정 반폭 + 여유
        private const float PASSAGE_DOOR_CLEARANCE = 2.5f;      // 문 판정 반폭(1.1) + 기록 판정 반폭(0.7) + 여유

        [Tooltip("통행 기록 책 그림(Assets/Art/UI/CodexBossHud/codex/tab-records-v1.png). 비우면 임시 색 사각형 — 상호작용은 그대로.")]
        [SerializeField] private Sprite passageRecordSprite;

        private PassageRecordInteractable passageRecord;
        private int passageRecordVersion = -1;

        /// <summary>
        /// 이 방이 기록 방이면 세운다. 같은 방 세대에 이미 세웠으면 다시 세우지 않는다.
        /// 기록 방·대상 스테이지·호환 시퀀스·맵 방·열린 세션 중 하나라도 아니면 아무것도 하지 않는다.
        /// </summary>
        private void TrySpawnPassageRecord(RoomData room)
        {
            if (room == null || !room.IsMapRoom || isRoomSessionClosed) return;
            if (!PassageDiscovery.IsRecordRoom(CurrentStage, room) || !PassageDiscovery.IsCompatible(sequence)) return;
            if (passageRecord != null && passageRecordVersion == roomEntryVersion) return;

            if (!TryResolvePassageRecordX(room, out float x))
            {
                Debug.LogWarning($"[StageDirector] {room.roomId} 제단과 출구 사이에 기록 둘 공간이 없다 — 생략(mapLength {room.mapLength})");
                return;
            }

            int version = roomEntryVersion;
            passageRecord = PassageRecordInteractable.Create(ResolveMapRoot(), new Vector3(x, ProbeFloorY(x), 0f), passageRecordSprite,
                () => CanReadPassageRecord(version), () => HandlePassageRecordDisplayed(version));
            passageRecordVersion = version;
            Debug.Log($"[StageDirector] {room.roomId} 통행 기록 배치 x={x:0.##} — 읽기 선택(진행과 무관)");
        }

        /// <summary>제단(맵 가운데)과 제단에 가장 가까운 출구 문의 가운데. 양쪽 최소 간격을 못 지키면 false.</summary>
        private bool TryResolvePassageRecordX(RoomData room, out float x)
        {
            float half = room.mapLength * 0.5f;
            int doorCount = CountNextDoors();
            float nearestDoorX = half - DOOR_RIGHT_INSET - Mathf.Max(0, doorCount - 1) * DOOR_SPACING;

            float min = PASSAGE_ALTAR_X + PASSAGE_ALTAR_CLEARANCE;
            float max = nearestDoorX - PASSAGE_DOOR_CLEARANCE;
            x = ClampToMap(Mathf.Clamp((PASSAGE_ALTAR_X + nearestDoorX) * 0.5f, min, max), half);
            return min <= max && x >= min && x <= max;
        }

        /// <summary>이 방을 마친 뒤 설 출구 문 수 — <see cref="TryOpenExitDoors"/>와 같은 규칙(마지막 방이면 다음 스테이지 문 하나).</summary>
        private int CountNextDoors()
        {
            var stage = CurrentStage;
            int nextStep = currentStepIndex + 1;
            if (stage == null || nextStep >= stage.steps.Count) return 1;

            int count = 0;
            var options = stage.steps[nextStep]?.options;
            if (options != null)
            {
                foreach (var option in options)
                {
                    if (option != null) count += 1;
                }
            }
            return Mathf.Max(1, count);
        }

        /// <summary>
        /// 읽을 수 있는가 — 같은 방 세대·열린 세션·런 활성·씬 전환 아님, 그리고 모달(폼 선택 포함)·정지·드래프트·문 전환·
        /// 상위 화면(설정·도감·저장 모달, 닫힌 그 프레임 포함)이 없을 때. 새 TimeScale 소유자를 만들지 않는다.
        /// </summary>
        private bool CanReadPassageRecord(int version) =>
            this != null && version == roomEntryVersion && !isRoomSessionClosed && !IsWorldInputBlocked &&
            !IsPassageOverlayOpen &&
            (!RunManager.HasInstance || RunManager.Instance.IsRunActive) &&
            (!SceneFlowController.HasInstance || !SceneFlowController.Instance.IsLoading);

        private static bool IsPassageOverlayOpen =>
            MenuButtonNavigation.IsUpperOverlayOpen || SettingsPanel.WasClosedThisFrame || CodexPanel.WasClosedThisFrame;

        /// <summary>글이 실제로 화면에 뜬 뒤. 발견은 처음 한 번만 저장된다(이후는 AlreadyRecorded — 보상 없음).</summary>
        private void HandlePassageRecordDisplayed(int version)
        {
            if (!CanReadPassageRecord(version)) return;

            var meta = MetaSaveService.Instance;
            if (meta == null) return;
            var result = meta.DiscoverPassage(PassageDiscovery.STAGE1_PASSAGE_RECORD);
            if (result == PassageSaveResult.SaveFailed)
            {
                Debug.LogWarning("[StageDirector] 통행 기록 발견 — 이번 세션에는 기록됐지만 디스크 저장 실패(재시작 시 마지막 저장으로 돌아갈 수 있다)");
            }
            else if (result == PassageSaveResult.Saved)
            {
                Debug.Log("[StageDirector] 통행 기록 발견 저장");
            }
        }

        /// <summary>기록을 닫고 치운다 — 방 이동(<see cref="ClearMapObjects"/>)과 세션 닫기(<see cref="CloseRoomSession"/>)가 부른다.</summary>
        private void ClearPassageRecord()
        {
            if (passageRecord != null)
            {
                passageRecord.Close();
                Destroy(passageRecord.gameObject);
            }
            passageRecord = null;
            passageRecordVersion = -1;
        }
    }
}

using Abyss.Runtime.Meta;
using Abyss.Runtime.Stage;

namespace Abyss.Runtime.Story
{
    /// <summary>통행 기록 하나의 발견 단계(26-expedition-discovery §4 E1 표).</summary>
    public enum PassageDiscoveryState
    {
        /// <summary>아직 조사하지 않았다(새 세이브·필드가 없던 옛 세이브).</summary>
        Unknown,

        /// <summary>조사했고 기록자 반응은 아직 끝까지 듣지 않았다.</summary>
        DiscoveredUnviewed,

        /// <summary>조사했고 기록자 반응도 정상 종료까지 들었다.</summary>
        Viewed
    }

    /// <summary>
    /// Stage1 「남겨진 통행 기록」 한 건의 판정 단일 지점 — 입구 목적(DungeonPortal)·기록 생성(StageDirector)·
    /// 기록자 반응(StoryNpc)이 같은 ID·같은 구성 검사를 쓴다. 별도 Stage1 표를 복사하지 않고 실제 시퀀스 에셋을 읽는다.
    ///
    /// 기록 문구는 매복방(room5_ambush)의 실제 초기 2 + 추가 2 구성을 가리킨다. 구성이 바뀌면 문구도 함께 고친다.
    /// </summary>
    public static class PassageDiscovery
    {
        /// <summary>발견·반응 저장 ID. 내용 개정으로 바꾸지 않는다 — 바꾸면 이미 본 기록이 초기화된다.</summary>
        public const string STAGE1_PASSAGE_RECORD = "stage1_passage_record";

        public const string TARGET_STAGE_ID = "stage_1_abyss_entrance";
        public const string RECORD_ROOM_ID = "room3_crowd";
        public const string CLUE_ROOM_ID = "room5_ambush";

        /// <summary>
        /// 이 시퀀스가 기록이 전제하는 구성인가 — 대상 스테이지에 기록 방과 단서 대상 방이 모두 있어야 한다.
        /// 아니면(다른 런·디버그 구성·방 삭제) 입구 목적도 기록 생성도 하지 않고 기본 동작으로 폴백한다.
        /// </summary>
        public static bool IsCompatible(StageSequenceData sequence)
        {
            if (sequence == null || sequence.stages == null) return false;
            foreach (var stage in sequence.stages)
            {
                if (IsTargetStage(stage) && HasRoom(stage, RECORD_ROOM_ID) && HasRoom(stage, CLUE_ROOM_ID)) return true;
            }
            return false;
        }

        /// <summary>지금 방이 기록을 둘 방인가 — 대상 스테이지의 기록 방이고, 그 스테이지에 단서 대상 방도 있어야 한다.</summary>
        public static bool IsRecordRoom(StageData stage, RoomData room) =>
            room != null && room.roomId == RECORD_ROOM_ID && IsTargetStage(stage) && HasRoom(stage, CLUE_ROOM_ID);

        public static PassageDiscoveryState GetState(MetaSaveService meta)
        {
            if (meta == null || !meta.IsPassageDiscovered(STAGE1_PASSAGE_RECORD)) return PassageDiscoveryState.Unknown;
            return meta.IsPassageReactionViewed(STAGE1_PASSAGE_RECORD)
                ? PassageDiscoveryState.Viewed
                : PassageDiscoveryState.DiscoveredUnviewed;
        }

        private static bool IsTargetStage(StageData stage) => stage != null && stage.stageId == TARGET_STAGE_ID;

        private static bool HasRoom(StageData stage, string roomId)
        {
            if (stage.steps == null) return false;
            foreach (var step in stage.steps)
            {
                if (step?.options == null) continue;
                foreach (var option in step.options)
                {
                    if (option != null && option.roomId == roomId) return true;
                }
            }
            return false;
        }
    }
}

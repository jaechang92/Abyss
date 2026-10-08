using System.Collections.Generic;

namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// 탐사 발견 저장 결과. 메모리 기록과 디스크 저장 성공을 구분한다 —
    /// <see cref="SaveFailed"/>면 이번 세션에서는 기록된 상태지만 재시작하면 마지막 성공 저장으로 돌아갈 수 있다.
    /// </summary>
    public enum PassageSaveResult
    {
        /// <summary>ID가 비었다. 아무것도 기록하지 않았다.</summary>
        Invalid,

        /// <summary>이미 기록돼 있다. 다시 저장하지 않았다(중복 처리 — 보상도 없다).</summary>
        AlreadyRecorded,

        /// <summary>새로 기록했고 디스크 저장에 성공했다.</summary>
        Saved,

        /// <summary>새로 기록했지만 저장이 실패·보류됐다(<see cref="MetaSaveService.IsSaveBlocked"/> 포함). 알림은 기존 저장 실패 경로가 맡는다.</summary>
        SaveFailed
    }

    /// <summary>
    /// 탐사 발견 상태(26-expedition-discovery E3·E4). 발견과 기록자 반응 열람은 별개 목록이다 —
    /// 도감 발견·스토리 진행도(<c>MarkChapterViewed</c>·스냅샷)는 건드리지 않는다.
    ///
    /// 두 API 모두 신규 ID일 때만 저장한다. 저장 성공 여부는 <see cref="Save"/> 반환값을 그대로 옮긴다 —
    /// 차단된 저장을 강제로 덮지 않고, 실패 알림은 기존 <c>OnSaveWriteFailed</c>·저장 상태 오버레이가 낸다.
    /// </summary>
    public sealed partial class MetaSaveService
    {
        /// <summary>통행 기록을 조사했다. 처음일 때만 기록·저장한다.</summary>
        public PassageSaveResult DiscoverPassage(string passageId) =>
            RecordPassage(Current.discoveredPassageIds, passageId);

        /// <summary>기록자 반응을 정상 종료까지 들었다. 처음일 때만 기록·저장한다.</summary>
        public PassageSaveResult MarkPassageReactionViewed(string passageId) =>
            RecordPassage(Current.viewedPassageReactionIds, passageId);

        public bool IsPassageDiscovered(string passageId) => Contains(Current.discoveredPassageIds, passageId);

        public bool IsPassageReactionViewed(string passageId) => Contains(Current.viewedPassageReactionIds, passageId);

        private PassageSaveResult RecordPassage(List<string> target, string passageId)
        {
            if (string.IsNullOrEmpty(passageId)) return PassageSaveResult.Invalid;
            if (target.Contains(passageId)) return PassageSaveResult.AlreadyRecorded;
            target.Add(passageId);
            return Save() ? PassageSaveResult.Saved : PassageSaveResult.SaveFailed;
        }
    }
}

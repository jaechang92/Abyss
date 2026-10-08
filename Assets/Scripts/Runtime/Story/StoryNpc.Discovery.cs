using Abyss.Runtime.Dialogue;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using UnityEngine;

namespace Abyss.Runtime.Story
{
    /// <summary>
    /// 기록자의 발견 반응(26-expedition-discovery E4). 통행 기록을 발견했고 반응을 아직 끝까지 듣지 않았으면,
    /// 자발적으로 말을 걸었을 때 기존 챕터보다 먼저 두 줄을 한 번 들려준다. 이어서 챕터를 연달아 재생하지 않는다.
    ///
    /// 🔴 <b>스토리 진행도와 분리한다.</b> <c>pendingStage</c>에 가짜 챕터를 넣지 않고 <c>MarkChapterViewed</c>를 부르지 않는다 —
    /// 챕터 열람 수·런/보스 스냅샷·각인사 흐름은 전후 같다. 열람은 <see cref="MetaSaveService.MarkPassageReactionViewed"/>만 올린다.
    /// 열람 처리는 <see cref="DialogueEndReason.Completed"/>(마지막 줄을 넘겨 닫힘)일 때만이다. 교체·중단·빈 대사는 잠금만 푼다.
    /// 기록자(<see cref="StorySpeakerIds.Chronicler"/>) 인스턴스만 반응한다.
    /// </summary>
    public sealed partial class StoryNpc
    {
        private static readonly string[] ReactionLineKeys =
        {
            ExpeditionTextKeys.PassageReactionLine1,
            ExpeditionTextKeys.PassageReactionLine2,
        };

        private bool isReactionPlaying;

        /// <summary>
        /// 자격이 되면 반응을 재생하고 true. 아니면(다른 화자·미발견·이미 열람·대사 키 누락·대화 UI 꺼짐) false —
        /// 호출자는 기존 챕터 경로로 간다. 키가 빠졌으면 열람 처리하지 않고 결손을 남긴다.
        /// </summary>
        private bool TryPlayDiscoveryReaction(int token)
        {
            if (SpeakerId != StorySpeakerIds.Chronicler || !dialogueUI.isActiveAndEnabled) return false;

            var meta = MetaSaveService.Instance;
            if (PassageDiscovery.GetState(meta) != PassageDiscoveryState.DiscoveredUnviewed) return false;

            var lines = BuildReactionLines();
            if (lines == null) return false;

            isReactionPlaying = true;
            dialogueUI.PlayTracked(lines, reason => OnReactionEnded(token, reason));
            return true;
        }

        private void OnReactionEnded(int token, DialogueEndReason reason)
        {
            if (this == null || token != sessionToken) return;   // 지난 세션의 늦은 콜백 — 지금 대화를 끝내지 않는다
            isReactionPlaying = false;

            if (reason == DialogueEndReason.Completed)
            {
                var meta = MetaSaveService.Instance;
                var result = meta != null
                    ? meta.MarkPassageReactionViewed(PassageDiscovery.STAGE1_PASSAGE_RECORD)
                    : PassageSaveResult.Invalid;
                if (result == PassageSaveResult.SaveFailed)
                    Debug.LogWarning("[StoryNpc] 발견 반응 열람 — 이번 세션에는 기록됐지만 디스크 저장 실패");
            }
            else
            {
                Debug.Log($"[StoryNpc] 발견 반응 미완료({reason}) — 열람 처리 안 함");
            }
            EndSession();
        }

        /// <summary>반응 재생 중 NPC가 꺼지면 세션을 닫는다 — 잠금을 풀고, 뒤늦게 온 완료가 열람으로 처리되지 않게 한다.</summary>
        private void OnDisable()
        {
            if (!isReactionPlaying) return;
            isReactionPlaying = false;
            EndSession();
        }

        /// <summary>반응 대사 두 줄. 화자 이름·본문 키가 하나라도 표에 없으면 null.</summary>
        private static DialogueLine[] BuildReactionLines()
        {
            string speakerKey = StringKey.Npc_Recordkeeper_Name;
            if (!Loc.HasKey(speakerKey))
            {
                Debug.LogWarning($"[StoryNpc] 발견 반응 화자 키 누락: {speakerKey} — 기존 챕터로 폴백");
                return null;
            }

            var lines = new DialogueLine[ReactionLineKeys.Length];
            for (int i = 0; i < ReactionLineKeys.Length; i++)
            {
                if (!Loc.HasKey(ReactionLineKeys[i]))
                {
                    Debug.LogWarning($"[StoryNpc] 발견 반응 대사 키 누락: {ReactionLineKeys[i]} — 기존 챕터로 폴백(열람 처리 안 함)");
                    return null;
                }
                lines[i] = new DialogueLine { speakerKey = speakerKey, textKey = ReactionLineKeys[i] };
            }
            return lines;
        }
    }
}

namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// 스토리 화자 식별자 상수. <see cref="StoryProgressEntry.speakerId"/>에 저장되는 값의 SoT다.
    ///
    /// Story 쪽이 아니라 Meta에 두는 이유: 이 문자열은 <b>세이브 파일에 적히는 키</b>라서
    /// 바뀌면 기존 세이브의 진행도가 통째로 사라진다. 표시용 이름(StringKey)과 달리
    /// 마음대로 고칠 수 없는 값이므로, 스키마를 소유한 쪽에 둔다.
    /// </summary>
    public static class StorySpeakerIds
    {
        /// <summary>
        /// 기록자 — 로비의 서사 NPC(<c>StoryNpc</c>). 연재 모델(챕터가 순차 개방).
        ///
        /// ⚠️ v2 → v3 마이그레이션이 옛 전역 진행도를 <b>이 id로</b> 옮긴다.
        /// 값을 바꾸면 변환된 세이브가 화자를 못 찾아 기록자 진행도가 0으로 되돌아간다.
        /// </summary>
        public const string Chronicler = "chronicler";

        /// <summary>각인사 — 로비의 정비 NPC(<c>ServiceNpc</c>). 사전 모델(폼 보유로 항목별 개방).</summary>
        public const string Engraver = "engraver";
    }
}

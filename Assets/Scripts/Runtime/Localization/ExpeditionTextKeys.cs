namespace Abyss.Runtime.Localization
{
    /// <summary>
    /// 분기 방 손익 예고(<c>ExpeditionRoomPreview</c>)의 GameText.csv 키 — 탐험 시스템 전용 묶음.
    /// <see cref="StringKey"/>와 같은 수동 동기화 규칙이다: CSV에 키를 추가하면 여기에도 const를 추가한다.
    ///
    /// 목록을 잇는 구분자도 키로 둔다 — CSV 값은 앞뒤 공백이 잘리므로 「A + B」처럼 두 칸을 끼우는 형식으로
    /// 쌍마다 접는다(일본어는 「、」처럼 언어마다 구분자가 다르다).
    /// </summary>
    public static class ExpeditionTextKeys
    {
        // ---- 전투 방 ----
        // {0} = 구성 문구(아래 근접/원거리 형식)
        public const string EnemiesFormat = "Expedition_EnemiesFormat";
        public const string ReinforcementsFormat = "Expedition_ReinforcementsFormat";
        // {0} = 근접 수, {1} = 원거리 수
        public const string MeleeRangedFormat = "Expedition_MeleeRangedFormat";
        public const string MeleeFormat = "Expedition_MeleeFormat";
        public const string RangedFormat = "Expedition_RangedFormat";
        public const string FormReward = "Expedition_FormReward";
        public const string FormRewardRandom = "Expedition_FormRewardRandom";
        public const string WeaponReward = "Expedition_WeaponReward";
        public const string WeaponRewardRandom = "Expedition_WeaponRewardRandom";
        // {0} = 배율 전 클리어 골드
        public const string ClearGoldFormat = "Expedition_ClearGoldFormat";

        // ---- 이벤트 방 — 대가 ----
        public const string GoldCostFormat = "Expedition_GoldCostFormat";
        public const string HpCostFormat = "Expedition_HpCostFormat";
        public const string Free = "Expedition_Free";
        public const string FreePass = "Expedition_FreePass";

        // ---- 이벤트 방 — 결과 ----
        public const string GoldGainFormat = "Expedition_GoldGainFormat";
        public const string HealFormat = "Expedition_HealFormat";
        public const string SkillDraft = "Expedition_SkillDraft";
        public const string SkillDraftCountFormat = "Expedition_SkillDraftCountFormat";
        public const string RerollFormat = "Expedition_RerollFormat";
        public const string WeaponDraw = "Expedition_WeaponDraw";
        public const string WeaponDrawCountFormat = "Expedition_WeaponDrawCountFormat";

        // ---- 이벤트 방 — 줄 조립 ----
        // {0} = 대가, {1} = 결과 목록
        public const string ChoiceFormat = "Expedition_ChoiceFormat";
        // {0}, {1} = 앞쪽 누적·다음 항목
        public const string CostPairFormat = "Expedition_CostPairFormat";
        public const string OutcomePairFormat = "Expedition_OutcomePairFormat";
        // {0} = 선택지 줄, {1} = 현재 보유 골드
        public const string UnaffordableFormat = "Expedition_UnaffordableFormat";

        // ---- 탐사 발견(E1·E3·E4) ----
        // 입구 근처 한 줄 — 미발견 목적 / 발견·반응 미열람 안내
        public const string PortalPurpose = "Expedition_PortalPurpose";
        public const string PortalRecorderHint = "Expedition_PortalRecorderHint";
        // 통행 기록 — 이름표·상호작용 문구·본문(본문은 서사 키: 한국어만, 다른 언어는 한국어 폴백)
        public const string PassageRecordTitle = "Expedition_PassageRecordTitle";
        public const string PassageRecordPrompt = "Expedition_PassageRecordPrompt";
        public const string PassageRecordText = "Story_PassageRecord_Line1";
        // 기록자 반응 두 줄(서사 키)
        public const string PassageReactionLine1 = "Story_PassageReaction_Line1";
        public const string PassageReactionLine2 = "Story_PassageReaction_Line2";
    }
}

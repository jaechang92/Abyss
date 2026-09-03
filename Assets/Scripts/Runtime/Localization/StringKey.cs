namespace Abyss.Runtime.Localization
{
    /// <summary>
    /// GameText.csv의 StringKey 컬럼을 컴파일 타임 상수로 노출.
    /// 사용 예: Loc.Get(StringKey.Common_Confirm)
    ///
    /// CSV에 키 추가 시 본 파일에도 const를 추가한다. (수동 동기화)
    /// EA 단계에서 자동 생성기 도입 시 본 파일은 자동 갱신 대상이 된다.
    /// </summary>
    public static class StringKey
    {
        // ---- 공통 ----
        public const string Common_Confirm = "Common_Confirm";
        public const string Common_Cancel = "Common_Cancel";
        public const string Common_Close = "Common_Close";
        public const string Common_Yes = "Common_Yes";
        public const string Common_No = "Common_No";
        public const string Common_Back = "Common_Back";

        // ---- 메인 메뉴 ----
        public const string Menu_NewRun = "Menu_NewRun";
        public const string Menu_Continue = "Menu_Continue";
        public const string Menu_Settings = "Menu_Settings";
        public const string Menu_Exit = "Menu_Exit";

        // ---- HUD ----
        public const string Hud_HpLabel = "Hud_HpLabel";
        public const string Hud_FormSwapHint = "Hud_FormSwapHint";

        // ---- 드래프트 ----
        public const string Draft_Title = "Draft_Title";
        public const string Draft_Reroll = "Draft_Reroll";
        public const string Draft_Skip = "Draft_Skip";
        public const string Draft_ReplaceSlotTitle = "Draft_ReplaceSlotTitle";

        // ---- 결과 ----
        public const string Result_RunClear = "Result_RunClear";
        public const string Result_Death = "Result_Death";
        public const string Result_PlayTimeFormat = "Result_PlayTimeFormat";
        public const string Result_KillCountFormat = "Result_KillCountFormat";
        public const string Result_ShardsGainedFormat = "Result_ShardsGainedFormat";
        public const string Result_Restart = "Result_Restart";
        public const string Result_MainMenu = "Result_MainMenu";

        // ---- 설정 ----
        public const string Settings_MasterVolume = "Settings_MasterVolume";
        public const string Settings_BgmVolume = "Settings_BgmVolume";
        public const string Settings_SfxVolume = "Settings_SfxVolume";
        public const string Settings_Language = "Settings_Language";

        // ---- 폼 ----
        public const string Form_DarkBlade = "Form_DarkBlade";
        public const string Form_VoidArcher = "Form_VoidArcher";

        // ---- 대화 (NPC) ----
        public const string Npc_Guide_Name = "Npc_Guide_Name";
        public const string Npc_Guide_Prompt = "Npc_Guide_Prompt";
        public const string Npc_Guide_Line1 = "Npc_Guide_Line1";
        public const string Npc_Guide_Line2 = "Npc_Guide_Line2";
        public const string Npc_Guide_Line3 = "Npc_Guide_Line3";

        // ---- 로비 상호작용 ----
        public const string Portal_Prompt = "Portal_Prompt";
        public const string Npc_Service_Prompt = "Npc_Service_Prompt";

        // ---- 심연의 제단 (메타 영구 업그레이드) ----
        public const string Npc_Altar_Prompt = "Npc_Altar_Prompt";
        public const string Altar_Title = "Altar_Title";
        public const string Altar_ShardsFormat = "Altar_ShardsFormat";
        public const string Altar_LevelFormat = "Altar_LevelFormat";
        public const string Altar_CostFormat = "Altar_CostFormat";
        public const string Altar_Maxed = "Altar_Maxed";
        public const string Altar_Purchase = "Altar_Purchase";

        // ── 유물 상점(가차) ──
        public const string Npc_RelicShop_Prompt = "Npc_RelicShop_Prompt";
        public const string Relic_Title = "Relic_Title";
        public const string Relic_DrawFormat = "Relic_DrawFormat";
        public const string Relic_Drawing = "Relic_Drawing";
        public const string Relic_NotEnough = "Relic_NotEnough";
        public const string Relic_GainedFormat = "Relic_GainedFormat";
        public const string Relic_LevelUpFormat = "Relic_LevelUpFormat";
        public const string Relic_MaxedFormat = "Relic_MaxedFormat";
        public const string Relic_Equipped = "Relic_Equipped";
        public const string Relic_SlotsFull = "Relic_SlotsFull";
        public const string Relic_EmptySlot = "Relic_EmptySlot";
        public const string Relic_OwnedHeader = "Relic_OwnedHeader";
        public const string Relic_NoneOwned = "Relic_NoneOwned";
        public const string Relic_LevelFormat = "Relic_LevelFormat";
        public const string Relic_Close = "Relic_Close";
        public const string Upgrade_MaxHp_Name = "Upgrade_MaxHp_Name";
        public const string Upgrade_MaxHp_Desc = "Upgrade_MaxHp_Desc";
        public const string Upgrade_Attack_Name = "Upgrade_Attack_Name";
        public const string Upgrade_Attack_Desc = "Upgrade_Attack_Desc";

        // 시작 특전(3-2). 스탯 강화와 달리 런을 시작할 때 한 번 적용된다.
        public const string Upgrade_StartingGold_Name = "Upgrade_StartingGold_Name";
        public const string Upgrade_StartingGold_Desc = "Upgrade_StartingGold_Desc";
        public const string Upgrade_FreeReroll_Name = "Upgrade_FreeReroll_Name";
        public const string Upgrade_FreeReroll_Desc = "Upgrade_FreeReroll_Desc";

        // ---- 서사 (기록자 NPC) ----
        public const string Npc_Chronicler_Name = "Npc_Chronicler_Name";
        public const string Npc_Chronicler_Prompt = "Npc_Chronicler_Prompt";
        public const string Story_Ch1_Line1 = "Story_Ch1_Line1";
        public const string Story_Ch1_Line2 = "Story_Ch1_Line2";
        public const string Story_Ch2_Line1 = "Story_Ch2_Line1";
        public const string Story_Ch2_Line2 = "Story_Ch2_Line2";
        public const string Story_Ch3_Line1 = "Story_Ch3_Line1";
        public const string Story_Ch3_Line2 = "Story_Ch3_Line2";
        public const string Story_Idle_Line1 = "Story_Idle_Line1";

        // ---- 서사 (각인사 NPC — 폼 4종 내력, N-2) ----
        public const string Npc_Engraver_Name = "Npc_Engraver_Name";
        public const string Story_Engraver_DarkBlade_Line1 = "Story_Engraver_DarkBlade_Line1";
        public const string Story_Engraver_DarkBlade_Line2 = "Story_Engraver_DarkBlade_Line2";
        public const string Story_Engraver_DarkBlade_Line3 = "Story_Engraver_DarkBlade_Line3";
        public const string Story_Engraver_VoidArcher_Line1 = "Story_Engraver_VoidArcher_Line1";
        public const string Story_Engraver_VoidArcher_Line2 = "Story_Engraver_VoidArcher_Line2";
        public const string Story_Engraver_VoidArcher_Line3 = "Story_Engraver_VoidArcher_Line3";
        public const string Story_Engraver_AncientShield_Line1 = "Story_Engraver_AncientShield_Line1";
        public const string Story_Engraver_AncientShield_Line2 = "Story_Engraver_AncientShield_Line2";
        public const string Story_Engraver_AncientShield_Line3 = "Story_Engraver_AncientShield_Line3";
        public const string Story_Engraver_VoidThrower_Line1 = "Story_Engraver_VoidThrower_Line1";
        public const string Story_Engraver_VoidThrower_Line2 = "Story_Engraver_VoidThrower_Line2";
        public const string Story_Engraver_VoidThrower_Line3 = "Story_Engraver_VoidThrower_Line3";
        public const string Story_Engraver_Closing_Line1 = "Story_Engraver_Closing_Line1";
        public const string Story_Engraver_Closing_Line2 = "Story_Engraver_Closing_Line2";

        // ---- 서사 (자막 — 프롤로그·엔딩, 4-3) ----
        // NPC 대사와 달리 화자가 주인공 자신이다. 인칭은 1인칭 「나」로 고정된다
        // (NPC는 주인공을 「자네」로 부른다 — 12-prologue-ending-text.md §3-1).
        // 프롤로그 마지막 문단과 엔딩 마지막 문단은 「내려다보던 것」으로 짝을 이룬다. 한쪽만 고치지 말 것.
        public const string Story_Prologue_Line1 = "Story_Prologue_Line1";
        public const string Story_Prologue_Line2 = "Story_Prologue_Line2";
        public const string Story_Prologue_Line3 = "Story_Prologue_Line3";
        public const string Story_Prologue_Line4 = "Story_Prologue_Line4";
        public const string Story_Ending_Line1 = "Story_Ending_Line1";
        public const string Story_Ending_Line2 = "Story_Ending_Line2";
        public const string Story_Ending_Line3 = "Story_Ending_Line3";
        public const string Story_Ending_Line4 = "Story_Ending_Line4";
    }
}

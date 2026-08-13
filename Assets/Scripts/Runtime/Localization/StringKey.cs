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
        public const string Upgrade_MaxHp_Name = "Upgrade_MaxHp_Name";
        public const string Upgrade_MaxHp_Desc = "Upgrade_MaxHp_Desc";
        public const string Upgrade_Attack_Name = "Upgrade_Attack_Name";
        public const string Upgrade_Attack_Desc = "Upgrade_Attack_Desc";

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
        public const string Story_Engraver_Idle_Line1 = "Story_Engraver_Idle_Line1";
        public const string Story_Engraver_Idle_Line2 = "Story_Engraver_Idle_Line2";
    }
}

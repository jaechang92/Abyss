using Abyss.Runtime.Draft;
using Abyss.Runtime.Stage;
using UnityEngine;

namespace Abyss.Runtime.ArtIntegration
{
    /// <summary>
    /// UI 그림 키 SoT(A2). 키는 색인(ui_art_index.json)의 <c>key</c> 와 같다.
    /// 데이터 ID(enemyId·shopId·synergyTag·RoomType)에서 키를 만드는 규칙도 여기 하나에 둔다 —
    /// 표시 문자열(현지화된 이름)로 그림을 고르지 않는다.
    /// </summary>
    public static class UiArtKeys
    {
        // ── 도감 ──
        public const string CODEX_ENTRY_FRAME = "codex/entry_frame";
        public const string CODEX_PORTRAIT_FRAME = "codex/portrait_frame";
        public const string CODEX_TAB_FORM = "codex/tab_form";
        public const string CODEX_TAB_SKILL = "codex/tab_skill";
        public const string CODEX_TAB_ENEMY = "codex/tab_enemy";
        public const string CODEX_TAB_BOSS = "codex/tab_boss";
        public const string CODEX_TAB_RELIC = "codex/tab_relic";
        public const string CODEX_TAB_RECORDS = "codex/tab_records";
        public const string CODEX_UNDISCOVERED = "codex/undiscovered";

        // ── HUD 프레임 ──
        public const string HUD_PLAYER_HEALTH_FRAME = "hud/player_health_frame";
        public const string HUD_BOSS_HEALTH_FRAME = "hud/boss_health_frame";
        public const string HUD_CURRENCY_FRAME = "hud/currency_frame";
        public const string HUD_FORM_SLOT_FRAME = "hud/form_slot_frame";
        public const string HUD_SKILL_SLOT_FRAME = "hud/skill_slot_frame";
        public const string HUD_SYNERGY_FRAME = "hud/synergy_frame";

        // ── 화폐·공용 ──
        public const string CURRENCY_ABYSS_SHARDS = "currency/abyss_shards";
        public const string CURRENCY_GOLD_SHARDS = "currency/gold_shards";
        public const string COMMON_REROLL = "common/reroll";
        public const string COMMON_SAVE_RECORD = "common/save_record";

        // ── 화면 틀·표식 ──
        public const string UI_DIALOGUE_FRAME = "ui/dialogue_frame";
        public const string UI_TUTORIAL_FRAME = "ui/tutorial_frame";
        public const string UI_RUN_RESULT_EMBLEM = "ui/run_result_emblem";
        public const string UI_FOCUS_BRACKET = "ui/focus_bracket";
        public const string UI_INPUT_KEY_FRAME = "ui/input_key_frame";
        public const string UI_INTERACTION_MARKER = "ui/interaction_marker";
        public const string UI_OBJECTIVE_COMPLETE = "ui/objective_complete";

        private const string BOSS_PREFIX = "boss/";
        private const string SHOP_PREFIX = "shop/";
        private const string SYNERGY_PREFIX = "synergy/";
        private const string ROUTE_PREFIX = "route/";

        /// <summary>보스·중간보스 초상 키. 실제 EnemyData.enemyId 를 그대로 잇는다(수호자는 v2 채택본).</summary>
        public static string BossPortrait(string enemyId) =>
            string.IsNullOrEmpty(enemyId) ? null : BOSS_PREFIX + enemyId;

        /// <summary>상점 상인 초상 키. ShopData.shopId 기준 — 현지화된 상점 이름으로 고르지 않는다.</summary>
        public static string ShopPortrait(string shopId) =>
            string.IsNullOrEmpty(shopId) ? null : SHOP_PREFIX + shopId;

        /// <summary>
        /// 시너지 축 아이콘 키. 키 꼬리는 <see cref="SynergyAxis"/> 축 ID 와 같다.
        /// frost·soul 은 규칙상 예약 축이다 — 조회만 연결하고 등장·발동은 보유 스킬 데이터가 정한다.
        /// </summary>
        public static string SynergyAxisIcon(string axisTag) => axisTag switch
        {
            SynergyAxis.AXIS_FIRE => SYNERGY_PREFIX + "fire",
            SynergyAxis.AXIS_ABYSS => SYNERGY_PREFIX + "abyss",
            SynergyAxis.AXIS_GUARD => SYNERGY_PREFIX + "guard",
            SynergyAxis.AXIS_BLOOD_PACT => SYNERGY_PREFIX + "blood_pact",
            SynergyAxis.AXIS_FROST => SYNERGY_PREFIX + "frost",
            SynergyAxis.AXIS_SOUL => SYNERGY_PREFIX + "soul",
            _ => null
        };

        /// <summary>방 타입 경로 그림 키. 그림이 없는 타입(보스·이벤트)은 null.</summary>
        public static string Route(RoomType type) => type switch
        {
            RoomType.Combat => ROUTE_PREFIX + "combat",
            RoomType.Elite => ROUTE_PREFIX + "elite",
            RoomType.Rest => ROUTE_PREFIX + "rest",
            RoomType.Shop => ROUTE_PREFIX + "shop",
            _ => null
        };

        /// <summary>
        /// 방 타입 경로 그림(노드 맵 카드가 쓰고, A3 월드 출구 예고도 같은 것을 쓴다).
        /// 그림이 없는 타입이거나 리소스가 빠지면 null.
        /// </summary>
        public static Sprite TryGetRouteIcon(RoomType type) => UiArtLibrary.Get(Route(type));
    }
}

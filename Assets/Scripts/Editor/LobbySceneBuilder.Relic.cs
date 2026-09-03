#if UNITY_EDITOR
using Abyss.Runtime.Lobby;
using Abyss.Runtime.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.EditorTools
{
    /// <summary>
    /// LobbySceneBuilder의 <b>유물 상점(가차)</b> 부분 — 상점 NPC + RelicShopPanel 루트.
    /// (메인 LobbySceneBuilder.cs와 같은 partial 클래스라 CreateRect/Stretch/SetObject 헬퍼를 공유한다.)
    ///
    /// 🔑 <b>박스는 여기서 만들지 않는다.</b> 패널이 <see cref="RelicShopPanel"/> 안에서
    /// 카탈로그 수에 맞춰 스스로 조립한다 — 유물이 늘어날 때마다 빌더를 고치고 씬을
    /// 다시 만들어야 한다면, 그 사이에 "코드에는 있는데 화면에는 없는" 상태가 생긴다.
    /// 빌더가 책임지는 것은 <b>루트와 배선</b>뿐이다.
    /// </summary>
    public static partial class LobbySceneBuilder
    {
        // 상점 오브젝트 색(청록 계열 — 제단의 보라, 정비의 초록, 포털의 자주와 구분)
        private static readonly Color RelicShopColor = new(0.30f, 0.62f, 0.62f);

        /// <summary>유물 상점 NPC 생성. 제단(-9)과 정비(3) 사이에 두어 동선이 겹치지 않게 한다.</summary>
        private static RelicShopNpc CreateRelicShopNpc()
        {
            return CreateNpcObject<RelicShopNpc>("RelicShopNpc", new Vector2(-4.5f, -2.5f), new Vector2(1f, 2f), RelicShopColor);
        }

        /// <summary>
        /// 유물 상점 패널의 루트만 만든다(전체 화면 반투명 배경). 내용은 최초 Open 시 런타임 조립.
        /// 초기 비활성 — 패널 Awake 가 다시 끄지만, 씬을 열었을 때 화면을 가리지 않게 여기서도 꺼 둔다.
        /// </summary>
        private static RelicShopPanel CreateRelicShopPanel(Canvas canvas)
        {
            var panel = canvas.gameObject.AddComponent<RelicShopPanel>();

            var root = CreateRect(canvas.transform, "RelicShopRoot", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)root.transform);
            root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
            root.SetActive(false);

            var so = new SerializedObject(panel);
            SetObject(so, "root", root);
            SetObject(so, "boxParent", root.transform as RectTransform);
            so.ApplyModifiedProperties();

            return panel;
        }

        private static void WireRelicShopNpc(RelicShopNpc npc, RelicShopPanel panel, LobbyPlayerController player)
        {
            var so = new SerializedObject(npc);
            SetObject(so, "shopPanel", panel);
            SetObject(so, "player", player);
            ApplyNpcPrompt(so, StringKey.Npc_RelicShop_Prompt);
        }
    }
}
#endif

#if UNITY_EDITOR
using System.IO;
using Abyss.Runtime.Lobby;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.EditorTools
{
    /// <summary>
    /// LobbySceneBuilder의 심연의 제단(메타 영구 업그레이드) 부분 —
    /// 제단 NPC + MetaUpgradePanel Canvas UI + MetaUpgradeData 에셋 load-or-create.
    /// (메인 LobbySceneBuilder.cs와 같은 partial 클래스라 CreateRect/CreateText/CreateButton 등 헬퍼를 공유한다.)
    /// </summary>
    public static partial class LobbySceneBuilder
    {
        // 제단 오브젝트 색(보라색 계열 — 안내자/정비/포털과 구분)
        private static readonly Color AltarColor = new Color(0.55f, 0.3f, 0.8f);

        /// <summary>제단 NPC 생성. 트리거 콜라이더로 PlayerInteractor가 감지한다.</summary>
        private static AltarNpc CreateAltarNpc()
        {
            return CreateNpcObject<AltarNpc>("AltarNpc", new Vector2(-9f, -2.5f), new Vector2(1f, 2f), AltarColor);
        }

        /// <summary>메타 업그레이드 패널 Canvas UI 생성 + 컴포넌트 와이어. 초기 비활성. 행은 런타임 동적 생성.</summary>
        private static MetaUpgradePanel CreateMetaUpgradePanel(Canvas canvas)
        {
            var panel = canvas.gameObject.AddComponent<MetaUpgradePanel>();

            // 전체 화면 반투명 배경 루트(토글 대상)
            var root = CreateRect(canvas.transform, "AltarRoot", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)root.transform);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.85f);

            var box = CreateRect(root.transform, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 600));
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.09f, 0.07f, 0.13f, 0.98f);

            // 제목/잔액(텍스트는 런타임에 MetaUpgradePanel이 Loc로 갱신)
            var title = CreateText(box.transform, "Title", "심연의 제단", 30, new Vector2(0, -46), new Vector2(760, 46), TextAnchor.MiddleCenter, new Color(0.85f, 0.7f, 1f));
            var shards = CreateText(box.transform, "Shards", "심연 조각: 0", 22, new Vector2(0, -98), new Vector2(600, 36), TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.6f));

            // 행 컨테이너(top-center 정렬 — 패널이 행을 위에서부터 채운다)
            var rowContainer = CreateRect(box.transform, "RowContainer", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(780, 360));

            var (close, _) = CreateButton(box.transform, "CloseButton", new Vector2(0, -250), new Vector2(240, 64), new Color(0.4f, 0.25f, 0.3f), "닫기");

            root.SetActive(false);

            var so = new SerializedObject(panel);
            SetObject(so, "root", root);
            SetObject(so, "titleLabel", title);
            SetObject(so, "shardsLabel", shards);
            SetObject(so, "rowContainer", rowContainer.GetComponent<RectTransform>());
            SetObject(so, "closeButton", close);
            so.ApplyModifiedProperties();

            return panel;
        }

        private static void WireAltarNpc(AltarNpc npc, MetaUpgradePanel panel, LobbyPlayerController player)
        {
            var so = new SerializedObject(npc);
            SetObject(so, "upgradePanel", panel);
            SetObject(so, "player", player);
            ApplyNpcPrompt(so, StringKey.Npc_Altar_Prompt);
        }

        /// <summary>
        /// 메타 업그레이드 SO 2개(생명력/공격력)를 로드하거나 없으면 생성하고 값을 보정한다.
        /// LobbySceneBuilder.Dialogue.cs의 Guide.asset load-or-create 패턴을 따른다.
        /// </summary>
        private static void LoadOrCreateMetaUpgrades()
        {
            if (!Directory.Exists(AbyssPaths.MetaUpgrades)) Directory.CreateDirectory(AbyssPaths.MetaUpgrades);

            UpsertMetaUpgrade("MaxHp", "meta_max_hp", MetaUpgradeType.MaxHp, 20f,
                new[] { 30, 60, 100, 150, 220 }, StringKey.Upgrade_MaxHp_Name, StringKey.Upgrade_MaxHp_Desc);
            UpsertMetaUpgrade("Attack", "meta_attack", MetaUpgradeType.AttackMultiplier, 0.08f,
                new[] { 40, 80, 130, 190, 260 }, StringKey.Upgrade_Attack_Name, StringKey.Upgrade_Attack_Desc);

            AssetDatabase.SaveAssets();
            MetaUpgrades.Reload();
        }

        private static void UpsertMetaUpgrade(string fileName, string upgradeId, MetaUpgradeType type, float valuePerLevel, int[] costLadder, string nameKey, string descKey)
        {
            string path = AbyssPaths.MetaUpgrades + "/" + fileName + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<MetaUpgradeData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<MetaUpgradeData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.upgradeId = upgradeId;
            data.type = type;
            data.valuePerLevel = valuePerLevel;
            data.costLadder = costLadder;
            data.nameKey = nameKey;
            data.descKey = descKey;
            EditorUtility.SetDirty(data);
        }
    }
}
#endif

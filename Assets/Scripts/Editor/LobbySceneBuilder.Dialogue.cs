#if UNITY_EDITOR
using System.IO;
using Abyss.Runtime.Dialogue;
using Abyss.Runtime.Lobby;
using Abyss.Runtime.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.EditorTools
{
    /// <summary>
    /// LobbySceneBuilder의 대화(H2) 부분 — 공용 DialogueUI 박스 + 안내자 NPC + DialogueData 에셋.
    /// (메인 LobbySceneBuilder.cs와 같은 partial 클래스라 CreateRect/CreateText 등 헬퍼를 공유한다.)
    /// </summary>
    public static partial class LobbySceneBuilder
    {
        /// <summary>화면 하단 대화 박스 UI 생성 + DialogueUI 컴포넌트 와이어. 초기 비활성.</summary>
        private static DialogueUI CreateDialogueUI(Canvas canvas)
        {
            var ui = canvas.gameObject.AddComponent<DialogueUI>();

            var root = CreateRect(canvas.transform, "DialogueRoot",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -380), new Vector2(1200, 220));
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);

            var speaker = CreateText(root.transform, "Speaker", "", 22, new Vector2(-540, 75), new Vector2(500, 36), TextAnchor.MiddleLeft, new Color(1f, 0.9f, 0.6f));
            var body = CreateText(root.transform, "Body", "", 20, new Vector2(0, -5), new Vector2(1120, 120), TextAnchor.UpperLeft, Color.white);
            var hint = CreateText(root.transform, "Hint", "Space: 다음", 16, new Vector2(520, -88), new Vector2(200, 30), TextAnchor.MiddleRight, new Color(0.7f, 0.7f, 0.75f));

            root.SetActive(false);

            var so = new SerializedObject(ui);
            SetObject(so, "root", root);
            SetObject(so, "speakerLabel", speaker);
            SetObject(so, "bodyLabel", body);
            SetObject(so, "hintLabel", hint);
            so.ApplyModifiedProperties();

            return ui;
        }

        /// <summary>안내자 NPC(스토리) 생성. 트리거 콜라이더로 PlayerInteractor가 감지한다.</summary>
        private static DialogueNpc CreateGuideNpc()
        {
            var go = new GameObject("GuideNpc");
            go.transform.position = new Vector3(-6f, -2.5f, 0f);
            go.transform.localScale = new Vector3(1f, 2f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = EditorPlatformFactory.LoadWhiteSquare();
            sr.color = new Color(0.9f, 0.8f, 0.3f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            return go.AddComponent<DialogueNpc>();
        }

        /// <summary>안내자 대화 SO를 로드하거나 없으면 생성. lines는 항상 최신 StringKey로 갱신.</summary>
        private static DialogueData LoadOrCreateGuideDialogue()
        {
            string path = AbyssPaths.Dialogue + "/Guide.asset";
            var data = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (data == null)
            {
                if (!Directory.Exists(AbyssPaths.Dialogue)) Directory.CreateDirectory(AbyssPaths.Dialogue);
                data = ScriptableObject.CreateInstance<DialogueData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.lines = new[]
            {
                new DialogueLine { speakerKey = StringKey.Npc_Guide_Name, textKey = StringKey.Npc_Guide_Line1 },
                new DialogueLine { speakerKey = StringKey.Npc_Guide_Name, textKey = StringKey.Npc_Guide_Line2 },
                new DialogueLine { speakerKey = StringKey.Npc_Guide_Name, textKey = StringKey.Npc_Guide_Line3 },
            };
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        private static void WireDialogueNpc(DialogueNpc npc, DialogueUI ui, LobbyPlayerController player)
        {
            var data = LoadOrCreateGuideDialogue();
            var so = new SerializedObject(npc);
            SetObject(so, "dialogue", data);
            SetObject(so, "dialogueUI", ui);
            SetObject(so, "player", player);
            var promptProp = so.FindProperty("promptKey");
            if (promptProp != null) promptProp.stringValue = StringKey.Npc_Guide_Prompt;
            so.ApplyModifiedProperties();
        }
    }
}
#endif

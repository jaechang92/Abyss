#if UNITY_EDITOR
using System.IO;
using Abyss.Runtime.Dialogue;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Lobby;
using Abyss.Runtime.Story;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// LobbySceneBuilder의 서사(기록자 NPC) 부분 — 기록자 NPC + StoryData 에셋 load-or-create.
    /// DialogueUI는 안내자와 공용(메인 빌더의 CreateDialogueUI 결과)을 재사용한다.
    /// (메인 LobbySceneBuilder.cs와 같은 partial 클래스라 SetObject 등 헬퍼를 공유한다.)
    /// </summary>
    public static partial class LobbySceneBuilder
    {
        // 기록자 색(청록 계열 — 안내자/정비/제단과 구분)
        private static readonly Color ChroniclerColor = new Color(0.3f, 0.62f, 0.66f);

        /// <summary>기록자 NPC 생성. 트리거 콜라이더로 PlayerInteractor가 감지한다.</summary>
        private static StoryNpc CreateStoryNpc()
        {
            return CreateNpcObject<StoryNpc>("StoryNpc", new Vector2(-2.5f, -2.5f), new Vector2(1f, 2f), ChroniclerColor);
        }

        private static void WireStoryNpc(StoryNpc npc, StoryData story, DialogueUI dialogueUI, LobbyPlayerController player)
        {
            var so = new SerializedObject(npc);
            SetObject(so, "story", story);
            SetObject(so, "dialogueUI", dialogueUI);
            SetObject(so, "player", player);
            ApplyNpcPrompt(so, StringKey.Npc_Chronicler_Prompt);
        }

        /// <summary>
        /// 기록자 서사 StoryData 에셋 load-or-create. 챕터 라인은 StringKey 참조로 채운다.
        /// 챕터: 1(첫 방문) / 2(런 1회 이상) / 3(보스 1처치 이상).
        /// </summary>
        private static StoryData LoadOrCreateStoryData()
        {
            if (!Directory.Exists(AbyssPaths.Story)) Directory.CreateDirectory(AbyssPaths.Story);

            string path = AbyssPaths.Story + "/Chronicler.asset";
            var data = AssetDatabase.LoadAssetAtPath<StoryData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<StoryData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.chapters = new[]
            {
                new StoryChapter
                {
                    chapterStage = 1, minRunCount = 0, minBossKills = 0,
                    lines = new[] { Line(StringKey.Story_Ch1_Line1), Line(StringKey.Story_Ch1_Line2) },
                },
                new StoryChapter
                {
                    chapterStage = 2, minRunCount = 1, minBossKills = 0,
                    lines = new[] { Line(StringKey.Story_Ch2_Line1), Line(StringKey.Story_Ch2_Line2) },
                },
                new StoryChapter
                {
                    chapterStage = 3, minRunCount = 0, minBossKills = 1,
                    lines = new[] { Line(StringKey.Story_Ch3_Line1), Line(StringKey.Story_Ch3_Line2) },
                },
            };
            data.idleLines = new[] { Line(StringKey.Story_Idle_Line1) };

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        private static DialogueLine Line(string textKey)
            => new DialogueLine { speakerKey = StringKey.Npc_Chronicler_Name, textKey = textKey };
    }
}
#endif

#if UNITY_EDITOR
using System.IO;
using Abyss.Runtime.Dialogue;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Lobby;
using Abyss.Runtime.Meta;
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
            SetString(so, "speakerId", StorySpeakerIds.Chronicler);
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

        /// <summary>
        /// 각인사 내력 StoryData 에셋 load-or-create (N-2). 폼 4종의 선행자 내력 4편 + idle.
        ///
        /// 기록자와 달리 <b>진행 델타 조건이 없다</b>(minRunCount·minBossKills 0) — 해금 조건은
        /// <c>requiredFormId</c> 하나뿐이다. 각인사는 연재가 아니라 사전이라,
        /// 폼을 얻는 순간이 곧 그 항목이 열리는 순간이어야 한다.
        ///
        /// formId 문자열의 SoT는 <c>ContentBuilder</c>가 만드는 FormData 에셋이다 — 오타가 나면
        /// 그 폼의 내력이 <b>영원히 안 열리는데 오류는 안 난다</b>. 값을 고칠 때는 둘을 함께 볼 것.
        /// </summary>
        private static StoryData LoadOrCreateEngraverStoryData()
        {
            if (!Directory.Exists(AbyssPaths.Story)) Directory.CreateDirectory(AbyssPaths.Story);

            string path = AbyssPaths.Story + "/Engraver.asset";
            var data = AssetDatabase.LoadAssetAtPath<StoryData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<StoryData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.chapters = new[]
            {
                EngraverChapter(1, "dark_blade",
                    StringKey.Story_Engraver_DarkBlade_Line1,
                    StringKey.Story_Engraver_DarkBlade_Line2,
                    StringKey.Story_Engraver_DarkBlade_Line3),
                EngraverChapter(2, "void_archer",
                    StringKey.Story_Engraver_VoidArcher_Line1,
                    StringKey.Story_Engraver_VoidArcher_Line2,
                    StringKey.Story_Engraver_VoidArcher_Line3),
                EngraverChapter(3, "ancient_shield",
                    StringKey.Story_Engraver_AncientShield_Line1,
                    StringKey.Story_Engraver_AncientShield_Line2,
                    StringKey.Story_Engraver_AncientShield_Line3),
                EngraverChapter(4, "void_thrower",
                    StringKey.Story_Engraver_VoidThrower_Line1,
                    StringKey.Story_Engraver_VoidThrower_Line2,
                    StringKey.Story_Engraver_VoidThrower_Line3),

                // 닫는 말 — 폼 조건이 없는 대신 "넷을 다 본 뒤"가 조건이다.
                // 아크 씨앗("자기 것은 못 새겼다")이 여기 실려 있고, 챕터라서 딱 한 번만 나온다.
                // 🔴 단계 99: 폼이 늘면 5·6·7…이 필요하다. 이 줄은 언제나 마지막이어야 한다.
                // ⚠️ 폼을 추가하면 minViewedChapters도 함께 올릴 것 — 안 올리면 새 폼의 내력보다
                //    닫는 말이 먼저 나온다(닫는 말인데 안 닫힌다).
                ClosingChapter(99, minViewedChapters: 4,
                    StringKey.Story_Engraver_Closing_Line1,
                    StringKey.Story_Engraver_Closing_Line2),
            };

            // 각인사에게 idle 대사를 두지 않는다 — ServiceNpc는 들려줄 챕터가 없으면 곧바로 패널을 연다.
            // 폼을 바꾸러 들를 때마다 같은 말을 듣게 되기 때문이다(11-engraver-lines §3-3).
            // 넣어 두면 §6-4의 unlockedFormIds처럼 "있는데 안 불리는 데이터"가 또 하나 생긴다.
            data.idleLines = System.Array.Empty<DialogueLine>();

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        /// <summary>
        /// 폼 조건 없이 <b>"이 화자의 것을 이만큼 본 뒤"</b>로만 열리는 챕터.
        /// 사전 모델(항목마다 독립)에는 순서가 없어서 "다 본 뒤"를 표현할 방법이 이것뿐이다.
        /// </summary>
        private static StoryChapter ClosingChapter(int chapterStage, int minViewedChapters, params string[] textKeys)
        {
            var lines = new DialogueLine[textKeys.Length];
            for (int i = 0; i < textKeys.Length; i++) lines[i] = EngraverLine(textKeys[i]);

            return new StoryChapter
            {
                chapterStage = chapterStage,
                minRunCount = 0,
                minBossKills = 0,
                requiredFormId = string.Empty,
                minViewedChapters = minViewedChapters,
                lines = lines,
            };
        }

        private static StoryChapter EngraverChapter(int chapterStage, string formId, params string[] textKeys)
        {
            var lines = new DialogueLine[textKeys.Length];
            for (int i = 0; i < textKeys.Length; i++) lines[i] = EngraverLine(textKeys[i]);

            return new StoryChapter
            {
                chapterStage = chapterStage,
                minRunCount = 0,
                minBossKills = 0,
                requiredFormId = formId,
                lines = lines,
            };
        }

        private static DialogueLine Line(string textKey)
            => new DialogueLine { speakerKey = StringKey.Npc_Chronicler_Name, textKey = textKey };

        private static DialogueLine EngraverLine(string textKey)
            => new DialogueLine { speakerKey = StringKey.Npc_Engraver_Name, textKey = textKey };
    }
}
#endif

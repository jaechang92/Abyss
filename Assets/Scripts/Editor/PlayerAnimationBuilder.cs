#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abyss.Runtime.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 슬라이스된 시트에서 <b>클립·컨트롤러·폼 오버라이드를 굽는다.</b> 손으로 만들지 않는다 —
    /// 상태 9종 × 폼 4종 = 36개를 에디터에서 일일이 만드는 것은 사람이 할 일이 아니고,
    /// 무엇보다 <b>이름 규약이 사람 손을 거치면 반드시 어긋난다.</b>
    ///
    /// 🔑 <b>규약 셋이 같아야 한다</b> — Animator 상태 이름 = 클립 이름 = <see cref="PlayerAnimationIds"/> 상수.
    /// 오버라이드는 <b>base 클립의 이름을 키로</b> 쓰기 때문에, 하나만 어긋나도
    /// 폼 교체가 조용히 실패한다(오류 없이 그림만 안 바뀐다). 그 셋을 여기서 한꺼번에 정한다.
    ///
    /// 🔴 <b>base 컨트롤러에는 9상태를 전부 깐다.</b> 아직 안 그린 상태에도 빈 자리표시자 클립을 둔다 —
    /// base 에 없는 이름은 <b>어떤 폼도 나중에 채울 수 없기</b> 때문이다.
    /// 폼은 자기가 그린 것만 덮어쓰고, 안 덮은 자리는 비어 있다.
    /// 그 "비어 있음"을 <c>AnimatorDriver.HasClip</c> 이 읽어 폴백 사슬을 돌린다.
    ///
    /// 📌 <b>자동 후처리기로 만들지 않았다.</b> 시트를 넣으면 알아서 도는 방식이 원래 참고한 공정이지만,
    /// 이 저장소에서는 에디터에서 사람이 직접 한 슬라이스를 도구가 덮어쓸 뻔한 적이 있다.
    /// 누른 적 없는 일이 일어나지 않도록 <b>메뉴로만</b> 돈다.
    /// </summary>
    internal static class PlayerAnimationBuilder
    {
        private const string AnimationRoot = "Assets/Art/Animation";
        private const string ClipFolder = AnimationRoot + "/Clips";
        private const string FormFolder = AnimationRoot + "/Forms";
        private const string BaseControllerPath = AnimationRoot + "/PlayerBase.controller";

        /// <summary>
        /// 🔴 <b>클립은 <see cref="SpriteRenderer"/>가 <see cref="Animator"/>와 같은 오브젝트에 있다고 보고 구워진다</b>
        /// (커브 경로가 비어 있다). 둘을 다른 오브젝트에 나누면 클립이 아무것도 못 찾는다 —
        /// 배선할 때 폼 몸을 담는 자식(Visual)에 <b>둘을 같이</b> 붙일 것.
        /// </summary>
        private const string SpriteCurvePath = "";

        private const string SpritePropertyName = "m_Sprite";

        /// <summary>
        /// 한 폼이 어느 시트에서 어느 상태를 얻는가. 폼이 늘면 여기에 줄을 더한다.
        ///
        /// 🔑 <b>속도를 정하는 방식이 클립의 성질에 따라 다르다.</b>
        /// 루프 클립(서기·달리기)은 몇 fps가 자연스러운지가 정하고,
        /// 한 번 재생하고 끝나는 클립(공격·피격)은 <b>FSM 상태가 얼마나 지속되는지</b>가 정한다.
        /// 후자는 길이가 곧 계약이라, fps를 손으로 적으면 지속시간을 바꿀 때 조용히 어긋난다.
        /// </summary>
        private readonly struct ClipSource
        {
            public readonly string AnimationId;
            public readonly string SheetPath;

            /// <summary>루프 클립용. 0이면 <see cref="DurationSeconds"/>로 역산한다.</summary>
            public readonly float FrameRate;

            /// <summary>한 번 재생하고 끝나는 클립용. 프레임 수를 이 값으로 나눠 fps를 얻는다.</summary>
            public readonly float DurationSeconds;

            private ClipSource(string animationId, string sheetPath, float frameRate, float durationSeconds)
            {
                AnimationId = animationId;
                SheetPath = sheetPath;
                FrameRate = frameRate;
                DurationSeconds = durationSeconds;
            }

            /// <summary>계속 도는 클립 — 속도는 눈으로 보고 정한다.</summary>
            public static ClipSource Looping(string animationId, string sheetPath, float frameRate)
                => new ClipSource(animationId, sheetPath, frameRate, 0f);

            /// <summary>한 번 돌고 멈추는 클립 — 길이를 FSM 상태 지속시간에 맞춘다.</summary>
            public static ClipSource OneShot(string animationId, string sheetPath, float durationSeconds)
                => new ClipSource(animationId, sheetPath, 0f, durationSeconds);
        }

        /// <summary>
        /// 폼 이름 → 그 폼이 가진 클립 원본.
        ///
        /// 📌 <b>파일명과 클립 이름이 다른 것은 의도다</b> — 소스는 <c>walk</c>지만 FSM 상태는 <c>Run</c>이고,
        /// 오버라이드 키는 <b>상태 이름</b>을 따라야 한다.
        /// </summary>
        private static readonly Dictionary<string, ClipSource[]> FormSources = new Dictionary<string, ClipSource[]>
        {
            ["KnightRed"] = new[]
            {
                // 숨쉬기는 느리게, 달리기는 빠르게. 눈으로 보고 조정할 출발점이다.
                ClipSource.Looping(PlayerAnimationIds.Idle, CharacterSheet("knight_red", "idle"), 10f),
                ClipSource.Looping(PlayerAnimationIds.Run, CharacterSheet("knight_red", "walk"), 12f),

                // 🔴 fps 를 안 적는다. 7프레임을 0.25초에, 9프레임을 0.6초에 — 즉 28fps 와 15fps 인데,
                //    그 숫자는 FSM 지속시간에서 따라 나온 결과이지 고른 값이 아니다.
                //    적어 두면 지속시간을 바꿀 때 여기가 안 따라와 클립이 잘리거나 남는다.
                ClipSource.OneShot(PlayerAnimationIds.AttackLight, CharacterSheet("knight_red", "attacklight"),
                    PlayerStateMachine.DefaultAttackLightDuration),
                ClipSource.OneShot(PlayerAnimationIds.AttackHeavy, CharacterSheet("knight_red", "attackheavy"),
                    PlayerStateMachine.DefaultAttackHeavyDuration),
            },
        };

        /// <summary>
        /// 시트 경로 규약. 파일명은 <c>{폼}_{상태}_east.png</c> — 방향은 east 한 벌만 그리고
        /// 좌향은 런타임 flip 으로 얻는다(비용 절반).
        /// </summary>
        private static string CharacterSheet(string filePrefix, string sheetState)
            => $"Assets/Art/Sprites/Characters/{filePrefix}_{sheetState}_east.png";

        /// <summary>base 컨트롤러가 갖출 상태. 순서가 곧 Animator 의 기본 상태 순서이며 첫 항목이 기본값이 된다.</summary>
        private static readonly string[] AllAnimationIds =
        {
            PlayerAnimationIds.Idle,
            PlayerAnimationIds.Run,
            PlayerAnimationIds.Jump,
            PlayerAnimationIds.Fall,
            PlayerAnimationIds.Dash,
            PlayerAnimationIds.AttackLight,
            PlayerAnimationIds.AttackHeavy,
            PlayerAnimationIds.Hit,
            PlayerAnimationIds.Dead,
        };

        [MenuItem(AbyssMenu.GeneratePlayerAnimation)]
        public static void BuildPlayerAnimation()
        {
            EnsureFolders();

            AnimatorController baseController = BuildBaseController();
            if (baseController == null) return;

            int builtClips = 0;
            int builtForms = 0;

            foreach (var entry in FormSources)
            {
                var formClips = BuildFormClips(entry.Key, entry.Value, ref builtClips);
                if (formClips.Count == 0) continue;

                BuildOverrideController(entry.Key, baseController, formClips);
                builtForms++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PlayerAnimationBuilder] 완료 — 상태 {AllAnimationIds.Length}개 · " +
                      $"클립 {builtClips}개 · 폼 {builtForms}벌. 컨트롤러: {BaseControllerPath}");
        }

        private static void EnsureFolders()
        {
            foreach (string folder in new[] { AnimationRoot, ClipFolder, FormFolder })
            {
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            }
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 전이가 하나도 없는 평평한 컨트롤러. <b>전이를 그리지 않는 것이 이 컨트롤러의 요점이다</b> —
        /// 무엇으로 갈지는 게임 FSM 이 정하고, 여기는 이름으로 지목된 클립을 틀기만 한다.
        /// </summary>
        private static AnimatorController BuildBaseController()
        {
            // 이미 있으면 다시 만든다. 상태 목록이 바뀌었을 때 낡은 상태가 남으면
            // HasClip 이 있지도 않은 그림을 있다고 답한다.
            AssetDatabase.DeleteAsset(BaseControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(BaseControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[PlayerAnimationBuilder] 컨트롤러를 만들지 못했다: {BaseControllerPath}");
                return null;
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            foreach (string animationId in AllAnimationIds)
            {
                AnimationClip placeholder = CreatePlaceholderClip(animationId);
                // 자리표시자를 컨트롤러 안에 넣어 둔다 — 파일로 흩어 두면 지워질 때 오버라이드 키가 통째로 사라진다.
                AssetDatabase.AddObjectToAsset(placeholder, controller);

                AnimatorState state = stateMachine.AddState(animationId);
                state.motion = placeholder;
                state.writeDefaultValues = false;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// 빈 클립. <b>이름이 전부다</b> — 오버라이드는 이 이름을 키로 쓰고,
        /// 폼이 덮어쓰지 않으면 이대로 남아 "그 폼은 이 상태를 아직 안 그렸다"를 뜻한다.
        /// </summary>
        private static AnimationClip CreatePlaceholderClip(string animationId)
        {
            return new AnimationClip { name = animationId };
        }

        private static Dictionary<string, AnimationClip> BuildFormClips(
            string formName, ClipSource[] sources, ref int builtClips)
        {
            var clips = new Dictionary<string, AnimationClip>();

            foreach (ClipSource source in sources)
            {
                Sprite[] sprites = LoadSlicedSprites(source.SheetPath);
                if (sprites.Length == 0)
                {
                    Debug.LogWarning($"[PlayerAnimationBuilder] 슬라이스된 스프라이트가 없다 — {source.SheetPath} " +
                                     "(Sprite Mode 가 Multiple 인지, 슬라이스가 Apply 되었는지 확인)");
                    continue;
                }

                float frameRate = ResolveFrameRate(source, sprites.Length);
                if (frameRate <= 0f)
                {
                    Debug.LogWarning($"[PlayerAnimationBuilder] 재생 속도를 못 정했다 — {formName}/{source.AnimationId}");
                    continue;
                }

                string clipPath = $"{ClipFolder}/{formName}_{source.AnimationId}.anim";
                AnimationClip clip = CreateSpriteClip(sprites, frameRate, source.AnimationId);

                AssetDatabase.DeleteAsset(clipPath);
                AssetDatabase.CreateAsset(clip, clipPath);

                clips[source.AnimationId] = clip;
                builtClips++;
            }

            return clips;
        }

        /// <summary>
        /// 재생 속도를 정한다. <b>한 번 재생하고 끝나는 클립은 fps 가 결과지 입력이 아니다</b> —
        /// FSM 상태가 그 길이만큼만 지속되므로, 프레임 수를 지속시간으로 나눈 값이 유일한 답이다.
        /// 프레임 수가 폼마다 다르기 때문에(수평 베기 7장 · 수직 베기 9장) 폼마다 다시 계산된다.
        /// </summary>
        private static float ResolveFrameRate(ClipSource source, int frameCount)
        {
            if (source.DurationSeconds > 0f) return frameCount / source.DurationSeconds;
            return source.FrameRate;
        }

        /// <summary>
        /// 프레임을 <see cref="SpriteRenderer"/>의 스프라이트 커브로 굽는다.
        ///
        /// 🔑 루프 여부는 <see cref="PlayerAnimationIds.IsOneShot"/>이 정한다 — 생성기와 재생기가
        /// <b>같은 답을 써야</b> 하기 때문이다. 공격이 루프로 구워지면 재생기가 아무리 한 번만 틀려 해도 계속 돈다.
        /// </summary>
        private static AnimationClip CreateSpriteClip(Sprite[] sprites, float frameRate, string animationId)
        {
            var clip = new AnimationClip { name = animationId, frameRate = frameRate };

            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = SpriteCurvePath,
                propertyName = SpritePropertyName,
            };

            var keyframes = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i / frameRate,
                    value = sprites[i],
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = !PlayerAnimationIds.IsOneShot(animationId);
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            return clip;
        }

        /// <summary>
        /// 시트에서 잘린 스프라이트를 <b>번호 순서대로</b> 읽는다.
        ///
        /// 🔴 이름 정렬로는 안 된다 — <c>_10</c>이 <c>_2</c> 앞에 온다. 지금은 9칸이라 안 드러나지만,
        /// 프레임이 열 장을 넘는 순간 <b>순서가 섞인 채로 조용히 구워진다.</b>
        /// </summary>
        private static Sprite[] LoadSlicedSprites(string sheetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(sheetPath)
                .OfType<Sprite>()
                .OrderBy(FrameIndexOf)
                .ToArray();
        }

        private static int FrameIndexOf(Sprite sprite)
        {
            int underscore = sprite.name.LastIndexOf('_');
            if (underscore >= 0 && int.TryParse(sprite.name[(underscore + 1)..], out int index)) return index;

            Debug.LogWarning($"[PlayerAnimationBuilder] 프레임 번호를 못 읽었다: {sprite.name} — 이름 뒤에 _0, _1 이 붙어야 한다");
            return int.MaxValue;
        }

        /// <summary>
        /// 폼 한 벌. <b>그린 것만 덮어쓰고 나머지는 비운다</b> — 그 빈자리가 폴백 사슬의 근거다.
        /// </summary>
        private static void BuildOverrideController(
            string formName, AnimatorController baseController, Dictionary<string, AnimationClip> formClips)
        {
            string path = $"{FormFolder}/{formName}.overrideController";
            AssetDatabase.DeleteAsset(path);

            var overrideController = new AnimatorOverrideController(baseController) { name = formName };

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
            overrideController.GetOverrides(overrides);

            for (int i = 0; i < overrides.Count; i++)
            {
                AnimationClip placeholder = overrides[i].Key;
                formClips.TryGetValue(placeholder.name, out AnimationClip replacement);
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(placeholder, replacement);
            }

            overrideController.ApplyOverrides(overrides);
            AssetDatabase.CreateAsset(overrideController, path);

            int filled = formClips.Count;
            Debug.Log($"[PlayerAnimationBuilder] {formName} — {filled}/{AllAnimationIds.Length} 상태. " +
                      $"나머지는 폴백 사슬이 처리한다.");
        }
    }
}
#endif

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 슬라이스된 시트 → 스프라이트 클립 · 전이 없는 컨트롤러 · 오버라이드를 굽는 <b>공용 부품</b>.
    ///
    /// 📌 2026-09-18 <c>PlayerAnimationBuilder</c> 에서 뽑았다 — 적 애니메이션(<c>EnemyAnimationBuilder</c>)이
    /// 같은 규약(ADR-012: 상태 이름 = 클립 이름 = 오버라이드 키)을 쓰므로 두 벌로 두면 한쪽만 고쳐진다.
    /// <b>무엇을 굽는지</b>(상태 목록 · fps · 루프 여부)는 각 빌더가 정하고, 여기는 <b>어떻게 굽는지</b>만 안다.
    /// </summary>
    internal static class SpriteClipAssets
    {
        /// <summary>
        /// 🔴 <b>클립은 <see cref="SpriteRenderer"/>가 <see cref="Animator"/>와 같은 오브젝트에 있다고 보고 구워진다</b>
        /// (커브 경로가 비어 있다). 둘을 다른 오브젝트에 나누면 클립이 아무것도 못 찾는다.
        /// </summary>
        private const string SpriteCurvePath = "";

        private const string SpritePropertyName = "m_Sprite";

        /// <summary>
        /// 전이가 하나도 없는 평평한 컨트롤러. <b>전이를 그리지 않는 것이 요점이다</b> —
        /// 무엇으로 갈지는 게임 FSM 이 정하고, 여기는 이름으로 지목된 클립을 틀기만 한다.
        /// 🔴 상태마다 자리표시자 클립을 <b>컨트롤러 안에</b> 넣는다 — base 에 없는 이름은 어떤 오버라이드도 채울 수 없다.
        /// </summary>
        public static AnimatorController CreateFlatController(string path, IReadOnlyList<string> stateIds)
        {
            // 이미 있으면 다시 만든다. 상태 목록이 바뀌었을 때 낡은 상태가 남으면
            // HasClip 이 있지도 않은 그림을 있다고 답한다.
            AssetDatabase.DeleteAsset(path);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            if (controller == null)
            {
                Debug.LogError($"[SpriteClipAssets] 컨트롤러를 만들지 못했다: {path}");
                return null;
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            foreach (string stateId in stateIds)
            {
                // 빈 클립. 이름이 전부다 — 오버라이드가 덮지 않으면 "아직 안 그렸다"를 뜻한다.
                var placeholder = new AnimationClip { name = stateId };
                AssetDatabase.AddObjectToAsset(placeholder, controller);

                AnimatorState state = stateMachine.AddState(stateId);
                state.motion = placeholder;
                state.writeDefaultValues = false;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>프레임을 <see cref="SpriteRenderer"/> 스프라이트 커브로 굽는다.</summary>
        /// <param name="loop">🔑 재생기가 쓰는 <c>IsOneShot</c> 과 같은 답이어야 한다 — 공격이 루프로 구워지면 계속 돈다.</param>
        public static AnimationClip CreateSpriteClip(Sprite[] sprites, float frameRate, string clipName, bool loop)
        {
            var clip = new AnimationClip { name = clipName, frameRate = frameRate };

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
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            return clip;
        }

        /// <summary>
        /// 클립을 파일로 저장한다(있으면 지우고 다시). 반환값은 저장된 클립.
        /// </summary>
        public static AnimationClip SaveClip(AnimationClip clip, string clipPath)
        {
            AssetDatabase.DeleteAsset(clipPath);
            AssetDatabase.CreateAsset(clip, clipPath);
            return clip;
        }

        /// <summary>
        /// 시트에서 잘린 스프라이트를 <b>번호 순서대로</b> 읽는다.
        /// 🔴 이름 정렬로는 안 된다 — <c>_10</c>이 <c>_2</c> 앞에 온다.
        /// </summary>
        public static Sprite[] LoadSlicedSprites(string sheetPath)
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

            Debug.LogWarning($"[SpriteClipAssets] 프레임 번호를 못 읽었다: {sprite.name} — 이름 뒤에 _0, _1 이 붙어야 한다");
            return int.MaxValue;
        }

        /// <summary>
        /// 오버라이드 한 벌. <b>그린 것만 덮어쓰고 나머지는 비운다</b> — 그 빈자리가 폴백 사슬의 근거다.
        /// </summary>
        public static AnimatorOverrideController CreateOverrideController(
            string path, string name, AnimatorController baseController, Dictionary<string, AnimationClip> clips)
        {
            AssetDatabase.DeleteAsset(path);

            var overrideController = new AnimatorOverrideController(baseController) { name = name };

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
            overrideController.GetOverrides(overrides);

            for (int i = 0; i < overrides.Count; i++)
            {
                AnimationClip placeholder = overrides[i].Key;
                clips.TryGetValue(placeholder.name, out AnimationClip replacement);
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(placeholder, replacement);
            }

            overrideController.ApplyOverrides(overrides);
            AssetDatabase.CreateAsset(overrideController, path);
            return overrideController;
        }
    }
}
#endif

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

        // 📌 굽는 방법(커브 경로 · 자리표시자 · 오버라이드)은 SpriteClipAssets 에 있다(2026-09-18 적 빌더와 공유).
        //    🔴 클립은 SpriteRenderer 가 Animator 와 같은 오브젝트에 있다고 보고 구워진다 — 폼 몸을 담는 자식(Visual)에 둘을 같이.

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
            ["KnightRed"] = StandardClips("knight_red"),

            // 2026-09-17 폼 3벌(SE). 시트는 build_form_sheets.py 가 레시피대로 조립한다 —
            // 상태 9개 · 파일명 규약이 knight_red 와 같으므로 같은 규칙을 그대로 쓴다.
            // 🔑 한 번 재생 클립은 프레임 수가 달라도(Hit 5장 · 9장) fps 가 지속시간에서 다시 계산된다.
            ["AncientShield"] = StandardClips("ancient_shield"),
            ["VoidArcher"] = StandardClips("void_archer"),
            ["VoidThrower"] = StandardClips("void_thrower"),
        };

        /// <summary>
        /// 오버라이드 컨트롤러 이름 → 연결할 <c>FormData.formId</c>.
        ///
        /// 🔑 <b>이름이 다른 것은 역사다</b> — 붉은 기사 한 벌이 먼저 있었고 그것이 암흑 검사 폼이 됐다.
        /// 컨트롤러를 만들고 연결을 손에 남기면 <b>만들어 놓고 안 쓰는</b> 상태가 오류 없이 남는다(2026-09-10 에 실제로
        /// 디스크에 연결이 안 보여 헷갈렸다). 그래서 굽는 자리에서 같이 잇는다.
        /// </summary>
        private static readonly Dictionary<string, string> FormIdsByController = new Dictionary<string, string>
        {
            ["KnightRed"] = "dark_blade",
            ["AncientShield"] = "ancient_shield",
            ["VoidArcher"] = "void_archer",
            ["VoidThrower"] = "void_thrower",
        };

        /// <summary>
        /// 9상태 표준 한 벌. 📌 <b>파일명과 클립 이름이 다른 것은 의도다</b> — 소스는 <c>walk</c>지만 FSM 상태는 <c>Run</c>.
        /// </summary>
        private static ClipSource[] StandardClips(string filePrefix) => new[]
        {
            // 숨쉬기는 느리게, 달리기는 빠르게. 눈으로 보고 조정할 출발점이다.
            ClipSource.Looping(PlayerAnimationIds.Idle, CharacterSheet(filePrefix, "idle"), 10f),
            ClipSource.Looping(PlayerAnimationIds.Run, CharacterSheet(filePrefix, "walk"), 12f),

            // 🔴 fps 를 안 적는다. knight_red 는 9프레임을 0.25초·0.6초에 — 즉 36fps 와 15fps 인데,
            //    그 숫자는 FSM 지속시간에서 따라 나온 결과이지 고른 값이 아니다.
            //    적어 두면 지속시간을 바꿀 때 여기가 안 따라와 클립이 잘리거나 남는다.
            ClipSource.OneShot(PlayerAnimationIds.AttackLight, CharacterSheet(filePrefix, "attacklight"),
                PlayerStateMachine.DefaultAttackLightDuration),
            ClipSource.OneShot(PlayerAnimationIds.AttackHeavy, CharacterSheet(filePrefix, "attackheavy"),
                PlayerStateMachine.DefaultAttackHeavyDuration),

            // 뒤로 젖혀졌다 선 자세로 돌아온다(2026-09-13 두 후보 플레이 비교 후 채택).
            // 2026-09-14: 9상태 전부 No Weapon 몸으로 통일됐다. 무기는 손 앵커로 얹는다.
            ClipSource.OneShot(PlayerAnimationIds.Hit, CharacterSheet(filePrefix, "hit"),
                PlayerStateMachine.DefaultHitDuration),

            // 🔑 공중·대시는 연속 동작이 아니라 「자세 하나 + 망토 루프」다(Hollow Knight·Dead Cells·Celeste 방식).
            //    동작의 중간을 잘라 돌리면 끝에서 튀어 어색했다. 상승은 약 0.4초라 4장이 한 바퀴쯤 돈다.
            //    공중 프레임도 발밑을 지상과 같은 줄(y=77)에 둔다 — 그림까지 떠 있으면 착지해 Idle 로 바뀌는 순간 몸이 툭 떨어진다.
            ClipSource.Looping(PlayerAnimationIds.Jump, CharacterSheet(filePrefix, "jump"), 10f),
            ClipSource.Looping(PlayerAnimationIds.Fall, CharacterSheet(filePrefix, "fall"), 10f),

            // 대시는 0.15초뿐이라 5장이 한 번 지나가게 빠르게 돈다. 속도감은 나중에 잔상 이펙트가 맡는다.
            ClipSource.Looping(PlayerAnimationIds.Dash, CharacterSheet(filePrefix, "dash"), 30f),

            // 사망은 스스로 나가지 않는 종착 상태라 FSM 지속시간이 없다 — 쓰러진 뒤 마지막 프레임에 멈춘다.
            ClipSource.OneShot(PlayerAnimationIds.Dead, CharacterSheet(filePrefix, "dead"), DeadClipDuration),
        };

        /// <summary>
        /// 사망 클립 길이. 다른 한 번 재생 클립과 달리 <b>FSM 계약이 아니다</b> — 사망은 나가지 않는 상태라
        /// 이 값은 쓰러지는 연출 속도만 정한다(9프레임 → 약 11fps).
        /// </summary>
        private const float DeadClipDuration = 0.8f;

        /// <summary>
        /// 시트 방향. <b>한 벌만 그리고 좌향은 좌우 반전으로 얻는다</b>(비용 절반).
        ///
        /// 🔴 <b>2026-09-15 에 <c>east</c> → <c>southeast</c> 로 바꿨다.</b>
        /// 순측면(east)은 <b>먼 팔이 몸에 가려</b> 실루엣에 안 나온다. 그래서 손 앵커 검출이
        /// 「가장 앞으로 나온 점」밖에 못 주고, 그 점이 <b>어느 손인지 모른다</b> —
        /// 자세마다 몸통 앞면이기도 하고 주먹이기도 해서 클립당 보정값 하나로는 원리적으로 못 맞춘다.
        /// 3/4 시점(south-east)은 <b>두 팔이 다 보인다.</b>
        /// 규약은 <c>Art_Source/characters/knight_red/animation_prompts.md</c> 에 있다.
        /// </summary>
        private const string SheetDirection = "southeast";

        /// <summary>
        /// 시트 경로 규약 — <c>Characters/{폼}/{방향}/{폼}_{상태}_{방향}.png</c>.
        /// 📌 <b>폴더로 나눠도 파일명은 그대로 둔다</b>(2026-09-17). 스프라이트 이름이 파일명에서 나오고, 이름이 바뀌면
        /// 슬라이서가 새 ID 를 발급해 클립 참조가 바뀐다 — 폴더 이동은 <c>.meta</c> 를 같이 옮기면 GUID 가 유지된다.
        /// </summary>
        private static string CharacterSheet(string filePrefix, string sheetState)
            => $"Assets/Art/Sprites/Characters/{filePrefix}/{SheetDirection}/{filePrefix}_{sheetState}_{SheetDirection}.png";

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

        private static AnimatorController BuildBaseController()
            => SpriteClipAssets.CreateFlatController(BaseControllerPath, AllAnimationIds);

        private static Dictionary<string, AnimationClip> BuildFormClips(
            string formName, ClipSource[] sources, ref int builtClips)
        {
            var clips = new Dictionary<string, AnimationClip>();

            foreach (ClipSource source in sources)
            {
                Sprite[] sprites = SpriteClipAssets.LoadSlicedSprites(source.SheetPath);
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
                AnimationClip clip = SpriteClipAssets.CreateSpriteClip(sprites, frameRate, source.AnimationId,
                    loop: !PlayerAnimationIds.IsOneShot(source.AnimationId));

                SpriteClipAssets.SaveClip(clip, clipPath);

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
        /// 폼 한 벌. <b>그린 것만 덮어쓰고 나머지는 비운다</b> — 그 빈자리가 폴백 사슬의 근거다.
        /// </summary>
        private static void BuildOverrideController(
            string formName, AnimatorController baseController, Dictionary<string, AnimationClip> formClips)
        {
            string path = $"{FormFolder}/{formName}.overrideController";
            AnimatorOverrideController overrideController =
                SpriteClipAssets.CreateOverrideController(path, formName, baseController, formClips);

            int filled = formClips.Count;
            Debug.Log($"[PlayerAnimationBuilder] {formName} — {filled}/{AllAnimationIds.Length} 상태 · " +
                      $"{WireFormData(formName, overrideController)}. 나머지는 폴백 사슬이 처리한다.");
        }

        /// <summary>
        /// 만든 오버라이드를 폼에 연결한다. 🔴 <b>지웠다 다시 만들었으므로</b> 기존 연결은 이미 끊겨 있다 —
        /// 여기서 다시 잇지 않으면 폼이 조용히 정지 그림(<c>bodySprite</c>)으로 물러난다.
        /// </summary>
        private static string WireFormData(string formName, AnimatorOverrideController controller)
        {
            if (!FormIdsByController.TryGetValue(formName, out string formId)) return "연결할 폼 미등록";

            foreach (string guid in AssetDatabase.FindAssets("t:FormData"))
            {
                var form = AssetDatabase.LoadAssetAtPath<Runtime.Form.FormData>(AssetDatabase.GUIDToAssetPath(guid));
                if (form == null || form.formId != formId) continue;

                form.animatorController = controller;
                EditorUtility.SetDirty(form);
                return $"{form.name}.animatorController 에 연결";
            }
            return $"formId '{formId}' 인 FormData 없음 — 연결 안 함";
        }
    }
}
#endif

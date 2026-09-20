#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abyss.Runtime.Enemy;
using Anim.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 적 시트 → <b>클립 · 적 공통 컨트롤러 · 적별 오버라이드</b>를 굽고, 스폰 프리팹에 <b>그림 자식(Visual)</b>을 배선한다.
    ///
    /// 🔑 <b>플레이어와 같은 재생층(ADR-012)</b>이다 — 전이 없는 컨트롤러 + 이름으로 틀기 + 비어 있으면 폴백.
    /// 굽는 방법은 <see cref="SpriteClipAssets"/> 를 공유하고, 여기는 <b>적에게 무엇을 굽는지</b>만 정한다.
    ///
    /// 🔴 <b>한 번 재생 클립의 길이는 <see cref="EnemyData"/> 의 동작 시간에서 나온다.</b>
    /// 공격 = 예비동작 + 회복, 피격 = 경직, 사망 = 사라지기까지 − 여운. fps 를 손으로 적으면
    /// 시간을 바꿀 때 클립이 잘리거나 남는다(플레이어 빌더와 같은 규칙). 시간을 바꿨으면 <b>이 메뉴를 다시 실행</b>한다.
    ///
    /// 📌 <b>원점은 그대로 둔다.</b> 루트는 콜라이더 중심이고, 그림만 발밑(콜라이더 바닥)으로 내린 자식에 둔다 —
    /// 원점을 바꾸면 발사 위치(+0.7)·거리 판정 등 읽는 곳이 조용히 어긋난다(메모리 unity_origin_convention_change).
    /// </summary>
    internal static class EnemyAnimationBuilder
    {
        private const string Tag = "[EnemyAnimationBuilder]";

        private const string AnimationRoot = "Assets/Art/Animation/Enemies";
        private const string ClipFolder = AnimationRoot + "/Clips";
        private const string BaseControllerPath = AnimationRoot + "/EnemyBase.controller";

        private const string SheetDirection = "southeast";
        private const string VisualName = "Visual";
        private const string NametagName = "Nametag";

        /// <summary>기어 가는 루프. 눈으로 보고 정할 출발점이다.</summary>
        private const float MoveFrameRate = 10f;

        /// <summary>사망 클립이 끝난 뒤 마지막 그림(더미)이 남아 있는 시간. 클립 길이 = deathLingerDuration − 이 값.</summary>
        private const float DeadHoldSeconds = 0.2f;

        /// <summary>이름표를 그림 머리 위로 띄우는 여백(유닛).</summary>
        private const float NametagMargin = 0.25f;

        /// <summary>
        /// 애니메이션이 있는 적 한 종. 적이 늘면 줄을 더한다.
        /// </summary>
        private readonly struct Entry
        {
            public readonly string EnemyId;

            /// <summary>시트 파일 접두어 — <c>Enemies/{접두어}/{방향}/{접두어}_{상태}_{방향}.png</c>.</summary>
            public readonly string FilePrefix;

            /// <summary>
            /// 몸 콜라이더(유닛). 🔴 그림 크기가 아니라 <b>맞는 곳</b>이다 — 근접 병사 그림은 73x61px(2.3x1.9)이지만
            /// 칼끝·투구 위 여백까지 판정에 넣으면 닿지도 않은 공격이 맞는다. 플레이로 조정한다.
            /// </summary>
            public readonly Vector2 BodySize;

            public Entry(string enemyId, string filePrefix, Vector2 bodySize)
            {
                EnemyId = enemyId;
                FilePrefix = filePrefix;
                BodySize = bodySize;
            }
        }

        private static readonly Entry[] Entries =
        {
            // 2026-09-18 근접 병사(빈 갑옷 R3 · 색 B 건메탈). 시트: Art_Source/characters/melee_grunt/sheet_recipe.json
            new Entry("melee_grunt", "melee_grunt", new Vector2(1.8f, 1.5f)),

            // 2026-09-18 중장 강적(녹으로 붙은 갑옷 둘 R2). 그림 폭 78px 은 창 포함 — 몸만 약 43px(1.3 유닛) · 키 58px(1.8).
            // 창은 무기라 판정에서 뺀다. 시트: Art_Source/characters/melee_brute/sheet_recipe.json
            new Entry("melee_brute", "melee_brute", new Vector2(1.5f, 1.6f)),

            // 2026-09-18 원거리 사수(하늘을 겨눈 채 굳은 것 R2). 🔴 <b>애니메이션이 붙는 첫 원거리 적</b>.
            // 그림 높이 83px 중 위쪽 절반은 머리 위로 뻗은 활이다 — 창과 같이 판정에서 뺀다.
            // 몸(무릎 꿇은 몸통 + 머리)만 약 42x52px = 1.3x1.6 유닛.
            // 시트: Art_Source/characters/ranged_archer/sheet_recipe.json
            new Entry("ranged_archer", "ranged_archer", new Vector2(1.3f, 1.6f)),

            // 2026-09-19 뼈 궁수(재로 된 사수 R2). 그림 74x64px 중 가로는 몸 앞으로 뻗은 활이,
            // 세로 아래 약 12px 은 바닥에 넓게 퍼진 재 더미가 차지한다 — 둘 다 판정에서 뺀다.
            // 몸(머리 그루터기 + 상체 + 어깨 크러스트)만 약 42x61px = 1.3x1.9 유닛.
            // 🔴 재 더미를 판정에 넣으면 사거리 7.5 의 원거리 적이 근접에서 지나치게 잘 맞는다.
            // 시트: Art_Source/characters/bone_archer/sheet_recipe.json
            new Entry("bone_archer", "bone_archer", new Vector2(1.3f, 1.9f)),

            // 2026-09-20 공허 술사(선이 하나도 없는 것 R1 · 색은 desaturate 후보 C). 그림 50x60px 인데
            // 가로 x56~76(21px)은 앞으로 뻗어 가리키는 팔이다 — 창·활과 같이 판정에서 뺀다. 몸만 약 29x60px.
            // 🔴 <b>일반 적 중 유일하게 바닥에 안 닿는다</b> — 레시피 footY 84 로 그림이 피벗보다 8px(0.25 유닛) 위에 뜬다.
            // 그래서 콜라이더 아래 0.25 유닛은 그림이 없고, 높이를 1.9 가 아니라 2.0 으로 둬 머리 위까지 덮는다.
            // 플레이에서 「발밑을 때렸는데 맞는다」가 거슬리면 레시피 footY 를 92 쪽으로 되돌린다.
            // 시트: Art_Source/characters/void_caster/sheet_recipe.json
            new Entry("void_caster", "void_caster", new Vector2(0.9f, 2.0f)),

            // 2026-09-20 화염 박격포(아직 내려오는 중인 것 R2 · 색 변환 없음). 그림 81x56px 로 <b>12종 중 가장 넓다</b>.
            // 좌우 끝은 얇게 퍼진 조각이라(열 높이 3~11px) 뺐다 — 열 높이 30px 이상인 구간만 62px = 1.9 유닛.
            // 높이는 꼭대기의 굴뚝·지붕 끝을 뺀 49px = 1.5 유닛. 창·활·재 더미를 뺀 것과 같은 판단이다.
            // 🔴 짐까지 다 넣으면 사거리 9 의 곡사 적이 근접에서 지나치게 잘 맞는다.
            // 시트: Art_Source/characters/flame_mortar/sheet_recipe.json
            new Entry("flame_mortar", "flame_mortar", new Vector2(1.9f, 1.5f)),

            // 2026-09-20 엘리트 사냥꾼(흉내를 내다 만 것 R4 · 색 변환 없음). 🔴 <b>첫 정예다</b> — 그림 49x84px 로
            // 12종 중 가장 크고(1.5 x 2.6 유닛) 일반 적(1.5~2.0 유닛 높이)과 한눈에 갈린다.
            // 🔴 <b>생성 캔버스만 120 이다</b>(앞 6종 92) — 정예 80px 은 size 120 에서만 나온다.
            // 92→64px · 112→62px · 120→84px 로 계단이 112 와 120 사이에 있다(Art_Source/20_SUBJECTS/enemies/elite_hunter.md).
            // 슬라이스 격자는 그대로다 — EnemyCell 124 · EnemyFootY 92 는 코드 상수라 적 전체에 똑같이 걸리고,
            // 84px 그림은 그 안에 들어간다(조립 실측: 발밑 103~105 · 산포 2px).
            // 🔴 폭 1.5 는 <b>머리 기준</b>이다 — 이 적은 머리가 몸통보다 넓다(머리 49px · 몸통 24~32px).
            // 창·활을 뺀 것과 달리 머리는 부속물이 아니라 몸의 절반이라 뺄 수 없다. 대신 다리 쪽이 과하게 넓어지므로
            // 「발밑을 때렸는데 맞는다」가 거슬리면 폭을 1.1 쪽으로 내린다.
            // 시트: Art_Source/characters/elite_hunter/sheet_recipe.json
            new Entry("elite_hunter", "elite_hunter", new Vector2(1.5f, 2.6f)),

            // 2026-09-21 감시자 거인(원을 쓴 것 R2 · 색 변환 없음). 🔴 <b>첫 중간보스다</b> — 그림 99x98px 로
            // 12종 중 가장 크고(1.3 x 3.0 유닛) 정예(2.6)·일반(1.5~2.0)과 높이로 갈린다.
            // 🔴 <b>폭 1.3 은 날을 뺀 축(몸통)이다</b> — 열 높이 실측이 날 끝 3~16px · 몸통 50~97px 이라
            // 50 이상인 47px = 1.5 유닛만 잡았다(창·활을 뺀 것과 같은 판단 · 떠 있는 몸으로 다시 재서 42 -> 47).
            // 🔑 날까지 넣으면 폭 3.1 유닛이 되어 <b>회전베기 반경(spinRadius 2.5)보다 넓은 몸</b>이 된다.
            // 🔴 <b>이 적 때문에 적 칸이 124 -> 148 로 커졌다</b> — SpriteSheetSlicer 주석 참조.
            // 시트: Art_Source/characters/midboss_sentinel/sheet_recipe.json
            new Entry("midboss_sentinel", "midboss_sentinel", new Vector2(1.5f, 3f)),
        };

        /// <summary>시트 파일명의 상태 → 애니메이션 상태 이름. 파일은 레시피의 <c>states</c> 키를 따른다.</summary>
        private static readonly (string SheetState, string AnimationId)[] SheetStates =
        {
            ("move", EnemyAnimationIds.Move),
            ("attack", EnemyAnimationIds.Attack),
            ("hit", EnemyAnimationIds.Hit),
            ("dead", EnemyAnimationIds.Dead),
        };

        public static void Build()
        {
            EnsureFolders();

            AnimatorController baseController =
                SpriteClipAssets.CreateFlatController(BaseControllerPath, EnemyAnimationIds.All.ToArray());
            if (baseController == null) return;

            int built = 0;
            foreach (Entry entry in Entries)
            {
                EnemyData data = FindEnemyData(entry.EnemyId);
                if (data == null)
                {
                    Debug.LogError($"{Tag} enemyId '{entry.EnemyId}' 인 EnemyData 가 없다 — 건너뜀");
                    continue;
                }

                Dictionary<string, AnimationClip> clips = BuildClips(entry, data);
                if (clips.Count == 0) continue;

                AnimatorOverrideController controller = SpriteClipAssets.CreateOverrideController(
                    OverridePath(entry), ControllerName(entry), baseController, clips);

                Debug.Log($"{Tag} {entry.EnemyId} — {clips.Count}/{EnemyAnimationIds.All.Count} 상태 · {WirePrefab(entry, data, controller)}");
                built++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"{Tag} 완료 — 적 {built}종. 컨트롤러: {BaseControllerPath}");
        }

        /// <summary>
        /// <see cref="PrefabBuilder"/> 가 프리팹을 새로 만들 때 부른다 — 강제 재생성이 그림 자식을 지우지 않게.
        /// 오버라이드가 아직 없으면(이 메뉴를 한 번도 안 돌렸으면) 아무것도 안 하고 false.
        /// </summary>
        public static bool TryApplyTo(GameObject root, EnemyData data)
        {
            if (root == null || data == null) return false;

            foreach (Entry entry in Entries)
            {
                if (entry.EnemyId != data.enemyId) continue;

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverridePath(entry));
                if (controller == null) return false;

                return ApplyVisual(root, entry, controller);
            }
            return false;
        }

        private static void EnsureFolders()
        {
            foreach (string folder in new[] { AnimationRoot, ClipFolder })
            {
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            }
            AssetDatabase.Refresh();
        }

        private static Dictionary<string, AnimationClip> BuildClips(Entry entry, EnemyData data)
        {
            var clips = new Dictionary<string, AnimationClip>();

            foreach (var (sheetState, animationId) in SheetStates)
            {
                string sheetPath = SheetPath(entry, sheetState);
                Sprite[] sprites = SpriteClipAssets.LoadSlicedSprites(sheetPath);
                if (sprites.Length == 0)
                {
                    Debug.LogWarning($"{Tag} 슬라이스된 스프라이트가 없다 — {sheetPath} (아트 ▸ Enemy Sheets (Grid Slice) 먼저)");
                    continue;
                }

                float frameRate = ResolveFrameRate(animationId, sprites.Length, data);
                if (frameRate <= 0f)
                {
                    // 🔴 시간이 0 이면 FSM 이 한 판정만 그 상태에 머문다 — 클립을 구워 봐야 첫 프레임만 보인다.
                    Debug.LogWarning($"{Tag} {entry.EnemyId}/{animationId} — EnemyData 동작 시간이 0 이라 굽지 않는다");
                    continue;
                }

                AnimationClip clip = SpriteClipAssets.CreateSpriteClip(sprites, frameRate, animationId,
                    loop: !EnemyAnimationIds.IsOneShot(animationId));
                SpriteClipAssets.SaveClip(clip, $"{ClipFolder}/{ControllerName(entry)}_{animationId}.anim");
                clips[animationId] = clip;
            }

            return clips;
        }

        /// <summary>한 번 재생 클립은 fps 가 결과다 — 프레임 수 ÷ FSM 지속시간.</summary>
        private static float ResolveFrameRate(string animationId, int frameCount, EnemyData data)
        {
            float duration = animationId switch
            {
                EnemyAnimationIds.Attack => data.attackWindup + data.attackRecovery,
                EnemyAnimationIds.Hit => data.staggerDuration,
                EnemyAnimationIds.Dead => Mathf.Max(0.1f, data.deathLingerDuration - DeadHoldSeconds),
                _ => 0f,
            };

            if (!EnemyAnimationIds.IsOneShot(animationId)) return MoveFrameRate;
            return duration > 0f ? frameCount / duration : 0f;
        }

        private static string WirePrefab(Entry entry, EnemyData data, AnimatorOverrideController controller)
        {
            if (data.spawnPrefab == null) return "spawnPrefab 없음 — 프리팹 배선 안 함(프리팹 ▸ Prototype Prefabs 먼저)";

            string prefabPath = AssetDatabase.GetAssetPath(data.spawnPrefab);
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (!ApplyVisual(root, entry, controller)) return $"{prefabPath} 배선 실패";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return $"{prefabPath} 에 배선";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 루트의 정지 그림을 걷고 <b>Visual 자식(SpriteRenderer + Animator + AnimatorDriver)</b>으로 옮긴다.
        /// 여러 번 돌려도 같은 결과다 — 이미 있는 자식·컴포넌트는 재사용한다.
        /// </summary>
        private static bool ApplyVisual(GameObject root, Entry entry, AnimatorOverrideController controller)
        {
            Sprite[] firstSheet = SpriteClipAssets.LoadSlicedSprites(SheetPath(entry, SheetStates[0].SheetState));
            if (firstSheet.Length == 0)
            {
                Debug.LogError($"{Tag} {entry.EnemyId} — 첫 시트가 슬라이스되지 않았다");
                return false;
            }

            // ── 콜라이더 · 그림 위치(파생값) ──
            var collider = root.GetComponent<BoxCollider2D>();
            if (collider == null) collider = root.AddComponent<BoxCollider2D>();
            collider.size = entry.BodySize;
            collider.offset = Vector2.zero;
            float footY = collider.offset.y - collider.size.y * 0.5f;

            // ── 루트의 정지 그림 → Visual 자식 ──
            Transform visual = root.transform.Find(VisualName);
            if (visual == null)
            {
                visual = new GameObject(VisualName).transform;
                visual.SetParent(root.transform, false);
            }
            visual.localPosition = new Vector3(0f, footY, 0f);

            var renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = visual.gameObject.AddComponent<SpriteRenderer>();

            var rootRenderer = root.GetComponent<SpriteRenderer>();
            if (rootRenderer != null)
            {
                renderer.sharedMaterial = rootRenderer.sharedMaterial;
                renderer.sortingLayerID = rootRenderer.sortingLayerID;
                renderer.sortingOrder = rootRenderer.sortingOrder;
                Object.DestroyImmediate(rootRenderer, true);
            }
            renderer.sprite = firstSheet[0];
            renderer.color = Color.white;

            var animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            var driver = visual.GetComponent<AnimatorDriver>();
            if (driver == null) driver = visual.gameObject.AddComponent<AnimatorDriver>();
            SetReference(driver, "animator", animator);

            // ── 루트 쪽 이음매 ──
            var binder = root.GetComponent<EnemyAnimationBinder>();
            if (binder == null) binder = root.AddComponent<EnemyAnimationBinder>();
            SetReference(binder, "animatorDriver", driver);
            SetReference(binder, "stateMachine", root.GetComponent<FSM.Core.StateMachine>());

            var visuals = root.GetComponent<EnemyVisuals>();
            if (visuals != null) SetBool(visuals, "flipsToFace", true);

            PlaceNametag(root.transform, footY, firstSheet[0]);
            return true;
        }

        /// <summary>
        /// 이름표를 그림 머리 위로. 🔑 <b>보이는 윤곽의 꼭대기</b>를 쓴다 — 스프라이트 bounds 는 124 칸 전체라
        /// 투명 여백만큼 높이 뜬다. Tight 메시의 꼭짓점이 알파 윤곽을 따르므로 그 최대 y 가 머리 끝이다.
        /// </summary>
        private static void PlaceNametag(Transform root, float footY, Sprite sprite)
        {
            Transform nametag = root.Find(NametagName);
            if (nametag == null) return;

            Vector2[] vertices = sprite.vertices;
            float top = vertices.Length > 0 ? vertices.Max(v => v.y) : sprite.bounds.max.y;
            nametag.localPosition = new Vector3(0f, footY + top + NametagMargin, 0f);
        }

        private static void SetReference(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"{Tag} {target.GetType().Name}.{fieldName} 필드를 못 찾았다");
                return;
            }
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string fieldName, bool value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"{Tag} {target.GetType().Name}.{fieldName} 필드를 못 찾았다");
                return;
            }
            property.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static EnemyData FindEnemyData(string enemyId)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data != null && data.enemyId == enemyId) return data;
            }
            return null;
        }

        private static string SheetPath(Entry entry, string sheetState)
            => $"{AbyssPaths.EnemySprites}/{entry.FilePrefix}/{SheetDirection}/{entry.FilePrefix}_{sheetState}_{SheetDirection}.png";

        /// <summary><c>melee_grunt</c> → <c>MeleeGrunt</c> — 프리팹 이름과 같은 규약을 같은 함수로.</summary>
        private static string ControllerName(Entry entry) => PrefabBuilder.ToPascalCase(entry.EnemyId);

        private static string OverridePath(Entry entry) => $"{AnimationRoot}/{ControllerName(entry)}.overrideController";
    }
}
#endif

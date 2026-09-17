#if UNITY_EDITOR
using Abyss.Runtime.Combat;
using Abyss.Runtime.Form;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    public static partial class ContentBuilder
    {
        /// <summary>
        /// 원거리 폼(궁수 · 투척사)의 기본 공격 설정. 기획: <c>Docs/game-design/15-ranged-basic-attack.md</c> §3.
        ///
        /// 🔑 <b>수치는 여기 한 곳에만 있다</b> — 프리팹은 모양만, 폼 에셋은 이 패스가 쓴 값을 갖는다.
        /// 🔴 약 · 강 <b>배율이 같다</b>(0.6). 사거리의 대가는 배율 하나로만 받는다 —
        /// 플레이에서 「너무 안전하다」가 나오면 이 값부터 내린다(값 하나만 움직이게).
        /// </summary>
        private const float RANGED_DAMAGE_SCALE = 0.6f;

        /// <summary>
        /// 무기 그림은 칼끝이 오른쪽 위 45° 로 그려져 있다(<c>dagger_06.png</c>). 진행 방향(오른쪽)에 맞추는 보정.
        /// </summary>
        private const float WEAPON_SPRITE_ANGLE_OFFSET = -45f;

        /// <summary>
        /// <see cref="CreateOrSkip"/> 은 기존 에셋을 건너뛰므로 폼 몸통 스프라이트 연결과 같은 이유로
        /// <b>매번 도는 별도 패스</b>다. 발사체 프리팹이 없으면(PrefabBuilder 전) 방식만 바꾸지 않고 건너뛴다 —
        /// 원거리로 바꿔 놓고 프리팹이 비면 런타임이 근접으로 물러나며 경고를 띄우지만, 애초에 안 바꾸는 편이 조용하다.
        /// </summary>
        private static void WireFormRangedAttacks()
        {
            var arrow = LoadProjectilePrefab(AbyssPaths.PlayerArrowPrefab);
            var dagger = LoadProjectilePrefab(AbyssPaths.PlayerDaggerPrefab);

            int wired = 0;
            wired += WireRangedForm("void_archer", arrow, useWeaponSprite: false, spriteAngleOffset: 0f);
            wired += WireRangedForm("void_thrower", dagger, useWeaponSprite: true, spriteAngleOffset: WEAPON_SPRITE_ANGLE_OFFSET);

            if (wired > 0) AssetDatabase.SaveAssets();
            Debug.Log($"[ContentBuilder] 원거리 기본 공격 연결: {wired}종 갱신.");
        }

        private static Projectile LoadProjectilePrefab(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var projectile = go != null ? go.GetComponent<Projectile>() : null;
            if (projectile == null)
            {
                Debug.LogWarning($"[ContentBuilder] 발사체 프리팹 없음({path}) — PrefabBuilder 먼저 실행 후 다시 돌릴 것.");
            }
            return projectile;
        }

        /// <returns>에셋을 바꿨으면 1.</returns>
        private static int WireRangedForm(string formId, Projectile prefab, bool useWeaponSprite, float spriteAngleOffset)
        {
            if (prefab == null) return 0;

            FormData form = FindForm(formId);
            if (form == null)
            {
                Debug.LogWarning($"[ContentBuilder] 폼 에셋 없음: {formId}");
                return 0;
            }

            form.attackStyle = FormAttackStyle.Ranged;

            // 약 — 빠른 단발. 사거리 16 × 0.45 = 7.2 (화면 가로 20 의 1/3)
            form.rangedLight = new RangedAttackSpec
            {
                projectilePrefab = prefab,
                speed = 16f,
                lifetime = 0.45f,
                pierceCount = 0,
                damageScale = RANGED_DAMAGE_SCALE,
                useWeaponSprite = useWeaponSprite,
                spriteAngleOffset = spriteAngleOffset
            };

            // 강 — 관통 3체. 사거리 20 × 0.5 = 10 (화면 절반)
            form.rangedHeavy = new RangedAttackSpec
            {
                projectilePrefab = prefab,
                speed = 20f,
                lifetime = 0.5f,
                pierceCount = 2,
                damageScale = RANGED_DAMAGE_SCALE,
                useWeaponSprite = useWeaponSprite,
                spriteAngleOffset = spriteAngleOffset
            };

            EditorUtility.SetDirty(form);
            return 1;
        }

        private static FormData FindForm(string formId)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:FormData", new[] { AbyssPaths.Forms }))
            {
                var form = AssetDatabase.LoadAssetAtPath<FormData>(AssetDatabase.GUIDToAssetPath(guid));
                if (form != null && form.formId == formId) return form;
            }
            return null;
        }
    }
}
#endif

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
        /// 🔴 <b>궁수와 투척사는 사거리와 한 방을 맞바꾼다</b>(P03) — 궁수는 멀리서 약하게, 투척사는 가까이서 세게.
        /// 둘이 같은 값이던 때는 그림 말고 다른 점이 없었다. 사거리 = speed × lifetime.
        /// </summary>
        // 궁수 — 약: 사거리 18 × 0.6 = 10.8 · 강: 24 × 0.6 = 14.4, 4체 관통
        private static readonly (float speed, float lifetime, int pierce, float damage) ArcherLight = (18f, 0.6f, 0, 0.5f);
        private static readonly (float speed, float lifetime, int pierce, float damage) ArcherHeavy = (24f, 0.6f, 3, 0.7f);
        // 투척사 — 약: 사거리 13 × 0.35 ≈ 4.5 · 강: 16 × 0.4 = 6.4, 2체 관통
        private static readonly (float speed, float lifetime, int pierce, float damage) ThrowerLight = (13f, 0.35f, 0, 0.9f);
        private static readonly (float speed, float lifetime, int pierce, float damage) ThrowerHeavy = (16f, 0.4f, 1, 1.3f);

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
            wired += WireRangedForm("void_archer", arrow, useWeaponSprite: false, spriteAngleOffset: 0f,
                                    ArcherLight, ArcherHeavy);
            wired += WireRangedForm("void_thrower", dagger, useWeaponSprite: true, spriteAngleOffset: WEAPON_SPRITE_ANGLE_OFFSET,
                                    ThrowerLight, ThrowerHeavy);

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
        private static int WireRangedForm(string formId, Projectile prefab, bool useWeaponSprite, float spriteAngleOffset,
                                          (float speed, float lifetime, int pierce, float damage) light,
                                          (float speed, float lifetime, int pierce, float damage) heavy)
        {
            if (prefab == null) return 0;

            FormData form = FindForm(formId);
            if (form == null)
            {
                Debug.LogWarning($"[ContentBuilder] 폼 에셋 없음: {formId}");
                return 0;
            }

            form.attackStyle = FormAttackStyle.Ranged;

            // 약 — 단발 · 강 — 관통(pierceCount 는 「추가로」 뚫는 수, 0 = 한 마리)
            form.rangedLight = new RangedAttackSpec
            {
                projectilePrefab = prefab,
                speed = light.speed,
                lifetime = light.lifetime,
                pierceCount = light.pierce,
                damageScale = light.damage,
                useWeaponSprite = useWeaponSprite,
                spriteAngleOffset = spriteAngleOffset
            };

            form.rangedHeavy = new RangedAttackSpec
            {
                projectilePrefab = prefab,
                speed = heavy.speed,
                lifetime = heavy.lifetime,
                pierceCount = heavy.pierce,
                damageScale = heavy.damage,
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

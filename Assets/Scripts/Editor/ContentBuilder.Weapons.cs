using System.Collections.Generic;
using System.Linq;
using Abyss.Runtime.Form;
using Abyss.Runtime.Weapon;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 폼마다 딸려 오는 <b>기본 무기</b> 하나씩. 등급은 전부 일반이고 배율도 1 이다 —
    /// 기본 무기는 「무기를 든다」는 상태를 만들 뿐이고, 강해지는 것은 획득 창구가 맡는다(§5).
    ///
    /// 🔑 <b>그림 배선을 생성과 분리한다.</b> 이미 있는 에셋에도 붙여야 하기 때문이다
    /// (<c>WireFormBodySprites</c> 와 같은 이유).
    /// </summary>
    public static partial class ContentBuilder
    {
        /// <summary>무기 ID → (에셋 이름, 폼 ID, 표시 이름, 설명, 그림 이름, 세워 드는가).</summary>
        private static readonly (string id, string asset, string form, string name, string desc,
                                 string sprite, bool braced)[] StarterWeapons =
        {
            ("rusted_blade", "RustedBlade", "dark_blade", "녹슨 검",
             "손에 익은 한 자루. 특별할 것은 없지만 늘 거기 있다.", "sword_00", false),
            ("worn_bow", "WornBow", "void_archer", "낡은 활",
             "시위가 늘어졌다. 그래도 당기면 쏘아진다.", "bow_00", false),
            ("dented_shield", "DentedShield", "ancient_shield", "찌그러진 방패",
             "여러 번 맞은 자국이 남았다. 아직 버틴다.", "shield_15", true),
            ("chipped_dagger", "ChippedDagger", "void_thrower", "이 빠진 단검",
             "던지고 다시 줍기를 반복한 흔적.", "dagger_06", false),
        };

        private static void CreateWeapons()
        {
            foreach (var w in StarterWeapons)
            {
                CreateOrSkip<WeaponData>($"{AbyssPaths.Weapons}/{w.asset}.asset", so =>
                {
                    so.weaponId = w.id;
                    so.formBound = w.form;
                    so.rarity = WeaponRarity.Common;
                    so.displayName = w.name;
                    so.description = w.desc;
                    so.attackMultiplier = 1f;
                    so.upgradeMultiplierStep = 0.1f;
                    so.bracedUpright = w.braced;
                });
            }
        }

        /// <summary>
        /// 무기 에셋에 그림을 붙인다 — 기본 그림 + 각도 스트립.
        ///
        /// 🔴 <b>각도 순서가 곧 각도다.</b> <c>bake_weapon_angles.py</c> 가 <c>i</c> 번째를
        /// <c>-360*i/N</c> 도 돌려 굽고 <c>WeaponSocket.AngleIndexOf</c> 가 그 약속을 믿는다.
        /// <see cref="AssetDatabase.LoadAllAssetsAtPath"/> 의 순서는 보장되지 않으므로
        /// <b>이름 끝의 숫자로 다시 정렬한다</b>(<c>WeaponAngleWirer</c> 와 같은 규칙).
        ///
        /// 📌 <b>세워 드는 무기는 각도 스트립을 안 붙인다.</b> 0 번만 쓰이므로 있으나 마나이고,
        /// 없으면 기본 그림 + 항등 회전으로 같은 결과가 나온다.
        /// </summary>
        private static void WireWeaponSprites()
        {
            foreach (var w in StarterWeapons)
            {
                var so = AssetDatabase.LoadAssetAtPath<WeaponData>($"{AbyssPaths.Weapons}/{w.asset}.asset");
                if (so == null) continue;

                bool changed = false;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{AbyssPaths.WeaponSprites}/{w.sprite}.png");
                if (sprite != null && so.sprite != sprite) { so.sprite = sprite; changed = true; }
                if (sprite == null)
                {
                    Debug.LogWarning($"[ContentBuilder] 무기 그림이 없다: {w.sprite}.png — " +
                                     "Tools ▸ Abyss ▸ Generate ▸ Weapon Sprites 를 먼저 돌릴 것.");
                }

                if (!w.braced)
                {
                    string kind = w.sprite.Split('_')[0];
                    Sprite[] angles = LoadAngleStrip($"{AbyssPaths.WeaponSprites}/{kind}_angles.png");
                    if (angles != null && !angles.SequenceEqual(so.angleSprites ?? new Sprite[0]))
                    {
                        so.angleSprites = angles;
                        changed = true;
                    }
                }

                if (changed) EditorUtility.SetDirty(so);
            }
        }

        /// <summary>이름 끝 숫자 순서로 정렬한 각도 스트립. 스트립이 없으면 null.</summary>
        private static Sprite[] LoadAngleStrip(string stripPath)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(stripPath).OfType<Sprite>().ToList();
            if (sprites.Count == 0) return null;

            var keyed = new List<(int index, Sprite sprite)>(sprites.Count);
            foreach (Sprite sprite in sprites)
            {
                int underscore = sprite.name.LastIndexOf('_');
                if (underscore < 0 || !int.TryParse(sprite.name[(underscore + 1)..], out int index))
                {
                    Debug.LogError($"[ContentBuilder] 각도 이름에서 번호를 못 읽었다: {sprite.name}");
                    return null;
                }
                keyed.Add((index, sprite));
            }
            keyed.Sort((a, b) => a.index.CompareTo(b.index));
            for (int i = 0; i < keyed.Count; i++)
            {
                if (keyed[i].index == i) continue;
                Debug.LogError($"[ContentBuilder] {stripPath} 의 번호가 0부터 연속이 아니다 ({i} 번 없음).");
                return null;
            }
            return keyed.Select(k => k.sprite).ToArray();
        }

        /// <summary>
        /// 폼에 기본 무기를 꽂는다. <b>이미 있는 폼 에셋에도 붙여야 하므로 생성과 분리된 패스다.</b>
        ///
        /// ⚠️ <c>formBound</c> 가 폼의 <c>formId</c> 와 다르면 <b>그 무기는 그 폼이 못 쓴다</b> —
        /// 화면에서는 그냥 안 보일 뿐이라 여기서 경고로 세운다.
        /// </summary>
        private static void WireFormDefaultWeapons()
        {
            foreach (var w in StarterWeapons)
            {
                var form = AssetDatabase.LoadAssetAtPath<FormData>(
                    $"{AbyssPaths.Forms}/{ToPascalCase(w.form)}.asset");
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(
                    $"{AbyssPaths.Weapons}/{w.asset}.asset");
                if (form == null || weapon == null) continue;

                if (weapon.formBound != form.formId)
                {
                    Debug.LogError($"[ContentBuilder] {weapon.name}.formBound({weapon.formBound}) 가 " +
                                   $"{form.name}.formId({form.formId}) 와 다르다 — 그 폼이 못 쓴다.");
                    continue;
                }

                if (form.defaultWeapon == weapon) continue;
                form.defaultWeapon = weapon;
                EditorUtility.SetDirty(form);
            }
        }
    }
}

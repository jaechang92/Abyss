using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Abyss.Runtime.Player;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 각도 스트립을 잘라 <see cref="WeaponSocket.angleSprites"/> 에 <b>순서대로</b> 꽂는다.
    ///
    /// 🔴 <b>순서가 곧 각도다.</b> 배열의 <c>i</c> 번째는 <c>-360*i/N</c> 도 돌린 그림이라는 것이
    /// <c>bake_weapon_angles.py</c> 와의 약속이고, <see cref="WeaponSocket.AngleIndexOf"/> 가 그 약속을
    /// 믿고 색인한다. <b>순서가 어긋나면 무기가 엉뚱한 각도로 나오는데 오류는 안 난다.</b>
    ///
    /// ⚠️ 그래서 <see cref="AssetDatabase.LoadAllAssetsAtPath"/> 가 주는 순서를 <b>그대로 쓰면 안 된다</b> —
    /// 그건 보장되지 않는다. 이름 끝의 숫자로 다시 정렬한다.
    ///
    /// 24칸을 손으로 꽂아 본 뒤에 만들었다. 무기가 넷이면 96칸이고, 그건 손으로 할 일이 아니다.
    /// </summary>
    public static class WeaponAngleWirer
    {
        private const string DefaultStrip = "Assets/Art/Sprites/Weapons/sword_angles.png";
        private const string PlayerPrefab = "Assets/Prefabs/Player/Player.prefab";

        public static void Wire()
        {
            string stripPath = SelectedTexturePath() ?? DefaultStrip;

            if (!TryLoadOrdered(stripPath, out List<Sprite> ordered, out string error))
            {
                Debug.LogError($"[WeaponAngleWirer] {error}");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var socket = root.GetComponentInChildren<WeaponSocket>(true);
                if (socket == null)
                {
                    Debug.LogError($"[WeaponAngleWirer] {PlayerPrefab} 에 WeaponSocket 이 없다.");
                    return;
                }

                var so = new SerializedObject(socket);
                SerializedProperty array = so.FindProperty("angleSprites");
                array.arraySize = ordered.Count;
                for (int i = 0; i < ordered.Count; i++)
                {
                    array.GetArrayElementAtIndex(i).objectReferenceValue = ordered[i];
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            float step = 360f / ordered.Count;
            Debug.Log($"[WeaponAngleWirer] {ordered.Count}칸 배선 — {step:0.##}도 간격\n" +
                      $"{System.IO.Path.GetFileName(stripPath)} → Player/WeaponSocket.angleSprites\n" +
                      $"첫 칸 {ordered[0].name}(0도) · 끝 칸 {ordered[^1].name}({-step * (ordered.Count - 1):0.##}도)");
        }

        /// <summary>Project 창에서 고른 텍스처. 없으면 null.</summary>
        private static string SelectedTexturePath()
        {
            if (Selection.activeObject is not Texture2D) return null;
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return string.IsNullOrEmpty(path) ? null : path;
        }

        /// <summary>
        /// 스트립의 하위 스프라이트를 <b>이름 끝의 숫자 순서</b>로 돌려준다.
        ///
        /// 🔑 격자가 성립하는지도 같이 본다 — 칸 크기나 피벗이 섞여 있으면 스트립이 아니라
        /// 다른 시트를 고른 것이다. 그대로 꽂으면 각도마다 무기가 튄다.
        /// </summary>
        private static bool TryLoadOrdered(string stripPath, out List<Sprite> ordered, out string error)
        {
            ordered = null;

            var sprites = AssetDatabase.LoadAllAssetsAtPath(stripPath).OfType<Sprite>().ToList();
            if (sprites.Count == 0)
            {
                error = $"{stripPath} 에 스프라이트가 없다 — Multiple 로 잘려 있는지 볼 것.";
                return false;
            }

            var keyed = new List<(int index, Sprite sprite)>(sprites.Count);
            foreach (Sprite sprite in sprites)
            {
                int underscore = sprite.name.LastIndexOf('_');
                if (underscore < 0 ||
                    !int.TryParse(sprite.name[(underscore + 1)..], NumberStyles.Integer,
                                  CultureInfo.InvariantCulture, out int index))
                {
                    error = $"이름에서 번호를 못 읽었다: {sprite.name} — `이름_0`, `이름_1` ... 규약이어야 한다.";
                    return false;
                }
                keyed.Add((index, sprite));
            }

            keyed.Sort((a, b) => a.index.CompareTo(b.index));
            for (int i = 0; i < keyed.Count; i++)
            {
                if (keyed[i].index == i) continue;
                error = $"번호가 0부터 연속이 아니다 — {i} 번이 없다(찾은 번호 {keyed[i].index}).";
                return false;
            }

            Vector2 size = keyed[0].sprite.rect.size;
            Vector2 pivot = keyed[0].sprite.pivot;
            foreach ((int index, Sprite sprite) in keyed)
            {
                if (sprite.rect.size != size)
                {
                    error = $"칸 크기가 섞여 있다 — {sprite.name} 이 {sprite.rect.size}, 첫 칸은 {size}.";
                    return false;
                }
                if ((sprite.pivot - pivot).sqrMagnitude > 0.01f)
                {
                    error = $"피벗이 섞여 있다 — {sprite.name} 이 {sprite.pivot}, 첫 칸은 {pivot}.\n" +
                            "자루가 칸 중앙이어야 각도가 같은 점을 돈다(bake_weapon_angles.py).";
                    return false;
                }
            }

            if (360 % keyed.Count != 0)
            {
                Debug.LogWarning($"[WeaponAngleWirer] 칸이 {keyed.Count}개라 360 이 나눠떨어지지 않는다. " +
                                 "간격이 균일하지 않으면 각도 색인이 어긋난다.");
            }

            ordered = keyed.Select(k => k.sprite).ToList();
            error = null;
            return true;
        }
    }
}

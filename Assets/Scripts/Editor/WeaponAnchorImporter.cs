using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Abyss.Runtime.Player;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// <c>Tools/ArtPipeline/hand_anchors.py</c> 가 낸 JSON을 <see cref="WeaponAnchorSet"/>으로 들여온다.
    ///
    /// 🔑 <b>시트 파일명이 곧 키다</b> — <c>knight_red_attacklight_southeast.png</c> → <c>AttackLight</c>.
    /// 클립 생성기(<see cref="PlayerAnimationBuilder"/>)가 쓰는 규약과 같은 자리라, 한쪽만 바뀌면
    /// 앵커가 조용히 안 붙는다. 그래서 <b>모르는 상태 이름은 오류로 세운다</b>.
    ///
    /// ⚠️ 덮어쓰기가 아니라 <b>갱신</b>이다 — 이미 있는 에셋이면 그 자리에 값을 채운다.
    /// 지웠다 다시 만들면 인스펙터에서 손으로 다듬은 값과 참조가 같이 날아간다.
    /// </summary>
    public static class WeaponAnchorImporter
    {
        private const string MenuPath = "Tools/Abyss/Generate/Weapon Anchor Set";
        private const string DefaultJson = "Art_Source/anchors/knight_red_hand_anchors.json";
        private const string OutputDir = "Assets/Resources/Data/WeaponAnchors";

        /// <summary>
        /// 시트 파일명의 상태 조각 → 애니메이션 ID.
        ///
        /// 📌 <b>walk → Run 은 의도다</b> — 소스는 걷기 그림이지만 FSM 상태는 달리기이고,
        /// 키는 상태 이름을 따라야 한다(<see cref="PlayerAnimationBuilder"/>와 같은 규약).
        /// </summary>
        private static readonly Dictionary<string, string> SheetStateToAnimationId = new()
        {
            ["idle"] = PlayerAnimationIds.Idle,
            ["walk"] = PlayerAnimationIds.Run,
            ["jump"] = PlayerAnimationIds.Jump,
            ["fall"] = PlayerAnimationIds.Fall,
            ["dash"] = PlayerAnimationIds.Dash,
            ["attacklight"] = PlayerAnimationIds.AttackLight,
            ["attackheavy"] = PlayerAnimationIds.AttackHeavy,
            ["hit"] = PlayerAnimationIds.Hit,
            ["dead"] = PlayerAnimationIds.Dead,
        };

        /// <summary>
        /// 루프인 상태. <see cref="WeaponAnchorSet.FrameIndexOf"/>가 끝에서 되감을지를 정한다.
        /// <c>PlayerAnimationBuilder</c>의 <c>Looping</c>/<c>OneShot</c> 구분과 같아야 한다.
        /// </summary>
        private static readonly HashSet<string> LoopingIds = new()
        {
            PlayerAnimationIds.Idle, PlayerAnimationIds.Run,
            PlayerAnimationIds.Jump, PlayerAnimationIds.Fall, PlayerAnimationIds.Dash,
        };

        [MenuItem(MenuPath)]
        public static void Import()
        {
            string jsonPath = Path.Combine(Directory.GetCurrentDirectory(), DefaultJson);
            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"[WeaponAnchorImporter] JSON 이 없다: {DefaultJson}\n" +
                               "먼저 hand_anchors.py protrusion 을 돌릴 것.");
                return;
            }

            AnchorDocument doc = JsonUtility.FromJson<AnchorDocument>(File.ReadAllText(jsonPath));
            if (doc?.sheets == null || doc.sheets.Length == 0)
            {
                Debug.LogError("[WeaponAnchorImporter] sheets 가 비어 있다. JSON 형식을 확인할 것.");
                return;
            }

            var clips = new List<WeaponAnchorClip>();
            int totalKeys = 0, totalFrames = 0;
            string formPrefix = null;

            foreach (AnchorSheet sheet in doc.sheets)
            {
                if (!TryParseSheetName(sheet.sheet, out string prefix, out string state))
                {
                    Debug.LogError($"[WeaponAnchorImporter] 시트 이름을 못 읽었다: {sheet.sheet}");
                    return;
                }
                if (!SheetStateToAnimationId.TryGetValue(state, out string animationId))
                {
                    Debug.LogError($"[WeaponAnchorImporter] 모르는 상태 '{state}' ({sheet.sheet}). " +
                                   "SheetStateToAnimationId 에 추가할 것.");
                    return;
                }
                formPrefix ??= prefix;

                var frames = new WeaponAnchorFrame[sheet.anchors.Length];
                for (int i = 0; i < sheet.anchors.Length; i++)
                {
                    AnchorFrame a = sheet.anchors[i];
                    frames[i] = new WeaponAnchorFrame
                    {
                        position = new Vector2(a.x, a.y),
                        // 각도·앞뒤는 아직 검출이 안 준다. 자리를 비워 두고 나중에 채운다.
                        angle = 0f,
                        isInFront = true,
                        isKey = a.source == "키",
                    };
                    if (frames[i].isKey) totalKeys++;
                }
                totalFrames += frames.Length;

                clips.Add(new WeaponAnchorClip
                {
                    animationId = animationId,
                    isLooping = LoopingIds.Contains(animationId),
                    needsManual = sheet.needsManual,
                    frames = frames,
                });
            }

            string assetName = ToPascalCase(formPrefix) + "WeaponAnchors";
            WeaponAnchorSet set = LoadOrCreate(assetName);

            // 🔴 손으로 맞춘 값을 옮겨 온다. clips 를 통째로 갈아치우므로 안 하면 날아간다 —
            //    앵커를 클립에 안 굽기로 한 것과 같은 함정이 한 겹 안쪽에 또 있다.
            //    검출이 주는 것(위치·키 여부)만 새로 쓰고, 사람이 정한 것(오프셋)은 보존한다.
            int carried = CarryOverTunedValues(set, clips);

            set.formId = formPrefix;
            set.clips = clips.ToArray();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var manual = clips.FindAll(c => c.needsManual).ConvertAll(c => c.animationId);
            string carriedNote = carried > 0 ? $" · 손으로 맞춘 오프셋 {carried}건 보존" : string.Empty;
            Debug.Log($"[WeaponAnchorImporter] {assetName} — 클립 {clips.Count}개 · " +
                      $"키 {totalKeys}/{totalFrames}프레임{carriedNote}\n" +
                      (manual.Count == 0
                          ? "전부 검출로 채워졌다."
                          : $"🔴 손으로 잡아야 하는 것: {string.Join(", ", manual)} — " +
                            "한 점을 잡아 그 클립 전체에 복사할 것."));
            Selection.activeObject = set;
        }

        /// <summary>
        /// 기존 에셋에서 <b>사람이 정한 값</b>을 새 클립 목록으로 옮긴다.
        /// 지금은 <see cref="WeaponAnchorClip.handOffset"/> 하나지만, 손으로 다듬는 값이
        /// 늘어나면 여기에 같이 넣는다 — <b>보존 목록이 한 곳에 있어야</b> 빠뜨리지 않는다.
        /// </summary>
        private static int CarryOverTunedValues(WeaponAnchorSet existing, List<WeaponAnchorClip> fresh)
        {
            if (existing == null || existing.clips == null) return 0;

            int carried = 0;
            foreach (WeaponAnchorClip clip in fresh)
            {
                WeaponAnchorClip old = existing.FindClip(clip.animationId);
                if (old == null || old.handOffset == Vector2.zero) continue;

                clip.handOffset = old.handOffset;
                carried++;
            }
            return carried;
        }

        private static WeaponAnchorSet LoadOrCreate(string assetName)
        {
            if (!Directory.Exists(OutputDir)) Directory.CreateDirectory(OutputDir);
            string path = $"{OutputDir}/{assetName}.asset";

            // 지웠다 다시 만들지 않는다 — 인스펙터에서 다듬은 값과 참조가 같이 날아간다.
            var existing = AssetDatabase.LoadAssetAtPath<WeaponAnchorSet>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<WeaponAnchorSet>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        /// <summary>
        /// `knight_red_attacklight_southeast.png` → prefix `knight_red` · state `attacklight`.
        /// 📌 <b>방향 조각은 이름을 몰라도 된다</b> — 끝의 `_` 하나를 통째로 벗기므로
        /// `_east` 든 `_southeast` 든 같이 동작한다(2026-09-15 방향 전환에서 확인).
        /// </summary>
        private static bool TryParseSheetName(string fileName, out string prefix, out string state)
        {
            prefix = state = null;
            if (string.IsNullOrEmpty(fileName)) return false;

            string stem = Path.GetFileNameWithoutExtension(fileName);
            int dir = stem.LastIndexOf('_');          // 끝의 방향 조각(_east · _southeast ...)
            if (dir <= 0) return false;
            string withoutDirection = stem[..dir];

            int split = withoutDirection.LastIndexOf('_');
            if (split <= 0) return false;

            prefix = withoutDirection[..split];
            state = withoutDirection[(split + 1)..];
            return true;
        }

        private static string ToPascalCase(string snake)
        {
            if (string.IsNullOrEmpty(snake)) return "Unnamed";
            string[] parts = snake.Split('_', StringSplitOptions.RemoveEmptyEntries);
            var sb = new System.Text.StringBuilder();
            foreach (string part in parts)
            {
                sb.Append(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(part));
            }
            return sb.ToString();
        }

        // ── JSON 대응 (JsonUtility 는 필드 이름이 정확히 같아야 한다) ──────────────────
        // CS0649 를 끈다 — 이 필드들은 코드가 아니라 JsonUtility 가 리플렉션으로 채운다.
        // 경고를 남겨 두면 「대입 안 하는 필드」와 「진짜 빠뜨린 필드」가 화면에서 같아진다.
#pragma warning disable 0649
        [Serializable]
        private sealed class AnchorDocument
        {
            public AnchorSheet[] sheets;
        }

        [Serializable]
        private sealed class AnchorSheet
        {
            public string sheet;
            public int frames;
            public int keys;
            public bool needsManual;
            public AnchorFrame[] anchors;
        }

        [Serializable]
        private sealed class AnchorFrame
        {
            public int frame;
            public float x;          // 피벗 기준 유닛
            public float y;
            public string source;    // "키" | "보간" | "제안"
        }
#pragma warning restore 0649
    }
}

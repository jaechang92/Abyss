#if UNITY_EDITOR
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Player;
using Abyss.Runtime.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 제단 빌더들이 공유하는 규약. <c>FormAltarBuilder</c>가 혼자 갖고 있던 것을
    /// <c>WeaponAltarBuilder</c>가 생기면서 떼어 냈다.
    ///
    /// 🔑 <b>복사하지 않은 이유.</b> 여기 담긴 셋은 전부 <b>한 번 어긋나면 조용한</b> 규약이다 —
    /// 인터랙터 확인·프롬프트 배선·트리거 크기. 두 벌로 두면 한쪽만 고쳐지고, 그 결과는
    /// <b>"제단 앞에 서도 아무 일이 없다"</b>로만 드러난다(2026-08-26에 실제로 겪은 모양).
    /// 이 저장소가 UI 정렬 순위·화폐 표기에서 이미 겪은 파편화와 같은 자리다.
    ///
    /// ⚠️ <b>보이는 것은 공유하지 않는다.</b> 스프라이트·폴백 색·보상 주입은 제단마다 다르고,
    /// 그건 어긋나도 <b>화면에서 바로 보인다</b> — 공유할 이유가 없다.
    /// </summary>
    internal static class AltarBuilderCommon
    {
        /// <summary>제단 스프라이트 32x48 @PPU32 = 1x1.5 유닛. 트리거를 여기에 맞춘다.</summary>
        public static readonly Vector2 TriggerSize = new(1f, 1.5f);

        private const string PromptName = "InteractPrompt";

        /// <summary>
        /// Run 플레이어의 인터랙터를 찾는다. <b>없으면 만들지 않고 알린다.</b>
        ///
        /// 🔴 <b>2026-08-26 — 씬 오버라이드로 붙이던 것을 그만뒀다.</b>
        /// 예전에는 <c>Undo.AddComponent</c>로 씬 인스턴스에 직접 붙였는데,
        /// 그 오버라이드는 <b>프리팹을 다시 만들거나 씬을 다시 빌드하면 조용히 사라진다.</b>
        /// 실제로 그렇게 없어져서 런 중 제단이 반응하지 않았고,
        /// 오류도 경고도 안 나서 <b>제단 앞에 서도 아무 일이 없는 것</b>으로만 드러났다.
        ///
        /// 📌 이제 <c>PlayerInteractor</c>는 <b>Player.prefab</b>에 들어간다(<c>PrefabBuilder</c>).
        /// 여기서 다시 오버라이드를 만들면 같은 함정이 그대로 돌아오므로, 안내만 하고 멈춘다.
        /// </summary>
        public static PlayerInteractor EnsureRunPlayerInteractor(string tag)
        {
            var player = Object.FindAnyObjectByType<PlayerCharacter>();
            if (player == null)
            {
                Debug.LogWarning($"{tag} 씬에서 PlayerCharacter 미발견 — 인터랙터 확인 생략. Run 씬에서 실행하세요.");
                return null;
            }

            var interactor = player.GetComponent<PlayerInteractor>();
            if (interactor == null)
            {
                Debug.LogError(
                    $"{tag} Run 플레이어 '{player.gameObject.name}'에 PlayerInteractor가 없다 — 제단이 반응하지 않는다.\n" +
                    $"  → Abyss Tools 창의 '{AbyssToolNames.GeneratePrefabs}'(또는 '{AbyssToolNames.GenerateRebuildPlayer}')를 먼저 실행해 프리팹에 심을 것.\n" +
                    "  씬 오버라이드로 붙이지 않는 이유: 프리팹·씬을 다시 빌드하면 날아가 같은 문제가 재발한다.");
            }
            return interactor;
        }

        /// <summary>PlayerInteractor.promptLabel을 HUD의 InteractPrompt Text에 연결한다(없으면 폴백 생성).</summary>
        public static void WireInteractPrompt(PlayerInteractor interactor, string tag)
        {
            if (interactor == null) return;

            var so = new SerializedObject(interactor);
            var promptProp = so.FindProperty("promptLabel");
            if (promptProp == null) return;
            if (promptProp.objectReferenceValue != null) return; // 이미 연결됨

            var hud = Object.FindAnyObjectByType<HUDPresenter>(FindObjectsInactive.Include);
            if (hud == null)
            {
                Debug.LogWarning($"{tag} HUDPresenter 미발견 — 프롬프트 배선 생략. HUD 빌드 후 재실행하세요.");
                return;
            }

            var prompt = FindPrompt(hud.transform) ?? HudBuilder.CreateInteractPrompt(hud.transform);
            promptProp.objectReferenceValue = prompt;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(interactor);
            Debug.Log($"{tag} PlayerInteractor.promptLabel → HUD InteractPrompt 연결.");
        }

        private static Text FindPrompt(Transform hudRoot)
        {
            foreach (var t in hudRoot.GetComponentsInChildren<Text>(true))
            {
                if (t.gameObject.name == PromptName) return t;
            }
            return null;
        }

        /// <summary>
        /// 근접 감지용 트리거 콜라이더를 보장한다. 크기는 제단 스프라이트(32x48@PPU32 = 1x1.5 유닛)에
        /// 맞춘다 — 기본값 1x1로 두면 제단 위쪽 절반이 감지 범위 밖이라 프롬프트가 잘 뜨지 않는다.
        ///
        /// 생성 분기가 아니라 <b>공통 경로</b>에서 부를 것. 크기는 스프라이트에 종속된 데이터 구동 값이므로
        /// 이미 배치된 제단도 재실행으로 갱신되어야 한다(StageBuilder가 보상 지정을 재실행 시
        /// 반영하는 것과 같은 규약).
        /// </summary>
        public static void ApplyTrigger(GameObject go)
        {
            var col = go.GetComponent<BoxCollider2D>();
            if (col == null) col = Undo.AddComponent<BoxCollider2D>(go);

            col.isTrigger = true; // 물리 차단 없이 PlayerInteractor가 감지(로비 NPC와 동일)
            col.size = TriggerSize;
        }

        /// <summary>
        /// 소품 스프라이트 임포트 설정 보장(Sprite/Point/PPU) 후 로드. 미생성 시 null.
        /// PPU 는 <see cref="Abyss.Runtime.Camera.PixelScale"/> 를 본다 — 제단은 월드에 놓이는
        /// Point 필터 픽셀 아트라 <b>환경 아트와 같은 격자</b>여야 도트 굵기가 안 어긋난다.
        /// </summary>
        public static Sprite LoadPropSprite(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return null;

            if (importer.textureType != TextureImporterType.Sprite
                || importer.filterMode != FilterMode.Point
                || Mathf.Abs(importer.spritePixelsPerUnit - Abyss.Runtime.Camera.PixelScale.PixelsPerUnit) > 0.1f)
            {
                importer.textureType         = TextureImporterType.Sprite;
                importer.spriteImportMode    = SpriteImportMode.Single;
                importer.filterMode          = FilterMode.Point;
                importer.textureCompression  = TextureImporterCompression.Uncompressed;
                importer.spritePixelsPerUnit = Abyss.Runtime.Camera.PixelScale.PixelsPerUnit;
                importer.mipmapEnabled       = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }
    }
}
#endif

#if UNITY_EDITOR
using Abyss.Runtime.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 타이틀 씬 협곡 회랑 배경 배선. 텍스처 생성기는 <c>Tools/PixelArt/generate_title_bg.py</c>.
    ///
    /// 깊이 레이어는 화면에 고정된 크기(텍스처 원본 치수)로 두고
    /// <see cref="Transform.localScale"/>로 키운다. <b>pivot이 곧 확대 중심</b>이라,
    /// 절벽은 밑동 안쪽 모서리에, 길바닥 무늬는 바닥선에 pivot을 맞춘다.
    /// 이미지 정중앙을 기준으로 키우면 원근이 통째로 어긋난다.
    /// </summary>
    public static partial class TitleSceneBuilder
    {
        // ⚠️ TitleBackdrop.CLIFF_PAIRS와 같아야 한다. 이미지 수는 이것의 2배(좌·우 교대).
        private const int CLIFF_PAIRS = 8;
        private const int CLIFF_TEXTURE_COUNT = 4;
        private const int MARK_COUNT = 4;
        private const int MARK_TEXTURE_COUNT = 2;

        // 생성기 title_bg_config.py의 CLIFF_W/CLIFF_H/CLIFF_PIVOT_* 에서 나온 값이다.
        private const float CLIFF_WIDTH = 560f;
        private const float CLIFF_HEIGHT = 2099f;
        private const float CLIFF_PIVOT_X = 0.07143f;   // 40 / 560
        private const float CLIFF_PIVOT_Y = 0.19771f;   // 1 - 1684 / 2099

        // 길바닥 무늬는 캔버스가 다르다(생성기 MARK_W/MARK_H). 캔버스가 달라도 피벗만
        // 소실점에 맞으면 같은 배율 스케줄로 돌릴 수 있다.
        private const float MARK_WIDTH = 2432f;
        private const float MARK_HEIGHT = 900f;
        private const float MARK_GROUND_RATIO = 0.12f;

        // 카메라가 흔들리고 진로를 따라 돌 때 화면 가장자리가 비지 않도록 두는 여유.
        // ⚠️ 생성기의 DRIFT_MARGIN과 같아야 한다. 하늘·길은 이 여백을 <b>포함해서</b> 구워지므로
        //    RectTransform을 같은 값만큼 넓혀야 텍스처가 1:1로 얹히고 소실점이 어긋나지 않는다.
        private const float DRIFT_MARGIN = 120f;

        private static TitleBackdrop BuildBackdrop(Transform parent)
        {
            var backdropGo = CreateRect(parent, "Backdrop", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)backdropGo.transform);

            var driftGo = CreateRect(backdropGo.transform, "DriftRoot", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var driftRect = (RectTransform)driftGo.transform;
            Stretch(driftRect);

            // 하늘·길은 여백째 구워진 텍스처라 같은 값만큼 넓혀야 1:1로 얹힌다.
            var sky = CreateLayer(driftGo.transform, "Sky", "title_sky");
            Inflate(sky.rectTransform, DRIFT_MARGIN);
            var road = CreateLayer(driftGo.transform, "Road", "title_road");
            Inflate(road.rectTransform, DRIFT_MARGIN);
            var horizon = CreateLayer(driftGo.transform, "Horizon", "title_horizon");

            // 길바닥 무늬는 절벽보다 뒤에 둔다. 절벽은 길 가장자리 바깥에만 있으므로
            // 뒤에 둬도 발밑까지 그대로 보인다.
            var markRoot = CreateRing(driftGo.transform, "RoadMarkRoot");
            var marks = new RawImage[MARK_COUNT];
            for (int i = 0; i < MARK_COUNT; i++)
            {
                char suffix = (char)('a' + (i % MARK_TEXTURE_COUNT));
                marks[i] = CreateDepthLayer(markRoot, $"RoadMark{i}", $"title_road_mark_{suffix}",
                    new Vector2(MARK_WIDTH, MARK_HEIGHT),
                    new Vector2(0.5f, 1f - MARK_GROUND_RATIO));
            }

            // 절벽은 자기들끼리만 앞뒤를 바꾼다 — 전용 부모가 없으면 SetSiblingIndex(0)이
            // 하늘·길보다 뒤로 보내 버린다.
            // 배열은 좌·우 교대로 채운다(짝수=왼쪽). 런타임이 그 규약으로 읽는다.
            var cliffRoot = CreateRing(driftGo.transform, "CliffRoot");
            var cliffs = new RawImage[CLIFF_PAIRS * 2];
            for (int depth = 0; depth < CLIFF_PAIRS; depth++)
            {
                for (int s = 0; s < 2; s++)
                {
                    // ⚠️ 같은 깊이의 좌·우에 같은 텍스처를 주면 안 된다 — 왼쪽은 오른쪽을
                    //    뒤집은 것이라 회랑이 통째로 거울처럼 보인다. 한쪽을 절반만큼
                    //    어긋내면 좌우가 늘 다르고, 한쪽 벽 안에서도 4칸 주기로 돈다.
                    char suffix = (char)('a' + ((depth + s * (CLIFF_TEXTURE_COUNT / 2))
                                                % CLIFF_TEXTURE_COUNT));
                    string sideName = s == 0 ? "L" : "R";
                    cliffs[depth * 2 + s] = CreateDepthLayer(cliffRoot,
                        $"Cliff{depth}{sideName}", $"title_cliff_{suffix}",
                        new Vector2(CLIFF_WIDTH, CLIFF_HEIGHT),
                        new Vector2(CLIFF_PIVOT_X, CLIFF_PIVOT_Y));
                }
            }

            var moteGo = CreateRect(driftGo.transform, "Motes", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)moteGo.transform);

            // 안개와 비네트는 드리프트 밖 — 화면에 붙어 있어야 한다.
            var fog = CreateLayer(backdropGo.transform, "Fog", "title_fog");
            var vignette = CreateLayer(backdropGo.transform, "Vignette", "title_vignette");

            var backdrop = backdropGo.AddComponent<TitleBackdrop>();
            WireArray(backdrop, "cliffs", cliffs);
            WireArray(backdrop, "roadMarks", marks);
            SetPrivateField(backdrop, "fog", fog);
            SetPrivateField(backdrop, "driftRoot", driftRect);
            SetPrivateField(backdrop, "cliffRoot", cliffRoot);
            SetPrivateField(backdrop, "horizon", horizon.rectTransform);
            SetPrivateField(backdrop, "moteRoot", (RectTransform)moteGo.transform);
            SetPrivateField(backdrop, "moteTexture", LoadBackdropTexture("title_mote"));

            if (sky.texture == null || vignette.texture == null)
            {
                Debug.LogWarning("[TitleSceneBuilder] 배경 텍스처가 없습니다 — "
                    + "'python Tools/PixelArt/generate_title_bg.py' 먼저 실행할 것. "
                    + "배경 없이도 씬은 생성되며 단색 배경으로 보인다.");
            }
            return backdrop;
        }

        /// <summary>깊이 순환 레이어 한 벌의 전용 부모. 형제 순서 재배치를 벌 안으로 가둔다.</summary>
        private static RectTransform CreateRing(Transform parent, string name)
        {
            var go = CreateRect(parent, name, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var rect = (RectTransform)go.transform;
            Stretch(rect);
            return rect;
        }

        /// <summary>
        /// 깊이 순환 레이어 하나(절벽 조각 또는 길바닥 무늬).
        /// 크기는 텍스처 원본 치수로 고정하고, pivot은 <b>소실점에 정렬될 지점</b>에 맞춘다 —
        /// 절벽은 밑동 안쪽 모서리, 길바닥 무늬는 바닥선이다.
        /// </summary>
        private static RawImage CreateDepthLayer(Transform parent, string name, string textureName,
            Vector2 size, Vector2 pivot)
        {
            var go = CreateRect(parent, name,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pivot,
                Vector2.zero, size);

            var image = go.AddComponent<RawImage>();
            image.raycastTarget = false;

            var texture = LoadBackdropTexture(textureName);
            if (texture != null) image.texture = texture;
            else image.enabled = false;   // 텍스처가 비면 RawImage는 흰 사각형을 그린다
            return image;
        }

        /// <summary>배열 필드는 요소별로 채워야 한다(objectReference 한 번으로 안 된다).</summary>
        private static void WireArray(TitleBackdrop backdrop, string fieldName, RawImage[] items)
        {
            var so = new SerializedObject(backdrop);
            var array = so.FindProperty(fieldName);
            if (array == null)
            {
                Debug.LogWarning($"[TitleSceneBuilder] TitleBackdrop.{fieldName} 필드를 찾지 못했습니다.");
                return;
            }

            array.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
            so.ApplyModifiedProperties();
        }

        /// <summary>화면 전체를 덮는 정적 레이어.</summary>
        private static RawImage CreateLayer(Transform parent, string name, string textureName)
        {
            var go = CreateRect(parent, name, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)go.transform);

            var image = go.AddComponent<RawImage>();
            image.raycastTarget = false;

            var texture = LoadBackdropTexture(textureName);
            if (texture != null) image.texture = texture;
            else image.enabled = false;
            return image;
        }

        /// <summary>임포트 설정(Texture/Clamp/밉맵 없음)을 보장한 뒤 로드한다.</summary>
        private static Texture2D LoadBackdropTexture(string textureName)
        {
            string path = $"{AbyssPaths.TitleBackdrop}/{textureName}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[TitleSceneBuilder] 배경 텍스처 없음: {path}");
                return null;
            }

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                dirty = true;
            }
            // 모든 레이어가 화면 고정 또는 중심 확대라 가로로 반복할 일이 없다.
            // Repeat이면 확대·드리프트 시 반대편 그림이 딸려 들어온다.
            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                dirty = true;
            }
            if (importer.mipmapEnabled)
            {
                // 깊이 레이어는 2.6배까지 확대되므로 축소용 밉맵은 메모리만 먹는다.
                importer.mipmapEnabled = false;
                dirty = true;
            }
            // 길바닥 무늬가 2432px, 절벽이 2099px, 하늘·길이 2160x1320이다.
            // 기본 상한(2048)이면 조용히 축소된다.
            if (importer.maxTextureSize < 4096)
            {
                importer.maxTextureSize = 4096;
                dirty = true;
            }
            // ⚠️ 기본값 ToNearest면 3072x1152 같은 비2의거듭제곱 텍스처가 가장 가까운 POT로
            //    리샘플된다 — 피벗이 미묘하게 어긋나고 결이 뭉개진다.
            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                dirty = true;
            }
            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }
            if (dirty) importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) Debug.LogWarning($"[TitleSceneBuilder] 텍스처 로드 실패: {path}");
            return texture;
        }

        /// <summary>스트레치된 RectTransform을 사방으로 margin만큼 넓힌다.</summary>
        private static void Inflate(RectTransform rect, float margin)
        {
            rect.offsetMin = new Vector2(-margin, -margin);
            rect.offsetMax = new Vector2(margin, margin);
        }
    }
}
#endif

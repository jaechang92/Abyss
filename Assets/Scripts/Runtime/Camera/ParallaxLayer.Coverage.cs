using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Runtime.Camera
{
    /// <summary>
    /// 최후방 반복 레이어(하늘)가 카메라 화면 전체를 덮게 한다(C3). C 프로필(세로 16.875유닛)은 기존 하늘
    /// (95px x4 = 11.875유닛)보다 커서 위아래가 비었다 — 데드존·경계만으로는 못 메운다.
    ///
    /// <list type="bullet">
    /// <item><b>대상 판별(Start 1회)</b>: 같은 최상위 루트 아래 ParallaxLayer 중 깊이가 가장 작고(1 미만), wrapWidth &gt; 0 이며,
    /// 자기 렌더러 없이 같은 스프라이트 조각만 자식으로 가진 레이어. 고유 그림(방 배경·로비 파노라마)·충돌·장식·지면·투명 앞 레이어는 대상이 아니다.</item>
    /// <item><b>배율</b>: 화면 세로+여유를 덮는 가장 작은 <b>정수</b> 배율(씬 저장 배율보다 작게는 안 줄인다). 원본 비율 유지.
    /// 매번 절대값으로 정하므로 누적되지 않는다. wrapWidth 는 「한 장 폭 x 배율」로 같이 맞춘다.</item>
    /// <item><b>칸 수</b>: 반 칸 wrap 기준으로 화면 반폭을 덮는 만큼만 기존 조각을 복제해 늘린다. 늘리기만 하고 상한이 있다.</item>
    /// <item><b>갱신 시점</b>: ortho·화면 비율이 바뀐 프레임에만 다시 계산한다(ArtTest 수동 확대축소·창 크기 변경). 프레임마다 검색하지 않는다.</item>
    /// <item><b>세로</b>: 시차로 가장자리가 화면 안에 들어오면 그 프레임 위치만 밀어 넣는다. 기준점은 그대로라 쌓이지 않는다.</item>
    /// </list>
    /// </summary>
    public sealed partial class ParallaxLayer
    {
        // 화면 위아래에 남길 여유(유닛). 흔들림·시차 한 프레임 오차를 흡수한다.
        private const float COVERAGE_MARGIN = 0.25f;
        // 조각 상한(한쪽 기준). 비정상적인 확대에서도 생성이 끝없이 늘지 않게 한다.
        private const int MAX_COVERAGE_HALF_TILES = 6;

        private bool isCoverageLayer;
        private UnityEngine.Camera coverageCamera;
        private Sprite coverageSprite;
        private Transform coverageTemplate;
        private float coveragePieceY;
        private float coverageBaseScale;
        private float coverageScale;
        private float coverageTileWidth;
        private int coverageMinIndex;
        private int coverageMaxIndex;
        private float lastCoverageOrtho = -1f;
        private float lastCoverageAspect = -1f;

        /// <summary>이 레이어가 화면 덮기 대상(최후방 반복 레이어)인가(검증·디버그용).</summary>
        public bool IsCoverageLayer => isCoverageLayer;

        private void InitCoverage()
        {
            isCoverageLayer = false;
            if (cam == null || wrapWidth <= 0f || depth >= 1f) return;
            if (!cam.TryGetComponent(out coverageCamera) || !coverageCamera.orthographic) return;
            if (GetComponent<Renderer>() != null || !IsRearmostInRoot()) return;

            coverageBaseScale = transform.localScale.y;
            if (coverageBaseScale <= 0f || !CollectPieces()) return;

            // 빌더가 넣은 wrapWidth 가 「한 장 폭 x 배율」과 다르면 구성을 모르는 레이어다 — 건드리지 않는다.
            if (Mathf.Abs(coverageTileWidth * coverageBaseScale - wrapWidth) > 0.01f) return;

            coverageScale = coverageBaseScale;
            isCoverageLayer = true;
        }

        // 같은 최상위 루트 아래(꺼진 것 포함) 더 먼 레이어가 없으면 최후방이다.
        private bool IsRearmostInRoot()
        {
            foreach (var other in transform.root.GetComponentsInChildren<ParallaxLayer>(true))
            {
                if (other != this && other.depth < depth) return false;
            }
            return true;
        }

        // 자식이 전부 「같은 스프라이트 하나만 그리는 조각」이고 한 장 폭 간격으로 빈틈없이 이어져 있어야 한다.
        private bool CollectPieces()
        {
            if (transform.childCount == 0) return false;

            coverageSprite = null;
            coverageMinIndex = int.MaxValue;
            coverageMaxIndex = int.MinValue;
            var indices = new HashSet<int>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var piece = transform.GetChild(i);
                if (piece.childCount > 0 || piece.GetComponents<Component>().Length != 2) return false; // Transform + SpriteRenderer 만
                if (!piece.TryGetComponent<SpriteRenderer>(out var renderer) || renderer.sprite == null) return false;
                if (renderer.drawMode != SpriteDrawMode.Simple || piece.localScale != Vector3.one) return false;

                if (coverageSprite == null)
                {
                    coverageSprite = renderer.sprite;
                    coverageTemplate = piece;
                    coveragePieceY = piece.localPosition.y;
                    coverageTileWidth = coverageSprite.bounds.size.x;
                    if (coverageTileWidth <= 0f) return false;
                }
                else if (renderer.sprite != coverageSprite || !Mathf.Approximately(piece.localPosition.y, coveragePieceY))
                {
                    return false;
                }

                float slot = piece.localPosition.x / coverageTileWidth;
                int index = Mathf.RoundToInt(slot);
                if (Mathf.Abs(slot - index) > 0.01f || !indices.Add(index)) return false;
                coverageMinIndex = Mathf.Min(coverageMinIndex, index);
                coverageMaxIndex = Mathf.Max(coverageMaxIndex, index);
            }
            return coverageMaxIndex - coverageMinIndex + 1 == indices.Count;
        }

        private void RefreshCoverage()
        {
            if (!isCoverageLayer || coverageCamera == null) return;

            float ortho = coverageCamera.orthographicSize;
            float aspect = coverageCamera.aspect;
            if (ortho == lastCoverageOrtho && aspect == lastCoverageAspect) return;
            lastCoverageOrtho = ortho;
            lastCoverageAspect = aspect;

            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            float parentX = Mathf.Abs(parentScale.x);
            float parentY = Mathf.Abs(parentScale.y);
            if (parentX <= 0f || parentY <= 0f) return;

            float viewHeight = ortho * 2f;
            float viewWidth = viewHeight * aspect;

            // 세로: 화면+여유를 덮는 가장 작은 정수 배율. 원본 비율 유지(가로·세로 같은 배율).
            float needed = (viewHeight + COVERAGE_MARGIN * 2f) / (coverageSprite.bounds.size.y * parentY);
            float scale = Mathf.Max(coverageBaseScale, Mathf.Ceil(needed));
            if (scale != coverageScale || transform.localScale.y != scale)
            {
                coverageScale = scale;
                transform.localScale = Vector3.one * scale;
            }
            wrapWidth = coverageTileWidth * coverageScale * parentX;

            // 가로: 반 칸 wrap 이면 카메라와 가운데 칸의 거리 ≤ 한 장/2. 가운데 칸 기준 halfTiles 장씩 있으면
            // 덮는 반폭 = (halfTiles + 0.5)·한 장 - 한 장/2 = halfTiles·한 장 ≥ 화면 반폭 + 여유.
            int halfTiles = Mathf.Clamp(Mathf.CeilToInt((viewWidth * 0.5f + COVERAGE_MARGIN) / wrapWidth), 1, MAX_COVERAGE_HALF_TILES);
            EnsurePieces(-halfTiles, halfTiles);
        }

        // 모자란 칸만 기존 조각을 복제해 붙인다. 지우지 않는다(다시 좁아져도 남은 칸은 화면 밖에서 무해하다).
        private void EnsurePieces(int minIndex, int maxIndex)
        {
            if (coverageTemplate == null) return;
            for (int index = minIndex; index < coverageMinIndex; index++) CreatePiece(index);
            for (int index = coverageMaxIndex + 1; index <= maxIndex; index++) CreatePiece(index);
            coverageMinIndex = Mathf.Min(coverageMinIndex, minIndex);
            coverageMaxIndex = Mathf.Max(coverageMaxIndex, maxIndex);
        }

        private void CreatePiece(int index)
        {
            var piece = Instantiate(coverageTemplate, transform, false);
            piece.name = $"{name}_cover{index}";
            piece.localPosition = new Vector3(coverageTileWidth * index, coveragePieceY, coverageTemplate.localPosition.z);
        }

        private void KeepVerticalCoverage()
        {
            if (!isCoverageLayer || coverageCamera == null) return;

            float halfHeight = coverageCamera.orthographicSize;
            float cameraY = cam.position.y;
            float scaleY = transform.lossyScale.y;
            Bounds bounds = coverageSprite.bounds;
            float layerY = transform.position.y;
            float bottom = layerY + (coveragePieceY + bounds.min.y) * scaleY;
            float top = layerY + (coveragePieceY + bounds.max.y) * scaleY;

            float shift = 0f;
            if (bottom > cameraY - halfHeight) shift = cameraY - halfHeight - bottom;
            else if (top < cameraY + halfHeight) shift = cameraY + halfHeight - top;
            if (shift != 0f) transform.position += new Vector3(0f, shift, 0f);
        }
    }
}

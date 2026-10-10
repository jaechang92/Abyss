using Abyss.Runtime.ArtIntegration;
using Abyss.Runtime.Enemy;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// <see cref="CodexPanel"/>의 A2 그림 파트(2026-10-09) — 탭 아이콘 6종 · 항목 프레임 · 상세 초상 프레임 · 보스 초상.
    ///
    /// 조립은 BuildUI 끝에서 한 번(<see cref="ApplyArt"/>) — 도감 인스턴스는 하나라 다시 열어도 누적되지 않는다.
    /// 보스 탭 아이콘은 <c>enemyId</c> 로 고른 초상, <b>미발견 보스는 정체를 드러내지 않는 「미발견」 그림</b>으로 대신한다 —
    /// 실제 초상을 어둡게 칠한 실루엣은 윤곽으로 정체가 드러난다. 일반 적 탭도 같은 「미발견」 그림을 쓰고,
    /// 발견한 적은 A1 전용 atlas 프레임으로 그린다. 그림이 없으면 기존 표시(프리팹 그림·실루엣)로 돌아간다.
    /// </summary>
    public sealed partial class CodexPanel
    {
        private const string TAB_ICON_NAME = "ArtTabIcon";
        private const string ENTRY_FRAME_NAME = "ArtEntryFrame";
        private const string PORTRAIT_FRAME_NAME = "ArtPortraitFrame";

        private const float TAB_ICON_SIZE = 34f;
        private const float TAB_ICON_LEFT = 6f;
        private const float TAB_TEXT_LEFT = 42f;

        // 상세 초상 칸: 이름 위 가장자리(84) + 6 ~ 상세 위 가장자리(265) - 6. 상세 중심 기준.
        private const float PORTRAIT_BOX_BOTTOM = 90f;
        private const float PORTRAIT_BOX_TOP = 259f;

        /// <summary>BuildUI 끝에서 한 번. 그림이 없는 요소는 기존 모양 그대로다.</summary>
        private void ApplyArt()
        {
            for (int i = 0; i < tabButtons.Length; i++) ApplyTabIcon(tabButtons[i], (CodexTab)i);
            for (int i = 0; i < tiles.Length; i++) ApplyEntryFrame(tiles[i]);
            ApplyPortraitFrame();
        }

        private static string TabIconKey(CodexTab tab) => tab switch
        {
            CodexTab.Form => UiArtKeys.CODEX_TAB_FORM,
            CodexTab.Skill => UiArtKeys.CODEX_TAB_SKILL,
            CodexTab.Enemy => UiArtKeys.CODEX_TAB_ENEMY,
            CodexTab.Boss => UiArtKeys.CODEX_TAB_BOSS,
            CodexTab.Relic => UiArtKeys.CODEX_TAB_RELIC,
            CodexTab.Records => UiArtKeys.CODEX_TAB_RECORDS,
            _ => null
        };

        /// <summary>탭 왼쪽에 아이콘, 글자는 아이콘 칸 오른쪽에서 가운데 정렬. 버튼 색 전환·콜백은 그대로.</summary>
        private static void ApplyTabIcon(Button button, CodexTab tab)
        {
            if (button == null || button.transform is not RectTransform rect) return;

            var size = UiArtDecor.SizeOf(rect);
            var box = new Rect(-size.x * 0.5f + TAB_ICON_LEFT, -TAB_ICON_SIZE * 0.5f, TAB_ICON_SIZE, TAB_ICON_SIZE);
            var icon = UiArtDecor.ApplyIconInRect(rect, TAB_ICON_NAME, TabIconKey(tab), box, Color.white);
            if (icon == null) return;

            if (rect.Find("Text") is RectTransform text)
            {
                text.offsetMin = new Vector2(TAB_TEXT_LEFT, text.offsetMin.y);
            }
        }

        /// <summary>항목 프레임은 타일 배경 안에 맞춰 배경 바로 위·아이콘 아래에 둔다(선택 외곽선·버튼 색은 그대로).</summary>
        private static void ApplyEntryFrame(TileView tile)
        {
            if (tile.Root == null || tile.Background == null) return;
            var background = tile.Background.rectTransform;
            var root = (RectTransform)tile.Root.transform;

            var frame = UiArtDecor.ApplyFittedOver(root, ENTRY_FRAME_NAME, background, UiArtKeys.CODEX_ENTRY_FRAME, UiArtFit.Inside, out _);
            if (frame == null) return;
            frame.transform.SetSiblingIndex(background.GetSiblingIndex() + 1);
        }

        /// <summary>
        /// 상세 초상 프레임: 이름과 상세 위 가장자리 사이 칸에 종횡비대로 맞추고, 아이콘·글리프·어두운 칸을 프레임 개구부로 옮긴다.
        /// </summary>
        private void ApplyPortraitFrame()
        {
            if (detailRoot == null || detailIcon == null) return;
            if (!UiArtLibrary.TryGetLayout(UiArtKeys.CODEX_PORTRAIT_FRAME, out var layout) || !layout.HasInner) return;
            var sprite = UiArtLibrary.Get(UiArtKeys.CODEX_PORTRAIT_FRAME);
            if (sprite == null) return;

            var root = (RectTransform)detailRoot.transform;
            var rootSize = UiArtDecor.SizeOf(root);
            var box = Rect.MinMaxRect(-rootSize.x * 0.5f, PORTRAIT_BOX_BOTTOM, rootSize.x * 0.5f, PORTRAIT_BOX_TOP);
            var placement = UiArtDecor.Place(layout, box.size, UiArtFit.Inside);
            var framed = new UiArtPlacement(placement.Size, box.center + placement.Center);
            var inner = framed.ToTarget(layout.Inner);

            var frame = UiArtDecor.EnsureImage(root, PORTRAIT_FRAME_NAME);
            frame.sprite = sprite;
            frame.color = Color.white;
            frame.enabled = true;
            UiArtDecor.SetCentered(frame.rectTransform, framed.Center, framed.Size);

            UiArtDecor.SetCentered(detailIcon.rectTransform, inner.center, inner.size);
            if (detailGlyph != null) UiArtDecor.SetCentered(detailGlyph.rectTransform, inner.center, inner.size);
            if (root.Find("IconFrame") is RectTransform backing) UiArtDecor.SetCentered(backing, inner.center, inner.size);

            // 프레임은 글리프 바로 뒤 — 개구부 밖 띠만 그려지므로 아이콘을 가리지 않고, 이름 글자보다는 앞서 그려진다.
            var anchor = detailGlyph != null ? detailGlyph.transform : detailIcon.transform;
            frame.transform.SetSiblingIndex(anchor.GetSiblingIndex() + 1);
        }

        /// <summary>
        /// 적·보스 항목의 아이콘(흰 색조 그대로).
        /// <list type="bullet">
        /// <item>미발견 — 두 탭 모두 정체를 드러내지 않는 「미발견」 그림. 없으면 기존 실루엣.</item>
        /// <item>발견한 보스 — enemyId 초상. 없으면 아래 적 그림 규칙.</item>
        /// <item>발견한 적 — 실제 전투와 같은 A1 전용 atlas 이동 행 첫 프레임(엘리트 변종 포함). 빌린 그림이 아니라 몸 색을 입히지 않는다.</item>
        /// </list>
        /// atlas 가 없으면 기존 규칙(프리팹 그림 · 프리팹 색)으로 돌아간다.
        /// </summary>
        private static (Sprite sprite, Color tint) ResolveEnemyArt(EnemyData enemy, bool found, bool bossTab)
        {
            if (!found)
            {
                var unknown = UiArtLibrary.Get(UiArtKeys.CODEX_UNDISCOVERED);
                return unknown != null ? (unknown, Color.white) : (ResolvePortrait(enemy).sprite, SilhouetteColor);
            }

            if (bossTab)
            {
                var art = UiArtLibrary.Get(UiArtKeys.BossPortrait(enemy.enemyId));
                if (art != null) return (art, Color.white);
            }

            var atlasFrame = ResolveAtlasFrame(enemy);
            if (atlasFrame != null) return (atlasFrame, Color.white);

            return ResolvePortrait(enemy);
        }

        /// <summary>
        /// A1 atlas 이동 행 첫 프레임. 발 높이 0 — 그림 자식(Visual)용 묶음과 캐시를 공유한다
        /// (UI Image 는 피벗을 쓰지 않아 발 높이와 무관하다). 없으면 null.
        /// </summary>
        private static Sprite ResolveAtlasFrame(EnemyData enemy)
        {
            var move = EnemyAtlasLibrary.TryGet(enemy.enemyId, 0f)?.Get(EnemyAtlasRowIds.Move);
            return move != null && move.FrameCount > 0 ? move.Sprites[0] : null;
        }
    }
}

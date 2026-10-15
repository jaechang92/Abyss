using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    public sealed partial class FormSlotPresenter
    {
        private enum SwapStatus { NoOtherForm, Waiting, Ready, Blocked }

        private const int SWAP_STATUS_FONT_SIZE = 24;
        private const float SWAP_STATUS_GAP = 12f;
        private const float SWAP_STATUS_PADDING = 8f;
        private static readonly Color SwapStatusBodyColor = new(0.906f, 0.878f, 0.808f);
        private static readonly Color SwapStatusBackColor = new(0.090f, 0.129f, 0.169f, 0.96f);

        private RectTransform swapStatusRoot;
        private RectTransform swapStatusRow;
        private Text swapStatusLabel;
        private Vector2 swapStatusBaseSize;
        private SwapStatus? shownSwapStatus;
        private int shownSwapSeconds = -1;
        private float shownSwapScale = -1f;
        private Vector2 swapStatusExpandedSize;

        private void RefreshSwapStatus()
        {
            if (!isActiveAndEnabled || formController == null)
            {
                HideSwapStatus();
                return;
            }
            if (!EnsureSwapStatus()) return;

            // CanSwap의 피격/시전/사망 게이트를 여기서 복제하지 않는다.
            float cooldown = formController.CurrentCooldown;
            var status = formController.OtherForm == null ? SwapStatus.NoOtherForm
                : cooldown > 0f ? SwapStatus.Waiting
                : formController.CanSwap ? SwapStatus.Ready : SwapStatus.Blocked;
            int seconds = status == SwapStatus.Waiting ? Mathf.Max(1, Mathf.CeilToInt(cooldown)) : 0;
            bool changed = shownSwapStatus != status || shownSwapSeconds != seconds;
            if (changed)
            {
                // 현재 한국어 HUD 범위. 공용 번역 키/CSV와 교체 로직은 변경하지 않는다.
                swapStatusLabel.text = status switch
                {
                    SwapStatus.NoOtherForm => "대기 폼 없음",
                    SwapStatus.Waiting => $"교체 대기 {seconds}초",
                    SwapStatus.Ready => "교체 가능",
                    _ => "교체 불가"
                };
                shownSwapStatus = status;
                shownSwapSeconds = seconds;
            }

            float scale = MenuTextReadability.Scale;
            if (changed || shownSwapScale != scale)
            {
                LayoutSwapStatus(scale);
                shownSwapScale = scale;
            }
            // 재활성/반복 Bind는 원본 크기에서 재계산한 동일 높이를 사용한다.
            SetSwapStatusRootSize(swapStatusExpandedSize);
            if (!swapStatusRow.gameObject.activeSelf) swapStatusRow.gameObject.SetActive(true);
        }

        private bool EnsureSwapStatus()
        {
            if (swapStatusLabel != null) return true;
            if (transform is not RectTransform root) return false;
            swapStatusRoot = root;
            // HUDPresenter.Start의 HudArtSkin 적용 후 최초 Bind/Update 시에만 기록한다.
            swapStatusBaseSize = root.rect.size;

            var existing = root.Find("SwapStatusRow");
            if (existing == null)
            {
                var row = new GameObject("SwapStatusRow", typeof(RectTransform), typeof(Image));
                row.layer = gameObject.layer;
                row.transform.SetParent(root, false);
                existing = row.transform;
            }
            swapStatusRow = existing as RectTransform;
            if (swapStatusRow == null) return false;
            if (!existing.TryGetComponent(out Image back)) back = existing.gameObject.AddComponent<Image>();
            back.color = SwapStatusBackColor;
            back.raycastTarget = false;

            var label = existing.Find("SwapStatusLabel");
            if (label == null)
            {
                var go = new GameObject("SwapStatusLabel", typeof(RectTransform), typeof(Text));
                go.layer = gameObject.layer;
                go.transform.SetParent(existing, false);
                label = go.transform;
            }
            if (!label.TryGetComponent(out swapStatusLabel)) swapStatusLabel = label.gameObject.AddComponent<Text>();
            UiFactory.ApplyFont(swapStatusLabel);
            swapStatusLabel.color = SwapStatusBodyColor;
            swapStatusLabel.alignment = TextAnchor.MiddleLeft;
            swapStatusLabel.raycastTarget = false;
            swapStatusLabel.resizeTextForBestFit = false;
            swapStatusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            swapStatusLabel.verticalOverflow = VerticalWrapMode.Overflow;
            var rect = swapStatusLabel.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(SWAP_STATUS_PADDING, SWAP_STATUS_PADDING);
            rect.offsetMax = new Vector2(-SWAP_STATUS_PADDING, -SWAP_STATUS_PADDING);
            shownSwapStatus = null;
            shownSwapScale = -1f;
            return true;
        }

        private void LayoutSwapStatus(float scale)
        {
            swapStatusLabel.fontSize = Mathf.RoundToInt(SWAP_STATUS_FONT_SIZE * scale);
            float width = swapStatusBaseSize.x * scale;
            float textWidth = Mathf.Max(1f, width - SWAP_STATUS_PADDING * 2f);
            var settings = swapStatusLabel.GetGenerationSettings(new Vector2(textWidth, 0f));
            float textHeight = swapStatusLabel.cachedTextGeneratorForLayout.GetPreferredHeight(
                swapStatusLabel.text, settings) / swapStatusLabel.pixelsPerUnit;
            float height = Mathf.Max(swapStatusLabel.fontSize, textHeight) + SWAP_STATUS_PADDING * 2f;
            var topLeft = new Vector2(0f, 1f);
            swapStatusRow.anchorMin = swapStatusRow.anchorMax = topLeft;
            swapStatusRow.pivot = topLeft;
            swapStatusRow.anchoredPosition = new Vector2(0f, -(swapStatusBaseSize.y + SWAP_STATUS_GAP));
            swapStatusRow.sizeDelta = new Vector2(width, height);
            // 루트 크기 변경 전 기존 내용의 실제 위치와 크기를 고정한다.
            // 중앙 앵커인 전용 아트 프레임도 아이콘 위의 현재 위치를 유지한다.
            swapStatusExpandedSize = new Vector2(width, swapStatusBaseSize.y + SWAP_STATUS_GAP + height);
        }

        private void HideSwapStatus()
        {
            if (swapStatusRow != null) swapStatusRow.gameObject.SetActive(false);
            if (swapStatusRoot != null) SetSwapStatusRootSize(swapStatusBaseSize);
        }

        private void SetSwapStatusRootSize(Vector2 size)
        {
            if (swapStatusRoot.rect.size == size) return;
            // 스킨 프레임은 중앙 앵커일 수 있다. 루트 pivot에 고정하면 크기가 변해도
            // 앵커 기준점이 움직이지 않는다. 위치·크기·형제 순서·자식 badge는 보존한다.
            for (int i = 0; i < swapStatusRoot.childCount; i++)
            {
                if (swapStatusRoot.GetChild(i) is not RectTransform child || child == swapStatusRow) continue;
                Vector2 contentSize = child.rect.size;
                Vector3 contentPosition = child.localPosition;
                child.anchorMin = child.anchorMax = swapStatusRoot.pivot;
                child.sizeDelta = contentSize;
                child.localPosition = contentPosition;
            }
            // HudStatusReadability가 LateUpdate에서 이 실제 높이 아래로 경고·시너지를 옮긴다.
            if (swapStatusRoot.rect.width != size.x)
                swapStatusRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            if (swapStatusRoot.rect.height != size.y)
                swapStatusRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }
    }
}

using Abyss.Runtime.ArtIntegration;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>프레임 그림을 대상 사각형에 맞추는 방식.</summary>
    public enum UiArtFit
    {
        /// <summary>보이는 그림 전체가 대상 안에 들어간다(종횡비 유지).</summary>
        Inside,

        /// <summary>보이는 그림이 대상을 덮는다(종횡비 유지, 짧은 축이 밖으로 나온다).</summary>
        Cover,

        /// <summary>그림의 개구부가 대상(+여백)을 감싼다 — 내용은 개구부 안, 띠는 바깥.</summary>
        AroundInner
    }

    /// <summary>
    /// 대상 사각형 기준 프레임 배치 결과. 좌표는 대상 중심 원점(uGUI 단위, y 위).
    /// </summary>
    public readonly struct UiArtPlacement
    {
        public readonly Vector2 Size;
        public readonly Vector2 Center;

        public UiArtPlacement(Vector2 size, Vector2 center)
        {
            Size = size;
            Center = center;
        }

        public bool IsValid => Size.x > 0f && Size.y > 0f;

        /// <summary>보이는 그림 기준 0..1 사각형(아래→위)을 대상 중심 좌표로 옮긴다.</summary>
        public Rect ToTarget(Rect normalized)
        {
            float left = Center.x - Size.x * 0.5f + normalized.xMin * Size.x;
            float bottom = Center.y - Size.y * 0.5f + normalized.yMin * Size.y;
            return new Rect(left, bottom, normalized.width * Size.x, normalized.height * Size.y);
        }
    }

    /// <summary>
    /// A2 UI 그림 배치 공용 도우미. 그림 로드·자르기는 <see cref="UiArtLibrary"/>, 여기는 uGUI 오브젝트만 다룬다.
    ///
    /// 🔑 규약 — 모든 장식은 <b>이름 고정 자식</b>이라 다시 불러도 같은 오브젝트를 재사용한다(재오픈·재진입 누적 없음).
    /// raycastTarget=false 라 클릭·Navigation·선택에 끼어들지 않는다. 그림이 없으면 아무것도 만들지 않고 false/null.
    /// 투명 여백은 Sprite rect 단계에서 이미 잘려 있으므로 여기 크기는 「보이는 그림」 기준이다.
    /// </summary>
    public static class UiArtDecor
    {
        /// <summary>대상 크기·방식으로 프레임 크기·중심을 계산한다. 개구부가 없는데 AroundInner 면 Inside 로 대신한다.</summary>
        public static UiArtPlacement Place(UiArtLayout layout, Vector2 targetSize, UiArtFit fit, float padding = 0f)
        {
            var visible = layout.VisibleSize;
            if (visible.x <= 0f || visible.y <= 0f || targetSize.x <= 0f || targetSize.y <= 0f) return default;

            if (fit == UiArtFit.AroundInner && layout.HasInner)
            {
                float innerWidth = layout.Inner.width * visible.x;
                float innerHeight = layout.Inner.height * visible.y;
                float scale = Mathf.Max((targetSize.x + padding * 2f) / innerWidth, (targetSize.y + padding * 2f) / innerHeight);
                var size = visible * scale;
                // 개구부 중심이 대상 중심에 오도록 프레임을 민다(개구부가 그림 가운데에 있지 않을 수 있다).
                var center = new Vector2((0.5f - layout.Inner.center.x) * size.x, (0.5f - layout.Inner.center.y) * size.y);
                return new UiArtPlacement(size, center);
            }

            float fitScale = fit == UiArtFit.Cover
                ? Mathf.Max(targetSize.x / visible.x, targetSize.y / visible.y)
                : Mathf.Min(targetSize.x / visible.x, targetSize.y / visible.y);
            return new UiArtPlacement(visible * fitScale, Vector2.zero);
        }

        /// <summary>
        /// target 의 이름 고정 자식 Image 에 그림을 맞춰 붙인다. 위치는 target 중심 기준이다.
        /// 형제 순서는 호출한 쪽이 정한다(앞/뒤가 화면마다 다르다). 그림이 없으면 기존 같은 이름 장식을 끄고 null.
        /// </summary>
        public static Image ApplyFitted(RectTransform target, string name, string key, UiArtFit fit,
            out UiArtPlacement placement, float padding = 0f)
        {
            placement = default;
            if (target == null) return null;

            var sprite = UiArtLibrary.Get(key);
            if (sprite == null || !UiArtLibrary.TryGetLayout(key, out var layout))
            {
                Hide(target, name);
                return null;
            }

            placement = Place(layout, SizeOf(target), fit, padding);
            if (!placement.IsValid)
            {
                Hide(target, name);
                return null;
            }

            var image = EnsureImage(target, name);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false; // 크기 자체를 종횡비대로 계산했다
            image.color = Color.white;
            image.enabled = true;
            image.gameObject.SetActive(true);
            SetCentered(image.rectTransform, placement.Center, placement.Size);
            return image;
        }

        /// <summary>
        /// <paramref name="over"/>(같은 부모의 형제) 위에 겹치는 프레임을 <paramref name="parent"/> 의 자식으로 붙인다.
        /// 프레임을 아이콘의 자식으로 두지 않는 이유: 쿨다운 오버레이 같은 형제보다 앞에 그려야 하는 경우가 있다.
        /// 반환 placement 의 좌표는 <paramref name="over"/> 중심 기준이다.
        /// </summary>
        public static Image ApplyFittedOver(RectTransform parent, string name, RectTransform over, string key, UiArtFit fit,
            out UiArtPlacement placement, float padding = 0f)
        {
            placement = default;
            if (parent == null || over == null) return null;

            var sprite = UiArtLibrary.Get(key);
            if (sprite == null || !UiArtLibrary.TryGetLayout(key, out var layout))
            {
                Hide(parent, name);
                return null;
            }

            placement = Place(layout, SizeOf(over), fit, padding);
            if (!placement.IsValid)
            {
                Hide(parent, name);
                return null;
            }

            var image = EnsureImage(parent, name);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
            image.enabled = true;
            image.gameObject.SetActive(true);
            SetCentered(image.rectTransform, CenterInParent(over, parent) + placement.Center, placement.Size);
            return image;
        }

        /// <summary>형제 사각형의 중심을 부모 중심 기준 좌표로.</summary>
        public static Vector2 CenterInParent(RectTransform child, RectTransform parent)
        {
            var size = SizeOf(child);
            var pivotOffset = Vector2.Scale(new Vector2(0.5f, 0.5f) - child.pivot, size);
            var parentRect = parent.rect;
            return (Vector2)child.localPosition + pivotOffset - parentRect.center;
        }

        /// <summary>정해진 칸(target 중심 기준 사각형) 안에 아이콘을 종횡비대로 맞춰 넣는다.</summary>
        public static Image ApplyIconInRect(RectTransform parent, string name, string key, Rect box, Color tint)
        {
            if (parent == null) return null;
            var sprite = UiArtLibrary.Get(key);
            if (sprite == null || !UiArtLibrary.TryGetLayout(key, out var layout))
            {
                Hide(parent, name);
                return null;
            }

            var placement = Place(layout, box.size, UiArtFit.Inside);
            var image = EnsureImage(parent, name);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = tint;
            image.enabled = true;
            image.gameObject.SetActive(true);
            SetCentered(image.rectTransform, box.center, placement.Size);
            return image;
        }

        /// <summary>
        /// 글자 앞(왼쪽)에 붙는 화폐·상태 아이콘. 글자 정렬을 읽어 실제 글자가 시작하는 자리 바로 왼쪽에 둔다 —
        /// 가운데 정렬 글자에 사각형 끝을 기준으로 붙이면 아이콘이 글자와 멀어진다. 글자 값이 바뀌는 갱신 경로에서 다시 부른다.
        /// </summary>
        public static Image PlaceLeadingIcon(Text label, string key, float size, float gap = 6f)
        {
            if (label == null) return null;
            var rect = label.rectTransform;
            var labelSize = SizeOf(rect);

            float textWidth = Mathf.Min(label.preferredWidth, labelSize.x);
            float textLeft = -labelSize.x * 0.5f;
            switch (label.alignment)
            {
                case TextAnchor.UpperCenter:
                case TextAnchor.MiddleCenter:
                case TextAnchor.LowerCenter:
                    textLeft = -textWidth * 0.5f;
                    break;
                case TextAnchor.UpperRight:
                case TextAnchor.MiddleRight:
                case TextAnchor.LowerRight:
                    textLeft = labelSize.x * 0.5f - textWidth;
                    break;
            }

            float x = textLeft - gap - size * 0.5f;
            return ApplyIconInRect(rect, "ArtLeadingIcon", key, new Rect(x - size * 0.5f, -size * 0.5f, size, size), Color.white);
        }

        /// <summary>이름 고정 장식 Image 를 만들거나 재사용한다. 입력을 받지 않는다.</summary>
        public static Image EnsureImage(Transform parent, string name)
        {
            var existing = parent.Find(name);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(name, typeof(RectTransform));
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
            }

            if (!go.TryGetComponent(out Image image)) image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        /// <summary>같은 이름 장식이 있으면 끈다(그림이 빠진 재적용에서 옛 장식이 남지 않게).</summary>
        public static void Hide(Transform parent, string name)
        {
            if (parent == null) return;
            var existing = parent.Find(name);
            if (existing != null && existing.TryGetComponent(out Image image)) image.enabled = false;
        }

        /// <summary>부모 중심 기준 위치·크기. 앵커·피벗을 가운데로 둔다.</summary>
        public static void SetCentered(RectTransform rect, Vector2 center, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        /// <summary>
        /// 부모 중심 기준 사각형(<paramref name="box"/>)을 대상의 offsetMin/Max 로 옮긴다 — 대상은 부모에 늘어붙는 앵커가 된다.
        /// Filled 게이지 같은 기존 Image 의 설정(fillMethod·fillAmount)은 그대로 두고 범위만 바꾼다.
        /// </summary>
        public static void StretchTo(RectTransform rect, RectTransform parent, Rect box)
        {
            var parentSize = SizeOf(parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(box.xMin + parentSize.x * 0.5f, box.yMin + parentSize.y * 0.5f);
            rect.offsetMax = new Vector2(box.xMax - parentSize.x * 0.5f, box.yMax - parentSize.y * 0.5f);
        }

        /// <summary>레이아웃 전(비활성)에도 쓸 수 있는 크기. rect 가 0이면 sizeDelta 로 대신한다.</summary>
        public static Vector2 SizeOf(RectTransform rect)
        {
            if (rect == null) return Vector2.zero;
            var size = rect.rect.size;
            if (size.x <= 0f || size.y <= 0f) size = rect.sizeDelta;
            return size;
        }
    }
}

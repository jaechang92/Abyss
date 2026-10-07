using System;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.UI;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 월드 이벤트 오브젝트(R1: Stage1 깨진 제단) — 입장으로 선택 UI를 열지 않고, 다가가 상호작용(G)했을 때만 연다.
    /// 21-room-reward-flow §4 「입장 → 오브젝트를 봄 → 접근 → 상호작용 → 기존 선택 UI」.
    ///
    /// 근접 감지·발동은 기존 <see cref="PlayerInteractor"/>가 한다(트리거 콜라이더 + <see cref="IInteractable"/>).
    /// 열 수 있는지(세션 단계·모달·정지·문 전환)는 <see cref="StageDirector"/>가 판정해 넘긴다 — 이 오브젝트는 상태를 들지 않는다.
    /// 씬 배선 없이 방 진입 때 <see cref="Create"/>로 만들고, 방을 떠나면 맵 루트와 함께 치워진다.
    ///
    /// 그림은 기존 제단 스프라이트를 빌려 쓴다(임시 표현 — 최종 아트는 별도 제작). 없으면 색 사각형.
    /// </summary>
    public sealed class RoomEventInteractable : MonoBehaviour, IInteractable
    {
        private const float FALLBACK_WIDTH = 1.4f;
        private const float FALLBACK_HEIGHT = 1.2f;
        private const float TRIGGER_WIDTH = 2.2f;
        private const float TRIGGER_HEIGHT = 2.4f;
        private const float LABEL_GAP = 0.45f;
        private const int ORDER_VISUAL = -3;          // 지형(-5) 앞, 캐릭터(0) 뒤 — 보상 문과 같은 층
        private const int ORDER_LABEL = 5;
        private const float RESOLVED_ALPHA_SCALE = 0.35f;   // FormAltar 소비 표시와 같은 배율

        private static readonly Color FallbackColor = new Color(0.55f, 0.5f, 0.62f);
        private static readonly Color LabelColor = new Color(1f, 0.9f, 0.7f);

        private static Sprite whiteSprite;

        private string title;
        private Func<bool> canOpen;
        private Action onOpen;
        private SpriteRenderer visual;
        private bool isResolved;

        public string InteractionPrompt => $"{title} — 살펴본다 (G)";

        public bool CanInteract => !isResolved && canOpen != null && onOpen != null && canOpen();

        /// <summary>
        /// 발밑(<paramref name="footPosition"/>)에 서는 이벤트 오브젝트를 만든다.
        /// <paramref name="sourceVisual"/>이 있으면 그 스프라이트·크기를 빌리고, 없으면 색 사각형으로 그린다.
        /// </summary>
        public static RoomEventInteractable Create(Transform parent, Vector3 footPosition, string title,
                                                   SpriteRenderer sourceVisual, Func<bool> canOpen, Action onOpen)
        {
            var go = new GameObject("WorldEvent_" + title);
            go.transform.SetParent(parent, false);
            go.transform.position = footPosition;

            var target = go.AddComponent<RoomEventInteractable>();
            target.title = title;
            target.canOpen = canOpen;
            target.onOpen = onOpen;

            float height = target.BuildVisual(sourceVisual);
            CreateLabel(go.transform, title, new Vector3(0f, height + LABEL_GAP, 0f));

            // 상호작용 판정 — PlayerInteractor 는 트리거 진입으로 후보를 모은다(RoomExitDoor 와 같은 방식).
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(TRIGGER_WIDTH, TRIGGER_HEIGHT);
            trigger.offset = new Vector2(0f, TRIGGER_HEIGHT * 0.5f);
            return target;
        }

        public void Interact(GameObject interactor)
        {
            // 판정과 발동 사이에 상태가 바뀌었을 수 있다 — 발동 직전에 다시 묻는다.
            if (!CanInteract) return;
            onOpen.Invoke();
        }

        /// <summary>해결 표시 — 흐리게 남겨 「이미 응답한 제단」임을 알린다. 상호작용은 다시 받지 않는다.</summary>
        public void MarkResolved()
        {
            if (isResolved) return;
            isResolved = true;
            if (visual == null) return;
            var c = visual.color;
            c.a *= RESOLVED_ALPHA_SCALE;
            visual.color = c;
        }

        /// <summary>그림을 만들고 발밑에서 윗면까지의 높이를 돌려준다(라벨 위치용).</summary>
        private float BuildVisual(SpriteRenderer source)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(transform, false);
            visual = go.AddComponent<SpriteRenderer>();
            visual.sortingOrder = ORDER_VISUAL;

            if (source != null && source.sprite != null)
            {
                visual.sprite = source.sprite;
                visual.color = new Color(source.color.r, source.color.g, source.color.b, 1f);
                visual.flipX = source.flipX;
                go.transform.localScale = source.transform.lossyScale;

                // 피벗이 어디든 그림 아래 끝이 발밑에 오게 올린다.
                var bounds = source.sprite.bounds;
                float scaleY = Mathf.Abs(go.transform.localScale.y);
                go.transform.localPosition = new Vector3(0f, -bounds.min.y * scaleY, 0f);
                return bounds.size.y * scaleY;
            }

            visual.sprite = WhiteSprite;
            visual.color = FallbackColor;
            go.transform.localScale = new Vector3(FALLBACK_WIDTH, FALLBACK_HEIGHT, 1f);
            go.transform.localPosition = new Vector3(0f, FALLBACK_HEIGHT * 0.5f, 0f);
            return FALLBACK_HEIGHT;
        }

        private static void CreateLabel(Transform parent, string text, Vector3 localPosition)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = UiFactory.GetDefaultFont();
            mesh.fontSize = 48;
            mesh.characterSize = 0.09f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = LabelColor;

            var renderer = go.GetComponent<MeshRenderer>();
            if (mesh.font != null) renderer.sharedMaterial = mesh.font.material;
            renderer.sortingOrder = ORDER_LABEL;
        }

        /// <summary>1유닛 흰 사각형(피벗 가운데). 빌릴 제단 그림이 없을 때만 쓴다.</summary>
        private static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite == null)
                {
                    var texture = Texture2D.whiteTexture;
                    whiteSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), texture.width);
                }
                return whiteSprite;
            }
        }
    }
}

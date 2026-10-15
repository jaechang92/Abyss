using System;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.UI;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 정비 공간(R2)의 상인·불씨 오브젝트. 입장으로 UI를 열지 않고, 다가가 상호작용(G)했을 때만 연다.
    ///
    /// 근접 감지·발동은 기존 <see cref="PlayerInteractor"/>가 한다(트리거 콜라이더 + <see cref="IInteractable"/>) —
    /// 콜라이더·라벨 규약은 <see cref="RoomEventInteractable"/>과 같다.
    /// 열 수 있는지·무엇을 할지·프롬프트 문구는 <see cref="StageDirector"/>가 넘긴다 — 이 오브젝트는 정비 상태를 들지 않는다.
    /// 방 진입 때 <see cref="Create"/>로 만들고, 방을 떠나면 맵 루트와 함께 치워진다.
    ///
    /// 그림은 기존 채택 아트를 빌린다(없으면 색 사각형). 정비 공간 전용 최종 아트가 아니다.
    /// </summary>
    public sealed class MaintenanceInteractable : MonoBehaviour, IInteractable
    {
        private const float VISUAL_HEIGHT = 1.8f;
        private const float FALLBACK_WIDTH = 1.2f;
        private const float FALLBACK_HEIGHT = 1.4f;
        private const float TRIGGER_WIDTH = 2.2f;     // 상인(-3)·불씨(+3) 사이 6 — 두 범위가 겹치지 않는다
        private const float TRIGGER_HEIGHT = 2.4f;
        private const float LABEL_GAP = 0.45f;
        private const int ORDER_VISUAL = -3;          // 지형(-5) 앞, 캐릭터(0) 뒤 — 보상 문과 같은 층
        private const int ORDER_LABEL = 5;
        private const float USED_ALPHA_SCALE = 0.35f; // RoomEventInteractable 해결 표시와 같은 배율

        private static readonly Color LabelColor = new Color(1f, 0.9f, 0.7f);

        private static Sprite whiteSprite;

        private Func<string> prompt;
        private Func<bool> canInteract;
        private Action onInteract;
        private SpriteRenderer visual;
        private bool isUsedShown;

        public string InteractionPrompt => prompt != null ? prompt() : string.Empty;

        public bool CanInteract => canInteract != null && onInteract != null && canInteract();

        /// <summary>
        /// 발밑(<paramref name="footPosition"/>)에 서는 오브젝트를 만든다. <paramref name="art"/>가 있으면 그 그림을
        /// 아래 끝이 발밑에 오게 세우고, 없으면 <paramref name="fallbackColor"/> 사각형으로 그린다.
        /// </summary>
        public static MaintenanceInteractable Create(Transform parent, string objectName, Vector3 footPosition, string title,
                                                     Sprite art, Color fallbackColor, Func<string> prompt,
                                                     Func<bool> canInteract, Action onInteract)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.position = footPosition;

            var target = go.AddComponent<MaintenanceInteractable>();
            target.prompt = prompt;
            target.canInteract = canInteract;
            target.onInteract = onInteract;

            float height = target.BuildVisual(art, fallbackColor);
            CreateLabel(go.transform, title, new Vector3(0f, height + LABEL_GAP, 0f));

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
            onInteract.Invoke();
        }

        /// <summary>사용 완료 표시 — 흐리게 남긴다. 상호작용 여부는 디렉터 판정이 정한다(재사용 안내만 남을 수 있다).</summary>
        public void ShowUsed()
        {
            if (isUsedShown || visual == null) return;
            isUsedShown = true;
            var c = visual.color;
            c.a *= USED_ALPHA_SCALE;
            visual.color = c;
        }

        /// <summary>그림을 만들고 발밑에서 윗면까지의 높이를 돌려준다(라벨 위치용).</summary>
        private float BuildVisual(Sprite art, Color fallbackColor)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(transform, false);
            visual = go.AddComponent<SpriteRenderer>();
            visual.sortingOrder = ORDER_VISUAL;

            if (art != null && art.bounds.size.y > 0f)
            {
                visual.sprite = art;
                float scale = VISUAL_HEIGHT / art.bounds.size.y;
                go.transform.localScale = Vector3.one * scale;
                // 피벗이 어디든 그림 아래 끝이 발밑에 오게 올린다.
                go.transform.localPosition = new Vector3(0f, -art.bounds.min.y * scale, 0f);
                return VISUAL_HEIGHT;
            }

            visual.sprite = WhiteSprite;
            visual.color = fallbackColor;
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

        /// <summary>1유닛 흰 사각형(피벗 가운데). 빌릴 그림이 없을 때만 쓴다.</summary>
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

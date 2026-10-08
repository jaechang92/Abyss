using System;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Localization;
using Abyss.Runtime.UI;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 형체 보상방(room3_crowd)의 「남겨진 통행 기록」(26-expedition-discovery E3). 다가가 상호작용하면
    /// 기록 위에 짧은 글을 띄운다 — 모달·정지·보상·출구 신호 없이 읽기만 한다. 다시 읽을 수 있다.
    ///
    /// 근접 감지·발동은 기존 <see cref="PlayerInteractor"/>가 한다(트리거 + <see cref="IInteractable"/>).
    /// 읽을 수 있는지(방 세대·세션·모달·정지·상위 화면·런 활성)는 <see cref="StageDirector"/>가 판정해 넘긴다.
    /// 글은 초점이 떠나거나 읽을 수 없는 상태가 되면 즉시 닫힌다. 방을 떠나면 맵 루트와 함께 치워진다.
    ///
    /// 그림은 디렉터가 직렬화로 받은 책 스프라이트를 작게 줄여 쓴다. 없으면 색 사각형(임시 — 최종 아트 아님)으로 그린다.
    /// </summary>
    public sealed class PassageRecordInteractable : MonoBehaviour, IInteractable
    {
        private const float VISUAL_HEIGHT = 0.9f;          // 세계 단위 — 발밑의 작은 책
        private const float FALLBACK_WIDTH = 0.8f;
        private const float FALLBACK_HEIGHT = 0.55f;
        private const float TRIGGER_WIDTH = 1.4f;
        private const float TRIGGER_HEIGHT = 2f;
        private const float TITLE_GAP = 0.3f;
        private const float TEXT_GAP = 0.45f;              // 이름표 위
        private const float TITLE_CHARACTER_SIZE = 0.065f;
        private const float TEXT_CHARACTER_SIZE = 0.065f;
        private const int ORDER_VISUAL = -3;               // 지형(-5) 앞, 캐릭터(0) 뒤 — 보상 문과 같은 층
        private const int ORDER_LABEL = 5;

        private static readonly Color FallbackColor = new Color(0.55f, 0.45f, 0.32f);
        private static readonly Color TitleColor = new Color(0.85f, 0.8f, 0.68f);
        private static readonly Color TextColor = new Color(1f, 0.95f, 0.85f);

        private static Sprite whiteSprite;

        private Func<bool> canRead;
        private Action onDisplayed;
        private TextMesh titleLabel;
        private TextMesh textLabel;
        private PlayerInteractor interactor;
        private bool isTextShown;
        private LocalizationLanguage shownLanguage;
        private bool hasTitleLanguage;
        private LocalizationLanguage titleLanguage;

        public string InteractionPrompt => Loc.Get(ExpeditionTextKeys.PassageRecordPrompt);

        public bool CanInteract => canRead != null && canRead();

        /// <summary>
        /// 발밑(<paramref name="footPosition"/>)에 서는 기록을 만든다. <paramref name="onDisplayed"/>는 글이 실제로 화면에 뜬 뒤
        /// 매번 불린다(발견 저장은 받는 쪽이 멱등으로 처리한다).
        /// </summary>
        public static PassageRecordInteractable Create(Transform parent, Vector3 footPosition, Sprite sprite,
                                                       Func<bool> canRead, Action onDisplayed)
        {
            var go = new GameObject("PassageRecord");
            go.transform.SetParent(parent, false);
            go.transform.position = footPosition;

            var record = go.AddComponent<PassageRecordInteractable>();
            record.canRead = canRead;
            record.onDisplayed = onDisplayed;

            float height = record.BuildVisual(sprite);
            record.titleLabel = CreateLabel(go.transform, new Vector3(0f, height + TITLE_GAP, 0f), TITLE_CHARACTER_SIZE,
                TitleColor, TextAnchor.LowerCenter);
            record.textLabel = CreateLabel(go.transform, new Vector3(0f, height + TITLE_GAP + TEXT_GAP, 0f), TEXT_CHARACTER_SIZE,
                TextColor, TextAnchor.LowerCenter);
            record.textLabel.gameObject.SetActive(false);
            record.RefreshTitle();

            // 상호작용 판정 — PlayerInteractor 는 트리거 진입으로 후보를 모은다(RoomExitDoor 와 같은 방식).
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(TRIGGER_WIDTH, TRIGGER_HEIGHT);
            trigger.offset = new Vector2(0f, TRIGGER_HEIGHT * 0.5f);
            return record;
        }

        /// <summary>읽기 — 닫혀 있으면 글을 띄우고, 떠 있으면 닫는다. 판정과 발동 사이 상태가 바뀌었을 수 있어 다시 묻는다.</summary>
        public void Interact(GameObject interactor)
        {
            if (!CanInteract) return;
            if (isTextShown)
            {
                HideText();
                return;
            }
            if (ShowText()) onDisplayed?.Invoke();
        }

        /// <summary>디렉터가 방 세션을 닫을 때 — 글을 닫고 더 받지 않는다.</summary>
        public void Close()
        {
            canRead = null;
            onDisplayed = null;
            HideText();
        }

        /// <summary>
        /// LateUpdate — <see cref="PlayerInteractor"/>가 이번 프레임 Update에서 옮긴 초점을 같은 프레임에 반영한다.
        /// 초점 이탈·읽기 불가(모달·정지·상위 화면·세션 종료)면 즉시 닫고, 떠 있는 동안 언어가 바뀌면 다시 쓴다.
        /// </summary>
        private void LateUpdate()
        {
            RefreshTitle();
            if (!isTextShown) return;

            if (!CanInteract || !IsFocused())
            {
                HideText();
                return;
            }
            if (Loc.CurrentLanguage != shownLanguage) ShowText();
        }

        private void OnDisable() => HideText();

        private bool IsFocused()
        {
            if (interactor == null) interactor = FindAnyObjectByType<PlayerInteractor>();
            return interactor != null && ReferenceEquals(interactor.CurrentTarget, this);
        }

        /// <summary>본문을 띄운다. 키가 없으면 띄우지 않고 false — 호출자는 발견으로 처리하지 않는다.</summary>
        private bool ShowText()
        {
            if (textLabel == null) return false;
            if (!Loc.HasKey(ExpeditionTextKeys.PassageRecordText))
            {
                Debug.LogWarning($"[PassageRecord] 본문 키 누락: {ExpeditionTextKeys.PassageRecordText} — 표시·발견 처리 안 함");
                HideText();
                return false;
            }

            string text = Loc.Get(ExpeditionTextKeys.PassageRecordText);
            if (string.IsNullOrEmpty(text))
            {
                HideText();
                return false;
            }

            textLabel.text = text;
            textLabel.gameObject.SetActive(true);
            shownLanguage = Loc.CurrentLanguage;
            isTextShown = true;
            return true;
        }

        private void HideText()
        {
            isTextShown = false;
            if (textLabel == null || !textLabel.gameObject.activeSelf) return;
            textLabel.text = string.Empty;
            textLabel.gameObject.SetActive(false);
        }

        private void RefreshTitle()
        {
            if (titleLabel == null) return;
            var language = Loc.CurrentLanguage;
            if (hasTitleLanguage && language == titleLanguage) return;
            titleLabel.text = Loc.Get(ExpeditionTextKeys.PassageRecordTitle);
            titleLanguage = language;
            hasTitleLanguage = true;
        }

        /// <summary>그림을 만들고 발밑에서 윗면까지의 높이를 돌려준다. 스프라이트는 높이 <see cref="VISUAL_HEIGHT"/>로 줄인다.</summary>
        private float BuildVisual(Sprite sprite)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(transform, false);
            var visual = go.AddComponent<SpriteRenderer>();
            visual.sortingOrder = ORDER_VISUAL;

            if (sprite != null && sprite.bounds.size.y > 0f)
            {
                visual.sprite = sprite;
                var bounds = sprite.bounds;
                float scale = VISUAL_HEIGHT / bounds.size.y;
                go.transform.localScale = new Vector3(scale, scale, 1f);
                // 피벗이 어디든 그림 아래 끝이 발밑에 오게 올린다.
                go.transform.localPosition = new Vector3(0f, -bounds.min.y * scale, 0f);
                return VISUAL_HEIGHT;
            }

            Debug.LogWarning("[PassageRecord] 책 스프라이트 미배선 — 임시 색 사각형으로 표시(상호작용은 그대로)");
            visual.sprite = WhiteSprite;
            visual.color = FallbackColor;
            go.transform.localScale = new Vector3(FALLBACK_WIDTH, FALLBACK_HEIGHT, 1f);
            go.transform.localPosition = new Vector3(0f, FALLBACK_HEIGHT * 0.5f, 0f);
            return FALLBACK_HEIGHT;
        }

        private static TextMesh CreateLabel(Transform parent, Vector3 localPosition, float characterSize, Color color, TextAnchor anchor)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var mesh = go.AddComponent<TextMesh>();
            mesh.text = string.Empty;
            mesh.font = UiFactory.GetDefaultFont();
            mesh.fontSize = 48;
            mesh.characterSize = characterSize;
            mesh.anchor = anchor;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;

            var renderer = go.GetComponent<MeshRenderer>();
            if (mesh.font != null) renderer.sharedMaterial = mesh.font.material;
            renderer.sortingOrder = ORDER_LABEL;
            return mesh;
        }

        /// <summary>1유닛 흰 사각형(피벗 가운데). 책 스프라이트가 없을 때만 쓴다.</summary>
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

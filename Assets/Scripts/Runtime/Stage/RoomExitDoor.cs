using System;
using Abyss.Runtime.ArtIntegration;
using Abyss.Runtime.Flow;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Run;
using Abyss.Runtime.UI;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 맵 방 오른쪽 끝의 보상 문 — 스컬의 「문 모양 = 다음 방의 보상」(17-stage-flow-boss-presentation §1-0).
    /// 갈림길 모달(<see cref="UI.NodeMapPanel"/>) 대신 월드에서 다가가 들어가며 고른다.
    ///
    /// 표시 규약은 모달과 같은 <see cref="RoomTypeDisplay"/>를 쓴다 — 같은 방이 모달과 문에서 다르게 불리지 않게.
    /// 하강 계단과 다음 방 아이콘을 표시한다. 아트 누락 시 색 사각형으로 폴백한다.
    /// </summary>
    public sealed class RoomExitDoor : MonoBehaviour, IInteractable
    {
        private const float DOOR_WIDTH = 1.6f;
        private const float DOOR_HEIGHT = 2.6f;
        private const int ORDER_DOOR = -3;          // 지형(-5) 앞, 캐릭터(0) 뒤
        private const int ORDER_LABEL = 5;
        private const float GLOW_SECONDS = 0.35f;           // 생성 점등 길이(게임 시간 — 정지 중엔 멈춘다)
        private const float GLOW_PEAK_AT = 0.3f;            // 점등 구간 중 가장 밝은 지점(비율)
        private const float GLOW_FRAME_BOOST = 0.75f;       // 테두리를 흰색 쪽으로
        private const float GLOW_PANEL_BOOST = 0.55f;       // 문짝 판을 방 색 쪽으로
        private const float DETAIL_BOTTOM = DOOR_HEIGHT + 1.15f;    // 보상 줄 위 — 이웃 문의 이름 줄과 겹치지 않게 위로 쌓는다
        private const float DETAIL_CHARACTER_SIZE = 0.06f;

        private static Sprite whiteSprite;

        private string headline;
        private string title;
        private Action onChosen;
        private bool isUsed;

        // 접근 상세 — 방 정보가 있는 문만 가진다(다음 스테이지 문은 room == null 이라 비어 있다).
        private RoomData room;
        private TextMesh detailLabel;
        private PlayerInteractor interactor;
        private bool isDetailShown;
        private ExpeditionRoomPreview.Inputs shownInputs;     // 지금 문장을 만든 골드·언어 — 바뀌면 다시 만든다

        // 생성 점등 — 표시 색만 잠깐 바꾼다. 콜라이더·상호작용·생성 타이밍은 건드리지 않는다.
        private SpriteRenderer frameRenderer;
        private SpriteRenderer panelRenderer;
        private Color frameColor;
        private Color panelColor;
        private float glowElapsed = -1f;

        public string InteractionPrompt => $"{headline} — {title} (G)";

        public bool CanInteract => !isUsed && onChosen != null;

        /// <summary>
        /// 다음 방으로 가는 문. 표시는 갈림길 모달과 같은 <see cref="RoomTypeDisplay"/> — 보상 한 줄 + 방 이름.
        /// 들어가면 <paramref name="chosen"/>에 방을 넘기고 스스로 닫힌다.
        /// </summary>
        public static RoomExitDoor Create(Transform parent, Vector3 footPosition, RoomData room, Action<RoomData> chosen)
        {
            var door = Create(parent, footPosition, $"ExitDoor_{room.roomId}", RoomTypeDisplay.RewardHeadline(room),
                room.ChoiceTitle, RoomTypeDisplay.Color(room.roomType), () => chosen?.Invoke(room));

            var route = UiArtKeys.TryGetRouteIcon(room.roomType);
            if (route != null)
            {
                var icon = new GameObject("RouteArt").AddComponent<SpriteRenderer>();
                icon.transform.SetParent(door.transform, false);
                icon.sprite = route;
                icon.sortingOrder = ORDER_LABEL;
                icon.transform.localPosition = new Vector3(0f, 2.5f, 0f);
                icon.transform.localScale = Vector3.one * (0.42f / route.bounds.size.y);
            }

            // 손익 예고 대상 방만 상세 줄을 단다 — 나머지 문은 예전처럼 보상 줄 + 이름만(ExpeditionRoomPreview).
            if (!string.IsNullOrEmpty(ExpeditionRoomPreview.WorldDetail(room, ExpeditionRoomPreview.ReadInputs())))
            {
                door.room = room;
                door.detailLabel = CreateLabel(door.transform, string.Empty, new Vector3(0f, DETAIL_BOTTOM, 0f),
                    DETAIL_CHARACTER_SIZE, new Color(0.9f, 0.9f, 0.95f), TextAnchor.LowerCenter);
                door.detailLabel.gameObject.SetActive(false);
            }
            return door;
        }

        /// <summary>
        /// 발밑(<paramref name="footPosition"/>)에 서는 문을 만든다 — 방이 아닌 곳(다음 스테이지 등)으로 가는 문도 이것으로.
        /// </summary>
        public static RoomExitDoor Create(Transform parent, Vector3 footPosition, string objectName, string headline,
                                          string title, Color color, Action chosen)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.position = footPosition;

            var door = go.AddComponent<RoomExitDoor>();
            door.headline = headline;
            door.title = title;
            door.onChosen = chosen;

            // 문짝 — 방 타입 색을 어둡게 깐 판 + 테두리 색 틀.
            var frame = CreateQuad(go.transform, "Frame", new Vector2(DOOR_WIDTH + 0.2f, DOOR_HEIGHT + 0.1f), color, ORDER_DOOR);
            frame.transform.localPosition = new Vector3(0f, (DOOR_HEIGHT + 0.1f) * 0.5f, 0f);
            var panel = CreateQuad(go.transform, "Panel", new Vector2(DOOR_WIDTH, DOOR_HEIGHT - 0.1f),
                new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 1f), ORDER_DOOR + 1);
            panel.transform.localPosition = new Vector3(0f, DOOR_HEIGHT * 0.5f - 0.05f, 0f);

            // 문 위 글자 — 보상 한 줄(크게) + 방 이름(작게).
            CreateLabel(go.transform, headline, new Vector3(0f, DOOR_HEIGHT + 0.75f, 0f), 0.11f, color, TextAnchor.MiddleCenter);
            CreateLabel(go.transform, title, new Vector3(0f, DOOR_HEIGHT + 0.3f, 0f), 0.075f, new Color(0.9f, 0.9f, 0.95f),
                TextAnchor.MiddleCenter);

            // 상호작용 판정 — PlayerInteractor 는 트리거 진입으로 후보를 모은다.
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(DOOR_WIDTH + 0.6f, DOOR_HEIGHT);
            trigger.offset = new Vector2(0f, DOOR_HEIGHT * 0.5f);

            var stairs = WorldArtLibrary.Place(go.transform, "exit/descending-stairs", Vector3.zero, 2.4f, ORDER_DOOR + 2);
            if (stairs != null)
            {
                frame.SetActive(false);
                panel.SetActive(false);
                door.BeginGlow(stairs, null);
            }
            else door.BeginGlow(frame.GetComponent<SpriteRenderer>(), panel.GetComponent<SpriteRenderer>());
            return door;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract) return;
            isUsed = true;
            onChosen.Invoke();
        }

        // ───────────────────────── 생성 점등 ─────────────────────────

        private void BeginGlow(SpriteRenderer frame, SpriteRenderer panel)
        {
            frameRenderer = frame;
            panelRenderer = panel;
            frameColor = frame != null ? frame.color : Color.white;
            panelColor = panel != null ? panel.color : Color.white;
            glowElapsed = 0f;
            ApplyGlow(0f);
        }

        private void Update()
        {
            if (glowElapsed < 0f) return;

            glowElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(glowElapsed / GLOW_SECONDS);
            // 짧게 차올랐다가(0 → 1) 원래 색으로 가라앉는다(1 → 0).
            float strength = t < GLOW_PEAK_AT ? t / GLOW_PEAK_AT : 1f - (t - GLOW_PEAK_AT) / (1f - GLOW_PEAK_AT);
            ApplyGlow(strength);
            if (t >= 1f) EndGlow();
        }

        /// <summary>비활성화되면 점등을 끊고 원래 색으로 되돌린다 — 다시 켜져도 밝은 채로 남지 않게. 상세도 지운다.</summary>
        private void OnDisable()
        {
            if (glowElapsed >= 0f) EndGlow();
            HideDetail();
        }

        // ───────────────────────── 접근 상세 ─────────────────────────

        /// <summary>
        /// 상세 판정은 LateUpdate — <see cref="PlayerInteractor"/>가 이번 프레임 Update에서 초점을 옮긴 결과를 같은 프레임에 반영한다
        /// (실행 순서에 따라 한 프레임 늦게 지워지지 않게).
        /// </summary>
        private void LateUpdate() => UpdateDetail();

        /// <summary>
        /// 상세는 <see cref="PlayerInteractor.CurrentTarget"/>이 이 문일 때만 띄운다(<c>RewardAltarFeedback</c>와 같은 타깃 기준) —
        /// 두 문의 긴 문장이 겹치지 않게, 다른 문·제단으로 초점이 옮겨 가면 즉시 지운다.
        /// 들어간 뒤·정지(모달·일시정지)·상위 화면(설정·도감·저장 모달, 닫힌 그 프레임 포함)·씬 전환·런 종료 중에는 띄우지 않는다.
        /// 문장은 보일 때 새로 만들고, 보이는 동안 골드·언어가 바뀌면 다시 만든다 — 낡은 문장을 들고 있지 않게.
        /// </summary>
        private void UpdateDetail()
        {
            if (detailLabel == null) return;

            if (!CanShowDetail)
            {
                HideDetail();
                return;
            }

            if (interactor == null) interactor = FindAnyObjectByType<PlayerInteractor>();
            bool isFocused = interactor != null && ReferenceEquals(interactor.CurrentTarget, this);
            if (!isFocused)
            {
                HideDetail();
                return;
            }

            var inputs = ExpeditionRoomPreview.ReadInputs();
            if (isDetailShown && inputs.Equals(shownInputs)) return;

            // 모달과 같은 원본·같은 입력(ExpeditionRoomPreview) — 잔액 부족 표식도 같은 판정이다.
            string detail = ExpeditionRoomPreview.WorldDetail(room, inputs);
            if (string.IsNullOrEmpty(detail))
            {
                HideDetail();
                return;
            }

            detailLabel.text = detail;
            detailLabel.gameObject.SetActive(true);
            shownInputs = inputs;
            isDetailShown = true;
        }

        private bool CanShowDetail => !isUsed && isActiveAndEnabled && Time.timeScale > 0f &&
            !IsUpperOverlayOwningScreen &&
            (!RunManager.HasInstance || RunManager.Instance.IsRunActive) &&
            (!SceneFlowController.HasInstance || !SceneFlowController.Instance.IsLoading);

        /// <summary>
        /// 설정·도감·저장 모달이 화면을 쥐고 있는지(닫은 그 프레임 포함). 정지 여부와 별개로 명시 확인한다 —
        /// 상위 화면이 timeScale을 멈추지 않는 경로에서도 문 상세가 그 위로 비치지 않게.
        /// </summary>
        private static bool IsUpperOverlayOwningScreen =>
            MenuButtonNavigation.IsUpperOverlayOpen ||
            SettingsPanel.WasClosedThisFrame || CodexPanel.WasClosedThisFrame;

        private void HideDetail()
        {
            isDetailShown = false;
            if (detailLabel == null || !detailLabel.gameObject.activeSelf) return;   // 이미 숨김 — 매 프레임 다시 쓰지 않는다
            detailLabel.text = string.Empty;
            detailLabel.gameObject.SetActive(false);
        }

        private void EndGlow()
        {
            glowElapsed = -1f;
            ApplyGlow(0f);
        }

        private void ApplyGlow(float strength)
        {
            if (frameRenderer != null) frameRenderer.color = Color.Lerp(frameColor, Color.white, strength * GLOW_FRAME_BOOST);
            if (panelRenderer != null)
            {
                var lit = new Color(frameColor.r, frameColor.g, frameColor.b, panelColor.a);
                panelRenderer.color = Color.Lerp(panelColor, lit, strength * GLOW_PANEL_BOOST);
            }
        }

        private static GameObject CreateQuad(Transform parent, string name, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = WhiteSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return go;
        }

        private static TextMesh CreateLabel(Transform parent, string text, Vector3 localPosition, float characterSize, Color color,
                                            TextAnchor anchor)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
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

        /// <summary>1유닛 흰 사각형(피벗 가운데). 문짝을 색으로만 그리는 동안 쓴다.</summary>
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

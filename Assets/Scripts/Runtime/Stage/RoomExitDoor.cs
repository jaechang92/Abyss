using System;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.UI;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 맵 방 오른쪽 끝의 보상 문 — 스컬의 「문 모양 = 다음 방의 보상」(17-stage-flow-boss-presentation §1-0).
    /// 갈림길 모달(<see cref="UI.NodeMapPanel"/>) 대신 월드에서 다가가 들어가며 고른다.
    ///
    /// 표시 규약은 모달과 같은 <see cref="RoomTypeDisplay"/>를 쓴다 — 같은 방이 모달과 문에서 다르게 불리지 않게.
    /// 아트가 나오기 전까지 색 사각형 + 글자다. 씬 배선 없이 <see cref="Create"/>로 만든다.
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

        private static Sprite whiteSprite;

        private string headline;
        private string title;
        private Action onChosen;
        private bool isUsed;

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
            return Create(parent, footPosition, $"ExitDoor_{room.roomId}", RoomTypeDisplay.RewardHeadline(room),
                room.ChoiceTitle, RoomTypeDisplay.Color(room.roomType), () => chosen?.Invoke(room));
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
            CreateLabel(go.transform, headline, new Vector3(0f, DOOR_HEIGHT + 0.75f, 0f), 0.11f, color);
            CreateLabel(go.transform, title, new Vector3(0f, DOOR_HEIGHT + 0.3f, 0f), 0.075f, new Color(0.9f, 0.9f, 0.95f));

            // 상호작용 판정 — PlayerInteractor 는 트리거 진입으로 후보를 모은다.
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(DOOR_WIDTH + 0.6f, DOOR_HEIGHT);
            trigger.offset = new Vector2(0f, DOOR_HEIGHT * 0.5f);

            door.BeginGlow(frame.GetComponent<SpriteRenderer>(), panel.GetComponent<SpriteRenderer>());
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

        /// <summary>비활성화되면 점등을 끊고 원래 색으로 되돌린다 — 다시 켜져도 밝은 채로 남지 않게.</summary>
        private void OnDisable()
        {
            if (glowElapsed >= 0f) EndGlow();
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

        private static void CreateLabel(Transform parent, string text, Vector3 localPosition, float characterSize, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = UiFactory.GetDefaultFont();
            mesh.fontSize = 48;
            mesh.characterSize = characterSize;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;

            var renderer = go.GetComponent<MeshRenderer>();
            if (mesh.font != null) renderer.sharedMaterial = mesh.font.material;
            renderer.sortingOrder = ORDER_LABEL;
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

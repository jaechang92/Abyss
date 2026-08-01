using System;
using System.Collections.Generic;
using Abyss.Runtime.Events;
using Abyss.Runtime.Stage;
using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 갈림길 선택 패널. 완주 루프 계획 1-4.
    ///
    /// 방 타입이 5종(전투·엘리트·보스·이벤트·휴식)이 되고 나서야 갈림길이 의미를 갖는다 —
    /// 3종일 때는 "전투 A vs 전투 B"라 선택이 아니었다.
    ///
    /// <see cref="EventRoomPanel"/>과 같은 규약이다: <see cref="UiFactory"/> 동적 생성(HudBuilder 무수정),
    /// 정지는 <c>RaiseDraftOpened/Closed</c>로 기존 DraftOpen FSM 상태를 빌린다.
    /// 표시 라벨·색은 <see cref="RoomTypeDisplay"/>(SoT)에서 가져온다.
    /// </summary>
    public sealed class NodeMapPanel : MonoBehaviour
    {
        private const int SORTING_ORDER = 100;   // 다른 모달과 같은 층. 일시정지(200)보다 아래
        private const int MAX_OPTIONS = 3;       // 현재 콘텐츠는 2갈래지만 데이터가 앞서갈 수 있다

        private const float PANEL_WIDTH = 820f;
        private const float PANEL_HEIGHT = 380f;
        private const float NODE_WIDTH = 300f;
        private const float NODE_HEIGHT = 190f;
        private const float NODE_GAP = 40f;

        private static NodeMapPanel instance;

        private GameObject body;
        private readonly List<Button> nodeButtons = new();
        private readonly List<Text> nodeTitles = new();
        private readonly List<Text> nodeTypes = new();
        private readonly List<Text> nodeHints = new();

        private IReadOnlyList<RoomData> options;
        private Action<RoomData> onPicked;
        private bool isOpen;

        public static bool IsOpen => instance != null && instance.isOpen;

        /// <summary>
        /// 갈림길을 연다. <paramref name="onPicked"/>는 선택된 방과 함께 한 번 호출된다.
        /// 유효한 선택지가 하나뿐이면 패널을 띄우지 않고 곧바로 콜백한다 — 선택이 아닌 것을 선택처럼
        /// 보여주면 플레이어가 데이터 오류를 게임 규칙으로 오해한다.
        /// </summary>
        public static void Open(IReadOnlyList<RoomData> roomOptions, Action<RoomData> onPicked)
        {
            var valid = Compact(roomOptions);

            if (valid.Count == 0)
            {
                Debug.LogWarning("[NodeMapPanel] 유효한 선택지가 없다 — 갈림길을 건너뛴다.");
                onPicked?.Invoke(null);
                return;
            }
            if (valid.Count == 1)
            {
                onPicked?.Invoke(valid[0]);
                return;
            }

            EnsureInstance();
            if (instance == null)
            {
                onPicked?.Invoke(valid[0]);
                return;
            }
            instance.Show(valid, onPicked);
        }

        /// <summary>null 엔트리를 걷어낸 목록. 표시 개수 상한도 여기서 적용한다.</summary>
        private static List<RoomData> Compact(IReadOnlyList<RoomData> source)
        {
            var result = new List<RoomData>();
            if (source == null) return result;

            foreach (var room in source)
            {
                if (room == null) continue;
                result.Add(room);
                if (result.Count >= MAX_OPTIONS) break;
            }
            return result;
        }

        /// <summary>도메인 리로드 비활성화 대비 정적 상태 리셋(AbyssBootstrap 선례).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private static void EnsureInstance()
        {
            if (instance != null) return;

            var go = CreateOverlayCanvas("NodeMapPanel", SORTING_ORDER);
            // Run 씬 전용이라 DontDestroyOnLoad 하지 않는다(EventRoomPanel과 같은 판단).
            instance = go.AddComponent<NodeMapPanel>();
            instance.BuildUI(go.transform);
            instance.body.SetActive(false);
        }

        // ───────────────────────── 흐름 ─────────────────────────

        private void Show(IReadOnlyList<RoomData> roomOptions, Action<RoomData> picked)
        {
            options = roomOptions;
            onPicked = picked;

            for (int i = 0; i < nodeButtons.Count; i++)
            {
                bool used = i < roomOptions.Count;
                nodeButtons[i].gameObject.SetActive(used);
                if (!used) continue;

                Bind(i, roomOptions[i]);
            }

            LayoutNodes(roomOptions.Count);
            body.SetActive(true);

            if (!isOpen)
            {
                isOpen = true;
                GameEvents.RaiseDraftOpened();   // 기존 DraftOpen 상태로 전역 정지
            }
        }

        private void Bind(int index, RoomData room)
        {
            nodeTypes[index].text = RoomTypeDisplay.Headline(room.roomType);
            nodeTypes[index].color = RoomTypeDisplay.Color(room.roomType);
            nodeTitles[index].text = room.ChoiceTitle;

            // 힌트가 없으면 줄을 비운다 — "정보 없음" 같은 문구는 선택에 도움이 안 된다.
            nodeHints[index].text = string.IsNullOrEmpty(room.hint) ? string.Empty : room.hint;
        }

        /// <summary>
        /// 선택지 수에 맞춰 가운데 정렬로 배치한다(2개면 좌우, 3개면 3열).
        /// 기본 폭으로 패널을 넘치면 노드를 줄인다 — 콘텐츠는 2갈래지만 데이터가 3갈래를 담을 수 있고,
        /// 그때 화면 밖으로 나가면 고를 수 없는 선택지가 생긴다.
        /// </summary>
        private void LayoutNodes(int count)
        {
            const float SIDE_PADDING = 40f;
            float available = PANEL_WIDTH - SIDE_PADDING - (count - 1) * NODE_GAP;
            float width = Mathf.Min(NODE_WIDTH, available / count);

            float span = count * width + (count - 1) * NODE_GAP;
            float start = -span * 0.5f + width * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var rect = (RectTransform)nodeButtons[i].transform;
                rect.sizeDelta = new Vector2(width, NODE_HEIGHT);
                rect.anchoredPosition = new Vector2(start + i * (width + NODE_GAP), -20f);
            }
        }

        private void OnNodeClicked(int index)
        {
            if (options == null || index < 0 || index >= options.Count) return;

            var picked = options[index];

            body.SetActive(false);
            options = null;

            if (isOpen)
            {
                isOpen = false;
                GameEvents.RaiseDraftClosed();
            }

            // 콜백을 먼저 비우고 호출한다 — 콜백 안에서 다시 열어도 중첩되지 않게.
            var callback = onPicked;
            onPicked = null;
            callback?.Invoke(picked);
        }

        // ───────────────────────── UI 구성 ─────────────────────────

        private void BuildUI(Transform root)
        {
            body = CreateDimBody(root, 0.82f);

            var panel = CreateRect(body.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PANEL_WIDTH, PANEL_HEIGHT));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.10f, 0.10f, 0.15f, 0.98f);

            CreateLabel(panel.transform, "TitleText", new Vector2(0, 138), new Vector2(720, 44),
                "다음 길을 고르십시오", 24, new Color(0.92f, 0.92f, 1f), TextAnchor.MiddleCenter);

            for (int i = 0; i < MAX_OPTIONS; i++)
            {
                BuildNode(panel.transform, i);
            }
        }

        private void BuildNode(Transform parent, int index)
        {
            // 라벨은 버튼 기본 Text를 쓰지 않고 3줄(타입·이름·힌트)을 따로 얹는다.
            var button = CreateButton(parent, $"Node{index}", Vector2.zero, new Vector2(NODE_WIDTH, NODE_HEIGHT),
                string.Empty, 18);
            button.onClick.AddListener(() => OnNodeClicked(index));

            var t = button.transform;
            var type = CreateLabel(t, "Type", new Vector2(0, 52), new Vector2(NODE_WIDTH - 24, 36),
                string.Empty, 22, Color.white, TextAnchor.MiddleCenter);
            var title = CreateLabel(t, "Title", new Vector2(0, 8), new Vector2(NODE_WIDTH - 24, 34),
                string.Empty, 19, new Color(0.92f, 0.92f, 1f), TextAnchor.MiddleCenter);
            var hint = CreateLabel(t, "Hint", new Vector2(0, -44), new Vector2(NODE_WIDTH - 32, 52),
                string.Empty, 15, new Color(0.68f, 0.68f, 0.78f), TextAnchor.UpperCenter);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            hint.verticalOverflow = VerticalWrapMode.Overflow;

            nodeButtons.Add(button);
            nodeTypes.Add(type);
            nodeTitles.Add(title);
            nodeHints.Add(hint);
            button.gameObject.SetActive(false);
        }
    }
}

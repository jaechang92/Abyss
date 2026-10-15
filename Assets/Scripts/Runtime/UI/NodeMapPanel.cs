using System;
using System.Collections.Generic;
using Abyss.Runtime.ArtIntegration;
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
    /// 정지는 <c>RaiseDraftOpened/Closed</c>로 기존 DraftOpen FSM 상태를 빌린다 — 둘 다 <see cref="RunModalPanel{T}"/>가 맡는다.
    /// 표시 라벨·색은 <see cref="RoomTypeDisplay"/>(SoT)에서 가져온다.
    /// </summary>
    public sealed partial class NodeMapPanel : RunModalPanel<NodeMapPanel>
    {
        private const int MAX_OPTIONS = 3;       // 현재 콘텐츠는 2갈래지만 데이터가 앞서갈 수 있다

        // 채택 UI 배치(layout-spec 방 경로 290/310/1340/460)의 기준 크기. 실제 배치·스크롤은 NodeMapReadability가 맡는다.
        private const float PANEL_WIDTH = 1340f;
        private const float PANEL_HEIGHT = 460f;
        private const float NODE_WIDTH = 384f;
        private const float NODE_HEIGHT = 280f;
        private const float NODE_LABEL_WIDTH = 320f;
        private const float PANEL_CORNER = 24f;
        private const int HINT_FONT_SIZE = 24;
        private const float ROUTE_ICON_SIZE = 64f;      // 카드 위 가장자리에 걸친다 — 실제 위치는 NodeMapReadability가 카드 높이에 맞춰 옮긴다

        private readonly List<Button> nodeButtons = new();
        private readonly List<Text> nodeTitles = new();
        private readonly List<Text> nodeTypes = new();
        private readonly List<Text> nodeHints = new();

        private IReadOnlyList<RoomData> options;
        private Action<RoomData> onPicked;
        private ExpeditionRoomPreview.Inputs shownHintInputs;   // 힌트 줄을 만든 골드·언어
        private NodeMapReadability readability;                 // 글자 확대·전체 예고 읽기 배치(카드 배치의 유일한 주인)

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

            var panel = EnsureInstance();
            if (panel == null)
            {
                onPicked?.Invoke(valid[0]);
                return;
            }
            panel.Show(valid, onPicked);
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

        /// <summary>도메인 리로드 비활성화 대비 정적 상태 리셋(AbyssBootstrap 선례). 제네릭 베이스에선 안 불려 여기 둔다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ResetInstance();

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

            BindHints(ExpeditionRoomPreview.ReadInputs());
            ShowBody();
            // 새 갈림길은 처음부터 읽는다. Bind가 맞춘 경로 그림을 다시 읽고, 첫 포커스 전에 배치해 카드 위치를 확정한다.
            if (readability != null) readability.ResetReading();
            LayoutNodes();
            BeginFocus();
        }

        private void Bind(int index, RoomData room)
        {
            nodeTypes[index].text = RoomTypeDisplay.Headline(room.roomType);
            nodeTypes[index].color = RoomTypeDisplay.Color(room.roomType);
            nodeTitles[index].text = room.ChoiceTitle;
            BindRouteIcon(index, room.roomType);
        }

        /// <summary>
        /// A2 경로 그림(방 타입별). 노드 위 가장자리 가운데에 걸쳐 둔다 — 절반은 노드 위 빈 칸(제목 아래)이라 타입·이름 줄을 가리지 않는다.
        /// 그림이 없는 타입(보스·이벤트)은 칸을 끈다. 노드는 재사용되므로 이름 고정 자식을 덮어쓴다.
        /// </summary>
        private void BindRouteIcon(int index, RoomType type)
        {
            var rect = (RectTransform)nodeButtons[index].transform;
            var box = new Rect(-ROUTE_ICON_SIZE * 0.5f, NODE_HEIGHT * 0.5f - ROUTE_ICON_SIZE * 0.5f, ROUTE_ICON_SIZE, ROUTE_ICON_SIZE);
            UiArtDecor.ApplyIconInRect(rect, "ArtRouteIcon", UiArtKeys.Route(type), box, Color.white);
        }

        /// <summary>
        /// 보이는 노드의 힌트 줄을 지금 골드·언어로 채운다. 열 때 한 번, 열린 동안 골드·언어가 바뀌면 다시(LateUpdate).
        /// 월드 문과 같은 원본·같은 입력(ExpeditionRoomPreview) — 대상 방은 데이터에서 만든 손익 예고(잔액 부족 표식 포함),
        /// 나머지는 기존 힌트. 힌트가 없으면 줄을 비운다 — "정보 없음" 같은 문구는 선택에 도움이 안 된다.
        /// </summary>
        private void BindHints(ExpeditionRoomPreview.Inputs inputs)
        {
            shownHintInputs = inputs;
            for (int i = 0; i < nodeHints.Count && i < options.Count; i++)
            {
                var hint = nodeHints[i];
                hint.text = ExpeditionRoomPreview.Detail(options[i], inputs, out _);

                // 손익 예고도 글자를 줄이지 않는다 — 전체 문구가 보이도록 카드가 늘어나고 넘치면 스크롤한다(NodeMapReadability).
                hint.resizeTextForBestFit = false;
            }
        }

        /// <summary>열린 동안 골드·언어가 바뀌면 힌트를 다시 만든다 — 포커스를 옮기거나 다시 열지 않아도 맞게.</summary>
        private void LateUpdate()
        {
            if (!IsBodyOpen || options == null) return;

            var inputs = ExpeditionRoomPreview.ReadInputs();
            if (inputs.Equals(shownHintInputs)) return;
            BindHints(inputs);
            // 글자가 바뀌었으면 같은 프레임에 다시 배치한다(세로 읽기 비율 유지).
            if (readability != null) readability.RefreshIfNeeded();
        }

        /// <summary>
        /// 카드 배치는 <see cref="NodeMapReadability"/> 한 곳이 맡는다 — 카드는 원래 폭(한 줄) 그대로 두고,
        /// 화면이 좁으면 줄이거나 세로로 쌓지 않고 가로로 스크롤한다. 고를 수 없는 선택지가 화면 밖에 남지 않는다.
        /// </summary>
        private void LayoutNodes()
        {
            if (readability != null) readability.Apply();
        }

        private void OnNodeClicked(int index)
        {
            // 연 프레임·이미 고른 뒤·상위 모달이 쥔 동안의 클릭은 버린다 — 방이 두 번 정해지지 않게(Navigation 참조).
            if (!CanAcceptPick) return;
            if (index < 0 || index >= options.Count) return;

            // 선택값·콜백을 지역으로 확보하고 공유 상태를 먼저 비운다 — HideBody(DraftClosed) 중에 새 Open이
            // 재진입해도 새 options/callback을 이 닫기가 덮어쓰거나 지우지 않게.
            var picked = options[index];
            var callback = onPicked;
            options = null;
            onPicked = null;

            // 본체를 숨기기 전에 자기 선택만 비운다 — 컴포넌트는 본체 밖이라 숨겨도 OnDisable이 안 돈다.
            // 이전 HUD 선택은 돌려주지 않는다 — 이어서 열릴 방 모달의 첫 포커스를 막지 않게.
            ClearOwnedSelection();
            HideBody();

            callback?.Invoke(picked);
        }

        // ───────────────────────── UI 구성 ─────────────────────────

        protected override void BuildContent(Transform body)
        {
            navigationRoot = body.gameObject;

            var panel = CreateRect(body, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PANEL_WIDTH, PANEL_HEIGHT));
            panel.AddComponent<Image>();
            float ppu = ModalArtSkin.ResolveReferencePpu(panel.transform);
            ModalArtSkin.ApplyPanel(panel.transform, PANEL_CORNER, ppu);

            var title = CreateLabel(panel.transform, "TitleText", new Vector2(0, 182), new Vector2(1240, 56),
                "다음 길을 고르십시오", 40, ModalArtSkin.BodyTextColor, TextAnchor.MiddleCenter);
            ModalArtSkin.StyleText(title, 40);

            for (int i = 0; i < MAX_OPTIONS; i++)
            {
                BuildNode(panel.transform, i, ppu);
            }

            // 같은 노드 참조를 한 줄 2축 스크롤 내용으로 옮긴다. 제목은 Panel에 고정으로 남는다.
            readability = NodeMapReadability.Attach(body, title, nodeButtons, nodeTypes, nodeTitles, nodeHints);
        }

        private void BuildNode(Transform parent, int index, float ppu)
        {
            // 라벨은 버튼 기본 Text를 쓰지 않고 3줄(타입·이름·힌트)을 따로 얹는다.
            var button = CreateButton(parent, $"Node{index}", Vector2.zero, new Vector2(NODE_WIDTH, NODE_HEIGHT),
                string.Empty, 18);
            button.onClick.AddListener(() => OnNodeClicked(index));

            // 세로 영역(노드 중심 기준): 타입 72..112 / 이름 8..72(2줄) / 힌트 -8..-128(4줄). 폭 320 안에서 줄바꿈, 넘는 줄은 자른다.
            var t = button.transform;
            var type = CreateLabel(t, "Type", new Vector2(0, 92), new Vector2(NODE_LABEL_WIDTH, 40),
                string.Empty, 28, Color.white, TextAnchor.MiddleCenter);
            var title = CreateLabel(t, "Title", new Vector2(0, 40), new Vector2(NODE_LABEL_WIDTH, 64),
                string.Empty, 28, ModalArtSkin.BodyTextColor, TextAnchor.MiddleCenter);
            var hint = CreateLabel(t, "Hint", new Vector2(0, -68), new Vector2(NODE_LABEL_WIDTH, 120),
                string.Empty, 24, ModalArtSkin.SubTextColor, TextAnchor.UpperCenter);
            ModalArtSkin.StyleText(type, 28);   // 색은 Bind에서 RoomTypeDisplay(SoT)가 정한다
            ModalArtSkin.StyleText(title, 28);
            ModalArtSkin.StyleText(hint, HINT_FONT_SIZE);   // 배율·줄바꿈·전체 높이는 NodeMapReadability가 다시 정한다

            // 프레임·호버 면·선택 표식(시각 전용). 클릭·Navigation은 위 onClick과 Navigation 파일 그대로다.
            ModalArtSkin.EnsureSelectableSkin(button, PANEL_CORNER, ppu);

            nodeButtons.Add(button);
            nodeTypes.Add(type);
            nodeTitles.Add(title);
            nodeHints.Add(hint);
            button.gameObject.SetActive(false);
        }
    }
}

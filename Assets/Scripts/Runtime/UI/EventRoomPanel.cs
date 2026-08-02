using System.Collections.Generic;
using Abyss.Runtime.Events;
using Abyss.Runtime.Run;
using Abyss.Runtime.Stage;
using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 이벤트 방 선택 모달. 완주 루프 계획 1-1.
    ///
    /// 로드맵의 "DialogueUI 재사용"은 성립하지 않았다 — <see cref="Dialogue.DialogueUI"/>는 로비 씬 전용이고
    /// (LobbySceneBuilder가 만든다) Space로 다음 줄만 넘길 뿐 <b>선택지 개념이 없다</b>.
    /// 실제 템플릿은 <see cref="FormReplacementModal"/>이다 — 선택 모달이고, 정지도 같은 방식으로 얻는다.
    ///
    /// 정지는 <see cref="GameEvents.RaiseDraftOpened"/>/<c>Closed</c>로 기존 DraftOpen FSM 상태를
    /// 빌려 쓴다(전용 정지 로직 불필요). 다만 FormReplacementModal과 달리 <b>동적 생성</b>이라
    /// HudBuilder 수정도 메뉴 재실행도 필요 없다 — HUD 재빌드가 다른 배선을 끊은 전력이 있다.
    /// </summary>
    public sealed class EventRoomPanel : MonoBehaviour
    {
        private const int SORTING_ORDER = 100;   // 다른 모달과 같은 층. 일시정지(200)보다는 아래
        private const int MAX_CHOICES = 3;

        private const float PANEL_WIDTH = 760f;
        private const float PANEL_HEIGHT = 560f;
        private const float CHOICE_HEIGHT = 56f;
        private const float CHOICE_GAP = 12f;

        private static EventRoomPanel instance;

        private GameObject body;
        private Text titleText;
        private Text descriptionText;
        private readonly List<Button> choiceButtons = new();
        private readonly List<Text> choiceLabels = new();
        private GameObject continueRoot;
        private Button continueButton;

        private EventData current;
        private EventChoice chosen;
        private bool isOpen;

        public static bool IsOpen => instance != null && instance.isOpen;

        /// <summary>이벤트를 연다. 선택이 끝나면 <see cref="GameEvents.OnEventResolved"/>가 발행된다.</summary>
        public static void Open(EventData data)
        {
            if (data == null || data.choices == null || data.choices.Count == 0)
            {
                // 데이터가 비어 있으면 방이 영영 안 끝난다 — 즉시 해결로 흘려보낸다.
                Debug.LogWarning("[EventRoomPanel] EventData 비어 있음 — 이벤트를 건너뛴다.");
                GameEvents.RaiseEventResolved();
                return;
            }

            EnsureInstance();
            if (instance == null) return;
            instance.Show(data);
        }

        /// <summary>도메인 리로드 비활성화 대비 정적 상태 리셋(AbyssBootstrap 선례).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private static void EnsureInstance()
        {
            // 씬 전환으로 파괴된 인스턴스는 Unity의 == 오버로드 덕에 여기서 null로 판정되어 다시 만들어진다.
            if (instance != null) return;

            var go = CreateOverlayCanvas("EventRoomPanel", SORTING_ORDER);
            // Run 씬 전용이므로 DontDestroyOnLoad 하지 않는다(LobbyMenuPanel과 같은 판단).
            instance = go.AddComponent<EventRoomPanel>();
            instance.BuildUI(go.transform);
            instance.body.SetActive(false);
        }

        // ───────────────────────── 흐름 ─────────────────────────

        private void Show(EventData data)
        {
            current = data;
            chosen = null;

            if (titleText != null) titleText.text = data.title;
            if (descriptionText != null) descriptionText.text = data.description;

            BindChoices(data);
            if (continueRoot != null) continueRoot.SetActive(false);

            body.SetActive(true);

            if (!isOpen)
            {
                isOpen = true;
                GameEvents.RaiseDraftOpened();   // 기존 DraftOpen 상태로 전역 정지
            }
        }

        private void BindChoices(EventData data)
        {
            int gold = RunManager.HasInstance ? RunManager.Instance.GoldShards : 0;

            for (int i = 0; i < choiceButtons.Count; i++)
            {
                bool used = i < data.choices.Count && i < MAX_CHOICES;
                choiceButtons[i].gameObject.SetActive(used);
                if (!used) continue;

                var choice = data.choices[i];
                int cost = choice.GoldCost;
                bool affordable = cost <= gold;

                // 살 수 없는 선택지는 비활성 + 이유를 라벨에 적는다. 왜 못 누르는지 화면에 없으면 버그처럼 보인다.
                choiceLabels[i].text = affordable ? choice.label : $"{choice.label}  (골드 {cost} 필요)";
                choiceButtons[i].interactable = affordable;
            }
        }

        private void OnChoiceClicked(int index)
        {
            if (current == null || index < 0 || index >= current.choices.Count) return;

            chosen = current.choices[index];

            // 선택 직후에는 결과 문구만 보여주고 효과는 아직 적용하지 않는다.
            // 스킬 드래프트가 섞여 있으면 이 패널이 열린 채 드래프트 모달이 겹치기 때문이다(아래 Continue 참조).
            if (descriptionText != null) descriptionText.text = chosen.resultText;

            foreach (var b in choiceButtons) b.gameObject.SetActive(false);
            if (continueRoot != null) continueRoot.SetActive(true);
        }

        /// <summary>
        /// [계속] — 닫고 나서 효과를 적용한다.
        ///
        /// 순서가 중요하다: 스킬 드래프트 효과는 다시 <c>DraftOpened</c>를 발행하므로,
        /// 이 패널이 먼저 <c>DraftClosed</c>로 정지를 풀어야 두 모달이 겹치지 않고 순차로 열린다.
        /// 게이트 해제(<see cref="GameEvents.RaiseEventResolved"/>)는 마지막이다 —
        /// StageDirector의 다음 방 진행은 스케일 시간 <c>Invoke</c>라 드래프트가 닫힐 때까지 알아서 기다린다.
        /// </summary>
        private void OnContinueClicked()
        {
            var applied = chosen;

            body.SetActive(false);
            current = null;
            chosen = null;

            if (isOpen)
            {
                isOpen = false;
                GameEvents.RaiseDraftClosed();
            }

            // 잔액은 BindChoices에서 이미 걸렀다. 드래프트는 패널을 닫은 지금 열어야
            // 정지가 끊기지 않고 이어진다(EventEffectApplier 주석 참조).
            int drafts = EventEffectApplier.ApplyNonModal(applied?.effects);
            EventEffectApplier.GrantDrafts(drafts);

            GameEvents.RaiseEventResolved();
        }

        // ───────────────────────── UI 구성 ─────────────────────────

        private void BuildUI(Transform root)
        {
            body = CreateDimBody(root, 0.82f);

            var panel = CreateRect(body.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PANEL_WIDTH, PANEL_HEIGHT));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.10f, 0.10f, 0.15f, 0.98f);

            titleText = CreateLabel(panel.transform, "TitleText", new Vector2(0, 226), new Vector2(680, 46),
                string.Empty, 26, new Color(1f, 0.9f, 0.7f), TextAnchor.MiddleCenter);

            // 본문은 선택 후 결과 문구로도 쓰인다 — 길이가 들쭉날쭉하므로 줄바꿈 + 세로 오버플로 허용.
            descriptionText = CreateLabel(panel.transform, "DescriptionText", new Vector2(0, 96), new Vector2(660, 180),
                string.Empty, 19, new Color(0.86f, 0.86f, 0.94f), TextAnchor.UpperCenter);
            descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descriptionText.verticalOverflow = VerticalWrapMode.Overflow;
            descriptionText.lineSpacing = 1.4f;

            BuildChoiceButtons(panel.transform);
            BuildContinue(panel.transform);
        }

        private void BuildChoiceButtons(Transform parent)
        {
            // 최대 개수만큼 미리 만들어 두고 표시 여부만 토글한다 — 이벤트마다 계층을 다시 짓지 않는다.
            for (int i = 0; i < MAX_CHOICES; i++)
            {
                float y = -40f - i * (CHOICE_HEIGHT + CHOICE_GAP);
                var button = CreateButton(parent, $"Choice{i}", new Vector2(0, y), new Vector2(600, CHOICE_HEIGHT),
                    string.Empty, 19);

                int index = i;
                button.onClick.AddListener(() => OnChoiceClicked(index));

                choiceButtons.Add(button);
                choiceLabels.Add(button.GetComponentInChildren<Text>());
                button.gameObject.SetActive(false);
            }
        }

        private void BuildContinue(Transform parent)
        {
            continueButton = CreateButton(parent, "ContinueButton", new Vector2(0, -196), new Vector2(300, 52),
                "계속", 20);
            continueButton.onClick.AddListener(OnContinueClicked);
            continueRoot = continueButton.gameObject;
            continueRoot.SetActive(false);
        }
    }
}

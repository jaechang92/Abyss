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
    /// 생성·정지 규약은 <see cref="RunModalPanel{T}"/>에 있다.
    /// </summary>
    public sealed class EventRoomPanel : RunModalPanel<EventRoomPanel>
    {
        // 선택지 상한. 넘치는 선택지는 조용히 안 보인다 — 에러도 로그도 없다.
        // 3 → 4 (무기 가차). 기존 이벤트가 정확히 3개씩이라, 올리지 않으면 더한 선택지가 그냥 사라진다.
        //
        // 🔴 이제 상한을 올리면 패널이 <b>알아서</b> 커진다. 상점(ShopRoomPanel)에서 같은 함정을
        //    같은 방식으로 풀었다 — 좌표를 「몇 개가 될 수 있는가」에서 계산하면 잊을 것이 없다.
        private const int MAX_CHOICES = 4;

        private const float PANEL_WIDTH = 760f;
        private const float CHOICE_HEIGHT = 56f;
        private const float CHOICE_GAP = 12f;
        private const float CHOICE_STRIDE = CHOICE_HEIGHT + CHOICE_GAP;

        // 선택지가 차지하지 않는 부분(제목·본문·아래 여백)의 합. 선택지 수와 무관한 상수다.
        private const float PANEL_CHROME = 356f;

        private const float PANEL_HEIGHT = PANEL_CHROME + MAX_CHOICES * CHOICE_STRIDE;
        private const float PANEL_HALF = PANEL_HEIGHT * 0.5f;

        private const float TITLE_Y = PANEL_HALF - 54f;
        private const float DESC_Y = PANEL_HALF - 184f;
        private const float FIRST_CHOICE_Y = DESC_Y - 136f;   // 본문 아래 첫 선택지 중심

        // [계속]은 선택지와 겹쳐도 된다 — 선택하는 순간 선택지들이 숨고 이 버튼이 뜬다(동시 표시 없음).
        private const float CONTINUE_Y = FIRST_CHOICE_Y - (MAX_CHOICES - 1) * CHOICE_STRIDE - 20f;

        private Text titleText;
        private Text descriptionText;
        private readonly List<Button> choiceButtons = new();
        private readonly List<Text> choiceLabels = new();
        private GameObject continueRoot;
        private Button continueButton;

        private EventData current;
        private EventChoice chosen;

        // 선택 시점에 적용하고 남은, [계속] 뒤로 미뤄야 하는 드래프트 횟수.
        // 🔴 미루는 이유는 <b>모달 겹침 하나뿐</b>이다 — 나머지 효과는 패널이 열린 채 적용해도 안전하다
        //    (ShopRoomPanel 이 구매 때마다 그렇게 부르고 있다).
        private int pendingDrafts;

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

            var panel = EnsureInstance();
            if (panel == null) return;
            panel.Show(data);
        }

        /// <summary>도메인 리로드 비활성화 대비 정적 상태 리셋(AbyssBootstrap 선례). 제네릭 베이스에선 안 불려 여기 둔다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ResetInstance();

        // ───────────────────────── 흐름 ─────────────────────────

        private void Show(EventData data)
        {
            current = data;
            chosen = null;

            if (titleText != null) titleText.text = data.title;
            if (descriptionText != null) descriptionText.text = data.description;

            BindChoices(data);
            if (continueRoot != null) continueRoot.SetActive(false);

            ShowBody();
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

            // 모달을 안 여는 효과는 여기서 적용한다. 드래프트만 [계속] 뒤로 미룬다 —
            // 그것만이 이 패널이 열린 채 겹치는 효과이기 때문이다(아래 Continue 참조).
            //
            // 🔴 <b>당긴 이유</b>: 무기 가차는 「무엇이 나왔는지」를 결과 문구에 실어야 하는데,
            //    resultText 는 에셋에 미리 쓴 문장이라 그걸 못 담는다. 적용을 [계속] 뒤로 미루면
            //    패널이 이미 닫힌 뒤라 <b>붙일 자리 자체가 없다</b> — 상자를 열고도 무엇을 얻었는지
            //    모른 채 방을 넘어가게 된다. 잔액은 BindChoices 가 이미 걸렀으므로 앞당겨도 안전하다.
            pendingDrafts = EventEffectApplier.ApplyNonModal(chosen.effects, out string weaponGain);

            if (descriptionText != null)
            {
                descriptionText.text = string.IsNullOrEmpty(weaponGain)
                    ? chosen.resultText
                    : $"{chosen.resultText}\n\n{weaponGain}";
            }

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
            int drafts = pendingDrafts;
            pendingDrafts = 0;

            current = null;
            chosen = null;
            HideBody();

            // 모달을 안 여는 효과는 선택 시점에 이미 적용됐다. 드래프트만 패널을 닫은 지금 연다 —
            // 그래야 정지가 끊기지 않고 이어진다(EventEffectApplier 주석 참조).
            EventEffectApplier.GrantDrafts(drafts);

            GameEvents.RaiseEventResolved();
        }

        // ───────────────────────── UI 구성 ─────────────────────────

        protected override void BuildContent(Transform body)
        {
            var panel = CreateRect(body, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PANEL_WIDTH, PANEL_HEIGHT));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.10f, 0.10f, 0.15f, 0.98f);

            titleText = CreateLabel(panel.transform, "TitleText", new Vector2(0, TITLE_Y), new Vector2(680, 46),
                string.Empty, 26, new Color(1f, 0.9f, 0.7f), TextAnchor.MiddleCenter);

            // 본문은 선택 후 결과 문구로도 쓰인다 — 길이가 들쭉날쭉하므로 줄바꿈 + 세로 오버플로 허용.
            descriptionText = CreateLabel(panel.transform, "DescriptionText", new Vector2(0, DESC_Y), new Vector2(660, 180),
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
                float y = FIRST_CHOICE_Y - i * CHOICE_STRIDE;
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
            continueButton = CreateButton(parent, "ContinueButton", new Vector2(0, CONTINUE_Y), new Vector2(300, 52),
                "계속", 20);
            continueButton.onClick.AddListener(OnContinueClicked);
            continueRoot = continueButton.gameObject;
            continueRoot.SetActive(false);
        }
    }
}

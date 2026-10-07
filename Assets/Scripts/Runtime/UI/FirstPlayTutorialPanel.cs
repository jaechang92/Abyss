using System;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 첫 플레이 튜토리얼의 화면 — 화면 위쪽 가운데의 작은 비모달 띠. 판단은 하지 않고 받은 글자만 그린다
    /// (<see cref="FirstPlayTutorialController"/>가 무엇을 보일지 정한다).
    ///
    /// 🔑 <b>비모달</b>이다: 딤 배경을 쓰지 않고 배경 이미지도 레이캐스트를 받지 않는다. 눌리는 것은
    /// 건너뛰기 버튼 하나이고, 패드 내비게이션이 드래프트 카드에서 이 버튼으로 새지 않게 내비게이션을 끈다.
    /// 층은 HUD·드래프트 패널 위, 보스 HUD·모달 아래다 — 모달이 열리면 가려진다.
    /// </summary>
    public sealed partial class FirstPlayTutorialPanel : MonoBehaviour
    {
        private const int SORTING_ORDER = UiSortingOrder.BossHud - 10;

        private static readonly Vector2 PanelSize = new(640f, 112f);
        private static readonly Color BackgroundColor = new(0f, 0f, 0f, 0.6f);
        private static readonly Color HeaderColor = new(0.95f, 0.85f, 0.55f);
        private static readonly Color HintColor = Color.white;
        private static readonly Color SubColor = new(0.7f, 0.7f, 0.8f);

        private GameObject body;
        private Text header;
        private Text hint;
        private Text padHint;
        private Button skipButton;

        /// <summary>건너뛰기 버튼 클릭.</summary>
        public event Action OnSkipClicked;

        /// <summary>
        /// 패널을 만들어 <paramref name="owner"/> 밑에 둔다 — 소유자(컨트롤러)와 함께 파괴되어
        /// 씬 전환 뒤에 띠가 남지 않는다.
        /// </summary>
        public static FirstPlayTutorialPanel Create(Transform owner)
        {
            var go = CreateOverlayCanvas("FirstPlayTutorialPanel", SORTING_ORDER);
            if (owner != null) go.transform.SetParent(owner, false);

            var panel = go.AddComponent<FirstPlayTutorialPanel>();
            panel.BuildUI(go.transform);
            panel.SetVisible(false);
            return panel;
        }

        public void SetVisible(bool isVisible)
        {
            if (body != null && body.activeSelf != isVisible) body.SetActive(isVisible);
            if (!isVisible) SetSpatialVisible(false);
        }

        /// <summary>세 줄을 갱신한다. 같은 글자면 건드리지 않는다(매 갱신마다 레이아웃을 다시 짜지 않게).</summary>
        public void SetContent(string headerText, string hintText, string padHintText)
        {
            Assign(header, headerText);
            Assign(hint, hintText);
            Assign(padHint, padHintText);
        }

        private static void Assign(Text label, string value)
        {
            if (label == null) return;
            value ??= string.Empty;
            if (label.text != value) label.text = value;
        }

        private void BuildUI(Transform root)
        {
            body = CreateRect(root, "Body", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), PanelSize);

            var background = body.AddComponent<Image>();
            background.color = BackgroundColor;
            background.raycastTarget = false;

            header = CreateLabel(body.transform, "Header", new Vector2(0f, 34f), new Vector2(600f, 28f),
                string.Empty, 18, HeaderColor, TextAnchor.MiddleCenter);
            hint = CreateLabel(body.transform, "Hint", new Vector2(0f, 4f), new Vector2(600f, 32f),
                string.Empty, 24, HintColor, TextAnchor.MiddleCenter);
            padHint = CreateLabel(body.transform, "PadSkipHint", new Vector2(-70f, -36f), new Vector2(440f, 24f),
                string.Empty, 14, SubColor, TextAnchor.MiddleLeft);

            skipButton = CreateLocalizedButton(body.transform, "SkipButton", new Vector2(250f, -36f),
                new Vector2(110f, 26f), StringKey.Tutorial_Skip, 14);
            skipButton.navigation = new Navigation { mode = Navigation.Mode.None };
            skipButton.onClick.AddListener(() => OnSkipClicked?.Invoke());
        }
    }
}

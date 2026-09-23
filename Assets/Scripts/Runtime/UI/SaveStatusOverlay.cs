using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using SaveSystem_Core;
using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 저장 상태를 사용자에게 알리는 오버레이 — 세이브 접근 실패 모달 · 「저장 안 됨」 상시 표시 · 저장 실패 토스트.
    ///
    /// 그전까지 <see cref="MetaSaveService.IsSaveBlocked"/>는 저장 코드 밖에서 아무도 읽지 않았다.
    /// 파일은 보호됐지만 화면은 임시 빈 세이브로 평소처럼 돌아가서, 사용자는 로그를 보기 전에는
    /// 진행이 저장되지 않는다는 것을 알 방법이 없었다(wave2 RESULT 「남은 일감」 5번).
    ///
    /// 흐름:
    /// <list type="number">
    /// <item>보류가 시작되면 모달 — [다시 시도] / [저장 없이 계속]. 계속을 고르면 모달을 닫고 상시 표시만 남긴다.</item>
    /// <item>상시 표시는 로비·Run 어디서든 보인다. 누르면 바로 재시도한다(결과는 토스트).</item>
    /// <item>재시도 성공 = 보류 해제 → 표시가 사라지고 「다시 연결됐다」 토스트. 그 사이 진행은 디스크 값으로 바뀐다(모달 문구로 미리 알린다).</item>
    /// <item>보류가 아닌데 쓰기가 실패하면 토스트만 — 직전 저장은 그대로라 막을 이유가 없다.</item>
    /// </list>
    /// 재시도는 <b>사용자가 누를 때만</b> 한다 — 저장 보류 계약의 「자동·무제한 재시도 없음」을 그대로 따른다.
    ///
    /// 설정 패널처럼 런타임 동적 생성 + <c>DontDestroyOnLoad</c>. 진입점은 타이틀(<see cref="TitleMenuPanel"/>)의
    /// <see cref="Ensure"/> 한 곳이다 — 정상 흐름은 반드시 타이틀을 지나고, 그 전(부트스트랩)에 난 보류도
    /// 생성 시점에 현재 상태를 읽어 띄운다.
    /// 루트 오브젝트는 끄지 않는다 — 끄면 이벤트 구독까지 멈춘다. 켜고 끄는 것은 자식(모달·표시·토스트)이다.
    /// </summary>
    public sealed class SaveStatusOverlay : MonoBehaviour
    {
        private const float TOAST_SECONDS = 3.5f;

        private static readonly Color PanelColor = new Color(0.10f, 0.10f, 0.15f, 0.98f);
        private static readonly Color TitleColor = new Color(1f, 0.78f, 0.45f);
        private static readonly Color BodyColor = new Color(0.90f, 0.90f, 0.96f);
        private static readonly Color SubtleColor = new Color(0.55f, 0.55f, 0.66f);
        private static readonly Color WarningColor = new Color(1f, 0.62f, 0.40f);
        private static readonly Color BadgeColor = new Color(0.45f, 0.20f, 0.12f, 0.92f);
        private static readonly Color ToastColor = new Color(0.08f, 0.08f, 0.12f, 0.92f);

        private static SaveStatusOverlay instance;

        private MetaSaveService service;
        private GameObject modalBody;
        private Text pathLabel;
        private Text retryResultLabel;
        private GameObject badge;
        private GameObject toast;
        private Text toastLabel;
        private float toastHideTime;
        // [저장 없이 계속]을 골랐다 — 같은 보류가 이어지는 동안 모달을 다시 띄우지 않는다.
        private bool isModalDismissed;

        public static bool IsModalOpen => instance != null && instance.modalBody != null && instance.modalBody.activeSelf;
        public static bool IsBadgeVisible => instance != null && instance.badge != null && instance.badge.activeSelf;

        /// <summary>오버레이를 한 번만 만든다. MetaSaveService가 없으면(에디터 단독 실행 등) 아무것도 하지 않는다.</summary>
        public static void Ensure()
        {
            if (instance != null) return;

            var meta = MetaSaveService.GetInstanceSafe();
            if (meta == null) return;

            var go = CreateOverlayCanvas("SaveStatusOverlay", UiSortingOrder.SaveStatus);
            DontDestroyOnLoad(go);

            instance = go.AddComponent<SaveStatusOverlay>();
            instance.BuildUI(go.transform);
            instance.Bind(meta);
        }

        /// <summary>도메인 리로드 비활성화 대비 정적 상태 리셋(AbyssBootstrap 선례).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private void Bind(MetaSaveService meta)
        {
            service = meta;
            service.OnSaveStatusChanged += HandleSaveStatusChanged;
            service.OnSaveWriteFailed += HandleSaveWriteFailed;
            ApplyBlockedState();
        }

        private void OnDestroy()
        {
            if (service != null)
            {
                service.OnSaveStatusChanged -= HandleSaveStatusChanged;
                service.OnSaveWriteFailed -= HandleSaveWriteFailed;
            }
            if (instance == this) instance = null;
        }

        /// <summary>정지(timeScale=0) 위에서도 사라져야 하므로 unscaledTime으로 잰다.</summary>
        private void Update()
        {
            if (toast != null && toast.activeSelf && Time.unscaledTime >= toastHideTime) toast.SetActive(false);
        }

        // ───────────────────────── 상태 반영 ─────────────────────────

        private void HandleSaveStatusChanged()
        {
            if (service.IsSaveBlocked)
            {
                // 새 보류다 — 이전에 닫았더라도 다시 알린다.
                isModalDismissed = false;
                ApplyBlockedState();
                return;
            }

            ApplyBlockedState();
            ShowToast(Loc.Get(StringKey.SaveStatus_Restored));
        }

        private void HandleSaveWriteFailed() => ShowToast(Loc.Get(StringKey.SaveStatus_WriteFailed));

        private void ApplyBlockedState()
        {
            bool isBlocked = service != null && service.IsSaveBlocked;

            modalBody.SetActive(isBlocked && !isModalDismissed);
            badge.SetActive(isBlocked && isModalDismissed);

            if (!isBlocked) return;
            retryResultLabel.text = string.Empty;
            pathLabel.text = SaveSystem.HasInstance
                ? Loc.GetFormat(StringKey.SaveStatus_PathFormat, SaveSystem.Instance.SaveDirectory)
                : string.Empty;
        }

        // ───────────────────────── 버튼 동작 ─────────────────────────

        private void OnModalRetryClicked()
        {
            if (service.RetrySaveAccess())
            {
                // 보통은 OnSaveStatusChanged가 이미 닫았다. 그 사이 다른 경로로 풀린 경우를 위해 한 번 더 맞춘다.
                ApplyBlockedState();
                return;
            }
            retryResultLabel.text = Loc.Get(StringKey.SaveStatus_RetryFailed);
        }

        private void OnContinueClicked()
        {
            isModalDismissed = true;
            ApplyBlockedState();
        }

        private void OnBadgeClicked()
        {
            if (service.RetrySaveAccess())
            {
                ApplyBlockedState();
                return;
            }
            ShowToast(Loc.Get(StringKey.SaveStatus_RetryFailed));
        }

        private void ShowToast(string message)
        {
            toastLabel.text = message;
            toast.SetActive(true);
            toastHideTime = Time.unscaledTime + TOAST_SECONDS;
        }

        // ───────────────────────── UI 구성 ─────────────────────────

        private void BuildUI(Transform root)
        {
            BuildModal(root);
            BuildBadge(root);
            BuildToast(root);
        }

        private void BuildModal(Transform root)
        {
            modalBody = CreateDimBody(root, 0.78f);

            var panel = CreateRect(modalBody.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780, 380));
            panel.AddComponent<Image>().color = PanelColor;

            CreateLocalizedLabel(panel.transform, "TitleText", new Vector2(0, 142), new Vector2(700, 40), StringKey.SaveStatus_BlockedTitle, 26, TitleColor, TextAnchor.MiddleCenter);

            var body = CreateLocalizedLabel(panel.transform, "BodyText", new Vector2(0, 48), new Vector2(700, 120), StringKey.SaveStatus_BlockedBody, 17, BodyColor, TextAnchor.MiddleCenter);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            pathLabel = CreateLabel(panel.transform, "PathText", new Vector2(0, -34), new Vector2(700, 36), string.Empty, 13, SubtleColor, TextAnchor.MiddleCenter);
            pathLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

            retryResultLabel = CreateLabel(panel.transform, "RetryResultText", new Vector2(0, -76), new Vector2(700, 30), string.Empty, 15, WarningColor, TextAnchor.MiddleCenter);

            var retry = CreateLocalizedButton(panel.transform, "RetryButton", new Vector2(-150, -140), new Vector2(260, 52), StringKey.SaveStatus_Retry, 19);
            retry.onClick.AddListener(OnModalRetryClicked);

            var cont = CreateLocalizedButton(panel.transform, "ContinueButton", new Vector2(150, -140), new Vector2(260, 52), StringKey.SaveStatus_ContinueWithoutSave, 19);
            cont.onClick.AddListener(OnContinueClicked);
        }

        /// <summary>우상단 「저장 안 됨」 표시. 누르면 재시도한다.</summary>
        private void BuildBadge(Transform root)
        {
            var button = CreateLocalizedButton(root, "Badge", Vector2.zero, new Vector2(340, 40), StringKey.SaveStatus_Badge, 16);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-16, -16);

            // 버튼 틴트는 이미지 색에 곱해진다 — 기본 버튼색 위에 곱하면 경고색이 탁해지므로 이미지를 흰색으로 둔다.
            button.targetGraphic.color = Color.white;
            var colors = button.colors;
            colors.normalColor = BadgeColor;
            colors.highlightedColor = new Color(0.60f, 0.28f, 0.16f, 0.95f);
            button.colors = colors;

            button.onClick.AddListener(OnBadgeClicked);
            badge = button.gameObject;
            badge.SetActive(false);
        }

        /// <summary>하단 중앙 토스트. 클릭을 가로채지 않는다.</summary>
        private void BuildToast(Transform root)
        {
            toast = CreateRect(root, "Toast", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 96), new Vector2(760, 48));
            var background = toast.AddComponent<Image>();
            background.color = ToastColor;
            background.raycastTarget = false;

            toastLabel = CreateLabel(toast.transform, "ToastText", Vector2.zero, new Vector2(740, 44), string.Empty, 17, BodyColor, TextAnchor.MiddleCenter);
            toast.SetActive(false);
        }
    }
}

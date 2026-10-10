using Abyss.Runtime.ArtIntegration;
using Abyss.Runtime.Events;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 첫 보스 조우의 하단 이름표(E1). <see cref="BossIntroPanel"/> 의 중앙 카드와 달리 <b>보스가 보이는 화면을 가리지 않는다</b> —
    /// 딤을 거의 걷고, 이름·별칭·대사를 화면 아래 띠에만 둔다.
    ///
    /// <list type="bullet">
    /// <item>정지는 <see cref="RunModalPanel{T}"/> 규약(DraftOpen 차용) 그대로다. 공용 규약은 바꾸지 않는다.</item>
    /// <item>무엇을 언제 보일지는 호출하는 쪽(<c>BossPresenter</c> 첫 보스 파트)이 정한다. 이 패널은 표시와 입력 판독만 한다.</item>
    /// <item>입력 의미는 기존 카드와 같다 — Space·Enter·Z·패드 A = 다음(넘기기). 대사 더 보기는 ↑·패드 Y.
    /// 열림·줄 바뀜 뒤 첫 0.25초는 입력을 받지 않는다. 저장 오버레이가 입력을 쥐고 있으면 양보한다.</item>
    /// </list>
    /// </summary>
    public sealed class KeeperIntroPanel : RunModalPanel<KeeperIntroPanel>
    {
        private const float INPUT_GRACE_SECONDS = 0.25f;
        private const float DIM_ALPHA = 0.08f;
        private const float BAND_HEIGHT = 230f;

        private static readonly Color BandColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color TitleColor = new Color(1f, 0.82f, 0.52f);
        private static readonly Color EpithetColor = new Color(0.70f, 0.70f, 0.80f);
        private static readonly Color LineColor = new Color(0.93f, 0.93f, 0.97f);
        private static readonly Color HintColor = new Color(0.55f, 0.55f, 0.66f);

        private CanvasGroup titleGroup;
        private Text titleLabel;
        private Text epithetLabel;
        private Text lineLabel;
        private Text pagerLabel;
        private GameObject nextHint;
        private GameObject bodyObject;
        private float inputOpenAt;

        // 지금 이 패널을 쓰는 소개 한 번의 소유 기록. 의도한 종료(Close·Discard) 중에는 isEndingIntentionally 가 켜진다.
        private Lease currentLease;
        private bool isEndingIntentionally;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ResetInstance();

        /// <summary>
        /// 소개 한 번의 정지 소유 기록(F1·F2). Unity 오브젝트가 아니라 패널이 파괴돼도 상태를 읽을 수 있다.
        /// <list type="bullet">
        /// <item><see cref="OwnsPause"/> — 자기 Open 이 발행한 DraftOpened 로 건 정지를 아직 쥐고 있다.</item>
        /// <item><see cref="IsTakenOver"/> — 열린 동안 <b>남의</b> DraftOpened/DraftClosed 가 왔다(경험치 드래프트 등). 이후 정지는 그쪽 소유라
        /// 닫기 이벤트를 내지 않는다.</item>
        /// <item><see cref="IsExternallyStopped"/> — Close·Discard 가 아닌 경로로 패널 GameObject·컴포넌트가 꺼지거나 파괴됐다.</item>
        /// </list>
        /// 이벤트 핸들러는 플래그만 바꾼다 — 정리는 소개 루프가 다음 프레임에 <see cref="Close"/>/<see cref="Abandon"/> 로 한다.
        /// </summary>
        public sealed class Lease
        {
            private enum OwnEvent { None, Opened, Closed }

            private readonly KeeperIntroPanel panel;
            private readonly GameObject panelObject;
            private OwnEvent pendingOwnEvent;
            private bool isEnded;

            public bool OwnsPause { get; private set; }
            public bool IsTakenOver { get; private set; }
            public bool IsExternallyStopped { get; private set; }

            /// <summary>표시·입력에 쓸 수 있는가 — 패널 생존 · 컴포넌트 활성 · body 활성 · 소유 유지 모두.</summary>
            public bool IsUsable => !isEnded && OwnsPause && !IsTakenOver && !IsExternallyStopped && panel != null && panel.IsLive;

            public KeeperIntroPanel Panel => panel;

            internal Lease(KeeperIntroPanel panel)
            {
                this.panel = panel;
                panelObject = panel.gameObject;
                GameEvents.OnDraftOpened += HandleDraftOpened;
                GameEvents.OnDraftClosed += HandleDraftClosed;
            }

            internal void MarkExternallyStopped()
            {
                if (!isEnded) IsExternallyStopped = true;
            }

            internal void OpenOwnModal()
            {
                pendingOwnEvent = OwnEvent.Opened;
                try { panel.ShowBody(); }
                finally { pendingOwnEvent = OwnEvent.None; }
            }

            private void HandleDraftOpened()
            {
                if (isEnded) return;
                if (pendingOwnEvent == OwnEvent.Opened)
                {
                    OwnsPause = true;
                    pendingOwnEvent = OwnEvent.None;
                }
                else if (OwnsPause || pendingOwnEvent != OwnEvent.None) IsTakenOver = true;
            }

            private void HandleDraftClosed()
            {
                if (isEnded) return;
                if (pendingOwnEvent == OwnEvent.Closed)
                {
                    pendingOwnEvent = OwnEvent.None;
                    return;
                }
                if (OwnsPause || pendingOwnEvent != OwnEvent.None) IsTakenOver = true;
            }

            /// <summary>
            /// 자기 모달을 한 번 닫는다(정상 종료·일반 중단). 남이 정지를 가져갔으면 닫기 이벤트를 내지 않는다.
            /// 패널이 살아 있으면 공용 HideBody(DraftClosed 1회), 밖에서 파괴됐으면 남은 자기 정지만 DraftClosed 로 1회 푼다.
            /// 밖에서 꺼졌던 패널은 닫은 뒤 파괴해 열림 기록을 지운다.
            /// </summary>
            public void Close()
            {
                if (isEnded) return;
                bool shouldRelease = OwnsPause && !IsTakenOver;
                if (!shouldRelease)
                {
                    Abandon();
                    return;
                }

                if (panel != null)
                {
                    // 닫기 전에 판정한다 — HideBody 뒤에는 body 가 꺼져 정상 종료도 「꺼짐」으로 보인다.
                    bool wasStoppedOutside = IsExternallyStopped || !panel.IsLive;
                    panel.isEndingIntentionally = true;
                    if (panel.bodyObject != null) RaiseOwn(panel.HideBody);
                    else RaiseOwn(GameEvents.RaiseDraftClosed);
                    if (wasStoppedOutside) Destroy(panel.gameObject);
                    else panel.isEndingIntentionally = false;
                }
                else
                {
                    RaiseOwn(GameEvents.RaiseDraftClosed);
                    if (panelObject != null) Destroy(panelObject);
                }
                End();
            }

            /// <summary>정지를 풀지 않고 패널만 없앤다 — 결과·포기·씬·다른 모달이 정지를 소유할 때.</summary>
            public void Abandon()
            {
                if (isEnded) return;
                if (panel != null)
                {
                    panel.isEndingIntentionally = true;
                }
                if (panelObject != null) Destroy(panelObject);
                End();
            }

            private void RaiseOwn(System.Action action)
            {
                pendingOwnEvent = OwnEvent.Closed;
                try { action(); }
                finally { pendingOwnEvent = OwnEvent.None; }
            }

            private void End()
            {
                isEnded = true;
                OwnsPause = false;
                GameEvents.OnDraftOpened -= HandleDraftOpened;
                GameEvents.OnDraftClosed -= HandleDraftClosed;
                if (panel != null && panel.currentLease == this) panel.currentLease = null;
            }
        }

        /// <summary>
        /// 패널을 비운 채 열고 정지를 건다. 반환한 소유 기록으로만 닫는다.
        /// 공용 열림 기록이 이미 켜져 있어 DraftOpened 가 나가지 않으면 <see cref="Lease.OwnsPause"/> 가 거짓이다 — 호출한 쪽이 양보한다.
        /// </summary>
        public static Lease Open(string title, string epithet) => Open(title, epithet, null);

        /// <summary>
        /// 위와 같고 이름 왼쪽에 보스 초상(A2 · <paramref name="enemyId"/>=EnemyData.enemyId)을 둔다.
        /// 초상은 이름 묶음 안이라 이름과 함께 나타나고 사라진다. 그림이 없으면 초상 칸을 끈다.
        /// </summary>
        public static Lease Open(string title, string epithet, string enemyId)
        {
            var lease = OpenPanel(title, epithet);
            var panel = EnsureInstance();
            if (panel.titleLabel != null)
            {
                UiArtDecor.PlaceLeadingIcon(panel.titleLabel, UiArtKeys.BossPortrait(enemyId), PORTRAIT_SIZE, PORTRAIT_GAP);
            }
            return lease;
        }

        private const float PORTRAIT_SIZE = 96f;
        private const float PORTRAIT_GAP = 16f;

        private static Lease OpenPanel(string title, string epithet)
        {
            var panel = EnsureInstance();
            panel.isEndingIntentionally = false;
            panel.titleLabel.text = title;
            panel.epithetLabel.text = epithet;
            panel.titleGroup.alpha = 0f;
            panel.SetLine(string.Empty, string.Empty, false);

            var lease = new Lease(panel);
            panel.currentLease = lease;
            lease.OpenOwnModal();
            return lease;
        }

        /// <summary>파괴되지 않았고 컴포넌트·GameObject·body 가 모두 켜져 있다.</summary>
        private bool IsLive => this != null && isActiveAndEnabled && bodyObject != null && bodyObject.activeInHierarchy;

        /// <summary>이름·별칭의 투명도(0~1). 쓸 수 없는 상태면 무시한다.</summary>
        public void SetTitleAlpha(float alpha)
        {
            if (IsLive) titleGroup.alpha = Mathf.Clamp01(alpha);
        }

        /// <summary>대사 한 줄과 쪽 표시. 줄이 바뀌면 입력 유예를 다시 건다. 쓸 수 없는 상태면 무시한다.</summary>
        public void SetLine(string line, string pager, bool isReading)
        {
            if (this == null || lineLabel == null) return;
            lineLabel.text = line ?? string.Empty;
            pagerLabel.text = pager ?? string.Empty;
            nextHint.SetActive(isReading);
            inputOpenAt = Time.unscaledTime + INPUT_GRACE_SECONDS;
        }

        // 밖에서 컴포넌트·GameObject 가 꺼지거나 파괴됨 — 기록만 남긴다. 여기서 이벤트·SetActive 를 부르지 않는다(재진입 방지).
        private void OnDisable()
        {
            if (!isEndingIntentionally) currentLease?.MarkExternallyStopped();
        }

        private void OnDestroy()
        {
            if (!isEndingIntentionally) currentLease?.MarkExternallyStopped();
        }

        /// <summary>다음(넘기기) — Space·Enter·Z·패드 A. 프레임당 한 번 부른다.</summary>
        public bool WasConfirmPressed()
        {
            if (!IsInputOpen()) return false;

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame ||
                                     keyboard.zKey.wasPressedThisFrame))
                return true;

            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        }

        /// <summary>대사 더 보기 — ↑·패드 Y. 프레임당 한 번 부른다.</summary>
        public bool WasMorePressed()
        {
            if (!IsInputOpen()) return false;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.upArrowKey.wasPressedThisFrame) return true;

            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonNorth.wasPressedThisFrame;
        }

        // 다른 모달·저장 오버레이에 양보했거나 패널이 꺼진 프레임에는 입력을 읽지 않는다.
        private bool IsInputOpen() =>
            currentLease != null && currentLease.IsUsable &&
            Time.unscaledTime >= inputOpenAt && !SaveStatusOverlay.IsCapturingInput;

        protected override void BuildContent(Transform body)
        {
            bodyObject = body.gameObject;

            // 공용 딤은 남기되(뒤쪽 클릭 차단) 거의 투명하게 — 보스와 플레이어의 크기 대비가 보여야 한다.
            var dim = body.GetComponent<Image>();
            if (dim != null) dim.color = new Color(0f, 0f, 0f, DIM_ALPHA);

            var band = CreateRect(body, "Band", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, BAND_HEIGHT));
            var bandImage = band.AddComponent<Image>();
            bandImage.color = BandColor;
            bandImage.raycastTarget = false;

            var titleRoot = CreateRect(body, "TitleGroup", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            titleGroup = titleRoot.AddComponent<CanvasGroup>();
            titleGroup.blocksRaycasts = false;

            titleLabel = CreateLabel(titleRoot.transform, "Title", new Vector2(0, -350), new Vector2(1600, 60), string.Empty, 46, TitleColor, TextAnchor.MiddleCenter);
            titleLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            epithetLabel = CreateLabel(titleRoot.transform, "Epithet", new Vector2(0, -395), new Vector2(1600, 32), string.Empty, 22, EpithetColor, TextAnchor.MiddleCenter);

            lineLabel = CreateLabel(body, "Line", new Vector2(0, -445), new Vector2(1400, 44), string.Empty, 26, LineColor, TextAnchor.MiddleCenter);
            lineLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            lineLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            // 「더 보기」 안내 — 문구 키가 없어(GameText.csv 범위 밖) 언어와 무관한 기호만 쓴다. 임시 표기.
            pagerLabel = CreateLabel(body, "Pager", new Vector2(0, -490), new Vector2(600, 26), string.Empty, 17, HintColor, TextAnchor.MiddleCenter);

            nextHint = CreateLocalizedLabel(body, "Hint", new Vector2(0, -515), new Vector2(600, 26), StringKey.Dialogue_NextHint, 17, HintColor, TextAnchor.MiddleCenter).gameObject;
            nextHint.SetActive(false);
        }
    }
}

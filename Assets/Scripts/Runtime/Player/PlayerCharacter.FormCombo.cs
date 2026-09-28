using Abyss.Runtime.Events;
using Abyss.Runtime.Feedback;
using Abyss.Runtime.Form;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Run;
using Abyss.Runtime.Stage;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// P04 폼 연계 시범 공용 파트 + <b>A 가드 후 교체 예약</b>. 기획 계약: <c>Docs/production/p04-20260929/implementation-assignment.md</c>.
    /// B(원거리→근접 접근)는 <c>PlayerCharacter.FormComboLunge</c>, C(원거리 표식)는 <c>PlayerCharacter.FormComboMark</c>.
    ///
    /// 🔑 <b>스위치 · 수치는 RunConfig 한 곳</b>(「P04 폼 연계 시범」 헤더)이다. 셋 다 끄면 기존 동작과 같다 —
    /// 각 진입점이 스위치를 먼저 보고 아무것도 바꾸지 않고 돌아간다.
    ///
    /// 🔑 <b>세대(<see cref="comboEpoch"/>)</b>: 룸 · 런 경계와 비활성화마다 올린다. 창 · 예약 · 발사체가 들고 있던 세대가
    /// 지금과 다르면 폐기한다 — 늦게 도착한 것이 새 방 · 새 런에 효과를 남기지 않게.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        private static readonly Color GuardSwapLabelColor = new(1f, 0.9f, 0.45f, 1f);
        private const float COMBO_LABEL_CHARACTER_SIZE = 0.07f;
        private const float COMBO_LABEL_GAP = 0.35f;
        private static readonly Color ComboLabelBackdropColor = new(0.04f, 0.03f, 0.07f, 0.72f);

        private int comboEpoch;
        private FormComboLabel comboPlayerLabel;
        private FormComboPlayerFx comboPlayerFx;

        // A 예약 교체 성공 확인(문구 · 확장 고리). 시각 전용 — 판정에는 쓰지 않는다.
        private bool isGuardSwapSuccessShown;
        private float guardSwapSuccessEnd;

        // ── A 상태 ──
        // 창이 열린 동안 isGuardSwapWindowOpen 은 예약 뒤에도 true — 예약 전 마감은 guardSwapWindowEnd(입력 허용),
        // 예약 뒤 마감은 guardSwapExecutionEnd(입력 시각 + 실행 대기)다.
        private bool isGuardSwapWindowOpen;
        private bool isGuardSwapReserved;
        private float guardSwapWindowEnd;
        private float guardSwapExecutionEnd;
        private int guardSwapEpoch;
        private FormData guardSwapFromForm;
        private FormData guardSwapToForm;
        private Weapon.WeaponData guardSwapWeapon;

        /// <summary>A 교체 입력이 예약돼 실행을 기다리는가(디버그 · 표시 조회용).</summary>
        public bool IsGuardSwapReserved => isGuardSwapReserved;

        /// <summary>
        /// 연계 설정. 체력 초기화와 같은 순서 — RunManager 가 들고 있는 config(에디터 오버라이드)를 우선, 없으면 공유 SoT.
        /// null 이면 A/B/C 모두 꺼진 것으로 본다.
        /// </summary>
        private static RunConfig ComboConfig =>
            RunManager.HasInstance && RunManager.Instance.Config != null
                ? RunManager.Instance.Config
                : RunConfigProvider.Current;

        private static bool IsGuardSwapReserveOn
        {
            get { var cfg = ComboConfig; return cfg != null && cfg.isGuardSwapReserveEnabled; }
        }

        /// <summary>같은 폼인가. 에셋이 같거나 formId 가 같으면 같은 폼이다.</summary>
        private static bool IsSameForm(FormData a, FormData b)
        {
            if (a == b) return true;
            return a != null && b != null && !string.IsNullOrEmpty(a.formId) && a.formId == b.formId;
        }

        private static void LogCombo(string tag, string message)
        {
            var cfg = ComboConfig;
            if (cfg != null && cfg.isFormComboLogEnabled) Debug.Log($"[FormCombo:{tag}] {message}");
        }

        // ====== 공용 수명 ======

        // PlayerCharacter.Movement 의 OnEnable/OnDisable 에서 호출(partial 중복 정의 회피).
        private void SubscribeFormComboEvents()
        {
            GameEvents.OnRoomEntered += HandleRoomEnteredForCombo;
            GameEvents.OnRunStarted += HandleRunBoundaryForCombo;
            GameEvents.OnRunEnded += HandleRunBoundaryForCombo;
            GameEvents.OnRunAbandoned += HandleRunBoundaryForCombo;
            GameEvents.OnPlayerDead += HandlePlayerDeadForCombo;
            GameEvents.OnDraftOpened += HandleGameplayHaltedForCombo;
            GameEvents.OnGamePaused += HandleGameplayHaltedForCombo;
            OnHpChanged += HandleHpChangedForCombo;
            if (formController != null) formController.OnSwapCompleted += HandleSwapCompletedForLunge;
        }

        private void UnsubscribeFormComboEvents()
        {
            GameEvents.OnRoomEntered -= HandleRoomEnteredForCombo;
            GameEvents.OnRunStarted -= HandleRunBoundaryForCombo;
            GameEvents.OnRunEnded -= HandleRunBoundaryForCombo;
            GameEvents.OnRunAbandoned -= HandleRunBoundaryForCombo;
            GameEvents.OnPlayerDead -= HandlePlayerDeadForCombo;
            GameEvents.OnDraftOpened -= HandleGameplayHaltedForCombo;
            GameEvents.OnGamePaused -= HandleGameplayHaltedForCombo;
            OnHpChanged -= HandleHpChangedForCombo;
            if (formController != null) formController.OnSwapCompleted -= HandleSwapCompletedForLunge;

            ResetFormCombo("비활성");
        }

        /// <summary>세대를 올리고 A/B/C 를 모두 거둔다(룸 · 런 경계 · 비활성).</summary>
        private void ResetFormCombo(string reason)
        {
            comboEpoch++;
            DiscardGuardSwap(reason);
            CancelLunge(reason);
            ClearRangedMark(reason);
            ClearComboPlayerFx();
        }

        private void HandleRoomEnteredForCombo(RoomData _) => ResetFormCombo("룸 변경");
        private void HandleRunBoundaryForCombo() => ResetFormCombo("런 변경");

        private void HandlePlayerDeadForCombo()
        {
            DiscardGuardSwap("사망");
            CancelLunge("사망");
            ClearRangedMark("사망");
            ClearComboPlayerFx();
        }

        /// <summary>정지 · 모달(드래프트 정지를 빌리는 모든 창)이 열렸다. C 표식은 시간이 멈출 뿐 남는다.</summary>
        private void HandleGameplayHaltedForCombo()
        {
            DiscardGuardSwap("정지/모달");
            CancelLunge("정지/모달");
            ClearComboPlayerFx();
        }

        /// <summary>
        /// 피격 = 막지 못한 HP 감소. 가드로 막은 피해(<see cref="IsSuppressingHitStun"/>)는 경직이 없으므로 피격으로 보지 않는다.
        /// </summary>
        private void HandleHpChangedForCombo(int previousHp, int currentHp)
        {
            if (currentHp >= previousHp || isSuppressingHitStun) return;
            DiscardGuardSwap("피격");
            CancelLunge("피격");
        }

        /// <summary>PlayerCharacter.Update 끝에서 매 프레임.</summary>
        private void UpdateFormCombo()
        {
            UpdateGuardSwapReserve();
            UpdateLunge();
            UpdateRangedMark();
            UpdateComboPlayerFx();
        }

        /// <summary>
        /// 시각 효과의 시간 · 스위치 정리. A 성공 확인이 끝나면 문구를 다음 상태로 돌리고,
        /// 스위치가 꺼지면 남은 성공 고리 · B 페이드를 즉시 거둔다.
        /// </summary>
        private void UpdateComboPlayerFx()
        {
            if (isGuardSwapSuccessShown && (Time.time >= guardSwapSuccessEnd || !IsGuardSwapReserveOn))
                StopGuardSwapSuccessFx();

            if (comboPlayerFx != null && !isLungeReady && !isLunging && !IsRangedToMeleeLungeOn)
                comboPlayerFx.ClearLunge();
        }

        /// <summary>A 성공 확인 · B 페이드까지 도형을 즉시 거둔다(정지 · 룸/런 이동 · 사망 · 비활성).</summary>
        private void ClearComboPlayerFx()
        {
            StopGuardSwapSuccessFx();
            if (comboPlayerFx != null) comboPlayerFx.ClearAll();
        }

        /// <summary>
        /// 플레이어 머리 위 문구 하나를 A 예약 &gt; A 성공 확인 &gt; B 준비 &gt; A 입력 가능 순으로 쓰고, 도형(고리 · 쐐기)을
        /// 같은 상태로 맞춘다(A 예약과 B 준비는 조건상 동시에 켜지지 않는다). A 입력 가능은 가장 약한 안내라 B 문구를 가리지 않는다.
        /// </summary>
        private void RefreshComboPlayerLabel()
        {
            // 비활성 · 파괴 도중에는 문구를 새로 만들지 않는다 — 씬을 닫는 중에 오브젝트가 생기면 남는다.
            if (!isActiveAndEnabled)
            {
                if (comboPlayerLabel != null) comboPlayerLabel.Hide();
                if (comboPlayerFx != null) comboPlayerFx.ClearAll();
                return;
            }

            bool isGuardSwapHintShown = isGuardSwapWindowOpen && !isGuardSwapReserved;
            if (isGuardSwapReserved || isGuardSwapHintShown || isLungeReady) ResolveComboPlayerFx();
            if (comboPlayerFx != null)
            {
                comboPlayerFx.SetHint(isGuardSwapHintShown);
                comboPlayerFx.SetReserve(isGuardSwapReserved);
                comboPlayerFx.SetReady(isLungeReady && !isGuardSwapReserved, lungeDirection);
            }

            if (isGuardSwapReserved)
            {
                ResolveComboPlayerLabel().Show(Loc.Get(StringKey.Combo_GuardSwapReserved), GuardSwapLabelColor, true);
                return;
            }
            if (isGuardSwapSuccessShown)
            {
                ResolveComboPlayerLabel().Show(Loc.Get(StringKey.Combo_GuardSwapSuccess), GuardSwapLabelColor);
                return;
            }
            if (isLungeReady)
            {
                ResolveComboPlayerLabel().Show(Loc.Get(StringKey.Combo_LungeReady), LungeLabelColor);
                return;
            }
            if (isGuardSwapHintShown)
            {
                ResolveComboPlayerLabel().Show(Loc.Get(StringKey.Combo_GuardSwapReady), GuardSwapLabelColor);
                return;
            }
            if (comboPlayerLabel != null) comboPlayerLabel.Hide();
        }

        private FormComboLabel ResolveComboPlayerLabel()
        {
            if (comboPlayerLabel == null)
            {
                comboPlayerLabel = FormComboLabel.Create("FormComboPlayerLabel", transform,
                    ComputeComboLabelOffset(transform), COMBO_LABEL_CHARACTER_SIZE);
                comboPlayerLabel.EnableBackdrop(ComboLabelBackdropColor);
            }
            return comboPlayerLabel;
        }

        /// <summary>A/B 도형 묶음. 처음 필요할 때 1회 만든다 — 몸 크기는 콜라이더 경계에서(없으면 1.5 × 0.8 유닛).</summary>
        private FormComboPlayerFx ResolveComboPlayerFx()
        {
            if (comboPlayerFx == null)
            {
                var col = GetComponentInChildren<Collider2D>();
                Vector3 center = col != null ? col.bounds.center - transform.position : new Vector3(0f, 0.75f, 0f);
                float height = col != null ? col.bounds.size.y : 1.5f;
                float halfWidth = col != null ? col.bounds.extents.x : 0.4f;
                center.z = 0f;
                comboPlayerFx = FormComboPlayerFx.Create("FormComboPlayerFx", transform, center, height, halfWidth);
            }
            return comboPlayerFx;
        }

        /// <summary>문구 높이 = 대상 콜라이더 윗면 + 여백. 콜라이더가 없으면 1.5 유닛 위.</summary>
        private static Vector3 ComputeComboLabelOffset(Transform owner)
        {
            var col = owner.GetComponentInChildren<Collider2D>();
            float top = col != null ? col.bounds.max.y - owner.position.y : 1.5f;
            return new Vector3(0f, top + COMBO_LABEL_GAP, 0f);
        }

        /// <summary>문구 오브젝트는 대상의 자식이 아니므로 직접 치운다(PlayerCharacter.Skills 의 OnDestroy 가 부른다).</summary>
        private void DestroyFormComboLabels()
        {
            if (comboPlayerLabel != null) Destroy(comboPlayerLabel.gameObject);
            if (markLabel != null) Destroy(markLabel.gameObject);
            if (comboPlayerFx != null) Destroy(comboPlayerFx.gameObject);
            comboPlayerLabel = null;
            markLabel = null;
            comboPlayerFx = null;
        }

        // ====== A — 저스트 가드 후 교체 예약 ======

        /// <summary>
        /// 가드 성공 뒤(자동 반격 판정 다음) 부른다. <b>저스트 가드만</b> 창을 연다 — 일반 가드는 예약이 없다.
        /// 현재 · 대상 폼이 없거나 같으면 열지 않는다.
        ///
        /// 🔑 이미 예약이 걸려 있으면 <b>그 예약을 그대로 둔다</b> — 새 저스트 가드가 예약 마감(입력 시각 + 실행 대기)을
        /// 늘리지 않는다. 슬롯 · 장비 유효성은 매 프레임 계속 본다. 예약 전(입력 대기) 창은 새 저스트 가드가 새로 연다.
        /// </summary>
        private void OpenGuardSwapWindow(GuardOutcome outcome)
        {
            if (outcome != GuardOutcome.JustGuarded) return;
            if (!IsGuardSwapReserveOn || isDead || formController == null) return;

            if (isGuardSwapReserved && IsGuardSwapAlive())
            {
                LogCombo("A", $"저스트 가드 — 기존 예약 유지 (남은 {Mathf.Max(0f, guardSwapExecutionEnd - Time.time):F2}s, 연장 없음)");
                return;
            }

            FormData from = formController.CurrentForm;
            FormData to = formController.OtherForm;
            if (from == null || to == null || IsSameForm(from, to))
            {
                LogCombo("A", "창 안 열림 — 현재/대상 폼이 없거나 같다");
                return;
            }

            DiscardGuardSwap(null);  // 예약 전 창이 남아 있으면 조용히 닫고 새로 연다

            float window = ComboConfig.guardSwapReserveWindow;
            isGuardSwapWindowOpen = true;
            guardSwapWindowEnd = Time.time + window;
            guardSwapEpoch = comboEpoch;
            guardSwapFromForm = from;
            guardSwapToForm = to;
            guardSwapWeapon = CurrentWeapon;
            RefreshComboPlayerLabel();  // 입력 전에도 「연계 가능」을 보인다
            LogCombo("A", $"저스트 가드 — 교체 입력 창 {window:F2}s ({from.formId} → {to.formId})");
        }

        /// <summary>
        /// 교체 입력(<c>OnFormSwap</c>)이 먼저 묻는다. <c>true</c> 면 입력을 여기서 소비했다(즉시 교체 · 예약 · 중복 무시).
        /// 창이 없거나 막 닫혔으면 <c>false</c> — 스위치가 꺼졌을 때와 같은 기존 동작이다.
        ///
        /// 🔑 마감은 <b>입력 콜백 안에서 먼저</b> 본다(<see cref="IsGuardSwapAlive"/>) — Update 순서에 따라
        /// 이미 지난 창의 입력이 예약으로 새지 않게.
        /// 🔑 지금 바로 바꿀 수 있으면 여기서 <c>RequestSwap</c> 1회 — 성공에만 A 성공 확인을 띄운다.
        /// 못 바꾸면 1회 예약하고 마감은 입력 시각 + <c>guardSwapExecutionWait</c>. 반복 입력은 마감을 늘리지 않는다.
        /// </summary>
        private bool TryReserveGuardSwap()
        {
            if (formController == null || !IsGuardSwapAlive()) return false;

            if (isGuardSwapReserved)
            {
                LogCombo("A", "이미 예약됨 — 추가 입력 무시(마감 연장 없음)");
                return true;
            }

            if (formController.CanSwap)
            {
                // 창을 먼저 닫는다 — RequestSwap 이 동기로 이벤트를 돌리는 동안 다시 들어와도 두 번 쏘지 않는다.
                DiscardGuardSwap(null);
                bool isSwapped = formController.RequestSwap();
                LogCombo("A", isSwapped ? "창 안 즉시 교체" : "창 안 즉시 교체 거부 — RequestSwap 이 막음");
                if (isSwapped) StartGuardSwapSuccessFx();
                return true;
            }

            float wait = ComboConfig.guardSwapExecutionWait;
            isGuardSwapReserved = true;
            guardSwapExecutionEnd = Time.time + wait;
            RefreshComboPlayerLabel();
            LogCombo("A", $"교체 예약 (실행 대기 {wait:F2}s, " +
                          $"state={formController.State}, cooldown={formController.CurrentCooldown:F2}s)");
            return true;
        }

        /// <summary>
        /// 창 · 예약이 아직 유효한가. 폐기 조건과 마감(예약 전 = 입력 창, 예약 뒤 = 실행 대기)을 보고,
        /// 지났으면 그 자리에서 거두고 <c>false</c>. 입력 콜백과 매 프레임이 같은 판정을 쓴다.
        /// </summary>
        private bool IsGuardSwapAlive()
        {
            if (!isGuardSwapWindowOpen) return false;

            string reason = ResolveGuardSwapDiscardReason();
            if (reason != null)
            {
                DiscardGuardSwap(reason);
                return false;
            }

            if (isGuardSwapReserved)
            {
                if (Time.time > guardSwapExecutionEnd)
                {
                    DiscardGuardSwap("시간 초과(예약 뒤 실행 대기 안에 교체 조건이 안 열림)");
                    return false;
                }
            }
            else if (Time.time > guardSwapWindowEnd)
            {
                // 입력 없이 창만 지나간 것은 실패가 아니다 — 조용히 닫는다.
                DiscardGuardSwap(null);
                return false;
            }
            return true;
        }

        /// <summary>매 프레임 — 폐기 조건 · 마감을 먼저 보고, 예약이 있고 기존 교체 조건이 열리면 1회 실행한다.</summary>
        private void UpdateGuardSwapReserve()
        {
            if (!IsGuardSwapAlive()) return;

            if (!isGuardSwapReserved || !formController.CanSwap) return;

            // 창을 먼저 닫는다 — RequestSwap 이 동기로 이벤트를 돌리는 동안 다시 들어와도 두 번 쏘지 않는다.
            DiscardGuardSwap(null);
            bool isSwapped = formController.RequestSwap();
            LogCombo("A", isSwapped ? "예약 교체 실행" : "예약 교체 거부 — RequestSwap 이 막음");
            if (isSwapped) StartGuardSwapSuccessFx();  // 성공 확인은 A 경로(즉시 · 예약)의 성공에만 — 거부 · 일반 교체 · 일반 가드엔 없다
        }

        /// <summary>A 교체 성공 확인(즉시 · 예약 공통): 짧은 확장 고리 + 「연계 교체」 문구(<see cref="FormComboPlayerFx.SUCCESS_DURATION"/>).</summary>
        private void StartGuardSwapSuccessFx()
        {
            if (!isActiveAndEnabled) return;
            isGuardSwapSuccessShown = true;
            guardSwapSuccessEnd = Time.time + FormComboPlayerFx.SUCCESS_DURATION;
            ResolveComboPlayerFx().PlaySuccess();
            RefreshComboPlayerLabel();
        }

        /// <summary>성공 확인 문구 · 고리를 거둔다. 켜져 있지 않으면 아무것도 안 한다.</summary>
        private void StopGuardSwapSuccessFx()
        {
            if (!isGuardSwapSuccessShown) return;
            isGuardSwapSuccessShown = false;
            if (comboPlayerFx != null) comboPlayerFx.StopSuccess();
            RefreshComboPlayerLabel();
        }

        private string ResolveGuardSwapDiscardReason()
        {
            if (!IsGuardSwapReserveOn) return "스위치 꺼짐";
            if (isDead) return "사망";
            if (guardSwapEpoch != comboEpoch) return "룸/런 변경";
            if (formController == null) return "폼 컨트롤러 없음";
            if (formController.CurrentForm != guardSwapFromForm || formController.OtherForm != guardSwapToForm)
                return "슬롯 폼 변경";
            if (CurrentWeapon != guardSwapWeapon) return "장비 교체";
            return null;
        }

        /// <summary>창 · 예약을 거둔다. <paramref name="reason"/> 이 null 이면 로그 없이 조용히.</summary>
        private void DiscardGuardSwap(string reason)
        {
            // 이전 성공 확인이 떠 있으면 함께 거둔다(새 창 · 피격 · 정지 · 룸 이동 · 사망).
            StopGuardSwapSuccessFx();
            if (!isGuardSwapWindowOpen && !isGuardSwapReserved) return;

            bool wasReserved = isGuardSwapReserved;
            isGuardSwapWindowOpen = false;
            isGuardSwapReserved = false;
            guardSwapFromForm = null;
            guardSwapToForm = null;
            guardSwapWeapon = null;
            RefreshComboPlayerLabel();

            if (reason != null) LogCombo("A", $"{(wasReserved ? "예약 폐기" : "창 닫힘")} — {reason}");
        }
    }
}

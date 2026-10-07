using Abyss.Runtime.Localization;
using Abyss.Runtime.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// <see cref="FirstPlayTutorialController"/>의 진행·표시 파트 — 6행동 기록, 다음 안내 고르기,
    /// 현재 장치에 맞는 실제 바인딩 글자.
    ///
    /// 순서는 이동 → 점프 → 대시 → 공격 → 폼 교체로 안내하지만 <b>기록은 순서와 무관하다</b> — 먼저 해 버린
    /// 행동은 다시 시키지 않는다. 드래프트는 실제 드래프트가 열렸을 때만 안내한다.
    /// </summary>
    public sealed partial class FirstPlayTutorialController
    {
        private enum TutorialStep
        {
            Move,
            Jump,
            Dash,
            Attack,
            Swap,
            Draft
        }

        private const int STEP_COUNT = 6;

        private readonly bool[] doneSteps = new bool[STEP_COUNT];
        private int doneCount;

        private bool IsDone(TutorialStep step) => doneSteps[(int)step];

        private void MarkDone(TutorialStep step)
        {
            int index = (int)step;
            if (doneSteps[index]) return;

            doneSteps[index] = true;
            doneCount++;
            isDirty = true;

            if (doneCount >= STEP_COUNT && HasSpatialEvidence) Complete();
        }

        private static TutorialStep ToStep(PlayerTutorialAction action) => action switch
        {
            PlayerTutorialAction.Jump => TutorialStep.Jump,
            PlayerTutorialAction.Dash => TutorialStep.Dash,
            PlayerTutorialAction.Attack => TutorialStep.Attack,
            _ => TutorialStep.Move
        };

        /// <summary>
        /// 지금 안내할 플레이 행동. 기본 넷 중 안 한 것이 먼저고, 폼 교체는 <b>두 번째 폼을 가진 뒤에만</b> —
        /// 없으면 null(대기 문구만 띄우고 기다린다, 진행은 막지 않는다).
        /// </summary>
        private TutorialStep? CurrentPlayStep()
        {
            for (int i = (int)TutorialStep.Move; i <= (int)TutorialStep.Attack && IsInFirstRoom; i++)
            {
                if (!doneSteps[i]) return (TutorialStep)i;
            }

            if (!IsDone(TutorialStep.Swap) && formController != null && formController.OtherForm != null)
            {
                return TutorialStep.Swap;
            }
            return null;
        }

        private bool IsShowingDraftHint => isModalOpen && isDraftSession && !IsDone(TutorialStep.Draft);

        private bool IsShowingObjective =>
            !isModalOpen && objectiveTimer > 0f && director != null && director.CurrentStepIndex == 0;

        /// <summary>
        /// 기본 넷은 끝났고 남은 것(두 번째 폼 없는 교체·아직 안 열린 드래프트)을 기다리는 중인가.
        /// 이때도 띠를 남겨 건너뛰기(버튼·패드 Select)에 계속 닿게 한다. 완료되면 <see cref="Complete"/>가 먼저 끝낸다.
        /// </summary>
        private bool IsWaitingForRemaining => !isModalOpen && !CurrentPlayStep().HasValue && doneCount < STEP_COUNT;

        private bool ShouldShowPanel()
        {
            if (isPaused) return false;
            if (isModalOpen) return IsShowingDraftHint;
            return CurrentPlayStep().HasValue || IsShowingObjective || IsWaitingForRemaining;
        }

        private void Render(bool isVisible)
        {
            if (panel == null) return;

            panel.SetVisible(isVisible);
            panel.SetSpatialVisible(false);
            if (!isVisible) return;
            if (RenderSpatialHint()) return;
            panel.SetCompact(IsWaitingForRemaining);

            string header = IsShowingObjective
                ? SafeGet(StringKey.Tutorial_Objective)
                : SafeGetFormat(StringKey.Tutorial_ProgressFormat, doneCount, STEP_COUNT);

            string hint = string.Empty;
            if (IsShowingDraftHint)
            {
                hint = SafeGet(StringKey.Tutorial_Draft);
            }
            else if (!isModalOpen)
            {
                TutorialStep? step = CurrentPlayStep();
                if (step.HasValue) hint = SafeGetFormat(HintKey(step.Value), BindingText(ActionName(step.Value)));
                else if (IsWaitingForRemaining) header = string.Empty;
            }

            panel.SetContent(header, hint, PadSkipHintText());
        }

        private static string HintKey(TutorialStep step) => step switch
        {
            TutorialStep.Jump => StringKey.Tutorial_JumpFormat,
            TutorialStep.Dash => StringKey.Tutorial_DashFormat,
            TutorialStep.Attack => StringKey.Tutorial_AttackFormat,
            TutorialStep.Swap => StringKey.Tutorial_SwapFormat,
            _ => StringKey.Tutorial_MoveFormat
        };

        // InputSystem_Actions.inputactions 의 Player 맵 액션 이름.
        private static string ActionName(TutorialStep step) => step switch
        {
            TutorialStep.Jump => "Jump",
            TutorialStep.Dash => "Dash",
            TutorialStep.Attack => "Attack",
            TutorialStep.Swap => "FormSwap",
            _ => "Move"
        };

        /// <summary>패드를 쓰는 중이면 Select 길게 누르기 안내(누르는 중이면 진행률 포함). 키보드면 빈 문자열 — 버튼만 보인다.</summary>
        private string PadSkipHintText()
        {
            var pad = Gamepad.current;
            if (pad == null || !IsUsingGamepad()) return string.Empty;

            string text = SafeGetFormat(StringKey.Tutorial_PadSkipHintFormat, pad.selectButton.displayName);
            if (padSkipHeld <= 0f) return text;

            int percent = Mathf.Clamp(Mathf.RoundToInt(padSkipHeld / PAD_SKIP_HOLD_SECONDS * 100f), 0, 100);
            return $"{text}  {percent}%";
        }

        // ───────────────────────── 바인딩 표시 ─────────────────────────

        /// <summary>
        /// 플레이어 PlayerInput 의 실제 액션에서, 지금 쓰는 장치(키보드·마우스 또는 패드)에 걸린 첫 바인딩의
        /// 표시 문자열. 재지정(override)도 effectivePath·GetBindingDisplayString 이 반영한다.
        /// 액션이 없거나 그 장치에 바인딩이 없으면 「미지정」.
        /// </summary>
        private string BindingText(string actionName)
        {
            InputAction action = playerInput != null && playerInput.actions != null
                ? playerInput.actions.FindAction(actionName)
                : null;
            if (action == null) return SafeGet(StringKey.Tutorial_Unbound);

            bool isPad = IsUsingGamepad();
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].isPartOfComposite) continue;
                if (!BindingTargetsDevice(action, i, isPad)) continue;

                string display = action.GetBindingDisplayString(i);
                if (!string.IsNullOrEmpty(display)) return display;
            }
            return SafeGet(StringKey.Tutorial_Unbound);
        }

        private static bool BindingTargetsDevice(InputAction action, int index, bool isPad)
        {
            var bindings = action.bindings;
            if (!bindings[index].isComposite) return PathTargetsDevice(bindings[index].effectivePath, isPad);

            for (int i = index + 1; i < bindings.Count && bindings[i].isPartOfComposite; i++)
            {
                if (PathTargetsDevice(bindings[i].effectivePath, isPad)) return true;
            }
            return false;
        }

        private static bool PathTargetsDevice(string path, bool isPad)
        {
            if (string.IsNullOrEmpty(path)) return false;

            if (isPad)
            {
                var pad = Gamepad.current;
                return pad != null && InputControlPath.TryFindControl(pad, path) != null;
            }

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            return (keyboard != null && InputControlPath.TryFindControl(keyboard, path) != null)
                || (mouse != null && InputControlPath.TryFindControl(mouse, path) != null);
        }

        /// <summary>
        /// 지금 패드를 쓰는가. PlayerInput 이 짝지은 장치가 한쪽뿐이면 그것을 따르고,
        /// 정할 수 없으면 마지막으로 입력이 들어온 쪽을 본다.
        /// </summary>
        private bool IsUsingGamepad()
        {
            if (playerInput != null)
            {
                bool hasPad = false;
                bool hasKeyboard = false;
                foreach (var device in playerInput.devices)
                {
                    if (device is Gamepad) hasPad = true;
                    else if (device is Keyboard || device is Mouse) hasKeyboard = true;
                }
                if (hasPad != hasKeyboard) return hasPad;
            }

            var pad = Gamepad.current;
            var keyboard = Keyboard.current;
            if (pad == null) return false;
            if (keyboard == null) return true;
            return pad.lastUpdateTime > keyboard.lastUpdateTime;
        }
    }
}

using Abyss.Runtime.Player;
using UnityEngine;

namespace Abyss.Runtime.UI
{
    /// <summary>행동 기록과 목적지 근거를 분리한다. 부족한 근거가 런 진행을 잠그지는 않는다.</summary>
    public sealed partial class FirstPlayTutorialController
    {
        private readonly bool[] spatialEvidence = new bool[4];
        private bool hasJumped;
        private bool hasDashed;
        private bool wasAirborneAfterJump;
        private bool wasDashing;
        private float dashStartX;
        private int spatialRoomVersion = -1;
        private Vector3 spatialTarget;
        private UnityEngine.Camera hintCamera;
        private int previousSpatialCount;
        private float spatialFeedbackUntil;

        private bool IsInFirstRoom => director != null && director.IsTutorialRoom;
        private bool HasSpatialEvidence => spatialEvidence[0] && spatialEvidence[1] && spatialEvidence[2] && spatialEvidence[3];

        private void RecordSpatialAction(PlayerTutorialAction action)
        {
            if (!IsInFirstRoom) return;
            if (action == PlayerTutorialAction.Jump) hasJumped = true;
            if (action == PlayerTutorialAction.Dash)
            {
                hasDashed = true;
                dashStartX = player.transform.position.x;
            }
        }

        private void UpdateSpatialLearning()
        {
            if (!CanCountPlay || !IsInFirstRoom || player == null) return;
            if (spatialRoomVersion != director.TutorialRoomVersion)
            {
                spatialRoomVersion = director.TutorialRoomVersion;
                System.Array.Clear(spatialEvidence, 0, spatialEvidence.Length);
                hasJumped = hasDashed = wasAirborneAfterJump = wasDashing = false;
                previousSpatialCount = 0;
                spatialFeedbackUntil = 0f;
            }
            float x = player.transform.position.x;
            spatialEvidence[0] |= IsDone(TutorialStep.Move) && x >= -14f;
            if (hasJumped && !player.IsGrounded) wasAirborneAfterJump = true;
            if (wasAirborneAfterJump && player.IsGrounded)
            {
                spatialEvidence[1] |= x >= -11.5f && x <= -6f;
                wasAirborneAfterJump = hasJumped = false;
            }
            wasDashing |= hasDashed && player.IsDashing;
            if (wasDashing && !player.IsDashing)
            {
                spatialEvidence[2] |= dashStartX >= -10f && dashStartX <= -6.8f && x >= -6.95f;
                wasDashing = hasDashed = false;
            }
            spatialEvidence[3] |= director.TutorialCombatKills > 0;
            int count = 0;
            foreach (bool evidence in spatialEvidence) if (evidence) count++;
            if (count > previousSpatialCount)
            {
                previousSpatialCount = count;
                spatialFeedbackUntil = Time.time + 0.6f;
                isDirty = true;
            }
            if (spatialEvidence[3]) MarkDone(TutorialStep.Attack);
            if (doneCount >= STEP_COUNT && HasSpatialEvidence) Complete();
        }

        private bool RenderSpatialHint()
        {
            if (!IsInFirstRoom || isPaused || isModalOpen || player == null) return false;
            float x = player.transform.position.x;
            int index = x < -14f ? 0 : x < -10f ? 1 : x < -6f ? 2 : 3;
            panel.SetCompact(true);
            panel.SetContent(string.Empty, string.Empty, PadSkipHintText());
            if (Time.time < spatialFeedbackUntil)
            {
                panel.SetSpatialContent(-1, "✓", string.Empty);
                panel.SetSpatialVisible(true);
                return true;
            }
            if (spatialEvidence[index]) return true;
            TutorialStep step = (TutorialStep)index;
            float targetX = index switch { 0 => -14f, 1 => -11f, 2 => -6.8f, _ => 0f };
            spatialTarget = new Vector3(targetX, player.transform.position.y + 2.2f, 0f);
            panel.SetSpatialContent(index, BindingText(ActionName(step)),
                SafeGetFormat(HintKey(step), BindingText(ActionName(step))));
            panel.SetSpatialVisible(true);
            return true;
        }

        private void LateUpdate()
        {
            if (panel == null || isEnding) return;
            if (hintCamera == null) hintCamera = UnityEngine.Camera.main;
            if (hintCamera != null) panel.PlaceSpatialHint(hintCamera, spatialTarget);
        }
    }
}

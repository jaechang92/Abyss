using Abyss.Runtime.Feedback;
using Abyss.Runtime.Flow;
using Abyss.Runtime.Lobby;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Run;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>실제 사망 후 로비 복귀의 짧은 흔적 안내. 포커스·입력·재입장·정산을 소유하지 않는다.</summary>
    public sealed class LobbyReturnPresenter : MonoBehaviour
    {
        private RunSummary summary;
        private LobbyPlayerController player;
        private GameObject canvas;
        private CanvasGroup group;
        private Text title;
        private Text detail;
        private LocalizationLanguage shownLanguage;
        private float elapsed;
        private float expiresAt;
        private int transitionVersion;
        private bool hasShown;

        private void Start()
        {
            summary = JourneyPresentation.ConsumeReturn();
            if (summary == null) { Destroy(gameObject); return; }
            transitionVersion = JourneyPresentation.CurrentTransitionVersion;
            expiresAt = Time.realtimeSinceStartup + 8f;
            canvas = UiFactory.CreateOverlayCanvas("ReturnEcho", UiSortingOrder.Hud);
            SceneManager.MoveGameObjectToScene(canvas, gameObject.scene);
            var band = UiFactory.CreateRect(canvas.transform, "Band", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(900f, 108f));
            var back = band.AddComponent<Image>();
            back.color = new Color(0.07f, 0.08f, 0.12f, 0.75f);
            back.raycastTarget = false;
            group = band.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            title = UiFactory.CreateLabel(band.transform, "Title", new Vector2(0f, 20f), new Vector2(850f, 36f),
                string.Empty, 26, new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleCenter);
            detail = UiFactory.CreateLabel(band.transform, "Detail", new Vector2(0f, -20f), new Vector2(850f, 32f),
                string.Empty, 22, new Color(0.78f, 0.78f, 0.82f), TextAnchor.MiddleCenter);
            title.raycastTarget = detail.raycastTarget = false;
            RefreshText();
        }

        private void Update()
        {
            if (summary == null || group == null) return;
            if (JourneyPresentation.CurrentTransitionVersion != transitionVersion || Time.realtimeSinceStartup >= expiresAt)
            {
                Destroy(gameObject);
                return;
            }
            if (player == null) player = FindAnyObjectByType<LobbyPlayerController>();
            bool isCovered = (SceneFlowController.HasInstance && SceneFlowController.Instance.IsLoading) ||
                SaveStatusOverlay.IsCapturingInput || LobbyMenuPanel.IsOpen || SettingsPanel.IsOpen || CodexPanel.IsOpen ||
                player == null || player.InputLocked;
            if (isCovered) { group.alpha = 0f; return; }
            if (!hasShown)
            {
                if (!player.IsGrounded) return;
                if (player.TryGetComponent<Rigidbody2D>(out var bodyPhysics) && bodyPhysics.linearVelocity.y > 0.1f) return;
                hasShown = true;
                var body = player.GetComponentInChildren<SpriteRenderer>();
                Vector3 feet = body != null ? new Vector3(body.bounds.center.x, body.bounds.min.y, body.transform.position.z)
                    : player.transform.position;
                ArrivalDustFx.Play(feet, 0.35f, transitionVersion, withSound: false);
            }
            if (shownLanguage != Loc.CurrentLanguage) RefreshText();
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Min(Mathf.Clamp01(elapsed / 0.2f), Mathf.Clamp01((2f - elapsed) / 0.3f));
            if (elapsed >= 2f) Destroy(gameObject);
        }

        private void RefreshText()
        {
            shownLanguage = Loc.CurrentLanguage;
            title.text = Loc.Get(summary.enemiesKilled > 0 ? "Journey_ReturnTitle" : "Journey_ReturnEarlyTitle");
            string stage = summary.reached.HasRecord ? summary.reached.stageNumber.ToString() : "—";
            detail.text = Loc.GetFormat("Journey_ReturnDetailFormat", stage, summary.enemiesKilled, summary.abyssEarned);
        }

        private void OnDisable()
        {
            if (canvas != null) canvas.SetActive(false);
        }

        private void OnDestroy()
        {
            if (canvas != null) Destroy(canvas);
        }
    }
}

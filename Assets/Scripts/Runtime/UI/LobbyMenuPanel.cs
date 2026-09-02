using System;
using Abyss.Runtime.Flow;
using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 로비 전용 메뉴(ESC). 완주 루프 계획 Phase 0 잔손질.
    ///
    /// Run 씬의 일시정지 구조를 그대로 쓸 수 없다: 정지 상태를 소유하는 GameFlowController가 로비에는 없고,
    /// 로비 입력은 <see cref="Lobby.LobbyPlayerController"/>가 직접 받는다. 그래서 <b>timeScale은 건드리지 않고</b>
    /// 플레이어 입력만 잠근다 — 로비에는 멈춰야 할 전투도 타이머도 없고, timeScale을 만지면 씬 전환 중
    /// 0으로 남는 사고 경로만 생긴다.
    ///
    /// <see cref="SettingsPanel"/>과 같이 런타임 동적 생성이라 LobbySceneBuilder 수정도 씬 재빌드도 필요 없다.
    /// 단 로비에서만 쓰이므로 <c>DontDestroyOnLoad</c>는 하지 않는다 — 씬과 함께 사라지고 다음 로비에서 다시 만든다.
    /// </summary>
    public sealed class LobbyMenuPanel : MonoBehaviour
    {
        private const string QUIT_DEFAULT = "게임 종료";
        private const string QUIT_ARMED = "게임 종료 — 확정? (다시 클릭)";

        private static LobbyMenuPanel instance;

        private GameObject body;
        private Text quitLabel;
        private bool quitArmed;
        private Action onClosed;

        public static bool IsOpen => instance != null && instance.body != null && instance.body.activeSelf;

        /// <summary>
        /// 메뉴를 연다. <paramref name="onClosed"/>는 메뉴가 닫힐 때 한 번 호출된다 —
        /// 호출자가 플레이어 입력 잠금을 되돌리는 지점이다.
        /// </summary>
        public static void Open(Action onClosed)
        {
            EnsureInstance();
            if (instance == null) return;

            instance.onClosed = onClosed;
            instance.DisarmQuit();
            instance.body.SetActive(true);
        }

        /// <summary>열려 있으면 닫고 onClosed 콜백을 1회 호출한다.</summary>
        public static void Close()
        {
            if (!IsOpen) return;

            instance.body.SetActive(false);
            instance.DisarmQuit();

            // 콜백을 먼저 비우고 호출한다 — 콜백 안에서 다시 열어도 중첩되지 않게.
            var callback = instance.onClosed;
            instance.onClosed = null;
            callback?.Invoke();
        }

        /// <summary>도메인 리로드 비활성화 대비 정적 상태 리셋(AbyssBootstrap 선례).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private static void EnsureInstance()
        {
            // 씬 전환으로 파괴된 인스턴스는 Unity의 == 오버로드 덕에 여기서 null로 판정되어 다시 만들어진다.
            if (instance != null) return;

            var go = CreateOverlayCanvas("LobbyMenuPanel", UiSortingOrder.Menu);
            instance = go.AddComponent<LobbyMenuPanel>();
            instance.BuildUI(go.transform);
            instance.body.SetActive(false);
        }

        // ───────────────────────── 버튼 동작 ─────────────────────────

        private void OnToTitleClicked()
        {
            // 로비에는 진행 중인 런이 없어 잃을 것이 없다 — 런을 버리는 PausePanel과 달리 2단계 확인을 두지 않는다.
            Close();

            if (SceneFlowController.HasInstance)
            {
                _ = SceneFlowController.Instance.LoadTitleAsync();
            }
            else
            {
                Debug.LogWarning("[LobbyMenuPanel] SceneFlowController 미가동 — 타이틀 전환 불가.");
            }
        }

        private void OnQuitClicked()
        {
            // 종료는 되돌릴 수 없으므로 타이틀·일시정지와 같은 2단계 확인 관습을 따른다.
            if (!quitArmed)
            {
                quitArmed = true;
                if (quitLabel != null) quitLabel.text = QUIT_ARMED;
                return;
            }
            QuitGame();
        }

        private void DisarmQuit()
        {
            quitArmed = false;
            if (quitLabel != null) quitLabel.text = QUIT_DEFAULT;
        }

        /// <summary>에디터에서는 Application.Quit이 아무 일도 하지 않으므로 플레이 모드를 끈다.</summary>
        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ───────────────────────── UI 구성 ─────────────────────────

        private void BuildUI(Transform root)
        {
            body = CreateDimBody(root, 0.72f);

            var panel = CreateRect(body.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 464));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.10f, 0.10f, 0.15f, 0.98f);

            CreateLabel(panel.transform, "TitleText", new Vector2(0, 184), new Vector2(360, 40), "메뉴", 24, new Color(0.92f, 0.92f, 1f), TextAnchor.MiddleCenter);

            var resume = CreateButton(panel.transform, "ResumeButton", new Vector2(0, 104), new Vector2(300, 54), "돌아가기", 20);
            resume.onClick.AddListener(Close);

            // 도감은 메뉴를 닫지 않고 그 위에 덮는다(sortingOrder 300 > 200) — 닫으면 이 메뉴로 돌아온다.
            var codex = CreateButton(panel.transform, "CodexButton", new Vector2(0, 40), new Vector2(300, 54), "도감", 20);
            codex.onClick.AddListener(CodexPanel.Open);

            var settings = CreateButton(panel.transform, "SettingsButton", new Vector2(0, -24), new Vector2(300, 54), "설정", 20);
            settings.onClick.AddListener(SettingsPanel.Open);

            var toTitle = CreateButton(panel.transform, "ToTitleButton", new Vector2(0, -88), new Vector2(300, 54), "타이틀로", 20);
            toTitle.onClick.AddListener(OnToTitleClicked);

            var quit = CreateButton(panel.transform, "QuitButton", new Vector2(0, -152), new Vector2(300, 54), QUIT_DEFAULT, 20);
            quit.onClick.AddListener(OnQuitClicked);
            quitLabel = quit.GetComponentInChildren<Text>();

            CreateLabel(panel.transform, "HintText", new Vector2(0, -204), new Vector2(360, 30), "ESC로 닫기", 14, new Color(0.55f, 0.55f, 0.66f), TextAnchor.MiddleCenter);
        }
    }
}

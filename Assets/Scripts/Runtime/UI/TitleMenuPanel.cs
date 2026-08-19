using Abyss.Runtime.Flow;
using Abyss.Runtime.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 타이틀 화면 메인 메뉴. 완주 루프 계획 Phase 0-2.
    ///
    /// 버튼이 셋뿐인 이유: 런 중간 저장을 하지 않으므로(로그라이크는 런 단위로 끝난다)
    /// "새 게임"과 "이어하기"가 둘 다 로비 이동이 되어 구분이 무의미해진다.
    /// 대신 플레이 기록이 있으면 시작 버튼 라벨만 "이어하기"로 바꿔 상태를 알린다.
    /// </summary>
    public sealed class TitleMenuPanel : MonoBehaviour
    {
        [Header("버튼")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button codexButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("표시")]
        [SerializeField] private Text startLabel;
        [SerializeField] private Text recordText;

        private const string START_NEW = "게임 시작";
        private const string START_CONTINUE = "이어하기";
        private const string QUIT_DEFAULT = "게임 종료";
        private const string QUIT_ARMED = "게임 종료 — 확정? (다시 클릭)";
        // 화폐 표기 규약: 파편 = 형태(폼) 전용 / 조각 = 메타 화폐 / 골드 = 런 화폐.
        // 이 줄은 "심연 파편"이었는데 제단은 "심연 조각", 결과 화면은 "어비스 조각"이라
        // 한 런에 같은 자원이 세 이름으로 보였다(13-novel-game-glossary B-1).
        private const string RECORD_FORMAT = "누적 {0}런 · 보스 격파 {1} · 심연 조각 {2}";
        // 엔딩을 본 플레이어에게만 붙는 꼬리표. 완주 여부는 누적 숫자만으로는 드러나지 않는다.
        private const string RECORD_CLEARED_SUFFIX = "  ✦ 심연 탈출";

        private bool quitArmed;
        private Text quitLabel;

        private void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(StartGame);
            // 설정·도감 패널은 씬에 배치하지 않고 각 패널이 동적 생성해 공유한다
            // (설정은 일시정지와, 도감은 로비 메뉴와 같은 인스턴스를 쓴다).
            if (codexButton != null) codexButton.onClick.AddListener(CodexPanel.Open);
            if (settingsButton != null) settingsButton.onClick.AddListener(SettingsPanel.Open);
            if (quitButton != null)
            {
                quitButton.onClick.AddListener(OnQuitClicked);
                quitLabel = quitButton.GetComponentInChildren<Text>();
                if (quitLabel != null) quitLabel.text = QUIT_DEFAULT;
            }
        }

        private void Start()
        {
            RefreshFromSave();
        }

        /// <summary>세이브 기록에 따라 시작 버튼 라벨과 누적 기록 줄을 갱신한다.</summary>
        private void RefreshFromSave()
        {
            var records = ResolveRecords();
            bool hasHistory = records != null && records.totalRunCount > 0;

            if (startLabel != null) startLabel.text = hasHistory ? START_CONTINUE : START_NEW;

            if (recordText != null)
            {
                // 기록이 없으면 빈 줄로 둔다 — 첫 플레이어에게 "0런"을 보여줄 이유가 없다.
                if (!hasHistory)
                {
                    recordText.text = string.Empty;
                }
                else
                {
                    string line = string.Format(RECORD_FORMAT, records.totalRunCount, records.totalBossKillCount, ResolveShards());
                    recordText.text = records.hasSeenEnding ? line + RECORD_CLEARED_SUFFIX : line;
                }
            }
        }

        private static MetaRecords ResolveRecords()
        {
            // Bootstrap을 거치지 않고 Title 씬을 직접 재생하는 경우(에디터 단독 실행)를 대비한 안전 조회.
            var service = MetaSaveService.GetInstanceSafe();
            return service != null && service.Current != null ? service.Current.records : null;
        }

        private static int ResolveShards()
        {
            var service = MetaSaveService.GetInstanceSafe();
            return service != null && service.Current != null ? service.Current.abyssShardsTotal : 0;
        }

        /// <summary>
        /// 게임 시작. 프롤로그(4-3)를 아직 안 봤으면 <b>로비로 넘어가기 전에</b> 한 번 재생한다.
        ///
        /// 판단을 여기서 하는 이유: 프롤로그 패널은 "틀면 튼다"만 하고 재생 여부를 모른다.
        /// 한쪽에 몰아 두어야 치트로 다시 보는 경로(<see cref="MetaSaveService.ResetPrologueSeen"/>)가
        /// 판단을 우회하지 않고 그대로 통한다.
        /// </summary>
        private void StartGame()
        {
            var service = MetaSaveService.GetInstanceSafe();
            if (service != null && !service.HasSeenPrologue)
            {
                // 재생 직전에 기록한다 — 콜백에서 기록하면 도중에 앱을 끈 플레이어가 다음에 또 본다.
                // 프롤로그는 건너뛰기가 있으므로 "봤다"의 기준은 재생 시작으로 충분하다.
                service.MarkPrologueSeen();
                PrologueSequencePanel.Play(EnterLobby);
                return;
            }

            EnterLobby();
        }

        private static void EnterLobby()
        {
            if (SceneFlowController.HasInstance)
            {
                _ = SceneFlowController.Instance.LoadLobbyAsync();
            }
            else
            {
                Debug.LogWarning("[TitleMenuPanel] SceneFlowController 미가동 — 로비 전환 불가. Bootstrap 씬부터 실행할 것.");
            }
        }

        private void OnQuitClicked()
        {
            // 타이틀에서의 종료는 잃을 진행이 없지만, 일시정지 패널과 조작 관습을 맞춘다.
            if (!quitArmed)
            {
                quitArmed = true;
                if (quitLabel != null) quitLabel.text = QUIT_ARMED;
                return;
            }
            QuitGame();
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
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 도감. 완주 루프 계획 3-1 — 두 번째 런의 이유를 만드는 가장 값싼 장치다
    /// (이미 있는 SO를 나열하기만 하면 되고, 새 콘텐츠를 만들지 않는다).
    ///
    /// 탭 5개: 폼 / 스킬 / 적 / 보스 / 기록. 항목은 <b>처음 만난 시점</b>에 발견 등록되며
    /// 미발견은 실루엣 + <c>???</c>로 남는다. 발견 판정은 <see cref="Meta.MetaSaveService"/>의
    /// discovered* 목록이 소유하고, 등록 시점은 <see cref="Run.RunManager"/>가 발행한다.
    ///
    /// <see cref="SettingsPanel"/>과 같은 <b>런타임 동적 오버레이</b>다 — 타이틀·로비 두 화면에서 같은
    /// 인스턴스를 공유하므로 씬에 배치하지 않고 <c>DontDestroyOnLoad</c>로 들고 다닌다.
    /// ESC도 자기가 소유한다(Update 폴링). 그래서 씬 빌더 수정 없이 동작하며, 진입 버튼만 각 화면이 붙인다.
    ///
    /// 스크롤 대신 <b>그리드 + 페이지 넘김</b>을 쓴다. 프로젝트 전체에 ScrollRect·LayoutGroup 사용처가
    /// 없어(모든 UI가 UiFactory 수동 좌표) 코드로 조립한 ScrollRect 하나만 규약을 벗어나게 되고,
    /// 페이지 방식은 스킬이 60~80종으로 늘어도 그대로 버틴다.
    /// </summary>
    public sealed partial class CodexPanel : MonoBehaviour
    {
        // 설정(500)보다 아래, 로비 메뉴(200)보다 위 — 로비 메뉴에서 열면 그 위에 덮여야 하고,
        // 도감 위에서 설정을 열 일은 없지만 설정이 항상 최상위라는 규약은 깨지 않는다.
        private const int SORTING_ORDER = 300;

        private const int GRID_COLS = 5;
        private const int GRID_ROWS = 3;
        private const int PAGE_SIZE = GRID_COLS * GRID_ROWS;

        private static readonly Color AccentColor = new(0.86f, 0.82f, 1f);
        private static readonly Color MutedColor = new(0.55f, 0.55f, 0.66f);
        private static readonly Color TabSelected = new(0.34f, 0.30f, 0.52f);
        private static readonly Color TabSelectedHover = new(0.42f, 0.38f, 0.62f);
        private static readonly Color TabNormal = new(0.18f, 0.18f, 0.24f);
        private static readonly Color TabNormalHover = new(0.27f, 0.27f, 0.36f);
        private static readonly Color SelectionOutline = new(0.72f, 0.68f, 1f, 1f);

        private static CodexPanel instance;

        // ESC를 이번 프레임에 소비했음을 알리는 표식(SettingsPanel과 같은 규약).
        private static int closedFrame = -1;

        private GameObject body;

        private readonly Button[] tabButtons = new Button[5];
        private Text countLabel;

        private readonly TileView[] tiles = new TileView[PAGE_SIZE];
        private GameObject gridRoot;
        private GameObject pagerRoot;
        private Button prevButton;
        private Button nextButton;
        private Text pageLabel;

        private GameObject detailRoot;
        private Image detailIcon;
        private Text detailGlyph;
        private Text detailName;
        private Text detailBadge;
        private Text detailDescription;
        private Text detailStats;

        private Text recordsText;
        private Text recordsSecondaryText;

        private CodexTab currentTab = CodexTab.Form;
        private int currentPage;
        private int selectedIndex;
        private List<CodexItem> items = new();

        /// <summary>열려 있는지. 씬의 ESC 처리기가 도감을 먼저 닫도록 판단하는 근거.</summary>
        public static bool IsOpen => instance != null && instance.body != null && instance.body.activeSelf;

        /// <summary>
        /// 이번 프레임에 도감이 ESC로 닫혔는지. 도감 자신의 Update가 먼저 닫은 경우
        /// 씬의 ESC 처리기가 같은 입력으로 뒤쪽 메뉴까지 닫아버리는 것을 막는다(순서 역전 가드).
        /// </summary>
        public static bool WasClosedThisFrame => closedFrame == Time.frameCount;

        /// <summary>도감을 연다. 열 때마다 세이브에서 발견 상태를 다시 읽는다.</summary>
        public static void Open()
        {
            EnsureInstance();
            if (instance == null) return;

            instance.body.SetActive(true);
            instance.Refresh();
        }

        public static void Close()
        {
            if (!IsOpen) return;
            instance.body.SetActive(false);
            closedFrame = Time.frameCount;
        }

        /// <summary>
        /// 도메인 리로드 비활성화 대비 정적 상태 리셋(SettingsPanel 선례).
        /// closedFrame은 리셋된 Time.frameCount와 우연히 맞아떨어질 수 있어 함께 되돌린다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            closedFrame = -1;
        }

        private static void EnsureInstance()
        {
            if (instance != null) return;

            var go = CreateOverlayCanvas("CodexPanel", SORTING_ORDER);
            DontDestroyOnLoad(go);

            instance = go.AddComponent<CodexPanel>();
            instance.BuildUI(go.transform);
            instance.body.SetActive(false);
        }

        /// <summary>
        /// ESC 닫기를 스스로 소유한다 — 타이틀 씬에는 ESC를 받는 입력 배선이 아예 없고,
        /// 로비는 LobbyPlayerController가 받는다. 화면마다 다른 경로에 기대지 않으려면 여기서 처리해야 한다.
        /// timeScale=0에서도 Update는 돌기 때문에 정지 화면 위에서도 유효하다.
        /// </summary>
        private void Update()
        {
            if (body == null || !body.activeSelf) return;

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

            Close();
        }

        // ───────────────────────── 상태 갱신 ─────────────────────────

        private void SelectTab(CodexTab tab)
        {
            currentTab = tab;
            currentPage = 0;
            selectedIndex = 0;
            Refresh();
        }

        private void ChangePage(int delta)
        {
            int pageCount = PageCount;
            int next = Mathf.Clamp(currentPage + delta, 0, pageCount - 1);
            if (next == currentPage) return;

            currentPage = next;
            // 상세가 화면에 보이지 않는 항목을 가리키면 어느 타일 얘기인지 알 수 없다 →
            // 페이지를 넘기면 그 페이지의 첫 항목을 고른다.
            selectedIndex = currentPage * PAGE_SIZE;
            Refresh();
        }

        private void SelectItem(int index)
        {
            if (index < 0 || index >= items.Count) return;
            selectedIndex = index;
            Refresh();
        }

        private int PageCount => Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)PAGE_SIZE));

        /// <summary>탭·페이지·발견 상태를 전부 화면에 반영한다. 값이 바뀌는 모든 경로가 이 하나를 부른다.</summary>
        private void Refresh()
        {
            items = Collect(currentTab);

            for (int i = 0; i < tabButtons.Length; i++)
            {
                ApplyTabColors(tabButtons[i], i == (int)currentTab);
            }

            bool isRecords = currentTab == CodexTab.Records;
            gridRoot.SetActive(!isRecords);
            detailRoot.SetActive(!isRecords);
            recordsText.gameObject.SetActive(isRecords);
            recordsSecondaryText.gameObject.SetActive(isRecords);

            if (isRecords)
            {
                pagerRoot.SetActive(false);
                countLabel.text = string.Empty;
                recordsText.text = BuildLastRunText();
                recordsSecondaryText.text = BuildRecordsText();
                return;
            }

            countLabel.text = $"발견 {CountText(items)}";
            RefreshGrid();
            RefreshPager();
            RefreshDetail();
        }

        private void RefreshGrid()
        {
            int start = currentPage * PAGE_SIZE;

            for (int slot = 0; slot < tiles.Length; slot++)
            {
                int index = start + slot;
                var tile = tiles[slot];

                if (index >= items.Count)
                {
                    tile.Root.SetActive(false);
                    continue;
                }

                tile.Root.SetActive(true);
                var item = items[index];

                tile.Background.color = item.TileColor;
                tile.NameLabel.text = item.Name;
                tile.NameLabel.color = item.IsDiscovered ? Color.white : MutedColor;

                // 아이콘이 있으면 스프라이트, 없으면 글리프 한 글자 — 폼 4종과 Passive 스킬은
                // 아이콘 에셋이 아직 없어서 둘 중 하나는 반드시 비어 있다.
                bool hasIcon = item.Icon != null;
                tile.Icon.enabled = hasIcon;
                tile.Icon.sprite = item.Icon;
                tile.Icon.color = item.IconTint;
                tile.Glyph.enabled = !hasIcon;
                tile.Glyph.text = item.Glyph;
                tile.Glyph.color = item.IsDiscovered ? Color.white : MutedColor;

                tile.Selection.color = index == selectedIndex ? SelectionOutline : Color.clear;
            }
        }

        private void RefreshPager()
        {
            int pageCount = PageCount;
            // 한 페이지에 다 들어가면 넘길 것이 없다 — 비활성 버튼을 보여주는 것보다 없는 게 낫다.
            pagerRoot.SetActive(pageCount > 1);
            if (pageCount <= 1) return;

            pageLabel.text = $"{currentPage + 1} / {pageCount}";
            prevButton.interactable = currentPage > 0;
            nextButton.interactable = currentPage < pageCount - 1;
        }

        private void RefreshDetail()
        {
            if (selectedIndex < 0 || selectedIndex >= items.Count)
            {
                detailName.text = EMPTY_VALUE;
                detailBadge.text = string.Empty;
                detailDescription.text = string.Empty;
                detailStats.text = string.Empty;
                detailIcon.enabled = false;
                detailGlyph.enabled = false;
                return;
            }

            var item = items[selectedIndex];

            detailName.text = item.Name;
            detailName.color = item.IsDiscovered ? Color.white : MutedColor;
            detailBadge.text = item.Badge;
            detailBadge.color = item.BadgeColor;
            detailStats.text = item.Stats;

            // 미발견 항목에 빈 설명만 두면 "설명이 없는 항목"과 구별되지 않는다 → 안내 문구를 넣는다.
            detailDescription.text = item.IsDiscovered ? item.Description : "아직 만나지 못했다.";
            detailDescription.color = item.IsDiscovered ? new Color(0.86f, 0.88f, 0.94f) : MutedColor;

            bool hasIcon = item.Icon != null;
            detailIcon.enabled = hasIcon;
            detailIcon.sprite = item.Icon;
            detailIcon.color = item.IconTint;
            detailGlyph.enabled = !hasIcon;
            detailGlyph.text = item.Glyph;
            detailGlyph.color = item.IsDiscovered ? Color.white : MutedColor;
        }

        private static void ApplyTabColors(Button button, bool isSelected)
        {
            if (button == null) return;

            // targetGraphic.color를 직접 만지면 Button이 상태 전환 때 normalColor로 되돌려 놓는다 →
            // ColorBlock 자체를 바꿔야 선택 상태가 유지된다.
            var colors = button.colors;
            colors.normalColor = isSelected ? TabSelected : TabNormal;
            // 색에 배율을 곱하지 않는다 — Color 곱셈은 알파까지 곱해서 1을 넘긴다.
            colors.highlightedColor = isSelected ? TabSelectedHover : TabNormalHover;
            colors.pressedColor = TabSelected;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
        }
    }
}

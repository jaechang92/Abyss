using System;
using System.Collections.Generic;
using System.Text;
using Abyss.Runtime.Flow;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 설정 위에 뜨는 데이터 초기화 모달. 1단계에서 묶음을 고르고(기본값 없음), 2단계에서 되돌릴 수 없다는 경고와
    /// 고른 묶음을 정확히 다시 보여 준 뒤 확정한다. <b>확정 전에는 아무것도 쓰지 않는다.</b>
    ///
    /// 허용 조건은 타이틀·로비 씬이면서 런이 진행 중이 아닐 때뿐이고, 열 때와 <b>확정하는 순간</b> 둘 다 다시 본다 —
    /// 오래 열어 둔 창이 런 도중에 초기화하지 못하게.
    ///
    /// 성공하면 이 창 → 설정 창 순서로 닫는다(죽은 위젯으로 포커스가 돌아가지 않게). 설정 창은 SaveAll 없이 닫혀
    /// 초기화 전 값을 되쓰지 않는다. 진행 데이터를 지웠으면 타이틀로 다시 들어가 로비 NPC 등 씬에 남은 상태를 비운다.
    /// 실패하면 창·선택·데이터가 그대로이고 씬 전환·설정 적용도 없다.
    ///
    /// 문구는 이 창 전용 3개 언어 표를 쓴다(GameText.csv 전체를 건드리지 않기 위해 — 읽기 힌트 선례).
    /// 배치는 DataResetPanel.Layout.cs, 입력·포커스·탐색은 DataResetPanel.Navigation.cs.
    /// </summary>
    public sealed partial class DataResetPanel : MonoBehaviour
    {
        private enum Stage
        {
            Select,
            Confirm
        }

        /// <summary>화면에 보이는 순서의 7개 묶음.</summary>
        internal static readonly MetaResetCategory[] Groups =
        {
            MetaResetCategory.Relics,
            MetaResetCategory.Currency,
            MetaResetCategory.PermanentUpgrades,
            MetaResetCategory.Unlocks,
            MetaResetCategory.CodexAndRunRecords,
            MetaResetCategory.StoryAndTutorial,
            MetaResetCategory.Settings
        };

        private static DataResetPanel instance;
        private static int consumedFrame = -1;

        // 실제 씬 판정·성공 후 처리. 공개 테스트 모드를 두지 않고 private로만 둔다 —
        // 테스트는 리플렉션으로 바꿔 실제 씬 전환 없이 확인하고, 끝나면 되돌린다.
        private static Func<string, bool> isSceneAllowed = DefaultIsSceneAllowed;
        private static Action<MetaResetCategory> completeAfterReset = DefaultCompleteAfterReset;

        private GameObject body;
        private Stage stage;
        private MetaResetCategory selection;
        private MetaResetCategory pending;
        private int openedFrame = -1;
        private GameObject previousSelection;
        private string statusKey;

        public static bool IsOpen => instance != null && instance.body != null && instance.body.activeSelf;

        /// <summary>이번 프레임에 이 창이 입력(ESC·확정·단계 이동)을 소비했는가. 설정 창의 같은 프레임 ESC 재사용을 막는다.</summary>
        public static bool WasInputConsumedThisFrame => consumedFrame == Time.frameCount;

        /// <summary>타이틀·로비 씬이고, 씬 전환 중이 아니며, 런이 진행 중이 아닌가. 어느 것도 새로 만들지 않는다.</summary>
        public static bool IsResetContextAllowed
        {
            get
            {
                var run = RunManager.GetInstanceSafe();
                if (run != null && run.IsRunActive) return false;
                var flow = SceneFlowController.GetInstanceSafe();
                if (flow != null && flow.IsLoading) return false;
                var predicate = isSceneAllowed ?? DefaultIsSceneAllowed;
                return predicate(SceneManager.GetActiveScene().name);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            consumedFrame = -1;
            isSceneAllowed = DefaultIsSceneAllowed;
            completeAfterReset = DefaultCompleteAfterReset;
        }

        private static bool DefaultIsSceneAllowed(string sceneName) =>
            sceneName == SceneNames.Title || sceneName == SceneNames.Lobby;

        /// <summary>진행 데이터를 지웠으면 타이틀로 다시 들어간다. 설정만 지웠으면 씬은 그대로 둔다.</summary>
        private static void DefaultCompleteAfterReset(MetaResetCategory categories)
        {
            if ((categories & ~MetaResetCategory.Settings) == 0) return;
            var flow = SceneFlowController.Instance;
            if (flow != null) _ = flow.LoadTitleAsync();
        }

        /// <summary>설정 창 위에 연다. 설정이 닫혀 있거나 허용 조건이 아니면 열지 않고 false.</summary>
        public static bool Open()
        {
            if (!SettingsPanel.IsOpen || SaveStatusOverlay.IsCapturingInput || KeyboardBindingsPanel.IsOpen) return false;
            if (!IsResetContextAllowed) return false;
            EnsureInstance();
            if (IsOpen) return true;

            var events = UnityEngine.EventSystems.EventSystem.current;
            instance.previousSelection = events != null ? events.currentSelectedGameObject : null;
            instance.openedFrame = Time.frameCount;
            instance.stage = Stage.Select;
            instance.selection = MetaResetCategory.None;
            instance.pending = MetaResetCategory.None;
            instance.statusKey = null;
            instance.body.SetActive(true);
            instance.SyncToggles();
            instance.Refresh();
            instance.SelectDefault();
            return true;
        }

        /// <summary>취소로 닫는다. 아무것도 쓰지 않고 설정 창의 이전 선택(초기화 버튼)을 돌려준다.</summary>
        public static void Close()
        {
            if (!IsOpen) return;
            consumedFrame = Time.frameCount;
            instance.body.SetActive(false);
            instance.RestoreFocus();
            instance.ClearState();
        }

        private static void EnsureInstance()
        {
            if (instance != null) return;
            var canvas = UiFactory.CreateOverlayCanvas("DataResetPanel", UiSortingOrder.Settings + 2);
            // 키보드 설정 창과 같은 높이 기준 — 좁은 화면·150% 글자에서도 창이 화면 안에 남는다.
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 1f;
            DontDestroyOnLoad(canvas);
            instance = canvas.AddComponent<DataResetPanel>();
            instance.BuildUI(canvas.transform);
            instance.body.SetActive(false);
        }

        private void OnEnable() => SceneManager.activeSceneChanged += OnSceneChanged;

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            Close();
        }

        private void OnSceneChanged(Scene previous, Scene next) => Close();

        private void ClearState()
        {
            stage = Stage.Select;
            selection = MetaResetCategory.None;
            pending = MetaResetCategory.None;
            statusKey = null;
            previousSelection = null;
        }

        // ───────────────────────── 명령 ─────────────────────────

        private bool CanAcceptCommand => IsOpen && !SaveStatusOverlay.IsCapturingInput &&
            !WasInputConsumedThisFrame && Time.frameCount != openedFrame;

        private void SetGroup(int index, bool isOn)
        {
            if (!IsOpen || stage != Stage.Select || index < 0 || index >= Groups.Length) return;
            selection = isOn ? selection | Groups[index] : selection & ~Groups[index];
            statusKey = null;
            Refresh();
        }

        private void ToggleSelectAll()
        {
            if (!CanAcceptCommand || stage != Stage.Select) return;
            selection = selection == MetaResetCategory.All ? MetaResetCategory.None : MetaResetCategory.All;
            statusKey = null;
            SyncToggles();
            Refresh();
        }

        private void RequestFullReset()
        {
            if (!CanAcceptCommand || stage != Stage.Select) return;
            EnterConfirm(MetaResetCategory.All);
        }

        private void RequestNext()
        {
            if (!CanAcceptCommand || stage != Stage.Select) return;
            if (!MetaSaveService.IsValidResetSelection(selection))
            {
                statusKey = "Error_InvalidSelection";
                Refresh();
                return;
            }
            EnterConfirm(selection);
        }

        private void EnterConfirm(MetaResetCategory categories)
        {
            consumedFrame = Time.frameCount;
            pending = categories;
            stage = Stage.Confirm;
            statusKey = null;
            Refresh();
            SelectDefault();
        }

        private void BackToSelect()
        {
            if (!IsOpen || stage != Stage.Confirm) return;
            consumedFrame = Time.frameCount;
            stage = Stage.Select;
            pending = MetaResetCategory.None;
            statusKey = null;
            Refresh();
            SelectDefault();
        }

        /// <summary>ESC·취소·돌아가기 버튼. 확인 단계면 선택 단계로, 선택 단계면 창을 닫는다.</summary>
        private void RequestBack()
        {
            if (!CanAcceptCommand) return;
            if (stage == Stage.Confirm) BackToSelect();
            else Close();
        }

        private void ConfirmReset()
        {
            if (!CanAcceptCommand || stage != Stage.Confirm || !MetaSaveService.IsValidResetSelection(pending)) return;
            consumedFrame = Time.frameCount;
            // 열어 둔 사이에 런이 시작됐거나 씬이 바뀌었을 수 있다 — 확정 순간에 다시 본다.
            if (!IsResetContextAllowed)
            {
                ShowError("Error_Context");
                return;
            }
            var service = MetaSaveService.GetInstanceSafe();
            if (service == null)
            {
                ShowError("Error_Unavailable");
                return;
            }
            var categories = pending;
            if (!service.TryResetData(categories, out var error))
            {
                ShowError(ErrorKey(error));
                return;
            }
            FinishAfterSuccess(categories);
        }

        private void ShowError(string key)
        {
            statusKey = key;
            Refresh();
        }

        private static string ErrorKey(MetaResetError error) => error switch
        {
            MetaResetError.InvalidSelection => "Error_InvalidSelection",
            MetaResetError.RunActive => "Error_Context",
            MetaResetError.SaveBlocked => "Error_SaveBlocked",
            MetaResetError.SaveFailed => "Error_SaveFailed",
            _ => "Error_Unavailable"
        };

        /// <summary>성공 후: 이 창을 먼저 닫고(포커스 복구 없음), 설정을 SaveAll 없이 닫은 뒤 후속 처리.</summary>
        private void FinishAfterSuccess(MetaResetCategory categories)
        {
            consumedFrame = Time.frameCount;
            body.SetActive(false);
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events != null && IsInside(events.currentSelectedGameObject)) events.SetSelectedGameObject(null);
            ClearState();
            SettingsPanel.CloseAfterDataReset((categories & MetaResetCategory.Settings) != 0);
            var complete = completeAfterReset ?? DefaultCompleteAfterReset;
            try
            {
                complete(categories);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataResetPanel] 초기화 후 처리 실패(데이터는 이미 저장됨): {e.Message}");
            }
        }

        // ───────────────────────── 문구 ─────────────────────────

        /// <summary>선택 묶음을 화면 순서대로 이름으로 나열한다.</summary>
        private static List<string> GroupNames(MetaResetCategory categories)
        {
            var names = new List<string>();
            foreach (var group in Groups)
            {
                if ((categories & group) == group) names.Add(Tr(GroupKey(group)));
            }
            return names;
        }

        private string BuildSummaryText()
        {
            if (selection == MetaResetCategory.None) return Tr("Summary_None");
            string summary = Tr("Summary_Prefix") + string.Join(", ", GroupNames(selection));
            return (selection & ~MetaResetCategory.Settings) != 0 ? summary + "\n" + Tr("Note_Title") : summary;
        }

        private string BuildWarningText()
        {
            var builder = new StringBuilder();
            builder.AppendLine(Tr("Warning_Header"));
            if (pending == MetaResetCategory.All)
            {
                builder.AppendLine(Tr("Warning_Full"));
            }
            else
            {
                foreach (var group in Groups)
                {
                    if ((pending & group) != group) continue;
                    builder.Append("• ").Append(Tr(GroupKey(group))).Append(" — ").AppendLine(Tr(GroupKey(group) + "_Desc"));
                }
            }
            if ((pending & ~MetaResetCategory.Settings) != 0) builder.Append(Tr("Note_Title"));
            else builder.Append(Tr("Note_SettingsOnly"));
            return builder.ToString().TrimEnd();
        }

        internal static string GroupKey(MetaResetCategory group) => group switch
        {
            MetaResetCategory.Relics => "Group_Relics",
            MetaResetCategory.Currency => "Group_Currency",
            MetaResetCategory.PermanentUpgrades => "Group_Upgrades",
            MetaResetCategory.Unlocks => "Group_Unlocks",
            MetaResetCategory.CodexAndRunRecords => "Group_Codex",
            MetaResetCategory.StoryAndTutorial => "Group_Story",
            _ => "Group_Settings"
        };

        /// <summary>이 창·설정 진입 행 전용 문구. 없는 키는 키 자체를 돌려준다.</summary>
        internal static string Tr(string key)
        {
            if (!Texts.TryGetValue(key, out var values)) return key;
            var language = LocalizationManager.GetInstanceSafe() != null ? Loc.CurrentLanguage : LocalizationLanguage.Korean;
            int index = (int)language;
            return index >= 0 && index < values.Length ? values[index] : values[0];
        }

        // 한국어 · 영어 · 일본어(LocalizationLanguage 선언 순서).
        private static readonly Dictionary<string, string[]> Texts = new()
        {
            ["Entry_Label"] = new[] { "데이터 초기화", "Reset Data", "データ初期化" },
            ["Entry_Open"] = new[] { "초기화 항목 선택…", "Choose What to Reset…", "初期化する項目…" },
            ["Entry_Disabled"] = new[] { "타이틀·로비 전용", "Title/Lobby only", "タイトル・ロビー専用" },
            ["Title"] = new[] { "데이터 초기화", "Reset Data", "データ初期化" },
            ["Intro"] = new[]
            {
                "초기화할 항목을 고르세요. 마지막 확인을 누르기 전에는 아무것도 바뀌지 않습니다.",
                "Choose what to reset. Nothing changes until you press the final confirmation.",
                "初期化する項目を選んでください。最後の確認を押すまで何も変わりません。"
            },
            ["Group_Relics"] = new[] { "유물", "Relics", "遺物" },
            ["Group_Relics_Desc"] = new[]
            {
                "보유 유물과 장착 슬롯 (심연 조각은 유지)",
                "Owned relics and equipped slots (Abyss Shards are kept)",
                "所持している遺物と装備スロット(深淵の欠片は維持)"
            },
            ["Group_Currency"] = new[] { "심연 조각", "Abyss Shards", "深淵の欠片" },
            ["Group_Currency_Desc"] = new[] { "보유한 심연 조각 전부", "All Abyss Shards you hold", "所持している深淵の欠片すべて" },
            ["Group_Upgrades"] = new[] { "영구 강화", "Permanent Upgrades", "永続強化" },
            ["Group_Upgrades_Desc"] = new[] { "심연의 제단 강화 레벨", "Upgrade levels bought at the Abyss Altar", "深淵の祭壇の強化レベル" },
            ["Group_Unlocks"] = new[] { "해금", "Unlocks", "解放" },
            ["Group_Unlocks_Desc"] = new[] { "해금한 폼과 스킬", "Unlocked forms and skills", "解放したフォームとスキル" },
            ["Group_Codex"] = new[] { "도감·런 기록", "Codex & Run Records", "図鑑・ラン記録" },
            ["Group_Codex_Desc"] = new[]
            {
                "도감 발견(폼·스킬·적), 직전 런 요약, 누적 런 기록 (프롤로그 시청 여부는 유지)",
                "Codex discoveries (forms, skills, enemies), last run summary, run records (prologue viewing is kept)",
                "図鑑の発見(フォーム・スキル・敵)、直前のラン概要、ラン記録(プロローグ視聴は維持)"
            },
            ["Group_Story"] = new[] { "이야기·튜토리얼", "Story & Tutorial", "物語・チュートリアル" },
            ["Group_Story_Desc"] = new[]
            {
                "이야기 진행, 통행 기록 발견과 반응, 첫 플레이 튜토리얼, 프롤로그 시청 여부",
                "Story progress, passage records and reactions, first-play tutorial, prologue viewing",
                "物語の進行、通行記録の発見と反応、初回チュートリアル、プロローグ視聴"
            },
            ["Group_Settings"] = new[] { "설정", "Settings", "設定" },
            ["Group_Settings_Desc"] = new[]
            {
                "음량·화면·언어·키보드·화면 효과·글자 크기 전부",
                "Volume, display, language, keyboard, screen effects and text size",
                "音量・画面・言語・キーボード・画面効果・文字サイズすべて"
            },
            ["Summary_None"] = new[] { "선택한 항목 없음 — 아무것도 바뀌지 않습니다.", "Nothing selected — nothing will change.", "未選択 — 何も変わりません。" },
            ["Summary_Prefix"] = new[] { "선택: ", "Selected: ", "選択: " },
            ["Note_Title"] = new[]
            {
                "초기화가 끝나면 타이틀 화면으로 돌아갑니다.",
                "You will return to the title screen afterwards.",
                "初期化後はタイトル画面に戻ります。"
            },
            ["Note_SettingsOnly"] = new[]
            {
                "설정만 기본값으로 돌아가며 진행 데이터는 그대로입니다.",
                "Only settings return to default; progress is kept.",
                "設定のみ初期値に戻り、進行データはそのままです。"
            },
            ["SelectAll"] = new[] { "모두 선택", "Select All", "すべて選択" },
            ["SelectNone"] = new[] { "모두 해제", "Clear All", "すべて解除" },
            ["FullReset"] = new[] { "전체 초기화…", "Reset Everything…", "すべて初期化…" },
            ["Next"] = new[] { "다음…", "Next…", "次へ…" },
            ["Cancel"] = new[] { "취소", "Cancel", "キャンセル" },
            ["Confirm"] = new[] { "초기화 확정", "Reset Now", "初期化を確定" },
            ["Back"] = new[] { "돌아가기", "Go Back", "戻る" },
            ["Warning_Header"] = new[]
            {
                "되돌릴 수 없습니다. 다음 항목을 처음 상태로 되돌립니다:",
                "This cannot be undone. The following will return to their initial state:",
                "元に戻せません。次の項目を初期状態に戻します:"
            },
            ["Warning_Full"] = new[]
            {
                "• 전체 — 설정, 모든 진행 데이터, 유물을 포함한 모든 데이터",
                "• Everything — settings, all progress and relics included",
                "• すべて — 設定、すべての進行データ、遺物を含む全データ"
            },
            ["Guide"] = new[] { "Esc: 취소 · 한 단계 뒤로", "Esc: Cancel / go back", "Esc: キャンセル・戻る" },
            ["Error_InvalidSelection"] = new[] { "항목을 하나 이상 고르세요.", "Select at least one item.", "項目を1つ以上選んでください。" },
            ["Error_Context"] = new[]
            {
                "타이틀·로비에서 런이 진행 중이 아닐 때만 초기화할 수 있습니다. 아무것도 바뀌지 않았습니다.",
                "Reset is only available in the title or lobby with no active run. Nothing changed.",
                "ランが進行中でないタイトル・ロビーでのみ初期化できます。何も変わっていません。"
            },
            ["Error_SaveBlocked"] = new[]
            {
                "세이브를 열지 못해 저장이 보류된 상태입니다. 아무것도 바뀌지 않았습니다.",
                "The save file could not be opened, so saving is on hold. Nothing changed.",
                "セーブを開けず保存が保留中です。何も変わっていません。"
            },
            ["Error_SaveFailed"] = new[]
            {
                "저장에 실패했습니다. 아무것도 바뀌지 않았습니다.",
                "Saving failed. Nothing changed.",
                "保存に失敗しました。何も変わっていません。"
            },
            ["Error_Unavailable"] = new[]
            {
                "세이브를 사용할 수 없습니다. 아무것도 바뀌지 않았습니다.",
                "Save data is unavailable. Nothing changed.",
                "セーブを利用できません。何も変わっていません。"
            }
        };
    }
}

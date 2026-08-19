using System.Collections.Generic;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Player;
using Abyss.Runtime.Run;
using Abyss.Runtime.Stage;
using GAS.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.Cheats
{
    /// <summary>
    /// 테스트용 런타임 치트 메뉴(IMGUI 오버레이). F1 토글 또는 좌상단 버튼으로 열고 닫는다.
    /// 에디터/개발 빌드에서만 자동 생성(릴리스 빌드 제외). 씬/프리팹 배치 불필요.
    /// 배속·재화·경험치·체력·적·스킬·드래프트·폼·스테이지를 실시간 조작.
    /// </summary>
    public sealed class CheatMenu : MonoBehaviour
    {
        private const KeyCode ToggleHint = KeyCode.F1;

        private bool visible;
        private Rect windowRect = new(12, 44, 380, 600);
        private Vector2 scroll;
        private float storedTimeScale = 1f;
        private bool isPaused;

        private PlayerCharacter player;
        private DraftSessionController draft;
        private DraftPoolManager pool;
        private StageDirector stage;
        private GUIStyle headerStyle;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindAnyObjectByType<CheatMenu>() != null) return;
            var go = new GameObject("[CheatMenu]");
            go.AddComponent<CheatMenu>();
            DontDestroyOnLoad(go);
        }
#endif

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame)
            {
                visible = !visible;
            }
        }

        private void OnGUI()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            }

            // 항상 보이는 토글 버튼.
            if (GUI.Button(new Rect(12, 12, 110, 26), visible ? "치트 닫기" : $"치트 ({ToggleHint})"))
            {
                visible = !visible;
            }

            if (!visible) return;
            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "Cheat Menu");
        }

        private void DrawWindow(int id)
        {
            scroll = GUILayout.BeginScrollView(scroll);

            DrawTimeSection();
            DrawPlayerSection();
            DrawCurrencySection();
            DrawStorySection();
            DrawCodexSection();
            DrawProgressSection();
            DrawEnemySection();
            DrawSkillSection();
            DrawDraftFormStageSection();

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        // ====== 시간 배속 ======
        private void DrawTimeSection()
        {
            GUILayout.Label("■ 시간 배속", headerStyle);
            GUILayout.BeginHorizontal();
            foreach (float s in new[] { 0.25f, 0.5f, 1f, 2f, 4f, 8f })
            {
                if (GUILayout.Button($"{s}x"))
                {
                    isPaused = false;
                    Time.timeScale = s;
                }
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button(isPaused ? "▶ 재개" : "Ⅱ 일시정지"))
            {
                if (isPaused)
                {
                    Time.timeScale = storedTimeScale;
                    isPaused = false;
                }
                else
                {
                    storedTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
                    Time.timeScale = 0f;
                    isPaused = true;
                }
            }
            GUILayout.Label($"현재 timeScale = {Time.timeScale:F2}");
            GUILayout.Space(6);
        }

        // ====== 플레이어 ======
        private void DrawPlayerSection()
        {
            GUILayout.Label("■ 플레이어", headerStyle);
            var p = ResolvePlayer();
            if (p == null)
            {
                GUILayout.Label("(PlayerCharacter 없음)");
                GUILayout.Space(6);
                return;
            }

            GUILayout.Label($"HP {p.CurrentHp}/{p.BaseHp}  무적={(p.DebugInvincible ? "ON" : "off")}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("풀 회복")) p.Heal(p.BaseHp);
            if (GUILayout.Button("25 피해")) p.TakeDamage(25);
            if (GUILayout.Button("즉사")) p.TakeDamage(p.CurrentHp);
            GUILayout.EndHorizontal();

            if (GUILayout.Button(p.DebugInvincible ? "무적 끄기" : "무적 켜기"))
            {
                p.DebugInvincible = !p.DebugInvincible;
            }

            // 시간제 버프 직접 테스트 — 스킬 발동 경로 없이 즉시 적용해 배율/타이머를 검증.
            string buffState = p.HasActiveBuff
                ? $"버프 {p.BuffRemaining:F1}s (이동x{p.MoveSpeedMultiplier:F1}/공격x{p.AttackMultiplier:F1}/방어x{p.DefenseMultiplier:F1})"
                : "버프 없음";
            GUILayout.Label(buffState);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("이동x1.5 (8s)")) p.ApplyTimedBuff(1.5f, 1f, 8f);
            if (GUILayout.Button("공격x2 (8s)")) p.ApplyTimedBuff(1f, 2f, 8f);
            if (GUILayout.Button("방어x0.5 (5s)")) p.ApplyTimedBuff(1f, 1f, 0.5f, 5f);
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
        }

        // ====== 재화 ======
        private void DrawCurrencySection()
        {
            GUILayout.Label("■ 재화", headerStyle);
            if (RunManager.HasInstance)
            {
                var rm = RunManager.Instance;
                GUILayout.Label($"골드(런) = {rm.GoldShards}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("+100")) rm.GainGoldShards(100);
                if (GUILayout.Button("+1000")) rm.GainGoldShards(1000);
                if (GUILayout.Button("-100")) rm.GainGoldShards(-100);
                GUILayout.EndHorizontal();
            }
            else GUILayout.Label("(RunManager 없음)");

            // 심연 조각은 영속 메타 재화 — Bootstrap을 거치지 않은 로비 단독 플레이에서도
            // Instance 접근으로 즉석 생성해 제단 업그레이드를 테스트할 수 있게 한다.
            var meta = MetaSaveService.Instance;
            if (meta != null)
            {
                GUILayout.Label($"심연 조각(메타) = {meta.Current.abyssShardsTotal}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("심연 +100")) meta.AddAbyssShards(100);
                if (GUILayout.Button("심연 +1000")) meta.AddAbyssShards(1000);
                GUILayout.EndHorizontal();
            }
            else GUILayout.Label("(MetaSaveService 없음)");
            GUILayout.Space(6);
        }

        // ====== 스토리(화자별) ======
        private void DrawStorySection()
        {
            GUILayout.Label("■ 스토리", headerStyle);
            var meta = MetaSaveService.Instance;
            if (meta == null)
            {
                GUILayout.Label("(MetaSaveService 없음)");
                GUILayout.Space(6);
                return;
            }

            DrawStorySpeaker(meta, "기록자", StorySpeakerIds.Chronicler);
            DrawStorySpeaker(meta, "각인사", StorySpeakerIds.Engraver);
            // 프롤로그는 세이브당 1회라, 이 버튼이 없으면 확인할 때마다 세이브를 지워야 한다.
            GUILayout.BeginHorizontal();
            GUILayout.Label($"프롤로그: {(meta.HasSeenPrologue ? "시청함" : "미시청")}");
            if (GUILayout.Button("다시 보기")) meta.ResetPrologueSeen();
            GUILayout.EndHorizontal();
            GUILayout.Label("※ 설정 시 스냅샷 0 리셋 — 다음 대화에서 해당 다음 챕터 열람 가능(누적 기준)");
            GUILayout.Label("※ 각인사 내력은 폼 해금이 조건이라 단계 +1만으로는 안 열린다(폼 해금 병행)");
            GUILayout.Space(6);
        }

        /// <summary>
        /// 화자 1명의 진행도 줄. 시청 기록이 집합이라 ±1은 "가장 높은 단계 하나를 넣고/빼기"로 옮긴다 —
        /// 순차 화자(기록자)에서는 옛 단계 ±1과 같고, 각인사에서는 리셋이 실질적인 도구다.
        /// </summary>
        private void DrawStorySpeaker(MetaSaveService meta, string label, string speakerId)
        {
            var save = meta.Current;
            meta.GetStorySnapshots(speakerId, out int runSnapshot, out int bossSnapshot);
            int runDelta = save.records.totalRunCount - runSnapshot;
            int bossDelta = save.records.totalBossKillCount - bossSnapshot;

            var viewed = meta.GetViewedChapterStages(speakerId);
            int highest = 0;
            foreach (int stage in viewed)
            {
                if (stage > highest) highest = stage;
            }

            string viewedText = viewed.Count > 0 ? string.Join(",", viewed) : "없음";
            GUILayout.Label($"{label}: 시청 [{viewedText}]  (런델타 {runDelta} / 보스델타 {bossDelta})");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("리셋")) meta.DebugSetViewedChapters(speakerId, null);
            if (GUILayout.Button("단계 -1")) meta.DebugSetViewedChapters(speakerId, StageRange(highest - 1));
            if (GUILayout.Button("단계 +1")) meta.DebugSetViewedChapters(speakerId, StageRange(highest + 1));
            GUILayout.EndHorizontal();
        }

        private static IEnumerable<int> StageRange(int highest)
        {
            for (int stage = 1; stage <= highest; stage++) yield return stage;
        }

        // ====== 도감 ======
        // 발견 상태는 영속 메타라 한 번 차면 되돌릴 방법이 없어, 실루엣·??? 표시를 다시 보려면
        // 초기화 수단이 반드시 필요하다. MetaSaveService가 아니라 여기에 두는 이유는
        // 카탈로그 3종(폼·스킬·적)을 훑어야 하고 그 의존을 저장 게이트웨이에 들일 이유가 없기 때문이다.
        private void DrawCodexSection()
        {
            GUILayout.Label("■ 도감", headerStyle);
            var meta = MetaSaveService.Instance;
            if (meta == null)
            {
                GUILayout.Label("(MetaSaveService 없음)");
                GUILayout.Space(6);
                return;
            }

            var save = meta.Current;
            GUILayout.Label($"발견 — 폼 {save.discoveredFormIds.Count} · 스킬 {save.discoveredSkillIds.Count} · 적/보스 {save.discoveredEnemyIds.Count}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("전체 발견"))
            {
                foreach (var form in FormCatalog.All)
                {
                    if (form != null) meta.DiscoverForm(form.formId, autoSave: false);
                }
                foreach (var skill in SkillCatalog.All)
                {
                    if (skill != null) meta.DiscoverSkill(skill.skillId, autoSave: false);
                }
                foreach (var enemy in EnemyCatalog.All)
                {
                    if (enemy != null) meta.DiscoverEnemy(enemy.enemyId, autoSave: false);
                }
                meta.Save();
            }
            if (GUILayout.Button("발견 초기화"))
            {
                save.discoveredFormIds.Clear();
                save.discoveredSkillIds.Clear();
                save.discoveredEnemyIds.Clear();
                meta.Save();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("※ 도감은 타이틀·로비 메뉴에서 열 수 있다(런 중에는 진입점 없음)");
            GUILayout.Space(6);
        }

        // ====== 경험치 / 레벨 ======
        private void DrawProgressSection()
        {
            GUILayout.Label("■ 경험치 / 레벨", headerStyle);
            if (!RunManager.HasInstance)
            {
                GUILayout.Label("(RunManager 없음)");
                GUILayout.Space(6);
                return;
            }

            var rm = RunManager.Instance;
            GUILayout.Label($"Lv {rm.CurrentLevel}  EXP {rm.CurrentExp}/{rm.ExpToNextLevel}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+100 EXP")) rm.GainExp(100, "cheat");
            if (GUILayout.Button("레벨업(드래프트)")) rm.GrantBonusLevel(DraftTriggerReason.LevelUp);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("보스 처치 보상")) rm.NotifyBossKilled();
            if (GUILayout.Button("엘리트 보상")) rm.NotifyEliteKilled();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
        }

        // ====== 적 ======
        private void DrawEnemySection()
        {
            GUILayout.Label("■ 적", headerStyle);
            var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            GUILayout.Label($"생존 적 = {CountAlive(enemies)}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("전체 처치"))
            {
                foreach (var e in enemies)
                {
                    if (e != null && !e.IsDead) e.TakeDamage(e.CurrentHp);
                }
            }
            if (GUILayout.Button("전체 50 피해"))
            {
                foreach (var e in enemies)
                {
                    if (e != null && !e.IsDead) e.TakeDamage(50);
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
        }

        // ====== 스킬 ======
        private void DrawSkillSection()
        {
            GUILayout.Label("■ 스킬", headerStyle);
            var d = ResolveDraft();
            var pm = ResolvePool();

            if (AbilitySystem.HasInstance && GUILayout.Button("모든 스킬 쿨다운 리셋"))
            {
                AbilitySystem.Instance.ResetAllCooldowns();
            }

            if (d == null || pm == null || pm.Pool == null)
            {
                GUILayout.Label("(Draft 시스템 없음)");
                GUILayout.Space(6);
                return;
            }

            GUILayout.Label("클릭하면 즉시 지급:");
            foreach (var skill in pm.Pool)
            {
                if (skill == null) continue;
                string tag = skill.category == SkillCategory.Active ? "[A]" : "[P]";
                if (GUILayout.Button($"{tag} {skill.displayName}"))
                {
                    d.DebugGrantSkill(skill);
                }
            }
            GUILayout.Space(6);
        }

        // ====== 드래프트 / 폼 / 스테이지 ======
        private void DrawDraftFormStageSection()
        {
            GUILayout.Label("■ 드래프트 / 폼 / 스테이지", headerStyle);
            var d = ResolveDraft();
            if (d != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("드래프트 열기") && RunManager.HasInstance)
                    RunManager.Instance.GrantBonusLevel(DraftTriggerReason.LevelUp);
                if (GUILayout.Button("리롤")) d.TryReroll();
                if (GUILayout.Button("스킵")) d.TrySkip();
                GUILayout.EndHorizontal();
            }

            var p = ResolvePlayer();
            if (p != null && p.Form != null)
            {
                string cur = p.Form.CurrentForm != null ? p.Form.CurrentForm.formId : "?";
                if (GUILayout.Button($"폼 교체 (현재: {cur})")) p.Form.RequestSwap();
#if UNITY_EDITOR
                if (GUILayout.Button("폼 보상 발동 (미보유 폼 제시)")) OfferRewardFormCheat(p.Form);
#endif
            }

            var st = ResolveStage();
            if (st != null && GUILayout.Button("다음 룸으로"))
            {
                st.ProceedToNextRoom();
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 스테이지 이동 — 스테이지 3의 방 하나를 보려고 31방을 처음부터 도는 비용을 없앤다.
            // '다음 룸으로'는 한 단계씩만 가고 갈림길마다 선택 패널이 떠서 스무 번을 눌러야 했다.
            if (st != null && st.DebugStageCount > 0)
            {
                string where = st.CurrentStageIndex < 0
                    ? "미시작"
                    : $"Stage {st.CurrentStageIndex + 1}/{st.DebugStageCount} · Step {st.CurrentStepIndex + 1}/{st.DebugStepCount}";
                string roomId = st.CurrentRoom != null ? st.CurrentRoom.roomId : "-";
                GUILayout.Label($"위치: {where}  ({roomId})");

                GUILayout.BeginHorizontal();
                GUILayout.Label("이동", GUILayout.Width(30));
                for (int i = 0; i < st.DebugStageCount; i++)
                {
                    if (GUILayout.Button($"S{i + 1}")) st.DebugJumpTo(i, 0);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("◀ 이전 단계")) st.DebugStepBy(-1);
                if (GUILayout.Button("다음 단계 ▶")) st.DebugStepBy(1);
                GUILayout.EndHorizontal();
            }
#endif
            GUILayout.Space(6);
        }

#if UNITY_EDITOR
        // 테스트용(에디터 전용): 현재 슬롯에 없는 FormData를 찾아 폼 보상 모달을 띄운다.
        // 정식 보상은 FormAltar(직렬화 rewardForm, Run 씬 배치)가 담당 — 이 치트는 제단 없이 빠르게 검증하는 대체 경로.
        private static void OfferRewardFormCheat(FormController controller)
        {
            if (controller == null) return;

            var ownedIds = new HashSet<string>();
            for (int i = 0; i < controller.SlotCount; i++)
            {
                var f = controller.GetSlot(i);
                if (f != null) ownedIds.Add(f.formId);
            }

            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:FormData"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var form = UnityEditor.AssetDatabase.LoadAssetAtPath<FormData>(path);
                if (form != null && !ownedIds.Contains(form.formId))
                {
                    GameEvents.RaiseFormRewardOffered(form);
                    return;
                }
            }
            Debug.Log("[CheatMenu] 미보유 폼 없음 — 모든 폼이 이미 슬롯에 있음");
        }
#endif

        // ====== 참조 해석(지연·재탐색) ======
        private PlayerCharacter ResolvePlayer()
        {
            if (player == null) player = FindAnyObjectByType<PlayerCharacter>(FindObjectsInactive.Include);
            return player;
        }

        private DraftSessionController ResolveDraft()
        {
            if (draft == null) draft = FindAnyObjectByType<DraftSessionController>(FindObjectsInactive.Include);
            return draft;
        }

        private DraftPoolManager ResolvePool()
        {
            if (pool == null) pool = FindAnyObjectByType<DraftPoolManager>(FindObjectsInactive.Include);
            return pool;
        }

        private StageDirector ResolveStage()
        {
            if (stage == null) stage = FindAnyObjectByType<StageDirector>(FindObjectsInactive.Include);
            return stage;
        }

        private static int CountAlive(EnemyBase[] enemies)
        {
            int n = 0;
            foreach (var e in enemies)
            {
                if (e != null && !e.IsDead) n++;
            }
            return n;
        }
    }
}

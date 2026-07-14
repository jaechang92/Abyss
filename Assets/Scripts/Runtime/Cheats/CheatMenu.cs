using System.Collections.Generic;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
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
                GUILayout.Label($"골드 파편(런) = {rm.GoldShards}");
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

        // ====== 스토리(기록자) ======
        private void DrawStorySection()
        {
            GUILayout.Label("■ 스토리(기록자)", headerStyle);
            var meta = MetaSaveService.Instance;
            if (meta == null)
            {
                GUILayout.Label("(MetaSaveService 없음)");
                GUILayout.Space(6);
                return;
            }

            var save = meta.Current;
            int runDelta = save.records.totalRunCount - save.storyRunSnapshot;
            int bossDelta = save.records.totalBossKillCount - save.storyBossSnapshot;
            GUILayout.Label($"storyStage = {meta.StoryStage}  (런델타 {runDelta} / 보스델타 {bossDelta})");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("리셋(0)")) meta.DebugSetStoryStage(0);
            if (GUILayout.Button("단계 -1")) meta.DebugSetStoryStage(meta.StoryStage - 1);
            if (GUILayout.Button("단계 +1")) meta.DebugSetStoryStage(meta.StoryStage + 1);
            GUILayout.EndHorizontal();
            GUILayout.Label("※ 설정 시 스냅샷 0 리셋 — 다음 대화에서 해당 다음 챕터 열람 가능(누적 기준)");
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
            }

            var st = ResolveStage();
            if (st != null && GUILayout.Button("다음 룸으로"))
            {
                st.ProceedToNextRoom();
            }
            GUILayout.Space(6);
        }

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

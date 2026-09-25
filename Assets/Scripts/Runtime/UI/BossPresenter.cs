using System.Collections.Generic;
using System.Linq;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Feedback;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 보스 연출 한 벌 — 등장 카드 → 체력바 → 페이즈 대사 → 처치 슬로모션·대사(17-stage-flow-boss-presentation §2).
    ///
    /// <see cref="BossEnemy.Spawned"/> 하나에 붙는다. 스폰 경로(StageDirector·치트)마다 호출을 심지 않고,
    /// 씬 배선·HudBuilder 수정 없이 처음 보스가 나올 때 코드로 만든다(RunModalPanel과 같은 동적 생성 규약).
    ///
    /// 대사는 <b>enemyId에서 규칙으로 만든 키</b>(<c>Boss_{Stem}_Intro1</c> …) 중 GameText.csv에 있는 것만 쓴다.
    /// 보스마다 SO를 두지 않은 이유: 보스 5종의 연출 데이터가 문구 키뿐이라, 에셋·참조 배선을 늘릴 만큼의
    /// 내용이 없다. 새 보스는 CSV에 행만 넣으면 연출이 붙는다(없으면 이름 카드 없이 체력바만).
    /// </summary>
    public sealed class BossPresenter : MonoBehaviour
    {
        private const float BAR_WIDTH = 900f;
        private const float SUBTITLE_SECONDS = 3f;
        private const float SUBTITLE_FADE_SECONDS = 0.4f;
        private const float FLASH_ALPHA = 0.35f;
        private const float FLASH_SECONDS = 0.3f;
        private const float TRAIL_DELAY_SECONDS = 0.35f;
        private const float TRAIL_SPEED = 0.8f;             // 초당 줄어드는 비율
        private const float PHASE_HITSTOP_SECONDS = 0.08f;
        private const float DEATH_SLOW_SCALE = 0.3f;
        private const float DEATH_SLOW_SECONDS = 1.2f;
        private const float BAR_HIDE_DELAY_SECONDS = 1.6f;
        private const int MAX_INTRO_LINES = 4;

        private static readonly Color NameColor = new Color(1f, 0.86f, 0.60f);
        private static readonly Color BarBackColor = new Color(0.08f, 0.06f, 0.08f, 0.9f);
        private static readonly Color FillColor = new Color(0.80f, 0.16f, 0.18f);
        private static readonly Color TrailColor = new Color(1f, 0.78f, 0.55f, 0.85f);
        private static readonly Color TickColor = new Color(1f, 1f, 1f, 0.65f);
        private static readonly Color SubtitleColor = new Color(0.96f, 0.93f, 0.88f);

        private static BossPresenter instance;

        private BossEnemy boss;
        private string stem;

        private GameObject barRoot;
        private Text nameLabel;
        private RectTransform fillRect;
        private RectTransform trailRect;
        private readonly List<GameObject> ticks = new();
        private float targetRatio = 1f;
        private float trailRatio = 1f;
        private float trailHoldUntil;
        private float barHideAt = -1f;

        private CanvasGroup subtitleGroup;
        private Text subtitleLabel;
        private float subtitleShownAt = -1f;

        private Image flash;
        private float flashStartedAt = -1f;

        // ───────────────────────── 진입 ─────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        // BossEnemy가 SubsystemRegistration에서 자기 이벤트를 비운 뒤에 구독한다(그 뒤 단계).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            BossEnemy.Spawned -= HandleBossSpawned;
            BossEnemy.Spawned += HandleBossSpawned;
        }

        private static void HandleBossSpawned(BossEnemy spawned)
        {
            if (spawned == null || spawned.Data == null) return;
            EnsureInstance().Begin(spawned);
        }

        /// <summary>Run 씬 전용 — DontDestroyOnLoad 하지 않는다. 씬이 바뀌면 파괴되고 다음 보스 때 다시 만든다.</summary>
        private static BossPresenter EnsureInstance()
        {
            if (instance != null) return instance;

            var go = CreateOverlayCanvas("BossPresenter", UiSortingOrder.BossHud);
            instance = go.AddComponent<BossPresenter>();
            instance.BuildUI(go.transform);
            return instance;
        }

        private void Begin(BossEnemy spawned)
        {
            // 보스는 한 번에 하나다. 살아 있는 보스가 있는데 또 나오면(치트 등) 먼저 것을 유지한다.
            if (boss != null && !boss.IsDead)
            {
                Debug.LogWarning($"[BossPresenter] 이미 추적 중인 보스가 있다 — '{spawned.Data.enemyId}' 연출 생략");
                return;
            }

            Detach();
            boss = spawned;
            stem = Stem(spawned.Data.enemyId);
            boss.OnHpChanged += HandleHpChanged;
            boss.OnPhaseChanged += HandlePhaseChanged;

            string title = Line("Title") ?? spawned.Data.displayName;
            string epithet = Line("Epithet") ?? string.Empty;
            PrepareBar(title, epithet);

            var intro = new List<string>();
            for (int i = 1; i <= MAX_INTRO_LINES; i++)
            {
                string line = Line($"Intro{i}");
                if (line == null) break;
                intro.Add(line);
            }

            // 등장 대사가 없는 보스(키 미작성)는 카드 없이 곧바로 체력바 — 정지 없이 싸움이 시작된다.
            if (intro.Count == 0 && Line("Title") == null)
            {
                ShowBar();
                return;
            }
            BossIntroPanel.Play(title, epithet, intro, ShowBar);
        }

        private void OnDestroy()
        {
            Detach();
            if (instance == this) instance = null;
        }

        private void Detach()
        {
            if (boss == null) return;
            boss.OnHpChanged -= HandleHpChanged;
            boss.OnPhaseChanged -= HandlePhaseChanged;
            boss = null;
        }

        // ───────────────────────── 이벤트 ─────────────────────────

        /// <summary>EnemyBase.OnHpChanged는 (이전 HP, 현재 HP)다 — 최대치는 보스에게 묻는다.</summary>
        private void HandleHpChanged(int previous, int current)
        {
            int max = boss != null ? boss.MaxHp : 0;
            if (max <= 0) return;
            targetRatio = Mathf.Clamp01((float)current / max);
            SetRatio(fillRect, targetRatio);
            trailHoldUntil = Time.unscaledTime + TRAIL_DELAY_SECONDS;

            if (current <= 0) HandleDeath();
        }

        private void HandlePhaseChanged(int phase)
        {
            ShowSubtitle(Line($"Phase{phase}"));
            Flash();
            if (HitstopController.HasInstance) HitstopController.Instance.Trigger(PHASE_HITSTOP_SECONDS);
        }

        private void HandleDeath()
        {
            if (HitstopController.HasInstance) HitstopController.Instance.TriggerSlow(DEATH_SLOW_SCALE, DEATH_SLOW_SECONDS);
            ShowSubtitle(Line("Death"));
            Flash();
            barHideAt = Time.unscaledTime + BAR_HIDE_DELAY_SECONDS;
            Detach();
        }

        // ───────────────────────── 매 프레임(실시간) ─────────────────────────

        /// <summary>슬로모션·정지 위에서도 흘러야 하므로 전부 실시간으로 잰다.</summary>
        private void Update()
        {
            float now = Time.unscaledTime;

            if (trailRatio > targetRatio && now >= trailHoldUntil)
            {
                trailRatio = Mathf.Max(targetRatio, trailRatio - TRAIL_SPEED * Time.unscaledDeltaTime);
                SetRatio(trailRect, trailRatio);
            }

            if (barHideAt > 0f && now >= barHideAt)
            {
                barHideAt = -1f;
                barRoot.SetActive(false);
            }

            if (subtitleShownAt >= 0f)
            {
                float elapsed = now - subtitleShownAt;
                float fadeStart = SUBTITLE_SECONDS - SUBTITLE_FADE_SECONDS;
                subtitleGroup.alpha = elapsed < fadeStart ? 1f : Mathf.Clamp01(1f - (elapsed - fadeStart) / SUBTITLE_FADE_SECONDS);
                if (elapsed >= SUBTITLE_SECONDS)
                {
                    subtitleShownAt = -1f;
                    subtitleGroup.gameObject.SetActive(false);
                }
            }

            if (flashStartedAt >= 0f)
            {
                float t = (now - flashStartedAt) / FLASH_SECONDS;
                var color = flash.color;
                color.a = FLASH_ALPHA * Mathf.Clamp01(1f - t);
                flash.color = color;
                if (t >= 1f)
                {
                    flashStartedAt = -1f;
                    flash.gameObject.SetActive(false);
                }
            }
        }

        // ───────────────────────── 표시 ─────────────────────────

        private void PrepareBar(string title, string epithet)
        {
            nameLabel.text = string.IsNullOrEmpty(epithet)
                ? title
                : $"{title}   <size=18><color=#B3B3CC>{epithet}</color></size>";

            targetRatio = trailRatio = 1f;
            SetRatio(fillRect, 1f);
            SetRatio(trailRect, 1f);
            barHideAt = -1f;

            foreach (var tick in ticks) Destroy(tick);
            ticks.Clear();
            AddTick(boss.Phase2HpThreshold);
            AddTick(boss.Phase3HpThreshold);

            barRoot.SetActive(false);
        }

        private void ShowBar()
        {
            // 등장 카드를 넘기는 사이 보스가 이미 죽었을 수 있다(치트 등) — 그때는 띄우지 않는다.
            if (boss == null) return;
            barRoot.SetActive(true);
        }

        private void ShowSubtitle(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            subtitleLabel.text = text;
            subtitleGroup.alpha = 1f;
            subtitleGroup.gameObject.SetActive(true);
            subtitleShownAt = Time.unscaledTime;
        }

        private void Flash()
        {
            flash.gameObject.SetActive(true);
            flashStartedAt = Time.unscaledTime;
        }

        private void AddTick(float ratio)
        {
            if (ratio <= 0f || ratio >= 1f) return;
            var tick = CreateRect(fillRect.parent, "PhaseTick", new Vector2(ratio, 0f), new Vector2(ratio, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 0f));
            var image = tick.AddComponent<Image>();
            image.color = TickColor;
            image.raycastTarget = false;
            ticks.Add(tick);
        }

        private static void SetRatio(RectTransform rect, float ratio)
        {
            rect.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
            rect.offsetMax = Vector2.zero;
        }

        private void BuildUI(Transform root)
        {
            // 체력바 — 화면 위 가운데. HUD의 좌상단 체력·우상단 골드와 겹치지 않는 자리다.
            barRoot = CreateRect(root, "BossBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(BAR_WIDTH, 64));

            nameLabel = CreateLabel(barRoot.transform, "Name", new Vector2(0, 14), new Vector2(BAR_WIDTH, 32), string.Empty, 24, NameColor, TextAnchor.MiddleLeft);
            nameLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            var back = CreateRect(barRoot.transform, "Back", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -14), new Vector2(BAR_WIDTH, 18));
            var backImage = back.AddComponent<Image>();
            backImage.color = BarBackColor;
            backImage.raycastTarget = false;

            trailRect = CreateFill(back.transform, "Trail", TrailColor);
            fillRect = CreateFill(back.transform, "Fill", FillColor);
            barRoot.SetActive(false);

            // 대사 자막 — 체력바 아래. 전투를 가리지 않도록 짧게 떴다가 사라진다.
            var subtitle = CreateRect(root, "Subtitle", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -104), new Vector2(1400, 44));
            subtitleGroup = subtitle.AddComponent<CanvasGroup>();
            subtitleGroup.blocksRaycasts = false;
            subtitleLabel = CreateLabel(subtitle.transform, "Text", Vector2.zero, new Vector2(1400, 44), string.Empty, 26, SubtitleColor, TextAnchor.MiddleCenter);
            subtitleLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            subtitle.SetActive(false);

            // 페이즈·처치 번쩍임 — 화면 전체, 클릭을 막지 않는다.
            var flashGo = CreateRect(root, "Flash", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)flashGo.transform);
            flash = flashGo.AddComponent<Image>();
            flash.color = new Color(1f, 1f, 1f, 0f);
            flash.raycastTarget = false;
            flashGo.SetActive(false);
        }

        private static RectTransform CreateFill(Transform parent, string name, Color color)
        {
            var go = CreateRect(parent, name, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            var rect = (RectTransform)go.transform;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        // ───────────────────────── 대사 키 ─────────────────────────

        /// <summary>규칙 키 <c>Boss_{Stem}_{part}</c>의 현재 언어 문구. 표에 없으면 null.</summary>
        private string Line(string part)
        {
            string key = $"Boss_{stem}_{part}";
            return Loc.HasKey(key) ? Loc.Get(key) : null;
        }

        /// <summary>
        /// enemyId → 키 줄기. 앞의 <c>boss_</c>·<c>midboss_</c>를 떼고 PascalCase로 잇는다.
        /// boss_abyss_keeper → AbyssKeeper · midboss_throne_warden → ThroneWarden.
        /// </summary>
        public static string Stem(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return string.Empty;

            string id = enemyId;
            foreach (string prefix in new[] { "midboss_", "boss_" })
            {
                if (!id.StartsWith(prefix)) continue;
                id = id.Substring(prefix.Length);
                break;
            }
            return string.Concat(id.Split('_').Where(part => part.Length > 0)
                .Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using Abyss.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Abyss.Tests.PlayMode
{
    public sealed class BossHudReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private readonly List<Object> owned = new();
        private readonly Dictionary<FieldInfo, object> statsSnapshot = new();
        private BossPresenter presenter;
        private BossPresenter previousPresenter;
        private BossEnemy boss;
        private Text name;
        private Text subtitle;
        private RectTransform bar;
        private RectTransform back;
        private RectTransform fill;
        private RectTransform trail;
        private Canvas canvas;
        private RunManager existingRun;
        private LocalizationLanguage language;
        private Random.State random;
        private float timeScale;
        private int phaseNotifications;
        private static readonly FieldInfo InstanceField = typeof(BossPresenter).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire();
            language = Loc.CurrentLanguage; random = Random.state; timeScale = Time.timeScale;
            Time.timeScale = 1f;
            existingRun = RunManager.GetInstanceSafe();
            if (existingRun != null)
                foreach (var field in typeof(RunStats).GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    object value = field.GetValue(existingRun.Stats);
                    if (value is List<string> list) value = new List<string>(list);
                    if (value is Dictionary<string, float> dict) value = new Dictionary<string, float>(dict);
                    statsSnapshot[field] = value;
                }
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            Assert.True(MetaSaveService.Instance.TrySaveReadability(1.25f, false));
            previousPresenter = (BossPresenter)InstanceField.GetValue(null);
            InstanceField.SetValue(null, null);
            presenter = (BossPresenter)typeof(BossPresenter).GetMethod("EnsureInstance", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            owned.Add(presenter.gameObject);
            canvas = presenter.GetComponent<Canvas>();
            boss = CreateBoss();
            // Use the actual spawn handler's Begin path while preventing autonomous attacks and intro-card ownership.
            Call(presenter, "Begin", boss);
            name = Field<Text>(presenter, "nameLabel"); subtitle = Field<Text>(presenter, "subtitleLabel");
            bar = (RectTransform)Field<GameObject>(presenter, "barRoot").transform;
            back = bar.Find("Back") as RectTransform;
            fill = Field<RectTransform>(presenter, "fillRect"); trail = Field<RectTransform>(presenter, "trailRect");
            Call(presenter, "ApplyBarPortrait", "boss_abyss_keeper");
            boss.OnPhaseChanged += HandlePhase;
            yield return Settle();
            Assert.True(bar.gameObject.activeSelf);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (boss != null) boss.OnPhaseChanged -= HandlePhase;
                foreach (var item in owned) if (item != null) Object.Destroy(item);
                owned.Clear(); yield return null;
                InstanceField.SetValue(null, previousPresenter != null ? previousPresenter : null);
                if (existingRun != null)
                    foreach (var entry in statsSnapshot)
                    {
                        object current = entry.Key.GetValue(existingRun.Stats);
                        if (current is List<string> list && entry.Value is List<string> savedList) { list.Clear(); list.AddRange(savedList); }
                        else if (current is Dictionary<string, float> dict && entry.Value is Dictionary<string, float> savedDict)
                        { dict.Clear(); foreach (var pair in savedDict) dict.Add(pair.Key, pair.Value); }
                        else entry.Key.SetValue(existingRun.Stats, entry.Value);
                    }
                statsSnapshot.Clear(); phaseNotifications = 0;
                Loc.SetLanguage(language); Time.timeScale = timeScale; Random.state = random;
            }
            finally { guard.Release(); }
        }

        [UnityTest]
        public IEnumerator LongTitleAndRichEpithetFitAllScalesAndLanguagesPreservingGaugeAndPortrait()
        {
            var backBaseline = back.anchoredPosition;
            var backSize = back.rect.size;
            var portrait = bar.Find("ArtBossPortrait") as RectTransform;
            Vector2 portraitSize = portrait != null ? portrait.rect.size : Vector2.zero;
            Vector2 portraitPosition = portrait != null ? portrait.anchoredPosition : Vector2.zero;
            var frame = back.Find("ArtImageFrame") as RectTransform;
            Vector2 frameSize = frame != null ? frame.rect.size : Vector2.zero;
            Vector2 framePosition = frame != null ? frame.anchoredPosition : Vector2.zero;
            foreach (var lang in new[] { LocalizationLanguage.Korean, LocalizationLanguage.English, LocalizationLanguage.Japanese })
            {
                Loc.SetLanguage(lang);
                string title = lang switch
                {
                    LocalizationLanguage.English => "The Ancient Guardian Who Keeps Every Forgotten Gate Beyond The Ruined Throne",
                    LocalizationLanguage.Japanese => "崩れた玉座の向こうで忘れられた全ての門を守り続ける古代の守護者",
                    _ => "무너진 왕좌 너머에서 잊힌 모든 문을 끝까지 지키는 오래된 심연의 수호자"
                };
                string epithet = "긴 별칭 A lengthy epithet 長い二つ名";
                foreach (float scale in new[] { 1f, 1.25f, 1.5f })
                {
                    Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale));
                    Call(presenter, "PrepareBar", title, epithet); Call(presenter, "ShowBar");
                    yield return Settle(); AssertName(scale);
                    StringAssert.Contains(title, name.text); StringAssert.Contains(epithet, name.text);
                    StringAssert.Contains("<size=" + Mathf.RoundToInt(18 * scale) + ">", name.text);
                    AssertVector(backBaseline, back.anchoredPosition); AssertVector(backSize, back.rect.size);
                    if (portrait != null) { AssertVector(portraitSize, portrait.rect.size); AssertVector(portraitPosition, portrait.anchoredPosition); }
                    if (frame != null) { AssertVector(frameSize, frame.rect.size); AssertVector(framePosition, frame.anchoredPosition); }
                    Assert.AreEqual(2, Field<List<GameObject>>(presenter, "ticks").Count);
                    Assert.AreEqual(1f, fill.anchorMax.x); Assert.AreEqual(1f, trail.anchorMax.x);
                    AssertSubtitleGap();
                }
            }
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.enabled = false;
            canvas.scaleFactor = 0.5f; yield return Settle(); AssertName(1.5f); AssertSubtitleGap();
            Assert.AreEqual(100, boss.CurrentHp);
        }

        [UnityTest]
        public IEnumerator ShortNameRestoresOriginalGeometryAndRepeatedPrepareCannotAccumulateRichScale()
        {
            Call(presenter, "PrepareBar", "Boss", "Guardian"); Call(presenter, "ShowBar"); yield return Settle();
            Vector2 nameSize = name.rectTransform.rect.size, namePosition = name.rectTransform.anchoredPosition;
            Vector2 barPosition = bar.anchoredPosition;
            var subtitleRoot = (RectTransform)subtitle.transform.parent; Vector2 subtitlePosition = subtitleRoot.anchoredPosition;
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            for (int i = 0; i < 3; i++)
            {
                Call(presenter, "PrepareBar", "Boss", "Guardian"); Call(presenter, "ShowBar");
                Call(presenter, "CaptureNameReadability"); yield return Settle();
                AssertName(1.5f); StringAssert.Contains("<size=27>", name.text); StringAssert.DoesNotContain("<size=41>", name.text);
            }
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f)); yield return Settle();
            AssertName(1f); StringAssert.Contains("<size=18>", name.text);
            AssertVector(nameSize, name.rectTransform.rect.size); AssertVector(namePosition, name.rectTransform.anchoredPosition);
            AssertVector(barPosition, bar.anchoredPosition); AssertVector(subtitlePosition, subtitleRoot.anchoredPosition);
        }

        [UnityTest]
        public IEnumerator ActualBossDamagePhaseThresholdsAndUnscaledTrailRemainFunctionalWhenEnlarged()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); yield return Settle();
            Time.timeScale = 0f;
            boss.TakeDamage(40);
            Assert.AreEqual(60, boss.CurrentHp); Assert.AreEqual(0.6f, fill.anchorMax.x, 0.001f);
            Assert.AreEqual(1f, trail.anchorMax.x, 0.001f);
            Call(boss, "CheckPhaseTransition"); Assert.AreEqual(2, boss.CurrentPhase); Assert.AreEqual(1, phaseNotifications);
            var ticks = Field<List<GameObject>>(presenter, "ticks");
            Assert.AreEqual(boss.Phase2HpThreshold, ((RectTransform)ticks[0].transform).anchorMin.x);
            Assert.AreEqual(boss.Phase3HpThreshold, ((RectTransform)ticks[1].transform).anchorMin.x);
            yield return new WaitForSecondsRealtime(0.65f);
            Assert.Less(trail.anchorMax.x, 1f); Assert.GreaterOrEqual(trail.anchorMax.x, fill.anchorMax.x);
            boss.TakeDamage(35); Call(boss, "CheckPhaseTransition");
            Assert.AreEqual(3, boss.CurrentPhase); Assert.AreEqual(2, phaseNotifications);
            Assert.AreEqual(0.25f, fill.anchorMax.x, 0.001f);
            AssertName(1.5f);
        }

        [UnityTest]
        public IEnumerator ActualDeathHidesBarAfterUnscaledDelayWhilePaused()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); yield return Settle();
            Time.timeScale = 0f;
            boss.TakeDamage(100);
            Assert.True(boss.IsDead); Assert.AreEqual(0f, fill.anchorMax.x);
            Assert.True(bar.gameObject.activeSelf);
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.False(bar.gameObject.activeSelf);
            Assert.AreEqual(0f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator MenuNameScaleDoesNotChangeNarrativeSubtitleScaleOrText()
        {
            string line = "별도로 설정한 서사 자막 Narrative subtitle uses its own setting 独立した物語字幕";
            Call(presenter, "ShowSubtitle", line); yield return Settle();
            int narrativeFont = subtitle.fontSize;
            Assert.AreEqual(Mathf.RoundToInt(26f * 1.25f), narrativeFont);
            foreach (float scale in new[] { 1f, 1.5f, 1.25f, 1f })
            {
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale)); yield return Settle();
                Assert.AreEqual(narrativeFont, subtitle.fontSize); Assert.AreEqual(line, subtitle.text);
                Assert.AreEqual(1.25f, NarrativeReadability.Scale); Assert.AreEqual(1.25f, MetaSaveService.Instance.Current.settings.narrativeTextScale);
                AssertName(scale); AssertSubtitleGap();
            }
            Assert.False(subtitle.GetComponentInParent<CanvasGroup>().blocksRaycasts);
        }

        [UnityTest]
        public IEnumerator WideHudCountersAvoidActualBossGeometryAcrossDifferentOverlayCanvasScales()
        {
            var hudCanvasGo = Own(new GameObject("CrossCanvasHud", typeof(RectTransform), typeof(Canvas)));
            var hudCanvas = hudCanvasGo.GetComponent<Canvas>(); hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var hud = HudNode(hudCanvas.transform, "HUD");
            var hp = HudNode(hud, "HealthBar"); HudImage(hp, "Background"); HudImage(hp, "Fill");
            var hpLabel = HudText(hp, "HPText", "100/100");
            var form = HudNode(hud, "FormSlot");
            HudImage(form, "CurrentFormIcon"); HudImage(form, "OtherFormIcon"); HudImage(form, "CooldownFill");
            for (int i = 0; i < 2; i++)
            {
                var slot = HudNode(hud, "SkillSlot" + i); HudImage(slot, "Icon"); HudImage(slot, "CooldownFill"); HudText(slot, "Label", "Skill");
            }
            HudText(HudNode(HudNode(hud, "FormBiasWarning"), "Body"), "Label", "Warning");
            var synergyBody = HudNode(HudNode(hud, "SynergyCounter"), "Body"); synergyBody.gameObject.AddComponent<GridLayoutGroup>();
            var template = HudNode(synergyBody, "ChipTemplate"); HudText(template, "Label", "Axis"); template.gameObject.SetActive(false);
            HudText(HudNode(HudNode(hud, "BurnStack"), "Body"), "Label", "Burn 10");
            var gold = HudNode(hud, "GoldCounter"); HudImage(gold, "Background");
            var goldLabel = HudText(gold, "Label", "골드 0");
            bar.gameObject.SetActive(false); hudCanvas.scaleFactor = 0.8f;
            HudArtSkin.Apply(hud);
            hpLabel.text = int.MaxValue + "/" + int.MaxValue; goldLabel.text = "골드 " + int.MaxValue;
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            Assert.True(MetaSaveService.Instance.TrySaveReadability(1.25f, true));
            var captionGroup = Field<CanvasGroup>(presenter, "subtitleGroup");
            foreach (float hudScale in new[] { 0.8f, 0.5f })
            {
                hudCanvas.scaleFactor = hudScale; bar.gameObject.SetActive(false); captionGroup.gameObject.SetActive(false); yield return Settle();
                Assert.AreEqual(0f, BossPresenter.ReservedHudBottom(hudCanvas));
                Vector2 hiddenHp = ((RectTransform)hp).anchoredPosition, hiddenGold = ((RectTransform)gold).anchoredPosition;
                Call(presenter, "PrepareBar", "The Very Ancient Guardian Of Every Forgotten Gate Beyond The Ruined Throne And Its Unending Shadow", "끝없이 길어진 보스의 별칭 An exceptionally long epithet 長いボスの二つ名");
                Call(presenter, "ShowBar");
                Call(presenter, "ShowSubtitle", "길어진 보스의 실제 자막을 전부 읽을 수 있어야 합니다. The entire boss caption stays readable while the HP and gold counters remain separate. 長いボスの字幕を最後まで読めます。");
                yield return Settle();
                Assert.True(captionGroup.gameObject.activeInHierarchy);
                var contrast = Field<Image>(subtitle.GetComponent<NarrativeReadability>(), "background");
                Assert.True(contrast.gameObject.activeInHierarchy);
                Assert.AreEqual(1.25f, NarrativeReadability.Scale);
                Vector2 captionHp = ((RectTransform)hp).anchoredPosition, captionGold = ((RectTransform)gold).anchoredPosition;
                float largeReserve = BossPresenter.ReservedHudBottom(hudCanvas);
                Assert.Greater(largeReserve, 0f); AssertHudDoesNotOverlapBoss((RectTransform)hp, (RectTransform)gold);
                Assert.AreEqual(42, hpLabel.fontSize); Assert.AreEqual(42, goldLabel.fontSize);
                Assert.GreaterOrEqual(hpLabel.rectTransform.rect.height + 1f, hpLabel.preferredHeight);
                Assert.GreaterOrEqual(goldLabel.rectTransform.rect.height + 1f, goldLabel.preferredHeight);
                captionGroup.gameObject.SetActive(false); yield return Settle();
                Assert.GreaterOrEqual(((RectTransform)hp).anchoredPosition.y + 0.01f, captionHp.y);
                Assert.GreaterOrEqual(((RectTransform)gold).anchoredPosition.y + 0.01f, captionGold.y);
                AssertHudDoesNotOverlapBoss((RectTransform)hp, (RectTransform)gold);
                Call(presenter, "PrepareBar", "Boss", "Guard"); Call(presenter, "ShowBar"); yield return Settle();
                Assert.LessOrEqual(BossPresenter.ReservedHudBottom(hudCanvas), largeReserve + 0.1f);
                AssertHudDoesNotOverlapBoss((RectTransform)hp, (RectTransform)gold);
                bar.gameObject.SetActive(false); yield return Settle();
                Assert.AreEqual(0f, BossPresenter.ReservedHudBottom(hudCanvas));
                AssertVector(hiddenHp, ((RectTransform)hp).anchoredPosition); AssertVector(hiddenGold, ((RectTransform)gold).anchoredPosition);
            }
        }

        private void AssertHudDoesNotOverlapBoss(RectTransform hp, RectTransform gold)
        {
            var captionContrast = Field<Image>(subtitle.GetComponent<NarrativeReadability>(), "background");
            foreach (var bossRect in new[] { name.rectTransform, back, back.Find("ArtImageFrame") as RectTransform,
                bar.Find("ArtBossPortrait") as RectTransform, subtitle.rectTransform, captionContrast.rectTransform })
            {
                if (bossRect == null || !bossRect.gameObject.activeInHierarchy) continue;
                var image = bossRect.GetComponent<Image>(); if (image != null && !image.enabled) continue;
                Rect actual = ScreenBounds(bossRect);
                Assert.False(actual.Overlaps(ScreenBounds(hp)), "Wide HP overlaps boss " + bossRect.name + " in screen coordinates.");
                Assert.False(actual.Overlaps(ScreenBounds(gold)), "Wide gold overlaps boss " + bossRect.name + " in screen coordinates.");
            }
        }
        private static Rect ScreenBounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var corner in corners)
            { Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static Transform HudNode(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; return go.transform;
        }
        private static void HudImage(Transform parent, string name) => HudNode(parent, name).gameObject.AddComponent<Image>();
        private static Text HudText(Transform parent, string name, string text)
        {
            var label = HudNode(parent, name).gameObject.AddComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24; label.text = text; return label;
        }
        [UnityTest]
        public IEnumerator ModerateHpBelowLegacyWidthLimitStillAvoidsActualBossPortrait()
        {
            var hudGo = Own(new GameObject("ModerateBossHud", typeof(RectTransform), typeof(Canvas)));
            var hudCanvas = hudGo.GetComponent<Canvas>(); hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            float sharedScale = Mathf.Max(0.01f, Screen.width / 1920f);
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.scaleFactor = hudCanvas.scaleFactor = sharedScale;
            var hud = HudNode(hudCanvas.transform, "HUD");
            var hp = HudNode(hud, "HealthBar"); HudImage(hp, "Background"); HudImage(hp, "Fill");
            var hpText = HudText(hp, "HPText", "100/100");
            var form = HudNode(hud, "FormSlot");
            HudImage(form, "CurrentFormIcon"); HudImage(form, "OtherFormIcon"); HudImage(form, "CooldownFill");
            for (int i = 0; i < 2; i++)
            {
                var slot = HudNode(hud, "SkillSlot" + i); HudImage(slot, "Icon"); HudImage(slot, "CooldownFill"); HudText(slot, "Label", "Skill");
            }
            HudText(HudNode(HudNode(hud, "FormBiasWarning"), "Body"), "Label", "Warning");
            var body = HudNode(HudNode(hud, "SynergyCounter"), "Body"); body.gameObject.AddComponent<GridLayoutGroup>();
            var template = HudNode(body, "ChipTemplate"); HudText(template, "Label", "Axis"); template.gameObject.SetActive(false);
            HudText(HudNode(HudNode(hud, "BurnStack"), "Body"), "Label", "Burn");
            var gold = HudNode(hud, "GoldCounter"); HudImage(gold, "Background"); HudText(gold, "Label", "골드 0");
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            Call(presenter, "PrepareBar", "Boss", "Guard"); Call(presenter, "ShowBar");
            yield return Settle();
            var portrait = bar.Find("ArtBossPortrait") as RectTransform;
            Assert.NotNull(portrait); Assert.True(portrait.GetComponent<Image>().enabled);
            bar.gameObject.SetActive(false); HudArtSkin.Apply(hud);
            float legacyLimit = (((RectTransform)hudCanvas.transform).rect.width - 900f) * 0.5f - 48f - 20f;
            bool found = false;
            Vector2 hiddenPosition = Vector2.zero;
            foreach (float scale in new[] { 1f, 1.25f, 1.5f })
            {
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale));
                foreach (int value in new[] { 999999, 9999999, 99999999, 999999999, int.MaxValue })
                {
                    hpText.text = value + "/" + value; yield return Settle();
                    Rect hpScreen = ScreenBounds((RectTransform)hp), portraitScreen = ScreenBounds(portrait);
                    if (((RectTransform)hp).rect.width <= legacyLimit && hpScreen.xMax > portraitScreen.xMin && hpScreen.xMin < portraitScreen.xMax)
                    {
                        found = true; hiddenPosition = ((RectTransform)hp).anchoredPosition;
                        Assert.LessOrEqual(((RectTransform)hp).rect.width, legacyLimit);
                        break;
                    }
                }
                if (found) break;
            }
            Assert.True(found, "Measure a genuine moderate HP value whose corner crosses the portrait before showing the boss.");
            Call(presenter, "ShowBar"); yield return Settle();
            AssertHudDoesNotOverlapBoss((RectTransform)hp, (RectTransform)gold);
            Assert.Less(((RectTransform)hp).anchoredPosition.y, hiddenPosition.y, "Actual portrait overlap must move HP despite passing the legacy 900-wide threshold.");
            Assert.Greater(BossPresenter.ReservedHudBottom(hudCanvas), 0f);
            bar.gameObject.SetActive(false); yield return Settle();
            AssertVector(hiddenPosition, ((RectTransform)hp).anchoredPosition);
        }

        private static void AssertVector(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.01f);
            Assert.AreEqual(expected.y, actual.y, 0.01f);
        }
        private BossEnemy CreateBoss()
        {
            var data = Own(ScriptableObject.CreateInstance<EnemyData>());
            data.enemyId = "boss_readability_fixture"; data.displayName = "Fixture Boss";
            data.baseHp = 100; data.expReward = 0; data.goldReward = 0;
            // Neutral reward tier isolates actual HP/death presentation from boss-clear progression.
            data.tier = EnemyTier.Normal;
            var go = Own(new GameObject("BossHudReadabilityTestEnemy")); go.SetActive(false);
            var enemy = go.AddComponent<BossEnemy>(); enemy.enabled = false; Set(enemy, "data", data);
            go.SetActive(true); enemy.Body.gravityScale = 0; enemy.Body.simulated = false;
            return enemy;
        }
        private void AssertName(float scale)
        {
            Assert.AreEqual(Mathf.RoundToInt(24 * scale), name.fontSize); Assert.False(name.resizeTextForBestFit);
            Assert.GreaterOrEqual(name.rectTransform.rect.height + 1f, name.preferredHeight);
            var corners = new Vector3[4]; name.rectTransform.GetWorldCorners(corners);
            var parent = (RectTransform)canvas.transform;
            foreach (var corner in corners)
            {
                Vector3 local = parent.InverseTransformPoint(corner);
                Assert.GreaterOrEqual(local.y, parent.rect.yMin + 3f); Assert.LessOrEqual(local.y, parent.rect.yMax - 3f);
                Assert.GreaterOrEqual(local.x, parent.rect.xMin + 3f); Assert.LessOrEqual(local.x, parent.rect.xMax - 3f);
            }
            Rect nameBounds = Bounds(name.rectTransform, bar);
            var frame = back.Find("ArtImageFrame") as RectTransform;
            Rect gaugeBounds = Bounds(frame != null ? frame : back, bar);
            Assert.GreaterOrEqual(nameBounds.yMin, gaugeBounds.yMax + 3f);
        }
        private void AssertSubtitleGap()
        {
            var frame = back.Find("ArtImageFrame") as RectTransform;
            Rect gauge = Bounds(frame != null ? frame : back, canvas.transform);
            Rect text = Bounds((RectTransform)subtitle.transform.parent, canvas.transform);
            Assert.LessOrEqual(text.yMax + 1f, gauge.yMin);
        }
        private static Rect Bounds(RectTransform rect, Transform parent)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var corner in corners) { Vector2 point = parent.InverseTransformPoint(corner); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private void HandlePhase(int phase) => phaseNotifications++;
        private T Own<T>(T item) where T : Object { owned.Add(item); return item; }
        private static IEnumerator Settle()
        {
            yield return null; yield return null; Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases();
        }
        private static FieldInfo FindField(System.Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                var result = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance); if (result != null) return result;
            }
            throw new System.MissingFieldException(name);
        }
        private static T Field<T>(object target, string name) => (T)FindField(target.GetType(), name).GetValue(target);
        private static void Set(object target, string name, object value) => FindField(target.GetType(), name).SetValue(target, value);
        private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    }
}
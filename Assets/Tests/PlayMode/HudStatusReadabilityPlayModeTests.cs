using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Abyss.Runtime.UI;

namespace Abyss.Tests.PlayMode
{
    public sealed class HudStatusReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private readonly List<Object> ownedObjects = new();
        private readonly Dictionary<FieldInfo, object> statsSnapshot = new();
        private Transform hud;
        private Canvas canvas;
        private FormBiasWarningPresenter bias;
        private SynergyCounterPresenter synergy;
        private Text warning;
        private GameObject biasBody;
        private GameObject synergyBody;
        private DraftSessionController session;
        private List<SkillData> ownedSkills;
        private RunManager run;
        private bool ownsRun;
        private bool runActive;
        private int goldBefore;
        private int earnedBefore;
        private RunEndReason endReasonBefore;
        private LocalizationLanguage language;
        private Random.State random;
        private float timeScale;
        private FormData[] originalCatalog;
        private const string LongForm = "긴 이름을 가진 테스트 폼 Long testing form name 長い名前のテストフォーム";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire();
            language = Loc.CurrentLanguage;
            random = Random.state;
            timeScale = Time.timeScale;
            ownsRun = !RunManager.HasInstance;
            run = RunManager.Instance;
            runActive = run.IsRunActive;
            goldBefore = run.GoldShards;
            earnedBefore = run.LastRunAbyssShardsEarned;
            endReasonBefore = run.LastRunEndReason;
            foreach (var field in typeof(RunStats).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = field.GetValue(run.Stats);
                if (value is List<string> list) value = new List<string>(list);
                if (value is Dictionary<string, float> dict) value = new Dictionary<string, float>(dict);
                statsSnapshot[field] = value;
            }
            Set(run, "isRunActive", true);
            run.Stats.formPlaytimeSeconds.Clear();
            run.Stats.formPlaytimeSeconds[LongForm] = 90f;
            run.Stats.formPlaytimeSeconds["other"] = 10f;
            originalCatalog = (FormData[])CatalogField.GetValue(null);
            // Unknown ID intentionally exercises the real presenter fallback without changing existing form assets.
            CatalogField.SetValue(null, new FormData[0]);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            var sessionGo = Own(new GameObject("HudStatusTestSession"));
            sessionGo.SetActive(false);
            var form = sessionGo.AddComponent<FormController>();
            form.SetRunContext(false);
            session = sessionGo.AddComponent<DraftSessionController>();
            Set(session, "formController", form);
            sessionGo.SetActive(true);
            session.enabled = false;
            ownedSkills = Field<List<SkillData>>(session, "owned");
            AddSkill("fire"); AddSkill("fire"); AddSkill("abyss");
            var canvasGo = Own(new GameObject("HudStatusTestCanvas", typeof(RectTransform), typeof(Canvas)));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.scaleFactor = Mathf.Max(0.01f, Screen.width / 1920f);
            hud = Node(canvasGo.transform, "HUD");
            var hudRect = (RectTransform)hud;
            hudRect.anchorMin = hudRect.anchorMax = new Vector2(0.5f, 0.5f);
            hudRect.sizeDelta = new Vector2(1920f, 1080f);
            var hp = Node(hud, "HealthBar");
            ImageNode(hp, "Background"); ImageNode(hp, "Fill"); TextNode(hp, "HPText").text = "100/100";
            var formRoot = Node(hud, "FormSlot");
            ImageNode(formRoot, "CurrentFormIcon"); ImageNode(formRoot, "OtherFormIcon"); ImageNode(formRoot, "CooldownFill");
            for (int i = 0; i < 2; i++)
            {
                var slot = Node(hud, "SkillSlot" + i);
                ImageNode(slot, "Icon"); ImageNode(slot, "CooldownFill"); TextNode(slot, "Label");
            }
            var biasRoot = Node(hud, "FormBiasWarning");
            biasBody = Node(biasRoot, "Body").gameObject;
            warning = TextNode(biasBody.transform, "Label");
            biasRoot.gameObject.SetActive(false);
            bias = biasRoot.gameObject.AddComponent<FormBiasWarningPresenter>();
            Set(bias, "root", biasBody); Set(bias, "label", warning);
            var synergyRoot = Node(hud, "SynergyCounter");
            synergyBody = Node(synergyRoot, "Body").gameObject;
            var grid = synergyBody.AddComponent<GridLayoutGroup>();
            var template = Node(synergyBody.transform, "ChipTemplate").gameObject;
            template.AddComponent<Image>(); template.AddComponent<LayoutElement>();
            TextNode(template.transform, "Label"); template.SetActive(false);
            synergyRoot.gameObject.SetActive(false);
            synergy = synergyRoot.gameObject.AddComponent<SynergyCounterPresenter>();
            Set(synergy, "root", synergyBody); Set(synergy, "chipContainer", (RectTransform)synergyBody.transform);
            Set(synergy, "chipTemplate", template); Set(synergy, "draftSession", session);
            TextNode(Node(Node(hud, "BurnStack"), "Body"), "Label");
            var gold = Node(hud, "GoldCounter"); ImageNode(gold, "Background"); TextNode(gold, "Label").text = "골드 0";
            // Apply first: the inactive template acquires the real HUD style before clones are created.
            HudArtSkin.Apply(hud);
            biasRoot.gameObject.SetActive(true);
            synergyRoot.gameObject.SetActive(true);
            bias.Refresh(); synergy.Refresh();
            yield return Settle();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                foreach (var item in ownedObjects) if (item != null) Object.Destroy(item);
                ownedObjects.Clear();
                foreach (var entry in statsSnapshot)
                {
                    object current = entry.Key.GetValue(run.Stats);
                    if (current is List<string> list && entry.Value is List<string> savedList)
                    { list.Clear(); list.AddRange(savedList); }
                    else if (current is Dictionary<string, float> dict && entry.Value is Dictionary<string, float> savedDict)
                    { dict.Clear(); foreach (var pair in savedDict) dict.Add(pair.Key, pair.Value); }
                    else entry.Key.SetValue(run.Stats, entry.Value);
                }
                statsSnapshot.Clear();
                Set(run, "isRunActive", runActive);
                Set(run, "lastRunEndReason", endReasonBefore);
                Set(run, "lastRunAbyssShardsEarned", earnedBefore);
                Set(run, "goldShards", goldBefore);
                GameEvents.RaiseGoldShardsChanged(goldBefore);
                if (ownsRun) Object.Destroy(run.gameObject);
                CatalogField.SetValue(null, originalCatalog);
                Loc.SetLanguage(language); Time.timeScale = timeScale; Random.state = random;
                yield return null;
            }
            finally { guard.Release(); }
        }

        [UnityTest]
        public IEnumerator AllScalesAndLanguagesKeepFullWarningAndChipsWithinSeparatedBands()
        {
            float originalScale = canvas.scaleFactor;
            foreach (var lang in new[] { LocalizationLanguage.Korean, LocalizationLanguage.English, LocalizationLanguage.Japanese })
            {
                Loc.SetLanguage(lang);
                bias.Refresh(); synergy.Refresh();
                foreach (float scale in new[] { 1f, 1.25f, 1.5f })
                {
                    Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale));
                    yield return Settle();
                    AssertLayout(scale);
                    Assert.AreEqual(Loc.GetFormat(StringKey.FormBias_WarningFormat, LongForm, 0.9f), warning.text);
                    Assert.True(biasBody.activeSelf); Assert.True(synergyBody.activeSelf);
                    AssertChip("[불꽃] 2 ✦"); AssertChip("[심연] 1");
                }
            }
            canvas.scaleFactor = 0.5f;
            yield return Settle(); AssertLayout(1.5f);
            canvas.scaleFactor = originalScale;
            // Same language and content before/after resizing make the restoration comparison meaningful.
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            yield return Settle();
            var baseline = Bounds((RectTransform)synergy.transform, hud);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); yield return Settle();
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f)); yield return Settle();
            Rect restored = Bounds((RectTransform)synergy.transform, hud);
            Assert.AreEqual(baseline.x, restored.x, 0.01f); Assert.AreEqual(baseline.y, restored.y, 0.01f);
            Assert.AreEqual(baseline.width, restored.width, 0.01f); Assert.AreEqual(baseline.height, restored.height, 0.01f);
        }

        [UnityTest]
        public IEnumerator ActualBiasGateRatioAndInactiveRunDetermineVisibilityWithoutChangingStats()
        {
            run.Stats.formPlaytimeSeconds.Clear(); run.Stats.formPlaytimeSeconds[LongForm] = 10f;
            bias.Refresh(); Assert.False(biasBody.activeSelf);
            run.Stats.formPlaytimeSeconds[LongForm] = 90f; run.Stats.formPlaytimeSeconds["other"] = 10f;
            GameEvents.RaiseFormSwapped(null, null);
            Assert.True(biasBody.activeSelf);
            Assert.AreEqual(0.9f, run.Stats.GetFormPlaytimeRatio(LongForm), 0.001f);
            run.Stats.formPlaytimeSeconds["other"] = 90f;
            bias.Refresh(); Assert.False(biasBody.activeSelf);
            Set(run, "isRunActive", false); bias.Refresh(); Assert.False(biasBody.activeSelf);
            Set(run, "isRunActive", true);
            run.Stats.formPlaytimeSeconds[LongForm] = 90f; run.Stats.formPlaytimeSeconds["other"] = 10f;
            bias.Refresh(); Assert.True(biasBody.activeSelf);
            run.AbandonRun();
            Assert.False(run.IsRunActive);
            Assert.AreEqual(RunEndReason.Abandoned, run.LastRunEndReason);
            Assert.False(biasBody.activeSelf, "Actual AbandonRun must hide immediately without Refresh or room events.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisabledBiasReevaluatesCurrentRatioAndLanguageOnEnable()
        {
            bias.gameObject.SetActive(false);
            Loc.SetLanguage(LocalizationLanguage.English);
            run.Stats.formPlaytimeSeconds[LongForm] = 80f; run.Stats.formPlaytimeSeconds["other"] = 20f;
            bias.gameObject.SetActive(true);
            Assert.True(biasBody.activeSelf);
            Assert.AreEqual(Loc.GetFormat(StringKey.FormBias_WarningFormat, LongForm, 0.8f), warning.text);
            bias.gameObject.SetActive(false);
            run.Stats.formPlaytimeSeconds[LongForm] = 50f; run.Stats.formPlaytimeSeconds["other"] = 50f;
            bias.gameObject.SetActive(true);
            Assert.False(biasBody.activeSelf);
            run.Stats.formPlaytimeSeconds[LongForm] = 90f; run.Stats.formPlaytimeSeconds["other"] = 10f;
            GameEvents.RaiseFormSwapped(null, null); Assert.True(biasBody.activeSelf);
            Loc.SetLanguage(LocalizationLanguage.Japanese);
            Assert.AreEqual(Loc.GetFormat(StringKey.FormBias_WarningFormat, LongForm, 0.9f), warning.text);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SynergyReenableRefreshesActualOwnedCountsAndHidesUnusedClones()
        {
            AssertChip("[불꽃] 2 ✦");
            synergy.gameObject.SetActive(false);
            ownedSkills.Clear(); AddSkill("guard");
            synergy.gameObject.SetActive(true);
            AssertChip("[수호] 1"); Assert.AreEqual(1, ActiveChips().Count);
            AddSkill("guard");
            GameEvents.RaiseSkillDrafted(ownedSkills[1], DraftTriggerReason.LevelUp);
            AssertChip("[수호] 2 ✦");
            ownedSkills.Clear(); synergy.Refresh(); Assert.False(synergyBody.activeSelf);
            AddSkill("fire"); synergy.Refresh(); Assert.True(synergyBody.activeSelf);
            AssertChip("[불꽃] 1"); Assert.AreEqual(1, ActiveChips().Count);
            yield return Settle();
        }

        [UnityTest]
        public IEnumerator LongUnknownAxisAndSixAxesUseFullChipHeightWithoutChangingOwnedValues()
        {
            ownedSkills.Clear();
            foreach (string tag in new[] { "fire", "abyss", "guard", "blood_pact", "frost", "soul" }) AddSkill(tag);
            string longTag = "길고 새로운 시너지 축 Long unknown synergy axis 長い新規シナジー軸";
            AddSkill(longTag); AddSkill(longTag);
            synergy.Refresh();
            var iconSizes = new Dictionary<RectTransform, Vector2>();
            foreach (var chip in ActiveChips())
            {
                var icon = chip.transform.Find("ArtAxisIcon") as RectTransform;
                if (icon != null && icon.gameObject.activeSelf && icon.GetComponent<Image>().enabled)
                    iconSizes.Add(icon, icon.rect.size);
            }
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            yield return Settle();
            Assert.AreEqual(7, ActiveChips().Count);
            AssertChip("[" + longTag + "] 2 ✦");
            AssertLayout(1.5f);
            Assert.AreEqual(8, session.Owned.Count);
            foreach (var chip in ActiveChips())
            {
                var icon = chip.transform.Find("ArtAxisIcon");
                if (icon != null && icon.gameObject.activeSelf && icon.GetComponent<Image>().enabled)
                {
                    var rect = (RectTransform)icon;
                    Assert.True(iconSizes.TryGetValue(rect, out Vector2 baseline));
                    Assert.AreEqual(baseline.x, rect.rect.width, 0.01f);
                    Assert.AreEqual(baseline.y, rect.rect.height, 0.01f);
                    Assert.LessOrEqual(rect.rect.width, 26.1f);
                    Assert.LessOrEqual(rect.rect.height, 26.1f);
                }
            }
        }

        private void AssertLayout(float scale)
        {
            AssertText(warning, scale);
            Inside(warning.rectTransform, (RectTransform)bias.transform);
            Rect form = Bounds((RectTransform)hud.Find("FormSlot"), hud);
            Rect biasBounds = Bounds((RectTransform)bias.transform, hud);
            Rect synergyBounds = Bounds((RectTransform)synergy.transform, hud);
            Assert.LessOrEqual(biasBounds.yMax + 15f, form.yMin);
            Assert.LessOrEqual(synergyBounds.yMax + 7f, biasBounds.yMin);
            Inside((RectTransform)bias.transform, (RectTransform)hud);
            Inside((RectTransform)synergy.transform, (RectTransform)hud);
            var chips = ActiveChips();
            foreach (var chip in chips)
            {
                AssertText(chip.GetComponentInChildren<Text>(), scale);
                Inside((RectTransform)chip.transform, (RectTransform)synergy.transform);
            }
            for (int i = 0; i < chips.Count; i++)
                for (int j = i + 1; j < chips.Count; j++)
                    Assert.False(Bounds((RectTransform)chips[i].transform, hud).Overlaps(Bounds((RectTransform)chips[j].transform, hud)));
        }
        private static void AssertText(Text text, float scale)
        {
            Assert.AreEqual(Mathf.RoundToInt(24f * scale), text.fontSize);
            Assert.False(text.resizeTextForBestFit);
            Assert.GreaterOrEqual(text.rectTransform.rect.height + 1f, text.preferredHeight);
        }
        private void AssertChip(string expected) => Assert.True(ActiveChips().Exists(chip => chip.GetComponentInChildren<Text>().text == expected), expected);
        private List<GameObject> ActiveChips()
        {
            var result = new List<GameObject>();
            foreach (Transform child in synergyBody.transform)
                if (child.name.StartsWith("SynergyChip") && child.gameObject.activeSelf) result.Add(child.gameObject);
            return result;
        }
        private void AddSkill(string tag)
        {
            var skill = Own(ScriptableObject.CreateInstance<SkillData>());
            skill.skillId = "status_test_" + ownedObjects.Count; skill.displayName = "Status test"; skill.synergyTag = tag;
            ownedSkills.Add(skill);
        }
        private T Own<T>(T value) where T : Object { ownedObjects.Add(value); return value; }
        private static IEnumerator Settle()
        {
            yield return null; yield return null; Canvas.ForceUpdateCanvases();
            yield return null; Canvas.ForceUpdateCanvases();
        }
        private static Transform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; return go.transform;
        }
        private static Image ImageNode(Transform parent, string name) => Node(parent, name).gameObject.AddComponent<Image>();
        private static Text TextNode(Transform parent, string name)
        {
            var text = Node(parent, name).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text = "HUD"; return text;
        }
        private static Rect Bounds(RectTransform rect, Transform relative)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var corner in corners)
            { Vector2 point = relative.InverseTransformPoint(corner); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static void Inside(RectTransform child, RectTransform parent)
        {
            Rect actual = Bounds(child, parent);
            Assert.GreaterOrEqual(actual.xMin, parent.rect.xMin - 1f); Assert.LessOrEqual(actual.xMax, parent.rect.xMax + 1f);
            Assert.GreaterOrEqual(actual.yMin, parent.rect.yMin - 1f); Assert.LessOrEqual(actual.yMax, parent.rect.yMax + 1f);
        }
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static readonly FieldInfo CatalogField = typeof(FormCatalog).GetField("all", BindingFlags.NonPublic | BindingFlags.Static);
    }
}
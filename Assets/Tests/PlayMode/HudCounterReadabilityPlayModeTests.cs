using System.Collections;
using System.Reflection;
using Abyss.Runtime.ArtIntegration;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Player;
using Abyss.Runtime.Run;
using Abyss.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Abyss.Tests.PlayMode
{
    public sealed class HudCounterReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private GameObject canvasObject;
        private GameObject playerObject;
        private Transform hud;
        private Text hpText;
        private Text goldText;
        private Image fill;
        private HealthBarPresenter health;
        private GoldCounterPresenter gold;
        private PlayerCharacter player;
        private LocalizationLanguage language;
        private Random.State random;
        private float timeScale;
        private int originalGold;
        private bool ownsRun;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire();
            language = Loc.CurrentLanguage;
            random = Random.state;
            timeScale = Time.timeScale;
            ownsRun = !RunManager.HasInstance;
            originalGold = RunManager.Instance.GoldShards;
            RunManager.Instance.GainGoldShards(-originalGold);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            canvasObject = new GameObject("HudCounterTestCanvas", typeof(RectTransform), typeof(Canvas));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.scaleFactor = Mathf.Max(0.01f, Screen.width / 1920f);
            hud = Node(canvasObject.transform, "HUD");
            var hudRect = (RectTransform)hud;
            hudRect.anchorMin = hudRect.anchorMax = new Vector2(0.5f, 0.5f);
            hudRect.sizeDelta = new Vector2(1920f, 1080f);
            var hp = Node(hud, "HealthBar");
            ImageNode(hp, "Background");
            fill = ImageNode(hp, "Fill");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 1;
            fill.fillAmount = 0.75f;
            hpText = TextNode(hp, "HPText");
            var form = Node(hud, "FormSlot");
            ImageNode(form, "CurrentFormIcon");
            ImageNode(form, "OtherFormIcon");
            ImageNode(form, "CooldownFill");
            for (int i = 0; i < 2; i++)
            {
                var slot = Node(hud, "SkillSlot" + i);
                ImageNode(slot, "Icon");
                ImageNode(slot, "CooldownFill");
                TextNode(slot, "Label");
            }
            TextNode(Node(Node(hud, "FormBiasWarning"), "Body"), "Label");
            var synergyBody = Node(Node(hud, "SynergyCounter"), "Body");
            synergyBody.gameObject.AddComponent<GridLayoutGroup>();
            TextNode(Node(synergyBody, "ChipTemplate"), "Label");
            TextNode(Node(Node(hud, "BurnStack"), "Body"), "Label");
            var goldRoot = Node(hud, "GoldCounter");
            ImageNode(goldRoot, "Background");
            goldText = TextNode(goldRoot, "Label");
            hp.gameObject.SetActive(false);
            health = hp.gameObject.AddComponent<HealthBarPresenter>();
            Set(health, "fillImage", fill);
            Set(health, "hpText", hpText);
            hp.gameObject.SetActive(true);
            goldRoot.gameObject.SetActive(false);
            gold = goldRoot.gameObject.AddComponent<GoldCounterPresenter>();
            Set(gold, "label", goldText);
            goldRoot.gameObject.SetActive(true);
            HudArtSkin.Apply(hud);
            yield return Settle();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (canvasObject != null) Object.Destroy(canvasObject);
                if (playerObject != null) Object.Destroy(playerObject);
                if (RunManager.HasInstance)
                {
                    RunManager.Instance.GainGoldShards(-RunManager.Instance.GoldShards);
                    RunManager.Instance.GainGoldShards(originalGold);
                    if (ownsRun) Object.Destroy(RunManager.Instance.gameObject);
                }
                Loc.SetLanguage(language);
                Time.timeScale = timeScale;
                Random.state = random;
                yield return null;
            }
            finally { guard.Release(); }
        }

        [UnityTest]
        public IEnumerator IntMaxCountersFitAtEveryScaleAndLanguageAndRestoreLayout()
        {
            var boss = (RectTransform)Node(hud, "ReservedBossBar");
            boss.anchorMin = boss.anchorMax = new Vector2(0.5f, 1f);
            boss.pivot = new Vector2(0.5f, 1f);
            boss.anchoredPosition = new Vector2(0f, -24f);
            boss.sizeDelta = new Vector2(900f, 64f);
            Vector2 smallHpPosition = ((RectTransform)hpText.transform.parent).anchoredPosition;
            Vector2 smallGoldPosition = ((RectTransform)goldText.transform.parent).anchoredPosition;
            hpText.text = int.MaxValue + "/" + int.MaxValue;
            RunManager.Instance.GainGoldShards(int.MaxValue);
            yield return Settle();
            var hpRoot = (RectTransform)hpText.transform.parent;
            var goldRoot = (RectTransform)goldText.transform.parent;
            Vector2 hpBase = hpRoot.sizeDelta;
            Vector2 goldBase = goldRoot.sizeDelta;
            Vector2 formBase = ((RectTransform)hud.Find("FormSlot")).anchoredPosition;
            Vector2 biasBase = ((RectTransform)hud.Find("FormBiasWarning")).anchoredPosition;
            Vector2 synergyBase = ((RectTransform)hud.Find("SynergyCounter")).anchoredPosition;
            var icon = hud.Find("GoldCounter/ArtCurrencyIcon") as RectTransform;
            Vector2 iconBase = icon != null ? icon.rect.size : Vector2.zero;
            foreach (var lang in new[] { LocalizationLanguage.Korean, LocalizationLanguage.English, LocalizationLanguage.Japanese })
            {
                Loc.SetLanguage(lang);
                foreach (float scale in new[] { 1f, 1.25f, 1.5f })
                {
                    Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale));
                    yield return Settle();
                    AssertCounter(hpText, scale);
                    AssertCounter(goldText, scale);
                    Assert.AreEqual("골드 " + int.MaxValue, goldText.text, "This HUD intentionally retains its original Korean format.");
                    Assert.AreEqual(Image.Type.Filled, fill.type);
                    Assert.AreEqual(Image.FillMethod.Horizontal, fill.fillMethod);
                    Assert.AreEqual(1, fill.fillOrigin);
                    Assert.AreEqual(0.75f, fill.fillAmount);
                    Inside(fill.rectTransform, hpRoot);
                    AssertFrameAspect(hpRoot);
                    AssertFrameAspect(goldRoot);
                    var background = hpRoot.Find("Background") as RectTransform;
                    Assert.AreEqual(Bounds(background, hpRoot), Bounds(fill.rectTransform, hpRoot), "HP fill and background must share the frame opening.");
                    Inside(hpRoot, (RectTransform)hud);
                    Inside(goldRoot, (RectTransform)hud);
                    Assert.False(Bounds(hpRoot, hud).Overlaps(Bounds(boss, hud)), "HP must avoid the reserved boss band.");
                    Assert.False(Bounds(goldRoot, hud).Overlaps(Bounds(boss, hud)), "Gold must avoid the reserved boss band.");
                    var formBounds = Bounds((RectTransform)hud.Find("FormSlot"), hud);
                    Assert.LessOrEqual(formBounds.yMax + 8f, Bounds(hpRoot, hud).yMin, "HP must leave a gap above form icons.");
                    if (icon != null)
                    {
                        Inside(icon, goldRoot);
                        Assert.AreEqual(iconBase.x, icon.rect.width, 0.1f);
                        Assert.AreEqual(iconBase.y, icon.rect.height, 0.1f);
                        Assert.LessOrEqual(Bounds(icon, goldRoot).xMax, Bounds(goldText.rectTransform, goldRoot).xMin + 1f);
                    }
                }
            }
            canvasObject.GetComponent<Canvas>().scaleFactor = 0.5f;
            yield return Settle();
            AssertCounter(hpText, 1.5f);
            AssertCounter(goldText, 1.5f);
            canvasObject.GetComponent<Canvas>().scaleFactor = Mathf.Max(0.01f, Screen.width / 1920f);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            yield return Settle();
            Assert.AreEqual(hpBase.x, hpRoot.sizeDelta.x, 0.1f);
            Assert.AreEqual(hpBase.y, hpRoot.sizeDelta.y, 0.1f);
            Assert.AreEqual(goldBase.x, goldRoot.sizeDelta.x, 0.1f);
            Assert.AreEqual(goldBase.y, goldRoot.sizeDelta.y, 0.1f);
            Assert.AreEqual(formBase, ((RectTransform)hud.Find("FormSlot")).anchoredPosition);
            Assert.AreEqual(biasBase, ((RectTransform)hud.Find("FormBiasWarning")).anchoredPosition);
            Assert.AreEqual(synergyBase, ((RectTransform)hud.Find("SynergyCounter")).anchoredPosition);
            hpText.text = "100/100";
            RunManager.Instance.SpendGoldShards(int.MaxValue);
            yield return Settle();
            Assert.AreEqual(smallHpPosition, hpRoot.anchoredPosition);
            Assert.AreEqual(smallGoldPosition, goldRoot.anchoredPosition);
        }

        [UnityTest]
        public IEnumerator BoundPlayerDamageHealAndReenableRefreshActualHealth()
        {
            playerObject = new GameObject("HudHealthTestPlayer");
            playerObject.SetActive(false);
            player = playerObject.AddComponent<PlayerCharacter>();
            Set(player, "context", PlayerContext.Lobby);
            playerObject.SetActive(true);
            player.enabled = false; // Isolate health APIs from unrelated movement and scene input.
            Set(player, "maxHp", 100);
            Set(player, "currentHp", 100);
            health.Bind(player);
            Assert.AreEqual("100/100", hpText.text);
            player.TakeDamage(25);
            Assert.AreEqual("75/100", hpText.text);
            Assert.AreEqual(0.75f, fill.fillAmount);
            player.Heal(10);
            Assert.AreEqual("85/100", hpText.text);
            health.enabled = false;
            player.Heal(10);
            Assert.AreEqual("85/100", hpText.text);
            health.enabled = true;
            Assert.AreEqual("95/100", hpText.text);
            player.TakeDamage(5);
            Assert.AreEqual("90/100", hpText.text);
            health.enabled = false;
            health.Bind(player);
            player.Heal(5);
            Assert.AreEqual("90/100", hpText.text, "Binding a disabled presenter must not subscribe.");
            health.enabled = true;
            Assert.AreEqual("95/100", hpText.text);
            health.Bind(null);
            player.Heal(5);
            Assert.AreEqual("95/100", hpText.text);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualGoldGainSpendAndFlashUseUnscaledTimeAndResumeSubscriptions()
        {
            Time.timeScale = 0f;
            RunManager.Instance.GainGoldShards(123);
            Assert.AreEqual("골드 123", goldText.text);
            Color gain = goldText.color;
            Assert.True(RunManager.Instance.SpendGoldShards(23));
            Assert.AreEqual("골드 100", goldText.text);
            Color spend = goldText.color;
            Assert.AreNotEqual(gain, spend);
            Assert.False(RunManager.Instance.SpendGoldShards(101));
            Assert.AreEqual("골드 100", goldText.text);
            yield return new WaitForSecondsRealtime(0.45f);
            Assert.AreNotEqual(spend, goldText.color);
            Assert.AreEqual(0f, Time.timeScale);
            gold.enabled = false;
            RunManager.Instance.GainGoldShards(1);
            Assert.AreEqual("골드 100", goldText.text);
            gold.enabled = true;
            Assert.AreEqual("골드 101", goldText.text);
            RunManager.Instance.GainGoldShards(1);
            Assert.AreEqual("골드 102", goldText.text);
        }

        private static IEnumerator Settle()
        {
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        private static void AssertCounter(Text text, float scale)
        {
            Assert.AreEqual(Mathf.RoundToInt(28f * scale), text.fontSize);
            Assert.False(text.resizeTextForBestFit);
            Assert.GreaterOrEqual(text.rectTransform.rect.width + 1f, text.preferredWidth);
            Assert.GreaterOrEqual(text.rectTransform.rect.height + 1f, text.preferredHeight);
            Inside(text.rectTransform, (RectTransform)text.transform.parent);
        }

        private static void AssertFrameAspect(RectTransform root)
        {
            var frame = root.Find("ArtImageFrame");
            if (frame == null) return; // The existing slate fallback has no dedicated fitted-art contract.
            var image = frame.GetComponent<Image>();
            Assert.NotNull(image.sprite);
            Rect actual = ((RectTransform)frame).rect;
            string key = root.name == "HealthBar" ? UiArtKeys.HUD_PLAYER_HEALTH_FRAME : UiArtKeys.HUD_CURRENCY_FRAME;
            Assert.True(UiArtLibrary.TryGetLayout(key, out var layout));
            Assert.AreEqual(layout.Aspect, actual.width / actual.height, 0.02f);
            Inside((RectTransform)frame, root);
        }

        private static void Inside(RectTransform child, RectTransform parent)
        {
            Rect actual = Bounds(child, parent);
            Assert.GreaterOrEqual(actual.xMin, parent.rect.xMin - 1f);
            Assert.LessOrEqual(actual.xMax, parent.rect.xMax + 1f);
            Assert.GreaterOrEqual(actual.yMin, parent.rect.yMin - 1f);
            Assert.LessOrEqual(actual.yMax, parent.rect.yMax + 1f);
        }

        private static Rect Bounds(RectTransform rect, Transform relative)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            foreach (Vector3 corner in corners)
            {
                Vector2 local = relative.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Transform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            return go.transform;
        }

        private static Image ImageNode(Transform parent, string name) => Node(parent, name).gameObject.AddComponent<Image>();

        private static Text TextNode(Transform parent, string name)
        {
            var text = Node(parent, name).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "HUD";
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}

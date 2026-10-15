using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Abyss.Runtime.Combat;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Player;
using Abyss.Runtime.Skill;
using Abyss.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Abyss.Tests.PlayMode
{
    public sealed class HudSkillReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private readonly List<Object> owned = new();
        private Transform hud;
        private Canvas canvas;
        private BurnStackPresenter burn;
        private GameObject burnBody;
        private Text burnLabel;
        private readonly SkillSlotPresenter[] slots = new SkillSlotPresenter[2];
        private readonly Text[] labels = new Text[2];
        private readonly Image[] fills = new Image[2];
        private readonly SkillData[] skills = new SkillData[2];
        private LocalizationLanguage language;
        private Random.State random;
        private float timeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire();
            language = Loc.CurrentLanguage;
            random = Random.state;
            timeScale = Time.timeScale;
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            var root = Own(new GameObject("HudSkillTestCanvas", typeof(RectTransform), typeof(Canvas)));
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.scaleFactor = Mathf.Max(0.01f, Screen.width / 1920f);
            hud = Node(root.transform, "HUD");
            var rect = (RectTransform)hud;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1920f, 1080f);
            var hp = Node(hud, "HealthBar");
            ImageNode(hp, "Background");
            ImageNode(hp, "Fill");
            TextNode(hp, "HPText").text = "100/100";
            var form = Node(hud, "FormSlot");
            ImageNode(form, "CurrentFormIcon");
            ImageNode(form, "OtherFormIcon");
            ImageNode(form, "CooldownFill");
            for (int i = 0; i < 2; i++)
            {
                var slot = Node(hud, "SkillSlot" + i);
                var icon = ImageNode(slot, "Icon");
                fills[i] = ImageNode(slot, "CooldownFill");
                fills[i].type = Image.Type.Filled;
                fills[i].fillMethod = Image.FillMethod.Vertical;
                fills[i].fillOrigin = 1;
                fills[i].fillClockwise = false;
                labels[i] = TextNode(slot, "Label");
                slots[i] = slot.gameObject.AddComponent<SkillSlotPresenter>();
                Set(slots[i], "iconImage", icon);
                Set(slots[i], "cooldownFill", fills[i]);
                Set(slots[i], "labelText", labels[i]);
                skills[i] = Own(ScriptableObject.CreateInstance<SkillData>());
                skills[i].skillId = "hud_readability_" + i;
                skills[i].displayName = "끝까지 보존하는 매우 긴 스킬 이름 A very long unchanged skill name 最後まで保持する長いスキル名 " + i;
                slots[i].SetSkill(skills[i]);
            }
            TextNode(Node(Node(hud, "FormBiasWarning"), "Body"), "Label");
            var synergy = Node(Node(hud, "SynergyCounter"), "Body");
            synergy.gameObject.AddComponent<GridLayoutGroup>();
            TextNode(Node(synergy, "ChipTemplate"), "Label");
            var burnRoot = Node(hud, "BurnStack");
            burnBody = Node(burnRoot, "Body").gameObject;
            burnLabel = TextNode(burnBody.transform, "Label");
            burnRoot.gameObject.SetActive(false);
            burn = burnRoot.gameObject.AddComponent<BurnStackPresenter>();
            Set(burn, "root", burnBody);
            Set(burn, "label", burnLabel);
            burnRoot.gameObject.SetActive(true);
            var gold = Node(hud, "GoldCounter");
            ImageNode(gold, "Background");
            TextNode(gold, "Label").text = "골드 0";
            HudArtSkin.Apply(hud);
            yield return Settle();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                foreach (var item in owned) if (item != null) Object.Destroy(item);
                owned.Clear();
                Loc.SetLanguage(language);
                Time.timeScale = timeScale;
                Random.state = random;
                yield return null;
            }
            finally { guard.Release(); }
        }

        [UnityTest]
        public IEnumerator SkillAndBurnTextScaleWithoutChangingSourceOrGaugeGeometry()
        {
            var enemy = Enemy();
            enemy.ApplyBurn(new BurnPayload(10, 10f, 1f));
            var baselinePositions = new Vector2[2];
            var baselineLabels = new Vector2[2];
            var baselineIcons = new Vector2[2];
            var baselineFills = new Rect[2];
            float canvasBaseline = canvas.scaleFactor;
            for (int i = 0; i < 2; i++)
            {
                baselinePositions[i] = ((RectTransform)slots[i].transform).anchoredPosition;
                baselineLabels[i] = labels[i].rectTransform.sizeDelta;
                baselineIcons[i] = ((RectTransform)slots[i].transform.Find("Icon")).rect.size;
                baselineFills[i] = Bounds(fills[i].rectTransform, slots[i].transform);
            }
            foreach (var lang in new[] { LocalizationLanguage.Korean, LocalizationLanguage.English, LocalizationLanguage.Japanese })
            {
                Loc.SetLanguage(lang);
                foreach (float scale in new[] { 1f, 1.25f, 1.5f })
                {
                    Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale));
                    yield return Settle();
                    AssertLayout(scale);
                    for (int i = 0; i < 2; i++)
                    {
                        Assert.AreEqual(skills[i].displayName, labels[i].text);
                        Assert.AreSame(skills[i], slots[i].CurrentSkill);
                        Assert.AreEqual(baselineIcons[i], ((RectTransform)slots[i].transform.Find("Icon")).rect.size);
                        AssertGaugeBounds(baselineFills[i], Bounds(fills[i].rectTransform, slots[i].transform));
                        Assert.AreEqual(Image.Type.Filled, fills[i].type);
                        Assert.AreEqual(Image.FillMethod.Vertical, fills[i].fillMethod);
                        Assert.AreEqual(1, fills[i].fillOrigin);
                        Assert.False(fills[i].fillClockwise);
                    }
                }
            }
            canvas.scaleFactor = 0.5f;
            yield return Settle();
            AssertLayout(1.5f);
            canvas.scaleFactor = canvasBaseline;
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            yield return Settle();
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(baselinePositions[i], ((RectTransform)slots[i].transform).anchoredPosition);
                Assert.AreEqual(baselineLabels[i], labels[i].rectTransform.sizeDelta);
            }
        }

        [UnityTest]
        public IEnumerator PresenterPollsActualPlayerSlotCooldownAndSkinPreservesFill()
        {
            var playerObject = Own(new GameObject("HudSkillCooldownPlayer"));
            playerObject.SetActive(false);
            var player = playerObject.AddComponent<PlayerCharacter>();
            Set(player, "context", PlayerContext.Lobby);
            playerObject.SetActive(true);
            player.enabled = false;
            var abilityData = Own(ScriptableObject.CreateInstance<GenericAbilityData>());
            abilityData.cooldownDuration = 5f;
            var ability = playerObject.AddComponent<GenericAbility>();
            ability.Configure(abilityData, player, "hud_cooldown_probe");
            var abilities = Field<GenericAbility[]>(player, "slotAbilities");
            var names = Field<string[]>(player, "slotNames");
            abilities[0] = ability;
            names[0] = "hud_cooldown_probe";
            slots[0].BindCooldownSource(player, 0);
            ability.Cooldown.StartCooldown();
            yield return null;
            Assert.Greater(player.GetSlotCooldownFill(0), 0f);
            Assert.AreEqual(player.GetSlotCooldownFill(0), fills[0].fillAmount, 0.05f);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            yield return Settle();
            Assert.AreEqual(player.GetSlotCooldownFill(0), fills[0].fillAmount, 0.05f);
            ability.Cooldown.Update(2f);
            yield return null;
            Assert.AreEqual(player.GetSlotCooldownFill(0), fills[0].fillAmount, 0.05f);
            Assert.Less(fills[0].fillAmount, 0.9f);
            ability.Cooldown.CompleteCooldown();
            yield return null;
            Assert.AreEqual(0f, fills[0].fillAmount);
            Assert.AreEqual(Image.FillMethod.Vertical, fills[0].fillMethod);
            Assert.AreEqual(1, fills[0].fillOrigin);
            slots[0].SetSkill(null);
            yield return null;
            Assert.AreEqual(0f, fills[0].fillAmount);
            Assert.AreEqual("—", labels[0].text);
            Assert.False(slots[0].transform.Find("Icon").GetComponent<Image>().enabled);
        }

        [UnityTest]
        public IEnumerator ActualBurnTracksNewestTargetAndIgnoresOtherTargetsClear()
        {
            var first = Enemy();
            var second = Enemy();
            first.ApplyBurn(new BurnPayload(3, 5f, 1f));
            Assert.True(burnBody.activeSelf);
            Assert.AreEqual("연소 3스택", burnLabel.text);
            second.ApplyBurn(new BurnPayload(20, 5f, 1f));
            Assert.AreEqual(10, second.BurnStacks);
            Assert.AreEqual("연소 10스택", burnLabel.text);
            first.ClearBurn();
            Assert.True(burnBody.activeSelf);
            Assert.AreEqual("연소 10스택", burnLabel.text);
            second.ClearBurn();
            Assert.False(burnBody.activeSelf);
            first.ApplyBurn(new BurnPayload(2, 5f, 1f));
            Assert.True(burnBody.activeSelf);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyedTargetAndExpiredBurnCannotLeaveStaleVisibleStacks()
        {
            var target = Enemy();
            target.ApplyBurn(new BurnPayload(2, 5f, 1f));
            Object.Destroy(target.gameObject);
            yield return null;
            yield return null;
            Assert.False(burnBody.activeSelf, "Despawn without ClearBurn must hide the old count.");
            var expiring = Enemy();
            expiring.enabled = true;
            Time.timeScale = 1f;
            expiring.ApplyBurn(new BurnPayload(1, 0.08f, 1f));
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(0, expiring.BurnStacks);
            Assert.False(burnBody.activeSelf);
        }

        [UnityTest]
        public IEnumerator DisabledHudResynchronizesTrackedActualStacksAndClearOnEnable()
        {
            var target = Enemy();
            target.ApplyBurn(new BurnPayload(2, 5f, 1f));
            burn.gameObject.SetActive(false);
            target.ApplyBurn(new BurnPayload(4, 5f, 1f));
            burn.gameObject.SetActive(true);
            Assert.AreEqual("연소 6스택", burnLabel.text);
            Assert.True(burnBody.activeSelf);
            burn.gameObject.SetActive(false);
            target.ClearBurn();
            burn.gameObject.SetActive(true);
            Assert.False(burnBody.activeSelf);
            target.ApplyBurn(new BurnPayload(1, 5f, 1f));
            Assert.True(burnBody.activeSelf);
            yield return null;
        }

        private void AssertLayout(float scale)
        {
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(Mathf.RoundToInt(24f * scale), labels[i].fontSize);
                Assert.False(labels[i].resizeTextForBestFit);
                Assert.AreEqual(VerticalWrapMode.Truncate, labels[i].verticalOverflow);
                Inside(labels[i].rectTransform, (RectTransform)hud);
                Assert.GreaterOrEqual(Bounds(labels[i].rectTransform, hud).yMin, ((RectTransform)hud).rect.yMin + 3f);
            }
            Rect left = Bounds(labels[0].rectTransform, hud);
            Rect right = Bounds(labels[1].rectTransform, hud);
            Assert.LessOrEqual(left.xMax + (scale > 1f ? 7f : -1f), right.xMin);
            Rect burnBounds = Bounds((RectTransform)burn.transform, hud);
            Assert.LessOrEqual(right.xMax + (scale > 1f ? 15f : 7f), burnBounds.xMin);
            Assert.AreEqual(Mathf.RoundToInt(24f * scale), burnLabel.fontSize);
            Assert.False(burnLabel.resizeTextForBestFit);
            Assert.GreaterOrEqual(burnLabel.rectTransform.rect.width + 1f, burnLabel.preferredWidth);
            Assert.GreaterOrEqual(burnLabel.rectTransform.rect.height + 1f, burnLabel.preferredHeight);
            Inside((RectTransform)burn.transform, (RectTransform)hud);
        }

        private EnemyBase Enemy()
        {
            var go = Own(new GameObject("HudBurnTestEnemy"));
            go.SetActive(false);
            var enemy = go.AddComponent<EnemyBase>();
            Set(enemy, "currentHp", 10000);
            enemy.enabled = false;
            go.SetActive(true);
            enemy.Body.gravityScale = 0f;
            return enemy;
        }

        private static void AssertGaugeBounds(Rect expected, Rect actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.01f);
            Assert.AreEqual(expected.y, actual.y, 0.01f);
            Assert.AreEqual(expected.width, actual.width, 0.01f);
            Assert.AreEqual(expected.height, actual.height, 0.01f);
        }

        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        private static IEnumerator Settle()
        {
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
        }
        private static Transform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
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
        private static Rect Bounds(RectTransform rect, Transform relative)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var corner in corners)
            {
                Vector2 point = relative.InverseTransformPoint(corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static void Inside(RectTransform child, RectTransform parent)
        {
            Rect actual = Bounds(child, parent);
            Assert.GreaterOrEqual(actual.xMin, parent.rect.xMin - 1f);
            Assert.LessOrEqual(actual.xMax, parent.rect.xMax + 1f);
            Assert.GreaterOrEqual(actual.yMin, parent.rect.yMin - 1f);
            Assert.LessOrEqual(actual.yMax, parent.rect.yMax + 1f);
        }
        private static T Field<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}

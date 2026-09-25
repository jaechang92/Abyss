using System;
using System.Collections.Generic;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 보스 등장 카드 — 이름 · 별칭 · 등장 대사를 띄우는 동안 게임을 멈춘다(17-stage-flow-boss-presentation §2-1).
    ///
    /// 정지는 <see cref="RunModalPanel{T}"/> 규약(DraftOpen 차용)을 그대로 쓴다. 정지 위에서 흐르므로
    /// 대기는 전부 실시간(<c>Time.unscaledTime</c>)으로 잰다 — deltaTime으로 재면 영원히 끝나지 않는다.
    ///
    /// 넘기기: Space·Enter·Z·패드 A. 줄마다 첫 0.25초는 입력을 받지 않는다 — 보스를 치던 Z가 그대로
    /// 첫 대사를 넘겨 버리지 않게. 입력이 없어도 한 줄은 <see cref="LINE_SECONDS"/> 뒤 넘어간다.
    /// </summary>
    public sealed class BossIntroPanel : RunModalPanel<BossIntroPanel>
    {
        private const float TITLE_SECONDS = 1.1f;
        private const float LINE_SECONDS = 4f;
        private const float INPUT_GRACE_SECONDS = 0.25f;

        private static readonly Color TitleColor = new Color(1f, 0.82f, 0.52f);
        private static readonly Color EpithetColor = new Color(0.70f, 0.70f, 0.80f);
        private static readonly Color LineColor = new Color(0.93f, 0.93f, 0.97f);
        private static readonly Color HintColor = new Color(0.55f, 0.55f, 0.66f);

        private Text titleLabel;
        private Text epithetLabel;
        private Text lineLabel;
        private bool isPlaying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ResetInstance();

        /// <summary>
        /// 카드를 띄우고 대사를 차례로 보인 뒤 닫고 <paramref name="onFinished"/>를 부른다.
        /// 이미 재생 중이면 겹치지 않고 곧바로 <paramref name="onFinished"/>만 부른다.
        /// </summary>
        public static void Play(string title, string epithet, IReadOnlyList<string> lines, Action onFinished)
        {
            var panel = EnsureInstance();
            if (panel.isPlaying)
            {
                onFinished?.Invoke();
                return;
            }
            _ = panel.RunAsync(title, epithet, lines, onFinished);
        }

        protected override void BuildContent(Transform body)
        {
            titleLabel = CreateLabel(body, "Title", new Vector2(0, 110), new Vector2(1600, 90), string.Empty, 64, TitleColor, TextAnchor.MiddleCenter);
            titleLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            epithetLabel = CreateLabel(body, "Epithet", new Vector2(0, 44), new Vector2(1600, 40), string.Empty, 26, EpithetColor, TextAnchor.MiddleCenter);

            var divider = CreateRect(body, "Divider", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 12), new Vector2(560, 2));
            var dividerImage = divider.AddComponent<Image>();
            dividerImage.color = new Color(TitleColor.r, TitleColor.g, TitleColor.b, 0.45f);
            dividerImage.raycastTarget = false;

            lineLabel = CreateLabel(body, "Line", new Vector2(0, -80), new Vector2(1400, 90), string.Empty, 30, LineColor, TextAnchor.MiddleCenter);
            lineLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

            CreateLocalizedLabel(body, "Hint", new Vector2(0, -400), new Vector2(600, 30), StringKey.Dialogue_NextHint, 17, HintColor, TextAnchor.MiddleCenter);
        }

        private async Awaitable RunAsync(string title, string epithet, IReadOnlyList<string> lines, Action onFinished)
        {
            isPlaying = true;
            try
            {
                titleLabel.text = title;
                epithetLabel.text = epithet;
                lineLabel.text = string.Empty;
                ShowBody();

                // 이름만 먼저 — 누구와 싸우는지가 대사보다 앞서야 한다.
                await WaitLineAsync(TITLE_SECONDS);

                if (lines != null)
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        lineLabel.text = lines[i];
                        await WaitLineAsync(LINE_SECONDS);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 씬 전환으로 파괴됐다 — 정지는 새 씬의 흐름이 정리한다.
                return;
            }
            finally
            {
                isPlaying = false;
            }

            HideBody();
            onFinished?.Invoke();
        }

        /// <summary>넘기기 입력 또는 <paramref name="seconds"/>(실시간) 중 먼저 오는 쪽까지 기다린다.</summary>
        private async Awaitable WaitLineAsync(float seconds)
        {
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < seconds)
            {
                await Awaitable.NextFrameAsync(destroyCancellationToken);
                if (Time.unscaledTime - start >= INPUT_GRACE_SECONDS && WasAdvancePressed()) return;
            }
        }

        private static bool WasAdvancePressed()
        {
            // 저장 실패 모달이 위에 떠 있으면 그 키는 모달 몫이다.
            if (SaveStatusOverlay.IsCapturingInput) return false;

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame ||
                                     keyboard.zKey.wasPressedThisFrame))
                return true;

            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        }
    }
}

using System;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 검은 화면 위에 문단을 하나씩 띄우는 자막 시퀀스. 페이드 인 → 유지 → 페이드 아웃의
    /// 호흡과 라벨 타이포그래피를 <b>한 곳에서</b> 소유한다.
    ///
    /// <see cref="EndingSequencePanel"/>이 갖고 있던 자막 페이즈를 뽑아낸 것이다.
    /// 프롤로그(4-3)가 같은 연출을 필요로 하는데, 상수만 복사하면 한쪽 호흡을 고쳤을 때
    /// 여는 자막과 닫는 자막이 조용히 어긋난다 — 같은 규약이 두 곳에 있으면 언젠가 갈라진다.
    ///
    /// MonoBehaviour가 아니라 평범한 클래스인 이유: 두 패널 모두 이미 자기 Update에서
    /// 시간과 입력을 관리한다. 컴포넌트로 만들면 GameObject 수명·활성화가 하나 더 늘 뿐
    /// 얻는 것이 없다. 시간은 호출자가 넘긴다 — 엔딩은 timeScale=0 위에서 도는 반면
    /// 프롤로그는 그렇지 않아, <b>어떤 시간을 쓸지는 호출자만 안다.</b>
    /// </summary>
    public sealed class SubtitleSequence
    {
        // 문단 하나의 호흡(초). 여는 자막과 닫는 자막이 같은 값을 쓴다.
        public const float FADE_IN = 0.9f;
        public const float HOLD = 2.8f;
        public const float FADE_OUT = 0.9f;
        public const float PARAGRAPH_DURATION = FADE_IN + HOLD + FADE_OUT;

        private static readonly string[] EmptyParagraphs = Array.Empty<string>();

        private readonly Text label;

        private string[] paragraphs = EmptyParagraphs;
        private int paragraphIndex;
        private float timer;

        /// <summary>모든 문단이 끝났는가. <see cref="Restart"/> 전이나 빈 배열이면 처음부터 true다.</summary>
        public bool IsFinished => paragraphIndex >= paragraphs.Length;

        private SubtitleSequence(Text label) => this.label = label;

        /// <summary>
        /// 자막 라벨을 만들고 시퀀스를 붙여 돌려준다.
        ///
        /// 라벨 생성까지 여기서 하는 이유: 자막의 크기·줄간격·줄바꿈 모드가 곧 연출의 일부라
        /// 호출부에 두면 두 자막의 생김새가 갈라진다. 호출자는 부모만 정한다.
        /// </summary>
        public static SubtitleSequence Create(Transform parent, string name = "Subtitle")
        {
            var text = CreateLabel(parent, name, Vector2.zero, new Vector2(1100, 260),
                string.Empty, 30, new Color(0.94f, 0.94f, 1f, 0f), TextAnchor.MiddleCenter);
            // 문단이 두 줄 이상이라 가로 오버플로 대신 줄바꿈이 필요하다(CreateLabel 기본값은 Overflow).
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.lineSpacing = 1.4f;

            return new SubtitleSequence(text);
        }

        /// <summary>
        /// StringKey 배열을 현재 언어의 문단 배열로 바꾼다. <see cref="Restart"/>에 그대로 넘긴다.
        ///
        /// 자막 텍스트의 출처가 CSV라는 규약을 여기 한 곳에 둔다 — 두 패널이 각자 조회하면
        /// 한쪽만 하드코딩으로 남는 형태가 다시 생긴다(4-3 착수 시점의 상태가 그랬다).
        /// <b>재생 시점에 조회한다.</b> 정적 초기화 시점에는 <see cref="LocalizationManager"/>가
        /// 아직 없을 수 있고, 언어를 바꾼 뒤 다시 재생하면 바뀐 언어로 나와야 한다.
        /// </summary>
        public static string[] Localize(params string[] stringKeys)
        {
            if (stringKeys == null || stringKeys.Length == 0) return EmptyParagraphs;

            var lines = new string[stringKeys.Length];
            for (int i = 0; i < stringKeys.Length; i++)
            {
                lines[i] = Loc.Get(stringKeys[i]);
            }
            return lines;
        }

        /// <summary>첫 문단부터 다시 재생한다. 알파 0에서 시작하므로 호출 즉시 화면에 뜨지는 않는다.</summary>
        public void Restart(string[] lines)
        {
            paragraphs = lines ?? EmptyParagraphs;
            paragraphIndex = 0;
            timer = 0f;

            SetAlpha(0f);
            if (label != null) label.text = paragraphs.Length > 0 ? paragraphs[0] : string.Empty;
        }

        /// <summary>
        /// 한 프레임 진행한다. <paramref name="deltaSeconds"/>는 호출자가 고른 시간
        /// (정지 위에서 도는 화면이면 <see cref="Time.unscaledDeltaTime"/>).
        /// </summary>
        /// <returns>이 프레임에 <b>마지막 문단까지 끝났으면</b> true. 이미 끝나 있었으면 false다 —
        /// 완료는 한 번만 알린다.</returns>
        public bool Tick(float deltaSeconds)
        {
            if (IsFinished) return false;

            timer += deltaSeconds;
            SetAlpha(CalcAlpha(timer));

            if (timer < PARAGRAPH_DURATION) return false;
            return Advance();
        }

        /// <summary>
        /// 지금 문단을 건너뛴다. 시퀀스 전체를 한 번에 닫지 않는 이유는 실수로 한 번 누른 것이
        /// 자막 전체를 삼키지 않게 하기 위해서다(엔딩이 세운 관습).
        /// </summary>
        /// <returns>이 호출로 마지막 문단까지 끝났으면 true.</returns>
        public bool Skip()
        {
            if (IsFinished) return false;
            return Advance();
        }

        /// <summary>자막을 화면에서 지운다(문단 진행 상태는 그대로).</summary>
        public void Clear() => SetAlpha(0f);

        private bool Advance()
        {
            paragraphIndex += 1;
            timer = 0f;

            if (IsFinished)
            {
                SetAlpha(0f);
                return true;
            }

            if (label != null) label.text = paragraphs[paragraphIndex];
            SetAlpha(0f);
            return false;
        }

        /// <summary>페이드 인 → 유지 → 페이드 아웃 구간별 알파.</summary>
        private static float CalcAlpha(float t)
        {
            if (t < FADE_IN) return Mathf.Clamp01(t / FADE_IN);
            if (t < FADE_IN + HOLD) return 1f;
            return Mathf.Clamp01(1f - (t - FADE_IN - HOLD) / FADE_OUT);
        }

        private void SetAlpha(float a)
        {
            if (label == null) return;
            var c = label.color;
            c.a = Mathf.Clamp01(a);
            label.color = c;
        }
    }
}

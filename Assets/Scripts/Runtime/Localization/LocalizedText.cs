using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.Localization
{
    /// <summary>
    /// StringKey 하나를 붙들고 있다가 언어가 바뀌면 스스로 글자를 다시 채우는 uGUI Text 부착 컴포넌트.
    ///
    /// <b>이것이 <see cref="LocalizationManager.OnLanguageChanged"/>의 첫 구독자다.</b>
    /// 그전까지 이벤트는 발행만 되고 듣는 쪽이 0명이었다 — 언어를 바꿔도 저장은 되는데
    /// <b>이미 그려진 글자는 그대로였다.</b> 화면을 다시 열기 전에는 아무 일도 안 일어난 것처럼 보인다.
    ///
    /// 부착은 만든 자리에서 한다:
    /// <code>LocalizedText.Attach(text, StringKey.Settings_Language);</code>
    /// 동적 패널은 <see cref="UI.UiFactory"/>의 CreateLocalizedLabel·CreateLocalizedButton을 쓰면
    /// 라벨 생성과 부착이 한 줄로 끝난다.
    ///
    /// ⚠️ <b>포맷 문자열(<c>{0}</c>)은 대상이 아니다.</b> 인자는 이 컴포넌트가 모르는 런타임 값이라
    /// 언어만 바꿔 다시 채우면 인자가 사라진다. 포맷 텍스트는 값이 바뀌는 자리에서
    /// <see cref="Loc.GetFormat"/>으로 직접 갱신한다.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string stringKey;

        private Text target;

        /// <summary>이 라벨이 물고 있는 StringKey.</summary>
        public string Key => stringKey;

        /// <summary>Text에 컴포넌트를 붙이고 키를 물린다. 붙는 즉시 현재 언어로 채워진다.</summary>
        public static LocalizedText Attach(Text text, string stringKey)
        {
            if (text == null) return null;

            var localized = text.gameObject.AddComponent<LocalizedText>();
            localized.SetKey(stringKey);
            return localized;
        }

        /// <summary>키를 갈아 끼우고 즉시 반영한다.</summary>
        public void SetKey(string key)
        {
            stringKey = key;
            Apply();
        }

        /// <summary>현재 언어로 글자를 다시 채운다.</summary>
        public void Apply()
        {
            if (target == null) target = GetComponent<Text>();
            if (target == null || string.IsNullOrEmpty(stringKey)) return;

            target.text = Loc.Get(stringKey);
        }

        /// <summary>
        /// 구독은 켜져 있는 동안만 유지하고, 켜질 때마다 한 번 채운다.
        ///
        /// 꺼져 있는 사이에 언어가 바뀌면 이벤트를 놓치지만 다시 켜질 때의 <see cref="Apply"/>가
        /// 그것을 메운다 — <b>구독은 놓쳐도 되고 켜질 때의 갱신은 놓치면 안 된다.</b>
        /// (설정 패널은 닫힐 때 SetActive(false)라 이 경로를 실제로 탄다.)
        /// </summary>
        private void OnEnable()
        {
            Loc.AddLanguageChangedListener(OnLanguageChanged);
            Apply();
        }

        private void OnDisable()
        {
            Loc.RemoveLanguageChangedListener(OnLanguageChanged);
        }

        private void OnLanguageChanged(LocalizationLanguage language) => Apply();
    }
}

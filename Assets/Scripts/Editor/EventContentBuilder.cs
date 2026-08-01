#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Abyss.Runtime.Stage;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 이벤트 방 콘텐츠(EventData SO) 생성. 완주 루프 계획 1-1.
    ///
    /// <see cref="StageBuilder"/>가 이벤트 룸을 만들 때 이 클래스에서 EventData를 가져온다.
    /// 이미 존재하는 에셋은 덮어쓰지 않는다 — 인스펙터에서 수치를 만졌을 수 있고,
    /// 빌더 재실행이 기획 조정을 되돌리면 안 된다.
    ///
    /// 이벤트 3종은 보상 축을 하나씩 나눠 갖는다: 드래프트 / 골드 / 회복.
    /// 셋 다 "대가 없이 지나가는 선택"을 하나씩 두어 무조건 손해 보는 방이 되지 않게 했다.
    ///
    /// 휴식 2종(<see cref="EnsureAllRests"/>)도 같은 <see cref="EventData"/>를 쓴다 —
    /// 선택 모달이 필요한 건 똑같아서 전용 SO·패널을 만들면 복제만 늘어난다. 성격 차이는 내용으로 가른다.
    /// </summary>
    public static class EventContentBuilder
    {
        public const string BrokenAltarFile = "Event_BrokenAltar";
        public const string ForgottenCacheFile = "Event_ForgottenCache";
        public const string AbyssalSpringFile = "Event_AbyssalSpring";

        public const string RestEmberFile = "Rest_Ember";
        public const string RestCampFile = "Rest_RuinedCamp";

        /// <summary>이벤트 SO 3종을 생성하고(없으면) 반환한다. 순서는 파일명 상수와 같다.</summary>
        public static void EnsureAllEvents(
            out EventData brokenAltar, out EventData forgottenCache, out EventData abyssalSpring)
        {
            EnsureDir(AbyssPaths.Events);

            brokenAltar = CreateOrLoad(BrokenAltarFile, "broken_altar", "깨진 제단",
                "무너진 제단이 희미하게 맥동한다. 무엇이든 바치면 응답할 것 같다.",
                new[]
                {
                    Choice("공물을 바친다",
                        "동전이 돌 틈으로 사라진다. 제단이 잠시 밝아지더니, 머릿속에 낯선 기술이 스며든다.",
                        Effect(EventEffectType.GoldSpend, 30),
                        Effect(EventEffectType.SkillDraft, 0)),

                    Choice("피를 바친다",
                        "손바닥을 그어 제단에 얹는다. 상처가 아리지만, 대가는 확실하게 돌아온다.",
                        Effect(EventEffectType.HpCostPercent, 15),
                        Effect(EventEffectType.SkillDraft, 0)),

                    Choice("그냥 지나친다",
                        "제단은 응답하지 않았다. 등 뒤에서 맥동이 잦아든다."),
                });

            forgottenCache = CreateOrLoad(ForgottenCacheFile, "forgotten_cache", "잊힌 보급함",
                "녹슨 보급함이 벽에 반쯤 묻혀 있다. 잠금장치가 아직 살아 있다.",
                new[]
                {
                    Choice("억지로 연다",
                        "경첩이 튀어 오르며 손등을 찢는다. 대신 안에 든 것은 전부 당신 몫이다.",
                        Effect(EventEffectType.HpCostPercent, 12),
                        Effect(EventEffectType.GoldGain, 70)),

                    Choice("조심히 연다",
                        "시간을 들여 잠금장치를 달랜다. 절반쯤은 부서졌지만 남은 것을 챙겼다.",
                        Effect(EventEffectType.GoldGain, 30)),

                    Choice("그냥 둔다",
                        "누군가 더 급한 사람이 열게 두기로 한다."),
                });

            abyssalSpring = CreateOrLoad(AbyssalSpringFile, "abyssal_spring", "심연의 샘",
                "검은 물이 소리 없이 솟는다. 들여다보면 바닥이 보이지 않는다.",
                new[]
                {
                    Choice("물을 마신다",
                        "차갑고 무겁다. 상처가 닫히는 감각이 목을 타고 내려간다.",
                        Effect(EventEffectType.HealPercent, 35)),

                    Choice("정수를 채취한다",
                        "샘이 손목을 붙잡고 놓지 않는다. 겨우 빼낸 손에 무언가 남았다.",
                        Effect(EventEffectType.HpCostPercent, 10),
                        Effect(EventEffectType.SkillDraft, 0)),

                    Choice("지나친다",
                        "바닥이 보이지 않는 물에는 손을 대지 않기로 한다."),
                });
        }

        /// <summary>
        /// 휴식 SO 2종을 생성하고(없으면) 반환한다. 완주 루프 계획 1-3.
        ///
        /// 이벤트와 같은 <see cref="EventData"/>를 쓰지만 성격이 다르다 —
        /// <b>손해 보는 선택지가 없고</b>, 회복과 강화 중 <b>하나만</b> 고르게 해 선택에 무게를 준다
        /// (StS 캠프파이어형). 보스 직전에 배치되므로 "지금 내 HP로 보스를 잡을 수 있나"를 스스로 묻게 된다.
        ///
        /// 두 방의 효과가 같은 것은 의도다 — 휴식 방은 <b>예측 가능해야</b> 계획을 세울 수 있다.
        /// 스테이지 정체성은 문구로만 가른다.
        /// </summary>
        public static void EnsureAllRests(out EventData ember, out EventData ruinedCamp)
        {
            EnsureDir(AbyssPaths.Events);

            ember = CreateOrLoad(RestEmberFile, "rest_ember", "불씨 앞에서",
                "누군가 피우다 만 불씨가 아직 살아 있다. 여기서 잠시 멈출 수 있다.",
                new[]
                {
                    Choice("쉬어간다",
                        "불에 손을 쬐고 숨을 고른다. 상처가 아물지는 않았지만, 걸을 만해졌다.",
                        Effect(EventEffectType.HealPercent, 60)),

                    Choice("무기를 보살핀다",
                        "날을 세우고 이음새를 조인다. 쉬지는 못했지만 손에 익은 감각이 하나 늘었다.",
                        Effect(EventEffectType.SkillDraft, 0)),
                });

            ruinedCamp = CreateOrLoad(RestCampFile, "rest_ruined_camp", "무너진 야영지",
                "먼저 내려온 자들의 흔적이다. 천막은 무너졌지만 불자리는 남아 있다.",
                new[]
                {
                    Choice("눈을 붙인다",
                        "얕은 잠에서 깨어난다. 아래에서 올라오는 열기가 조금은 견딜 만해졌다.",
                        Effect(EventEffectType.HealPercent, 60)),

                    Choice("남은 장비를 뒤진다",
                        "쓸 만한 것은 거의 없었지만, 그들이 남긴 방식 하나를 배웠다.",
                        Effect(EventEffectType.SkillDraft, 0)),
                });
        }

        private static EventData CreateOrLoad(
            string fileName, string eventId, string title, string description, EventChoice[] choices)
        {
            string path = $"{AbyssPaths.Events}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EventData>(path);
            if (existing != null)
            {
                Debug.Log($"[EventContentBuilder] 건너뜀 (존재): {path}");
                return existing;
            }

            var so = ScriptableObject.CreateInstance<EventData>();
            so.eventId = eventId;
            so.title = title;
            so.description = description;
            so.choices = new List<EventChoice>(choices);
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[EventContentBuilder] 생성: {path}");
            return so;
        }

        private static EventChoice Choice(string label, string resultText, params EventEffect[] effects)
        {
            return new EventChoice
            {
                label = label,
                resultText = resultText,
                effects = new List<EventEffect>(effects),
            };
        }

        private static EventEffect Effect(EventEffectType type, int amount)
        {
            return new EventEffect { type = type, amount = amount };
        }

        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }
    }
}
#endif

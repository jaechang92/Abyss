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
    /// 이벤트 6종은 보상 축을 나눠 갖는다: 드래프트 / 골드 / 회복 / 골드↔드래프트 교환 /
    /// 큰 대가·큰 보상(왕관) / 골드 대 회복(맹세의 돌).
    /// 전부 "대가 없이 지나가는 선택"을 하나씩 두어 무조건 손해 보는 방이 되지 않게 했다.
    /// <b>개수는 분기 지점 수를 따라간다</b> — 스테이지당 2곳 × 3스테이지 = 6곳이라 6종이다.
    /// 모자라면 한 런에서 같은 이벤트를 두 번 만나고, 두 번째는 선택이 아니라 이미 아는 답이 된다.
    /// 스테이지 4가 생기면 여기도 2종이 더 필요하다.
    ///
    /// 휴식 3종(<see cref="EnsureAllRests"/>)도 같은 <see cref="EventData"/>를 쓴다 —
    /// 선택 모달이 필요한 건 똑같아서 전용 SO·패널을 만들면 복제만 늘어난다. 성격 차이는 내용으로 가른다.
    /// </summary>
    public static class EventContentBuilder
    {
        public const string BrokenAltarFile = "Event_BrokenAltar";
        public const string ForgottenCacheFile = "Event_ForgottenCache";
        public const string AbyssalSpringFile = "Event_AbyssalSpring";
        public const string SealedDoorFile = "Event_SealedDoor";
        public const string HollowCrownFile = "Event_HollowCrown";
        public const string OathStoneFile = "Event_OathStone";

        public const string RestEmberFile = "Rest_Ember";
        public const string RestCampFile = "Rest_RuinedCamp";
        public const string RestThroneFile = "Rest_ThroneHall";

        /// <summary>
        /// 이벤트·휴식 SO를 점검한다. <b>없으면 만들고, 있으면 빠진 선택지만 채운다.</b>
        ///
        /// 🔴 <b>전용 메뉴가 없어서 겪은 일이 있다.</b> 이전에는 <c>EnsureAllEvents</c>를
        /// <c>StageBuilder</c>만 불렀다. 그래서 이벤트 선택지를 코드에 더해도
        /// 「어느 메뉴를 돌려야 반영되는가」가 어디에도 안 보였고,
        /// <c>Generate ▸ Prototype Content</c>를 돌린 뒤 <b>아무 로그도 안 나는</b> 상태로 멈췄다.
        /// 상점(<c>Generate ▸ Shop Content</c>)에는 이미 있던 자리라 대칭을 맞춘다.
        /// </summary>
        public static void GenerateEventContentMenu()
        {
            EnsureAllEvents(out _, out _, out _, out _, out _, out _);
            EnsureAllRests(out _, out _, out _);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EventContentBuilder] 이벤트 6종 + 휴식 3종 점검 완료 — 위 로그에서 추가된 선택지 수를 확인할 것.");
        }

        /// <summary>
        /// 이벤트 SO 6종을 생성하고(없으면) 반환한다. 순서는 파일명 상수와 같다.
        ///
        /// 6종인 이유는 <b>분기 지점이 6곳</b>(스테이지 3개 × 2곳)이기 때문이다 —
        /// 종류가 모자라면 한 런에서 같은 이벤트를 두 번 만나게 되고, 그러면 두 번째 방은
        /// 선택이 아니라 이미 아는 답을 다시 고르는 절차가 된다.
        /// </summary>
        public static void EnsureAllEvents(
            out EventData brokenAltar, out EventData forgottenCache, out EventData abyssalSpring,
            out EventData sealedDoor, out EventData hollowCrown, out EventData oathStone)
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

                    Choice("무구 칸을 연다",
                        "제일 안쪽 칸이 따로 잠겨 있었다. 안에서 쇳내가 올라온다.",
                        Effect(EventEffectType.GoldSpend, 90),
                        Effect(EventEffectType.WeaponGacha, 1)),
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

            sealedDoor = CreateOrLoad(SealedDoorFile, "sealed_door", "봉인된 문",
                "쇠사슬로 묶인 문이다. 안쪽에서 무언가 낮게 울린다.",
                new[]
                {
                    Choice("자물쇠를 산다",
                        "떠돌이가 남긴 열쇠를 값을 치르고 얻는다. 문 안쪽의 것이 순순히 따라 나온다.",
                        Effect(EventEffectType.GoldSpend, 50),
                        Effect(EventEffectType.SkillDraft, 0)),

                    Choice("부순다",
                        "어깨로 밀어붙인다. 쇠사슬이 끊기며 살점을 가져갔지만, 안에 있던 것은 챙겼다.",
                        Effect(EventEffectType.HpCostPercent, 20),
                        Effect(EventEffectType.GoldGain, 40)),

                    Choice("돌아선다",
                        "울림이 잦아들 때까지 기다렸다가 자리를 뜬다."),

                    Choice("쇠사슬째 끌어낸다",
                        "문 안쪽에 세워져 있던 것을 통째로 끌어냈다. 무엇인지는 손에 쥐고서야 알았다.",
                        Effect(EventEffectType.GoldSpend, 60),
                        Effect(EventEffectType.WeaponGacha, 1)),
                });

            // Stage 3 "왕좌의 잔해" — 대가가 더 크다. 이 구간의 골드·HP 수입이 앞 스테이지의 2배 이상이라
            // 앞과 같은 값이면 선택이 아니라 공짜 줍기가 된다.
            hollowCrown = CreateOrLoad(HollowCrownFile, "hollow_crown", "빈 왕관",
                "받침대 위에 왕관이 놓여 있다. 쓴 자의 머리는 남아 있지 않다.",
                new[]
                {
                    Choice("왕관을 쓴다",
                        "관자놀이가 조여든다. 남의 기억이 밀려들지만, 그중 하나는 쓸 만한 기술이었다.",
                        Effect(EventEffectType.HpCostPercent, 25),
                        Effect(EventEffectType.SkillDraft, 0)),

                    Choice("보석을 뜯어낸다",
                        "왕관을 부수는 데 죄책감은 들지 않았다. 값은 나갈 것이다.",
                        Effect(EventEffectType.GoldGain, 110)),

                    Choice("그대로 둔다",
                        "쓴 자가 어떻게 되었는지는 보지 않아도 알 것 같다."),

                    Choice("받침대 아래를 연다",
                        "왕관보다 아래에 묻어 둔 것이 있었다. 왕이 마지막까지 쥐고 있던 쪽이다.",
                        Effect(EventEffectType.GoldSpend, 140),
                        Effect(EventEffectType.WeaponGacha, 1)),
                });

            oathStone = CreateOrLoad(OathStoneFile, "oath_stone", "맹세의 돌",
                "무릎 자국이 파인 돌이다. 여기서 무언가를 바친 자들이 있었다.",
                new[]
                {
                    Choice("맹세를 바친다",
                        "이름을 부르자 돌이 데워진다. 값을 치른 만큼 돌려받았다.",
                        Effect(EventEffectType.GoldSpend, 90),
                        Effect(EventEffectType.SkillDraft, 0)),

                    Choice("상처를 씻는다",
                        "고인 물에 손을 담근다. 차갑지만 아물어간다.",
                        Effect(EventEffectType.HealPercent, 45)),

                    Choice("무릎을 꿇지 않는다",
                        "돌은 아무 말도 하지 않았다. 그편이 나았다."),
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
        public static void EnsureAllRests(out EventData ember, out EventData ruinedCamp, out EventData throneHall)
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

            // 수치는 앞의 둘과 같다. 휴식 방은 스테이지가 깊어져도 계산이 달라지지 않아야
            // 마지막 보스 앞에서 "지금 HP로 되나"를 그대로 이어서 물을 수 있다.
            throneHall = CreateOrLoad(RestThroneFile, "rest_throne_hall", "알현실의 침묵",
                "부서진 옥좌 앞이다. 여기까지 온 자를 막아설 것은 이제 하나뿐이다.",
                new[]
                {
                    Choice("숨을 고른다",
                        "천장이 무너진 자리로 빛이 들어온다. 오래 앉아 있지는 않았다.",
                        Effect(EventEffectType.HealPercent, 60)),

                    Choice("마지막을 준비한다",
                        "손에 익은 것을 한 번 더 확인한다. 그것으로 충분해야 한다.",
                        Effect(EventEffectType.SkillDraft, 0)),
                });
        }

        /// <summary>
        /// 기존 이벤트에 <b>없는 선택지만</b> 덧붙인다. 라벨로 대조하므로
        /// <b>인스펙터에서 이름을 바꾼 선택지는 다른 것으로 보여 다시 들어온다</b>.
        /// 문구·효과는 절대 건드리지 않는다 — 손으로 맞춘 것을 되돌리지 않는 것이 원래 규약이다
        /// (<c>ShopContentBuilder.AddMissingItems</c>와 같다).
        ///
        /// ⚠️ <c>EventRoomPanel.MAX_CHOICES</c>를 넘기면 넘친 선택지는 <b>화면에 안 나온다</b>.
        /// 에러도 로그도 없으므로, 선택지를 늘릴 때는 그 상한을 함께 볼 것.
        /// </summary>
        private static int AddMissingChoices(EventData data, EventChoice[] choices)
        {
            if (data == null || choices == null) return 0;
            data.choices ??= new List<EventChoice>();

            int added = 0;
            for (int i = 0; i < choices.Length; i++)
            {
                if (data.choices.Exists(existing => existing != null && existing.label == choices[i].label)) continue;
                data.choices.Add(choices[i]);
                added += 1;
            }

            if (added > 0) EditorUtility.SetDirty(data);
            return added;
        }

        private static EventData CreateOrLoad(
            string fileName, string eventId, string title, string description, EventChoice[] choices)
        {
            string path = $"{AbyssPaths.Events}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EventData>(path);
            if (existing != null)
            {
                // 🔴 예전에는 그냥 건너뛰었다. 그러면 <b>코드에 선택지를 더해도 아무 일이 안 일어난다</b> —
                //    빌더를 재실행해도 조용히 그대로라, 「왜 안 나오지」를 한참 찾게 된다.
                //    상점(ShopContentBuilder.AddMissingItems)이 이미 쓰던 규약을 여기에도 맞춘다.
                int added = AddMissingChoices(existing, choices);
                Debug.Log(added > 0
                    ? $"[EventContentBuilder] 선택지 {added}개 추가: {path}"
                    : $"[EventContentBuilder] 건너뜀 (존재): {path}");
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

using System.Collections;
using Abyss.Runtime.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abyss.Tests.PlayMode
{
    /// <summary>
    /// 씬 전환 실패 경로의 화면 복구 스모크(2026-09-23 총괄 검수 보완).
    ///
    /// 🔴 <b>실패가 조용하지 않고 캄캄하다.</b> 전환은 화면을 검게 덮은 뒤 씬을 로드한다. 등록되지 않은 씬
    /// 이름은 Unity 버전에 따라 <c>null</c> 반환이 아니라 <b>예외</b>로 나올 수 있고, 그대로 빠져나가면
    /// 페이드가 걷히지 않아 아무것도 보이지 않고 클릭도 막힌 화면이 남는다(오버레이가 레이캐스트를 먹는다).
    ///
    /// 🔑 그래서 "로그가 났는가"가 아니라 <b>"화면이 돌아왔는가"</b>를 본다 —
    /// <c>IsLoading</c>이 풀리고, 페이드 오버레이의 alpha가 0이고, 입력이 다시 통과하는지.
    /// 실제 씬 로드는 <b>일어나지 않는다</b>(존재하지 않는 이름을 쓴다) — 테스트 씬을 날리지 않기 위함이다.
    ///
    /// 정지 회수(timeScale·입력 모드)는 전환이 <b>확정된</b> 경우의 계약이라 여기서는 보지 않는다.
    /// 확정 전 실패는 옛 씬의 정지 소유자(GameFlowController)가 그대로 쥐는 것이 옳다.
    /// </summary>
    public sealed class SceneFlowRecoveryPlayModeSmoke
    {
        private const string MissingSceneName = "A2_NoSuchScene_ForTest";
        private const string FaderObjectName = "ScreenFader";

        /// <summary>페이드아웃 0.35초 + 복구 페이드인 0.35초. 프레임률이 낮은 러너를 감안해 넉넉히 준다.</summary>
        private const float SettleTimeoutSeconds = 5f;

        [UnityTest]
        public IEnumerator 등록되지_않은_씬_요청은_검은_화면을_남기지_않는다()
        {
            bool isHadFlow = SceneFlowController.HasInstance;
            var flow = SceneFlowController.Instance;
            bool isHadIgnoreFailingMessages = LogAssert.ignoreFailingMessages;

            // 실패 경로라 에러 로그가 난다. 로그 문구 자체는 계약이 아니므로 테스트를 로그로 실패시키지 않는다.
            LogAssert.ignoreFailingMessages = true;
            try
            {
                // null 반환이든 예외든 같은 복구를 기대한다 — 어느 쪽인지는 Unity 버전에 달렸다.
                _ = flow.LoadSceneAsync(MissingSceneName);

                float deadline = Time.realtimeSinceStartup + SettleTimeoutSeconds;
                while (flow.IsLoading && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.IsFalse(flow.IsLoading,
                    "실패한 전환이 제한 시간 안에 끝나지 않았다 — isLoading 이 고착되면 이후 모든 씬 전환이 무시된다.");

                var group = FindFaderGroup();
                Assert.IsNotNull(group,
                    "페이드 오버레이(" + FaderObjectName + ")를 찾지 못했다 — 이름이 바뀌었는지 확인할 것.");

                Assert.Less(group.alpha, 0.01f,
                    "등록되지 않은 씬을 요청한 뒤 검은 화면이 남았다 — 플레이어가 아무것도 못 보고 못 누른다.");
                Assert.IsFalse(group.blocksRaycasts,
                    "화면은 걷혔는데 오버레이가 입력을 계속 막고 있다.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = isHadIgnoreFailingMessages;

                // 이 테스트가 만든 영속 객체만 치운다. 이미 있던 것은 다른 테스트·씬의 것이다.
                if (!isHadFlow)
                {
                    var fader = GameObject.Find(FaderObjectName);
                    if (fader != null) Object.Destroy(fader);
                    if (flow != null) Object.Destroy(flow.gameObject);
                }
            }
            yield return null;
        }

        private static CanvasGroup FindFaderGroup()
        {
            var go = GameObject.Find(FaderObjectName);
            return go != null ? go.GetComponent<CanvasGroup>() : null;
        }
    }
}

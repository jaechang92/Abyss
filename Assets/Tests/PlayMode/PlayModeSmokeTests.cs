using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abyss.Tests.PlayMode
{
    /// <summary>
    /// PlayMode 어셈블리 인식·실행 경로 확인용 스모크. 실제 시스템 통합 스모크는 후속.
    /// </summary>
    public sealed class PlayModeSmokeTests
    {
        [UnityTest]
        public IEnumerator PlayMode_Assembly_IsLoadedAndRuns()
        {
            yield return null;
            Assert.Pass("PlayMode 어셈블리 정상 로드 및 1프레임 경과 확인.");
        }

        [UnityTest]
        public IEnumerator GameObject_CanBeCreatedAndDestroyed()
        {
            var go = new GameObject("SmokeProbe");
            yield return null;
            Assert.IsNotNull(go);
            Object.DestroyImmediate(go);
        }
    }
}

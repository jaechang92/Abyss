#if UNITY_EDITOR
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// C2 검증 진입점의 결과 기록. 항목 하나 = 로그 한 줄 <c>[접두어] PASS|FAIL 이름 — 근거</c>.
    /// 마지막에 합계 한 줄. 배치 로그에서 <c>[C2-</c> 로 걸러 세면 되게 했다(통과 수와 실패 수를 따로 적는다).
    /// </summary>
    internal sealed class C2Report
    {
        private readonly string prefix;
        private int passed;
        private int failed;

        public C2Report(string prefix)
        {
            this.prefix = prefix;
        }

        /// <summary>지금까지 실패가 없는가.</summary>
        public bool IsOk => failed == 0;

        public void Check(string name, bool isPass, string detail)
        {
            string line = $"[{prefix}] {(isPass ? "PASS" : "FAIL")} {name} — {detail}";
            if (isPass)
            {
                passed++;
                Debug.Log(line);
            }
            else
            {
                failed++;
                Debug.LogError(line);
            }
        }

        /// <summary>합계를 남기고 전체 통과 여부를 돌려준다.</summary>
        public bool Finish()
        {
            string line = $"[{prefix}] 합계 PASS {passed} · FAIL {failed}";
            if (failed == 0) Debug.Log(line);
            else Debug.LogError(line);
            return failed == 0;
        }
    }
}
#endif

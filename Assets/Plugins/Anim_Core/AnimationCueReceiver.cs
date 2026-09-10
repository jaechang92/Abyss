using System;
using UnityEngine;

namespace Anim.Core
{
    /// <summary>
    /// <b>Animation Event가 도착하는 유일한 창구.</b> 타격 판정 프레임·발소리 같은
    /// "클립의 이 지점에서 무언가 일어난다"를 게임 쪽 이벤트로 옮긴다.
    ///
    /// 🔴 <b>왜 메서드를 하나만 두는가</b> — Animation Event는 <b>메서드 이름을 문자열로</b> 들고 있다.
    /// 이름을 바꾸거나 지워도 <b>컴파일러가 아무 말도 하지 않고</b>, 클립은 조용히 아무도 안 부른다.
    /// 이벤트마다 메서드를 따로 두면 그 지뢰가 클립 수만큼 깔린다.
    /// 창구를 하나로 모으면 <b>문자열로 남는 이름이 이 하나</b>뿐이고, 나머지는 전부
    /// <c>cueId</c> 상수 — 즉 컴파일러가 지키는 영역으로 들어온다.
    ///
    /// 📌 <c>cueId</c>가 무엇을 뜻하는지는 게임이 정한다. 이 코어는 번호를 나를 뿐이다.
    /// </summary>
    public sealed class AnimationCueReceiver : MonoBehaviour
    {
        /// <summary>클립이 신호를 보냈다. 인자는 게임이 정한 큐 번호.</summary>
        public event Action<int> OnCue;

        /// <summary>
        /// 🔴 <b>이 이름은 클립 안에 문자열로 박힌다.</b> 바꾸면 이미 구운 클립이 전부 조용해진다 —
        /// 정말 바꿔야 한다면 클립을 다시 굽는 것까지가 한 작업이다.
        /// </summary>
        private void OnAnimationCue(int cueId)
        {
            OnCue?.Invoke(cueId);
        }
    }
}

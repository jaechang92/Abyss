using System;
using System.Collections.Generic;
using UnityEngine;

namespace Anim.Core
{
    /// <summary>
    /// <see cref="Animator"/>를 <b>전이 없는 클립 재생기로만</b> 쓰는 <see cref="IAnimationDriver"/> 구현.
    ///
    /// 전제: 붙는 컨트롤러는 <b>전이(Transition)가 하나도 없는 평평한 상태 집합</b>이고,
    /// 상태 이름 = 클립 이름 = 게임이 넘기는 <c>animationId</c>다.
    /// 이 셋이 어긋나면 <b>조용히 안 움직인다</b>(경고 한 줄 나고 화면은 그대로) — 생성기가 규약을 강제한다.
    ///
    /// 🔑 <b>Animator에게서 빌리는 것은 셋뿐이다</b> — 폼별 클립 교체(<c>AnimatorOverrideController</c>),
    /// 클립 타임라인에 구워진 손 앵커, Animation Event. 전이·파라미터·블렌드는 쓰지 않는다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class AnimatorDriver : MonoBehaviour, IAnimationDriver
    {
        /// <summary>이 재생기는 레이어를 쓰지 않는다. 겹쳐 재생할 일이 생기면 그때 계약을 늘린다.</summary>
        private const int BaseLayer = 0;

        [SerializeField] private Animator animator;

        // 이름 -> 상태 해시. Animator.Play(string)은 부를 때마다 해싱하므로 한 번만 계산한다.
        private readonly Dictionary<string, int> stateHashes = new Dictionary<string, int>();

        // 클립 이름 -> 그 이름에 실제 클립이 걸려 있는가. 오버라이드가 걸린 경우에만 채워진다.
        private readonly Dictionary<string, bool> clipFilled = new Dictionary<string, bool>();

        private readonly List<KeyValuePair<AnimationClip, AnimationClip>> overrideBuffer =
            new List<KeyValuePair<AnimationClip, AnimationClip>>();

        private string currentAnimationId = string.Empty;

        public string CurrentAnimationId => currentAnimationId;

        public event Action OnClipsChanged;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            RefreshClipTable();
        }

        /// <summary>
        /// 폼 교체 등으로 <b>클립 한 벌을 통째로 갈아끼운다.</b>
        ///
        /// 🔑 같은 base 컨트롤러에서 파생된 오버라이드끼리는 교체해도 <b>상태 기계가 리셋되지 않는다</b> —
        /// 달리던 중에 폼을 바꿔도 달리기가 안 끊긴다. 미드런 교체가 이 성질 위에 서 있다.
        ///
        /// 🔴 <b>그래도 다시 판정해야 한다.</b> 재생 중이던 이름이 새 폼에는 없을 수 있고,
        /// 그때 화면은 <b>이전 폼의 마지막 그림에 얼어붙는다.</b> 그래서 <see cref="OnClipsChanged"/>를 알린다.
        /// </summary>
        public void SetController(RuntimeAnimatorController controller)
        {
            if (animator == null || controller == null) return;
            if (ReferenceEquals(animator.runtimeAnimatorController, controller)) return;

            animator.runtimeAnimatorController = controller;
            RefreshClipTable();
            OnClipsChanged?.Invoke();
        }

        public bool HasClip(string animationId)
        {
            if (animator == null || string.IsNullOrEmpty(animationId)) return false;
            if (animator.runtimeAnimatorController == null) return false;

            // ① 그 이름의 상태가 컨트롤러에 있는가.
            if (!animator.HasState(BaseLayer, StateHash(animationId))) return false;

            // ② 그 자리에 실제 클립이 들어 있는가.
            //    오버라이드를 안 쓰는 컨트롤러라면 표가 비어 있고, 그때는 ①로 충분하다.
            return !clipFilled.TryGetValue(animationId, out bool filled) || filled;
        }

        public void Play(string animationId, bool restart, bool immediate)
        {
            if (animator == null || string.IsNullOrEmpty(animationId)) return;
            if (!restart && animationId == currentAnimationId) return;

            // normalizedTime 0 — 이 재생기는 언제나 클립의 처음부터 튼다.
            // 이어 붙이기·블렌딩은 Animator의 전이가 하던 일이고, 그것을 쓰지 않기로 한 것이 이 설계다.
            animator.Play(StateHash(animationId), BaseLayer, 0f);
            currentAnimationId = animationId;

            // 🔴 Animator의 시간 갱신은 이 프레임의 뒤쪽(PreLateUpdate)에 있어서,
            //    Play만 부르고 두면 화면은 다음 프레임에야 바뀐다. 게임 FSM이 동기 전이로
            //    즉시성을 확보해 둔 상태(공격·피격·사망)에서 그 한 프레임이 그대로 체감된다.
            //    Update(0)은 시간을 안 흘리고 지금 상태만 평가시킨다.
            if (immediate && animator.isActiveAndEnabled) animator.Update(0f);
        }

        /// <summary>
        /// 이름표를 다시 만든다. 상태 해시는 컨트롤러가 바뀌어도 이름이 같으면 같은 값이라 유지하고,
        /// <b>클립이 채워졌는지만</b> 새로 읽는다.
        /// </summary>
        private void RefreshClipTable()
        {
            clipFilled.Clear();

            if (animator == null) return;
            if (animator.runtimeAnimatorController is not AnimatorOverrideController overrideController) return;

            overrideBuffer.Clear();
            overrideController.GetOverrides(overrideBuffer);

            for (int i = 0; i < overrideBuffer.Count; i++)
            {
                AnimationClip original = overrideBuffer[i].Key;
                if (original == null) continue;

                // value가 null이면 "이 폼은 아직 이 상태를 안 그렸다"는 뜻이다.
                // base의 자리표시자 클립이 그대로 남아 있는 상태이며, 폴백 사슬이 여기서 갈라진다.
                clipFilled[original.name] = overrideBuffer[i].Value != null;
            }
        }

        private int StateHash(string animationId)
        {
            if (stateHashes.TryGetValue(animationId, out int hash)) return hash;

            hash = Animator.StringToHash(animationId);
            stateHashes[animationId] = hash;
            return hash;
        }
    }
}

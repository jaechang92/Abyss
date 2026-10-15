using UnityEngine;

namespace Abyss.Runtime.Camera
{
    /// <summary>
    /// 연출용 임시 가로 중심(E1 첫 보스 조우). <b>소유 토큰</b> 하나만 쥘 수 있고, 쥔 쪽만 세기를 바꾸거나 풀 수 있다.
    ///
    /// <list type="bullet">
    /// <item>추적 기준점(smoothedPosition)·offset·smoothTime·orthographicSize 는 건드리지 않는다. 최종 위치에 가로 이동만 더한다.</item>
    /// <item>이동량은 <c>maxPan</c> 으로 묶는다 — 두 인물이 같이 읽히지 않을 만큼 밀지 않는다. 결과는 기존 경계(<see cref="ConstrainPosition"/>)로 다시 자른다.</item>
    /// <item>세기(0~1)는 소유자가 실시간으로 몰아 준다 — 정지(timeScale 0) 위에서도 흐른다. 이 컴포넌트는 시간을 재지 않는다.</item>
    /// <item>소유자가 파괴됐는데 풀지 않았으면 다음 프레임에 스스로 푼다(전투 추적으로 복귀).</item>
    /// <item>쥐고 있는 동안 추적 기준점·앞보기는 진행하지 않고(<see cref="IsPresentationHeld"/>), 흔들림은 섞지 않고 버린다.</item>
    /// <item>새 방·경계 변경·카메라 비활성은 임대를 <b>무효화</b>한다. 무효화된 소유자의 세기 변경은 무시되고,
    /// 그 소유자가 <see cref="EndPresentationFocus"/>로 옛 임대를 정리하기 전까지 새로 쥘 수 없다 — 늦게 도는 옛 async 정리가 새 방을 건드리지 않게.</item>
    /// </list>
    /// </summary>
    public sealed partial class PlayerCameraFollow
    {
        private object presentationOwner;
        private object stalePresentationOwner;
        private float presentationFocusX;
        private float presentationMaxPan;
        private float presentationWeight;

        /// <summary>현재 화면 가로 반폭(월드). 카메라가 없으면 0.</summary>
        public float ViewHalfWidth =>
            ViewCamera != null ? ViewCamera.orthographicSize * ViewCamera.aspect : 0f;

        // 연출이 카메라를 쥐고 있다 — 추적 기준점·앞보기를 멈추고 흔들림을 버린다.
        private bool IsPresentationHeld => presentationOwner != null;

        /// <summary>임시 중심을 쥔다. 다른 소유자가 쥐고 있거나, 무효화된 옛 임대를 아직 정리하지 않은 소유자면 실패한다. 세기는 0에서 시작한다.</summary>
        public bool TryBeginPresentationFocus(object owner, float focusX, float maxPan)
        {
            if (owner == null) return false;
            ReleaseIfOwnerDestroyed();
            if (ReferenceEquals(owner, stalePresentationOwner)) return false;
            if (presentationOwner != null && !ReferenceEquals(presentationOwner, owner)) return false;

            presentationOwner = owner;
            presentationFocusX = focusX;
            presentationMaxPan = Mathf.Max(0f, maxPan);
            presentationWeight = 0f;
            return true;
        }

        /// <summary>세기(0 = 전투 추적 그대로, 1 = 임시 중심 쪽으로 최대). 소유자가 아니면 무시한다.</summary>
        public void SetPresentationWeight(object owner, float weight)
        {
            if (owner == null || !ReferenceEquals(owner, presentationOwner)) return;
            presentationWeight = Mathf.Clamp01(weight);
        }

        /// <summary>임시 중심을 푼다(멱등). 소유자가 아니면 무시한다 — 남의 연출을 끊지 않는다.</summary>
        public void EndPresentationFocus(object owner)
        {
            if (owner == null) return;
            if (ReferenceEquals(owner, stalePresentationOwner))
            {
                // 무효화된 옛 임대의 정리 — 이미 풀려 있다. 현재 임대(다른 소유자)는 건드리지 않는다.
                stalePresentationOwner = null;
                return;
            }
            if (!ReferenceEquals(owner, presentationOwner)) return;
            presentationOwner = null;
            presentationWeight = 0f;
        }

        /// <summary>현재 임대를 무효화한다(새 방·경계 변경·카메라 비활성). 소유자는 다음 정리 호출까지 무시된다.</summary>
        private void InvalidatePresentation()
        {
            if (presentationOwner != null) stalePresentationOwner = presentationOwner;
            presentationOwner = null;
            presentationWeight = 0f;
        }

        private Vector3 ApplyPresentationOffset(Vector3 basePosition)
        {
            ReleaseIfOwnerDestroyed();
            if (presentationOwner == null || presentationWeight <= 0f) return basePosition;

            float shift = Mathf.Clamp(presentationFocusX - basePosition.x, -presentationMaxPan, presentationMaxPan);
            basePosition.x += shift * presentationWeight;
            return basePosition;
        }

        private void ReleaseIfOwnerDestroyed()
        {
            // Unity 오브젝트 소유자는 파괴되면 == null 로 판정된다(C# 참조는 남아 있다).
            if (presentationOwner is Object unityOwner && unityOwner == null)
            {
                presentationOwner = null;
                presentationWeight = 0f;
            }
            // 파괴된 옛 소유자는 정리 호출을 보낼 수 없다 — 기다리지 않는다.
            if (stalePresentationOwner is Object staleOwner && staleOwner == null) stalePresentationOwner = null;
        }
    }
}

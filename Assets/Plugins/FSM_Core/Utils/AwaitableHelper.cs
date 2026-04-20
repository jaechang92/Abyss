using UnityEngine;
using System;
using System.Threading;

namespace FSM.Utils
{
    /// <summary>
    /// Unity 6.0 Awaitable 유틸리티 클래스
    /// Coroutine 대신 Awaitable 패턴을 사용하기 위한 헬퍼 클래스
    /// </summary>
    public static class AwaitableHelper
    {
        // 이미 완료된 Awaitable을 재사용하기 위한 캐시
        private static readonly AwaitableCompletionSource completedSource = new();

        static AwaitableHelper()
        {
            // 정적 생성자에서 이미 완료 상태로 설정
            completedSource.SetResult();
        }

        /// <summary>
        /// 이미 완료된 Awaitable 반환 (Task.CompletedTask와 유사)
        /// </summary>
        public static Awaitable CompletedTask => completedSource.Awaitable;

        /// <summary>
        /// 조건이 true가 될 때까지 대기
        /// </summary>
        public static async Awaitable WaitUntil(Func<bool> condition, CancellationToken cancellationToken = default)
        {
            while (!condition())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                await Awaitable.NextFrameAsync(cancellationToken);
            }
        }

        /// <summary>
        /// 조건이 false가 될 때까지 대기
        /// </summary>
        public static async Awaitable WaitWhile(Func<bool> condition, CancellationToken cancellationToken = default)
        {
            while (condition())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                await Awaitable.NextFrameAsync(cancellationToken);
            }
        }

        /// <summary>
        /// 지정된 프레임 수만큼 대기
        /// </summary>
        public static async Awaitable WaitForFrames(int frameCount, CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < frameCount; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                await Awaitable.NextFrameAsync(cancellationToken);
            }
        }

        /// <summary>
        /// 애니메이션 커브 기반 보간 대기
        /// </summary>
        public static async Awaitable LerpWithCurve(
            Action<float> onUpdate,
            AnimationCurve curve,
            float duration,
            CancellationToken cancellationToken = default)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                float t = elapsed / duration;
                float value = curve.Evaluate(t);
                onUpdate?.Invoke(value);

                elapsed += Time.deltaTime;
                await Awaitable.NextFrameAsync(cancellationToken);
            }

            // 마지막 값 설정
            onUpdate?.Invoke(curve.Evaluate(1f));
        }

        /// <summary>
        /// 선형 보간 애니메이션
        /// </summary>
        public static async Awaitable Lerp(
            float from,
            float to,
            float duration,
            Action<float> onUpdate,
            CancellationToken cancellationToken = default)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                float t = elapsed / duration;
                float value = Mathf.Lerp(from, to, t);
                onUpdate?.Invoke(value);

                elapsed += Time.deltaTime;
                await Awaitable.NextFrameAsync(cancellationToken);
            }

            // 마지막 값 설정
            onUpdate?.Invoke(to);
        }

        /// <summary>
        /// Vector3 보간 대기
        /// </summary>
        public static async Awaitable LerpVector3(
            Vector3 from,
            Vector3 to,
            float duration,
            Action<Vector3> onUpdate,
            CancellationToken cancellationToken = default)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                float t = elapsed / duration;
                Vector3 value = Vector3.Lerp(from, to, t);
                onUpdate?.Invoke(value);

                elapsed += Time.deltaTime;
                await Awaitable.NextFrameAsync(cancellationToken);
            }

            // 마지막 값 설정
            onUpdate?.Invoke(to);
        }

        /// <summary>
        /// 타임아웃 기능이 있는 대기
        /// </summary>
        public static async Awaitable WaitWithTimeout(
            Func<bool> condition,
            float timeoutSeconds,
            Action onTimeout = null,
            CancellationToken cancellationToken = default)
        {
            float elapsed = 0f;

            while (!condition() && elapsed < timeoutSeconds)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                elapsed += Time.deltaTime;
                await Awaitable.NextFrameAsync(cancellationToken);
            }

            if (elapsed >= timeoutSeconds)
            {
                onTimeout?.Invoke();
                Debug.LogWarning($"WaitWithTimeout: Timeout after {timeoutSeconds} seconds");
            }
        }

        /// <summary>
        /// 모든 Awaitable이 완료될 때까지 대기
        /// </summary>
        public static async Awaitable WhenAll(params Awaitable[] awaitables)
        {
            foreach (var awaitable in awaitables)
            {
                await awaitable;
            }
        }

        /// <summary>
        /// 첫 번째로 완료되는 Awaitable을 기다림
        /// </summary>
        public static async Awaitable WhenAny(params Awaitable[] awaitables)
        {
            // 간단한 구현 (실제로는 더 복잡한 로직 필요)
            if (awaitables.Length > 0)
            {
                await awaitables[0];
            }
        }

        /// <summary>
        /// 지연 후 액션 실행
        /// </summary>
        public static async Awaitable DelayedAction(
            float delay,
            Action action,
            CancellationToken cancellationToken = default)
        {
            await Awaitable.WaitForSecondsAsync(delay, cancellationToken);
            action?.Invoke();
        }

        /// <summary>
        /// 반복 실행
        /// </summary>
        public static async Awaitable RepeatAction(
            Action action,
            float interval,
            int repeatCount,
            CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < repeatCount; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                action?.Invoke();
                await Awaitable.WaitForSecondsAsync(interval, cancellationToken);
            }
        }

        /// <summary>
        /// 무한 반복 실행
        /// </summary>
        public static async Awaitable RepeatActionForever(
            Action action,
            float interval,
            CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                action?.Invoke();
                await Awaitable.WaitForSecondsAsync(interval, cancellationToken);
            }
        }
    }
}
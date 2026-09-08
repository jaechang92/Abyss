using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Singleton_Core
{
    /// <summary>
    /// 제네릭 싱글톤 패턴 기본 클래스
    /// MonoBehaviour를 상속받는 Unity 컴포넌트용 싱글톤
    /// 모든 싱글톤 인스턴스를 자동으로 추적 및 관리
    /// </summary>
    /// <typeparam name="T">싱글톤으로 구현할 클래스 타입</typeparam>
    public abstract class SingletonManager<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T instance;
        private static readonly object lockObject = new object();
        private static bool applicationIsQuitting = false;

        // 모든 싱글톤 인스턴스 추적 (디버그용)
        private static readonly HashSet<MonoBehaviour> allSingletons = new HashSet<MonoBehaviour>();

        /// <summary>
        /// 싱글톤 인스턴스 접근자
        /// </summary>
        public static T Instance
        {
            get
            {
                if (applicationIsQuitting)
                {
                    Debug.LogWarning($"[SingletonManager] Instance '{typeof(T)}' already destroyed on application quit. Won't create again - returning null.");
                    return null;
                }

                lock (lockObject)
                {
                    if (instance == null)
                    {
                        instance = FindAnyObjectByType<T>();

                        if (instance == null)
                        {
                            GameObject singleton = new GameObject();
                            instance = singleton.AddComponent<T>();
                            singleton.name = $"[Singleton] {typeof(T).Name}";

                            DontDestroyOnLoad(singleton);

                            Debug.Log($"[SingletonManager] Created new instance: {typeof(T).Name}");
                        }
                        else
                        {
                            Debug.Log($"[SingletonManager] Using existing instance: {typeof(T).Name}");
                        }
                    }

                    return instance;
                }
            }
        }

        /// <summary>
        /// 싱글톤 인스턴스가 존재하는지 확인
        /// </summary>
        public static bool HasInstance => instance != null && !applicationIsQuitting;

        /// <summary>
        /// 인스턴스를 안전하게 가져오기 (없으면 null 반환)
        /// </summary>
        public static T GetInstanceSafe()
        {
            return HasInstance ? instance : null;
        }

        protected virtual void Awake()
        {
            if (instance == null)
            {
                instance = this as T;
                DontDestroyOnLoad(gameObject);

                // 싱글톤 목록에 추가
                allSingletons.Add(this);
                Debug.Log($"[SingletonManager] 싱글톤 등록: {typeof(T).Name} (총 {allSingletons.Count}개)");

                OnAwake();
                OnSingletonAwake();
            }
            else if (instance != this)
            {
                Debug.LogWarning($"[SingletonManager] 중복 인스턴스 감지 및 파괴: {typeof(T).Name}");
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 싱글톤 초기화 시 호출되는 메서드
        /// 상속받은 클래스에서 오버라이드하여 초기화 로직 구현
        /// </summary>
        protected virtual void OnAwake() { }

        /// <summary>
        /// 싱글톤 초기화 시 호출되는 메서드 (매니저들과의 호환성을 위한 별칭)
        /// </summary>
        protected virtual void OnSingletonAwake() { }

        protected virtual void OnApplicationQuit()
        {
            applicationIsQuitting = true;
        }

        protected virtual void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;

                // 싱글톤 목록에서 제거
                allSingletons.Remove(this);
                Debug.Log($"[SingletonManager] 싱글톤 제거: {typeof(T).Name} (남은 개수: {allSingletons.Count})");
            }
        }

        /// <summary>
        /// 싱글톤 인스턴스를 강제로 생성한다.
        ///
        /// <b>[RuntimeInitializeOnLoadMethod]를 붙이지 않는다.</b> 제네릭 타입 안의 메서드는 Unity가
        /// 어떤 T로 호출할지 정할 수 없어 애초에 수집 대상이 아니다 — 이전 버전에서는 조용히
        /// 무시됐고, Unity 6.6부터 "methods cannot be in generic types" 에러 로그로 드러난다.
        /// 즉 이 메서드는 지금껏 자동 실행된 적이 없으며, 각 싱글톤은 첫 <see cref="Instance"/>
        /// 접근 시점에 지연 생성돼 왔다. 속성을 떼도 런타임 동작은 달라지지 않는다.
        ///
        /// 씬 로드 전에 미리 만들어야 하는 싱글톤이 생기면, 비제네릭 클래스에
        /// [RuntimeInitializeOnLoadMethod]를 두고 <c>SomeManager.EnsureInstance()</c>처럼
        /// T를 확정해 호출할 것.
        /// </summary>
        public static void EnsureInstance()
        {
            if (!HasInstance)
            {
                var _ = Instance; // 인스턴스 생성 트리거
            }
        }

        #region 디버그 및 관리 메서드

        /// <summary>
        /// 모든 싱글톤 인스턴스 목록 출력
        /// </summary>
        public static void LogAllSingletons()
        {
            Debug.Log($"[SingletonManager] ========== 모든 싱글톤 목록 ({allSingletons.Count}개) ==========");
            int index = 1;
            foreach (var singleton in allSingletons)
            {
                if (singleton != null)
                {
                    Debug.Log($"[SingletonManager] {index}. {singleton.GetType().Name} - {singleton.gameObject.name}");
                    index++;
                }
            }
            Debug.Log("[SingletonManager] =========================================");
        }

        /// <summary>
        /// 모든 싱글톤 인스턴스 가져오기
        /// </summary>
        public static IEnumerable<MonoBehaviour> GetAllSingletons()
        {
            return allSingletons.Where(s => s != null);
        }

        /// <summary>
        /// 파괴된 싱글톤 정리
        /// </summary>
        public static void CleanupDestroyedSingletons()
        {
            int beforeCount = allSingletons.Count;
            allSingletons.RemoveWhere(s => s == null);
            int removedCount = beforeCount - allSingletons.Count;

            if (removedCount > 0)
            {
                Debug.Log($"[SingletonManager] 파괴된 싱글톤 {removedCount}개 정리 완료. 남은 개수: {allSingletons.Count}");
            }
        }

        /// <summary>
        /// 특정 타입의 싱글톤 존재 여부 확인
        /// </summary>
        public static bool HasSingletonOfType<TSingleton>() where TSingleton : MonoBehaviour
        {
            return allSingletons.Any(s => s != null && s is TSingleton);
        }

        #endregion
    }
}

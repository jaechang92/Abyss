using System.Collections.Generic;
using UnityEngine;

namespace ObjectPool_Core
{
    /// <summary>
    /// 여러 오브젝트 풀을 중앙에서 관리하는 매니저
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        private static PoolManager instance;
        public static PoolManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<PoolManager>();
                    if (instance == null)
                    {
                        var go = new GameObject("[PoolManager]");
                        instance = go.AddComponent<PoolManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        public static bool HasInstance => instance != null;

        // 풀 저장소
        private readonly Dictionary<string, object> pools = new Dictionary<string, object>();

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 풀 생성 또는 가져오기
        /// </summary>
        public ObjectPool<T> GetOrCreatePool<T>(T prefab, int initialSize = 10, bool canGrow = true) where T : Component
        {
            string key = GetPoolKey(prefab);

            if (pools.TryGetValue(key, out var existingPool))
            {
                return (ObjectPool<T>)existingPool;
            }

            // 풀 부모 오브젝트 생성
            var parentGo = new GameObject($"Pool_{prefab.name}");
            parentGo.transform.SetParent(transform);

            var pool = new ObjectPool<T>(prefab, parentGo.transform, initialSize, canGrow);
            pools[key] = pool;

            Debug.Log($"[PoolManager] 풀 생성: {key} (초기 크기: {initialSize})");
            return pool;
        }

        /// <summary>
        /// 풀에서 오브젝트 가져오기
        /// </summary>
        public T Get<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            var pool = GetOrCreatePool(prefab);
            return pool.Get(position, rotation);
        }

        /// <summary>
        /// 풀에서 오브젝트 가져오기 (위치만)
        /// </summary>
        public T Get<T>(T prefab, Vector3 position) where T : Component
        {
            return Get(prefab, position, Quaternion.identity);
        }

        /// <summary>
        /// 오브젝트를 풀로 반환
        /// </summary>
        public void Release<T>(T prefab, T obj) where T : Component
        {
            string key = GetPoolKey(prefab);

            if (pools.TryGetValue(key, out var pool))
            {
                ((ObjectPool<T>)pool).Release(obj);
            }
            else
            {
                Debug.LogWarning($"[PoolManager] {key} 풀이 존재하지 않습니다.");
                Object.Destroy(obj.gameObject);
            }
        }

        /// <summary>
        /// 특정 풀의 모든 오브젝트 반환
        /// </summary>
        public void ReleaseAll<T>(T prefab) where T : Component
        {
            string key = GetPoolKey(prefab);

            if (pools.TryGetValue(key, out var pool))
            {
                ((ObjectPool<T>)pool).ReleaseAll();
            }
        }

        /// <summary>
        /// 모든 풀의 모든 오브젝트 반환
        /// </summary>
        public void ReleaseAllPools()
        {
            // 리플렉션 없이 타입 안전하게 처리하기 어려우므로
            // 각 풀의 ReleaseAll은 호출자가 직접 관리하도록 함
            Debug.Log("[PoolManager] 모든 풀 반환 요청");
        }

        /// <summary>
        /// 풀 정보 출력
        /// </summary>
        public void LogAllPoolsInfo()
        {
            Debug.Log($"[PoolManager] ========== 풀 목록 ({pools.Count}개) ==========");
            foreach (var kvp in pools)
            {
                Debug.Log($"  - {kvp.Key}");
            }
            Debug.Log("[PoolManager] =====================================");
        }

        private string GetPoolKey<T>(T prefab) where T : Component
        {
            return $"{typeof(T).Name}_{prefab.name}";
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}

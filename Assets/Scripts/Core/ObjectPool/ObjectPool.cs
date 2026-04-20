using System.Collections.Generic;
using UnityEngine;

namespace ObjectPool_Core
{
    /// <summary>
    /// 제네릭 오브젝트 풀
    /// 특정 타입의 GameObject를 재사용하여 Instantiate/Destroy 비용 절감
    /// </summary>
    /// <typeparam name="T">풀링할 컴포넌트 타입</typeparam>
    public class ObjectPool<T> where T : Component
    {
        private readonly T prefab;
        private readonly Transform poolParent;
        private readonly Queue<T> availableObjects = new Queue<T>();
        private readonly HashSet<T> activeObjects = new HashSet<T>();
        private readonly int initialSize;
        private readonly bool canGrow;

        /// <summary>
        /// 풀의 총 오브젝트 수
        /// </summary>
        public int TotalCount => availableObjects.Count + activeObjects.Count;

        /// <summary>
        /// 현재 활성 오브젝트 수
        /// </summary>
        public int ActiveCount => activeObjects.Count;

        /// <summary>
        /// 사용 가능한 오브젝트 수
        /// </summary>
        public int AvailableCount => availableObjects.Count;

        /// <summary>
        /// 오브젝트 풀 생성
        /// </summary>
        /// <param name="prefab">풀링할 프리팹</param>
        /// <param name="parent">풀 오브젝트의 부모 Transform</param>
        /// <param name="initialSize">초기 풀 크기</param>
        /// <param name="canGrow">풀이 부족할 때 자동 확장 여부</param>
        public ObjectPool(T prefab, Transform parent, int initialSize = 10, bool canGrow = true)
        {
            this.prefab = prefab;
            this.poolParent = parent;
            this.initialSize = initialSize;
            this.canGrow = canGrow;

            // 초기 풀 생성
            for (int i = 0; i < initialSize; i++)
            {
                CreateNewObject();
            }
        }

        /// <summary>
        /// 새 오브젝트 생성 및 풀에 추가
        /// </summary>
        private T CreateNewObject()
        {
            T newObj = Object.Instantiate(prefab, poolParent);
            newObj.gameObject.SetActive(false);
            availableObjects.Enqueue(newObj);
            return newObj;
        }

        /// <summary>
        /// 풀에서 오브젝트 가져오기
        /// </summary>
        public T Get(Vector3 position, Quaternion rotation)
        {
            T obj;

            // 사용 가능한 오브젝트가 없으면
            if (availableObjects.Count == 0)
            {
                if (canGrow)
                {
                    obj = CreateNewObject();
                }
                else
                {
                    Debug.LogWarning($"[ObjectPool] {typeof(T).Name} 풀이 부족합니다! 가장 오래된 오브젝트 재사용");
                    // 가장 오래된 활성 오브젝트 강제 반환
                    var enumerator = activeObjects.GetEnumerator();
                    enumerator.MoveNext();
                    obj = enumerator.Current;
                    Release(obj);
                }
            }

            obj = availableObjects.Dequeue();
            activeObjects.Add(obj);

            // 위치 및 회전 설정
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.gameObject.SetActive(true);

            // IPoolable 인터페이스 호출
            if (obj is IPoolable poolable)
            {
                poolable.OnSpawn();
            }

            return obj;
        }

        /// <summary>
        /// 풀에서 오브젝트 가져오기 (위치만)
        /// </summary>
        public T Get(Vector3 position)
        {
            return Get(position, Quaternion.identity);
        }

        /// <summary>
        /// 풀에서 오브젝트 가져오기 (기본 위치)
        /// </summary>
        public T Get()
        {
            return Get(Vector3.zero, Quaternion.identity);
        }

        /// <summary>
        /// 오브젝트를 풀로 반환
        /// </summary>
        public void Release(T obj)
        {
            if (obj == null) return;

            if (!activeObjects.Contains(obj))
            {
                Debug.LogWarning($"[ObjectPool] {obj.name}은 이미 풀에 반환되었거나 이 풀의 오브젝트가 아닙니다.");
                return;
            }

            // IPoolable 인터페이스 호출
            if (obj is IPoolable poolable)
            {
                poolable.OnDespawn();
            }

            activeObjects.Remove(obj);
            obj.gameObject.SetActive(false);
            obj.transform.SetParent(poolParent);
            availableObjects.Enqueue(obj);
        }

        /// <summary>
        /// 모든 활성 오브젝트 반환
        /// </summary>
        public void ReleaseAll()
        {
            var activeList = new List<T>(activeObjects);
            foreach (var obj in activeList)
            {
                Release(obj);
            }
        }

        /// <summary>
        /// 풀 미리 채우기 (Warm up)
        /// </summary>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (availableObjects.Count < count)
                {
                    CreateNewObject();
                }
            }
        }

        /// <summary>
        /// 풀 정보 가져오기
        /// </summary>
        public PoolInfo GetInfo()
        {
            return new PoolInfo
            {
                TypeName = typeof(T).Name,
                TotalCount = TotalCount,
                ActiveCount = ActiveCount,
                AvailableCount = AvailableCount,
                InitialSize = this.initialSize,
                CanGrow = this.canGrow
            };
        }
    }

    /// <summary>
    /// 풀 정보 구조체
    /// </summary>
    public struct PoolInfo
    {
        public string TypeName;
        public int TotalCount;
        public int ActiveCount;
        public int AvailableCount;
        public int InitialSize;
        public bool CanGrow;

        public override string ToString()
        {
            return $"[{TypeName}] Total: {TotalCount}, Active: {ActiveCount}, Available: {AvailableCount}";
        }
    }

    /// <summary>
    /// 풀링 가능한 오브젝트 인터페이스
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 풀에서 꺼내져 활성화될 때 호출
        /// </summary>
        void OnSpawn();

        /// <summary>
        /// 풀로 반환되어 비활성화될 때 호출
        /// </summary>
        void OnDespawn();
    }
}

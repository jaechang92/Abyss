using UnityEngine;
using System.Collections.Generic;

namespace GAS.Core
{
    /// <summary>
    /// 기본 게임플레이 컨텍스트 구현
    /// </summary>
    public class DefaultGameplayContext : MonoBehaviour, IGameplayContext
    {
        [Header("상태 설정")]
        [SerializeField] private bool isAlive = true;
        [SerializeField] private bool canAct = true;

        [Header("타겟팅")]
        [SerializeField] private Transform currentTarget;

        // 상태 저장소
        private Dictionary<string, bool> gameStates = new Dictionary<string, bool>();
        private Dictionary<string, object> customData = new Dictionary<string, object>();

        // IGameplayContext 구현
        public GameObject Owner => gameObject;
        public Transform Transform => transform;
        public bool IsAlive => isAlive;
        public bool CanAct => canAct;
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;

        /// <summary>
        /// 상태 확인
        /// </summary>
        public bool IsInState(string stateName)
        {
            return gameStates.TryGetValue(stateName, out var state) && state;
        }

        /// <summary>
        /// 상태 설정
        /// </summary>
        public void SetState(string stateName, bool value)
        {
            gameStates[stateName] = value;
        }

        /// <summary>
        /// 타겟 가져오기
        /// </summary>
        public Transform GetTarget()
        {
            return currentTarget;
        }

        /// <summary>
        /// 타겟 설정
        /// </summary>
        public void SetTarget(Transform target)
        {
            currentTarget = target;
        }

        /// <summary>
        /// 커스텀 데이터 가져오기
        /// </summary>
        public T GetCustomData<T>(string key) where T : class
        {
            if (customData.TryGetValue(key, out var data) && data is T typedData)
            {
                return typedData;
            }
            return null;
        }

        /// <summary>
        /// 커스텀 데이터 설정
        /// </summary>
        public void SetCustomData<T>(string key, T data) where T : class
        {
            customData[key] = data;
        }

        /// <summary>
        /// 생존 상태 설정
        /// </summary>
        public void SetAlive(bool alive)
        {
            isAlive = alive;
        }

        /// <summary>
        /// 행동 가능 상태 설정
        /// </summary>
        public void SetCanAct(bool canAct)
        {
            this.canAct = canAct;
        }

        /// <summary>
        /// 모든 상태 초기화
        /// </summary>
        public void ClearAllStates()
        {
            gameStates.Clear();
        }

        /// <summary>
        /// 모든 커스텀 데이터 초기화
        /// </summary>
        public void ClearAllCustomData()
        {
            customData.Clear();
        }
    }
}
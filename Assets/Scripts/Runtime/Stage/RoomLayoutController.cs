using System;
using System.Collections.Generic;
using Abyss.Runtime.Events;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 룸 진입 시 해당 룸의 지형 레이아웃 루트만 활성화하고 나머지는 비활성화한다.
    /// <see cref="GameEvents.OnRoomEntered"/>를 구독하므로 StageDirector를 수정하지 않는다(디커플링).
    ///
    /// 현재 아레나는 플레이어/카메라가 고정된 채 적 웨이브로 룸이 진행되므로,
    /// 모든 레이아웃은 동일 원점에 겹쳐 배치되고 한 번에 하나만 활성화된다.
    /// 바인딩되지 않은 룸은 어떤 레이아웃도 켜지 않아 기본 평지(Ground)만 남는다.
    /// </summary>
    public sealed class RoomLayoutController : MonoBehaviour
    {
        [Serializable]
        public sealed class RoomLayoutBinding
        {
            public RoomData room;
            public GameObject layoutRoot;
        }

        [SerializeField] private List<RoomLayoutBinding> bindings = new();

        [Tooltip("스테이지 시작 전 미리보기로 켜둘 레이아웃 인덱스. 음수면 전부 끔.")]
        [SerializeField] private int defaultLayoutIndex = 0;

        private void OnEnable()
        {
            GameEvents.OnRoomEntered += HandleRoomEntered;
        }

        private void OnDisable()
        {
            GameEvents.OnRoomEntered -= HandleRoomEntered;
        }

        private void Start()
        {
            // 스테이지가 첫 룸 진입 이벤트를 쏘기 전 미리보기 상태 정리.
            ActivateOnly(defaultLayoutIndex);
        }

        private void HandleRoomEntered(RoomData room)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || binding.layoutRoot == null) continue;

                bool match = binding.room == room;
                if (binding.layoutRoot.activeSelf != match)
                    binding.layoutRoot.SetActive(match);
            }
        }

        private void ActivateOnly(int index)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || binding.layoutRoot == null) continue;
                binding.layoutRoot.SetActive(i == index);
            }
        }
    }
}

using UnityEngine;

namespace Abyss.Runtime.Interaction
{
    /// <summary>
    /// 플레이어가 상호작용할 수 있는 대상(던전 포털·NPC·정비대 등)의 공통 인터페이스.
    /// PlayerInteractor가 근접 대상을 추적해 Interact를 호출한다.
    /// H2에서 대화 NPC·정비 NPC도 같은 인터페이스로 동일 파이프라인에 연결된다.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>상호작용 프롬프트 문구. 예: "던전 입장 (G)".</summary>
        string InteractionPrompt { get; }

        /// <summary>현재 상호작용 가능 여부(쿨다운·상태 가드).</summary>
        bool CanInteract { get; }

        /// <summary>상호작용 실행. interactor는 상호작용을 건 플레이어 GameObject.</summary>
        void Interact(GameObject interactor);
    }
}

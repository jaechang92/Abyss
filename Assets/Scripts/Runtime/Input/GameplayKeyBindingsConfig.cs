using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.Input
{
    /// <summary>읽기 전용 기본 입력 자원. 런타임은 복제본만 변경한다.</summary>
    public sealed class GameplayKeyBindingsConfig : ScriptableObject
    {
        [SerializeField] private InputActionAsset sourceActions;
        public InputActionAsset SourceActions => sourceActions;
    }
}

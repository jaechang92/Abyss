using UnityEngine;

namespace Abyss.Runtime.Dialogue
{
    /// <summary>
    /// NPC 한 명의 대화 묶음(라인 시퀀스). StringKey 참조라 텍스트 자체는 GameText.csv에 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueData", menuName = "Abyss/Data/Dialogue Data")]
    public sealed class DialogueData : ScriptableObject
    {
        public DialogueLine[] lines;
    }
}

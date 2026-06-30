using System;

namespace Abyss.Runtime.Dialogue
{
    /// <summary>
    /// 대화 한 줄. 화자명·대사 모두 StringKey로 보관해 다국어(GameText.csv) 연동한다.
    /// </summary>
    [Serializable]
    public struct DialogueLine
    {
        public string speakerKey; // StringKey (화자 이름)
        public string textKey;    // StringKey (대사 본문)
    }
}

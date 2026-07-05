using System;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.Dialogue
{
    /// <summary>
    /// 씬에 1개 존재하는 공용 대화 박스 UI. NPC가 Play(data, onComplete)로 호출한다.
    /// 진행 입력은 Space/Enter(상호작용 키 g와 분리해 시작-진행 충돌 방지).
    /// 대사·화자명은 Loc.Get(StringKey)로 다국어 조회한다.
    /// </summary>
    public sealed class DialogueUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text speakerLabel;
        [SerializeField] private Text bodyLabel;
        [SerializeField] private Text hintLabel;

        private DialogueLine[] lines;
        private int index;
        private Action onComplete;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        /// <summary>DialogueData 재생. 라인이 없으면 즉시 complete 호출.</summary>
        public void Play(DialogueData data, Action complete)
            => Play(data != null ? data.lines : null, complete);

        /// <summary>대사 라인 배열 직접 재생(스토리 챕터 등). 라인이 없으면 즉시 complete 호출.</summary>
        public void Play(DialogueLine[] dialogueLines, Action complete)
        {
            if (dialogueLines == null || dialogueLines.Length == 0)
            {
                complete?.Invoke();
                return;
            }

            lines = dialogueLines;
            onComplete = complete;
            index = 0;
            if (root != null) root.SetActive(true);
            if (hintLabel != null) hintLabel.text = "Space: 다음";
            ShowLine();
        }

        private void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
            {
                Advance();
            }
        }

        private void ShowLine()
        {
            var line = lines[index];
            if (speakerLabel != null) speakerLabel.text = Loc.Get(line.speakerKey);
            if (bodyLabel != null) bodyLabel.text = Loc.Get(line.textKey);
        }

        private void Advance()
        {
            index++;
            if (index >= lines.Length)
            {
                Close();
                var cb = onComplete;
                onComplete = null;
                cb?.Invoke();
                return;
            }
            ShowLine();
        }

        private void Close()
        {
            if (root != null) root.SetActive(false);
            lines = null;
        }
    }
}

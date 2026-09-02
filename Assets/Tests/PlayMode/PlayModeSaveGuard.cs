using System.IO;
using Abyss.Runtime.Meta;
using SaveSystem_Core;

namespace Abyss.Tests.PlayMode
{
    /// <summary>
    /// PlayMode 테스트가 사용자 메타 세이브를 건드리지 않게 지키는 공용 가드.
    ///
    /// 런을 돌리는 테스트는 대부분 <see cref="MetaSaveService"/>에 닿는다 —
    /// RunManager.StartNewRun 이 시작 골드 특전을 읽고, EndRun 이 파편·기록을 정산한다.
    /// 그래서 "세이브를 안 건드리는 런 테스트"는 사실상 없다.
    ///
    /// 백업/복구를 테스트마다 다시 쓰면 한 곳만 빠뜨려도 사용자 진행도가 날아간다.
    /// 세 스모크가 같은 절차를 쓰게 되어 여기로 뽑았다.
    /// </summary>
    public sealed class PlayModeSaveGuard
    {
        private string savePath;
        private string backupPath;
        private bool hadOriginal;

        /// <summary>세이브를 백업하고 깨끗한 상태로 시작한다.</summary>
        public void Acquire()
        {
            savePath = SaveSystem.Instance.GetFilePath(MetaSave.FileName);
            backupPath = savePath + ".testbackup";

            hadOriginal = File.Exists(savePath);
            if (hadOriginal && !File.Exists(backupPath)) File.Copy(savePath, backupPath, overwrite: true);
            if (File.Exists(savePath)) File.Delete(savePath);

            // 메모리 상 인스턴스도 초기화. autoSave:false 라 디스크를 건드리지 않는다.
            MetaSaveService.Instance.ResetAll(autoSave: false);
        }

        /// <summary>테스트가 만든 세이브를 지우고 원본을 되돌린다.</summary>
        public void Release()
        {
            if (!string.IsNullOrEmpty(savePath) && File.Exists(savePath)) File.Delete(savePath);

            if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
            {
                if (hadOriginal) File.Move(backupPath, savePath);
                else File.Delete(backupPath);
            }

            MetaSaveService.Instance.ResetAll(autoSave: false);
        }
    }
}

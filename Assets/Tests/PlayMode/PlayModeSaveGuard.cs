using System;
using System.IO;
using Abyss.Runtime.Meta;
using SaveSystem_Core;
using UnityEngine;

namespace Abyss.Tests.PlayMode
{
    /// <summary>
    /// PlayMode 테스트가 사용자 메타 세이브를 건드리지 않게 지키는 공용 가드.
    ///
    /// 런을 돌리는 테스트는 대부분 <see cref="MetaSaveService"/>에 닿는다 —
    /// RunManager.StartNewRun 이 시작 골드 특전을 읽고, EndRun 이 파편·기록을 정산한다.
    /// 그래서 "세이브를 안 건드리는 런 테스트"는 사실상 없다.
    ///
    /// <b>격리 방식(2026-09-23 변경)</b>: 사용자 파일을 옮겼다 되돌리지 않는다. 가드가 잡혀 있는 동안
    /// SaveSystem의 저장 폴더를 새 임시 폴더(<c>%TEMP%/AbyssPlayModeSave-{guid}</c>)로 돌린다.
    /// 예전 방식(본 파일만 .testbackup으로 옮김)은 안전 저장이 남기는 직전 정상본(.prev)·임시 파일(.tmp)·
    /// 손상 백업(*.bak)을 보호하지 못했고, 해제 뒤 빈 메모리 상태가 사용자 폴더에 저장될 여지도 있었다.
    ///
    /// Acquire 시: ① 임시 폴더 생성(실패하면 아무 상태도 바꾸지 않고 예외) ② 이전 저장 폴더 대체값·파일 조작 구현·
    /// MetaSaveService 메모리 상태를 잡아 둠 ③ 임시 폴더·기본 파일 조작으로 전환 ④ 메모리를 "미로드"로 비움 —
    /// 다음 접근은 빈 임시 폴더에서 NewFile로 시작하며 사용자 세이브는 읽지 않는다.
    /// Release 시: 잡아 둔 셋을 그대로 되돌린 뒤 임시 폴더를 지운다. 지우기에 실패해도 되돌리기는 끝나 있고
    /// 경고만 남긴다(남는 것은 %TEMP% 아래 테스트 파일뿐이다).
    ///
    /// 사용 규칙: 동시에 하나만 잡는다(중첩 미지원). <b>이미 잡혀 있으면 Acquire는 예외로 거절한다</b> —
    /// 같은 인스턴스든 다른 인스턴스든 마찬가지이고, 거절된 Acquire는 아무것도 바꾸지 않는다(먼저 잡은 가드의
    /// 격리가 그대로 살아 있다). 중첩과 "앞 테스트의 누수"는 밖에서 구별할 수 없으므로 자동으로 풀지 않는다 —
    /// 누수 회복은 <see cref="ForceReleaseActive"/>를 직접 부르는 명시적 경로뿐이다.
    /// Release는 몇 번 불러도 안전하다(SetUp이 Acquire 전에 실패한 경우 포함).
    ///
    /// Combat / MetaPerk / RunAbandon / StageDirector 스모크가 이 가드를 쓴다.
    /// </summary>
    public sealed class PlayModeSaveGuard
    {
        private const string DIRECTORY_PREFIX = "AbyssPlayModeSave-";

        // 현재 잡혀 있는 가드. 둘이 동시에 잡히면 먼저 풀린 쪽이 다른 쪽의 격리를 풀어 버린다.
        private static PlayModeSaveGuard active;

        /// <summary>지금 격리 중인 가드가 있는가. 누수 진단용.</summary>
        public static bool HasActiveGuard => active != null;

        private string testDirectory;
        private string previousDirectoryOverride;
        private ISaveFileOperations previousFileOperations;
        private MetaSaveService.MemorySnapshot previousMemory;

        /// <summary>이 가드가 지금 격리 중인가.</summary>
        public bool IsAcquired => active == this;

        /// <summary>격리 중인 임시 저장 폴더. 잡혀 있지 않으면 null.</summary>
        public string TestDirectory => IsAcquired ? testDirectory : null;

        /// <summary>
        /// 저장 폴더를 빈 임시 폴더로 돌리고 메타 메모리를 비운 채 시작한다.
        /// 이미 잡혀 있는 가드가 있으면 <see cref="InvalidOperationException"/>으로 거절하며,
        /// 그 경우 임시 폴더를 만들지도, 어떤 상태도 바꾸지도 않는다.
        /// </summary>
        public void Acquire()
        {
            if (active == this)
                throw new InvalidOperationException("[PlayModeSaveGuard] 이미 Acquire 했다 — Release 없이 다시 잡을 수 없다.");
            if (active != null)
                throw new InvalidOperationException(
                    "[PlayModeSaveGuard] 다른 가드가 격리 중이다 — 중첩은 지원하지 않는다. " +
                    $"살아 있는 격리를 풀지 않고 거절한다(격리 폴더: {active.testDirectory}). " +
                    "앞 테스트의 TearDown이 Release를 못 한 누수라면 ForceReleaseActive()를 명시적으로 부를 것.");

            string directory = Path.Combine(Path.GetTempPath(), DIRECTORY_PREFIX + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            var saveSystem = SaveSystem.Instance;
            var metaSave = MetaSaveService.Instance;
            previousDirectoryOverride = saveSystem.SaveDirectoryOverride;
            previousFileOperations = saveSystem.FileOperations;
            previousMemory = metaSave.CaptureMemoryState();

            testDirectory = directory;
            active = this;

            saveSystem.SetSaveDirectoryOverride(directory);
            saveSystem.SetFileOperations(null);
            metaSave.UnloadWithoutSaving();
        }

        /// <summary>저장 폴더·파일 조작·메타 메모리를 Acquire 전으로 되돌리고 임시 폴더를 지운다.</summary>
        public void Release()
        {
            if (active != this) return;

            try
            {
                if (SaveSystem.HasInstance)
                {
                    SaveSystem.Instance.SetSaveDirectoryOverride(previousDirectoryOverride);
                    SaveSystem.Instance.SetFileOperations(previousFileOperations);
                }
                if (MetaSaveService.HasInstance) MetaSaveService.Instance.RestoreMemoryState(previousMemory);
            }
            finally
            {
                active = null;
                previousMemory = null;
                previousFileOperations = null;
                previousDirectoryOverride = null;
                DeleteTestDirectory();
            }
        }

        /// <summary>
        /// 남아 있는 가드를 강제로 푼다. <b>누수 회복 전용의 명시적 경로</b>이며 Acquire가 자동으로 부르지 않는다 —
        /// 살아 있는 격리를 임의로 풀면 중첩 사용과 구별되지 않아 다른 테스트의 격리를 깨뜨린다.
        /// 남은 가드가 있을 때만 true. 플레이 모드를 나갔다 들어와 되돌릴 SaveSystem이 이미 사라졌다면
        /// 표시와 임시 폴더만 정리한다.
        /// </summary>
        public static bool ForceReleaseActive()
        {
            if (active == null) return false;

            var leaked = active;
            bool isStillIsolating = SaveSystem.HasInstance && SaveSystem.Instance.SaveDirectoryOverride == leaked.testDirectory;
            Debug.LogWarning($"[PlayModeSaveGuard] 남아 있던 가드를 강제로 푼다(앞 테스트의 TearDown 확인). " +
                             $"{(isStillIsolating ? "되돌린다" : "대상이 이미 없어 표시만 지운다")}: {leaked.testDirectory}");
            if (isStillIsolating) leaked.Release();
            else
            {
                active = null;
                leaked.DeleteTestDirectory();
            }
            return true;
        }

        private void DeleteTestDirectory()
        {
            string directory = testDirectory;
            testDirectory = null;
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;

            // 이 가드가 만든 폴더만 지운다 — 경로가 어떤 이유로든 바뀌었으면 손대지 않는다.
            bool isOwnDirectory = Path.GetFileName(directory).StartsWith(DIRECTORY_PREFIX, StringComparison.Ordinal)
                                  && string.Equals(Path.GetDirectoryName(directory)?.TrimEnd(Path.DirectorySeparatorChar),
                                                   Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),
                                                   StringComparison.OrdinalIgnoreCase);
            if (!isOwnDirectory)
            {
                Debug.LogWarning($"[PlayModeSaveGuard] 가드가 만든 폴더가 아니라 지우지 않는다: {directory}");
                return;
            }

            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayModeSaveGuard] 임시 저장 폴더 정리 실패(사용자 세이브와 무관, 수동 삭제 가능): {directory} — {e.Message}");
            }
        }
    }
}

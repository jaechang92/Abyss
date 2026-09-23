using System;
using System.IO;
using System.Text;

namespace SaveSystem_Core
{
    // SaveSystem.cs 500줄 규약으로 분리 — 안전 저장이 쓰는 파일 조작과 읽기 결과 상태.

    /// <summary>저장 파일 하나를 읽어 본 결과(<see cref="SaveSystem.LoadWithBackup{T}"/>).</summary>
    public enum SaveFileStatus
    {
        /// <summary>확인하지 않았다 — 앞 순위 파일로 이미 불러왔거나, 앞 순위 파일을 확인하지 못해 멈췄다.</summary>
        NotChecked,
        /// <summary>파일이 없다.</summary>
        Missing,
        /// <summary>읽고 역직렬화했다.</summary>
        Loaded,
        /// <summary>
        /// <b>내용이 손상됐다</b> — 읽기는 됐지만 빈 파일이거나 역직렬화에 실패했다.
        /// 다시 읽어도 결과가 같으므로 직전 정상본으로 대신해도 된다.
        /// </summary>
        Unreadable,
        /// <summary>
        /// <b>열지 못했다</b>(잠금·권한 등 I/O 실패, 정해진 횟수 재시도 후). 내용은 멀쩡한 최신본일 수 있다 —
        /// 손상으로 취급해 직전 정상본·새 세이브로 대신하거나 그 위에 저장하면 최신 진행도를 잃는다.
        /// </summary>
        Inaccessible
    }

    /// <summary>
    /// 안전 저장이 쓰는 파일 조작. 기본 구현은 <see cref="SaveFileOperations"/>이고,
    /// 테스트가 특정 단계에서 실패를 일으키려고 <see cref="SaveSystem.SetFileOperations"/>로 바꾼다.
    /// </summary>
    public interface ISaveFileOperations
    {
        /// <summary>파일 전체를 읽는다. 잠금·권한 실패는 예외로 알린다.</summary>
        string ReadAllText(string path);
        /// <summary>임시 파일에 내용을 끝까지 쓰고 디스크에 내린다.</summary>
        void WriteAllText(string path, string contents);
        /// <summary>최초 저장: 임시 파일을 본 파일 이름으로 옮긴다.</summary>
        void Move(string sourcePath, string destinationPath);
        /// <summary>갱신: 임시 파일로 본 파일을 교체한다. backupPath가 null이 아니면 교체 직전 본 파일을 그 이름으로 남긴다.</summary>
        void Replace(string sourcePath, string destinationPath, string backupPath);
    }

    /// <summary>기본 파일 조작. 테스트용 실패 주입은 이 클래스를 상속해 원하는 단계만 바꾼다.</summary>
    public class SaveFileOperations : ISaveFileOperations
    {
        public static readonly SaveFileOperations Default = new();

        // File.WriteAllText 기본값과 같은 인코딩(UTF-8, BOM 없음) — 기존 세이브와 바이트 형식이 같아야 한다.
        private static readonly UTF8Encoding utf8NoBom = new(false);

        public virtual string ReadAllText(string path) => File.ReadAllText(path);

        public virtual void WriteAllText(string path, string contents) =>
            WriteBytesFlushed(path, utf8NoBom.GetBytes(contents));

        public virtual void Move(string sourcePath, string destinationPath) =>
            File.Move(sourcePath, destinationPath);

        /// <summary>
        /// 보장 수준:
        /// <list type="bullet">
        /// <item><b>Windows(현재 대상)</b> — <c>File.Replace</c> = Win32 ReplaceFile. 같은 볼륨 안에서 본 파일 → 직전 정상본,
        /// 임시 → 본 파일 교체가 한 호출로 일어난다. 실패 시 본 파일이 남거나, 본 파일이 직전 정상본 이름으로 옮겨진
        /// 상태로 끝난다(ERROR_UNABLE_TO_MOVE_REPLACEMENT_2) — 후자는 로드가 직전 정상본에서 읽는다.</item>
        /// <item><b>File.Replace 미지원 플랫폼</b>(<see cref="PlatformNotSupportedException"/>일 때만) —
        /// <see cref="ReplaceByCopy"/>. <b>원자적이지 않다.</b> 다른 예외는 폴백하지 않고 저장 실패로 올린다.</item>
        /// </list>
        /// </summary>
        public virtual void Replace(string sourcePath, string destinationPath, string backupPath)
        {
            try
            {
                File.Replace(sourcePath, destinationPath, backupPath, ignoreMetadataErrors: true);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceByCopy(sourcePath, destinationPath, backupPath);
            }
        }

        /// <summary>
        /// File.Replace 없이 교체한다. 본 파일을 먼저 지우지 않는다. 단계별로 끊겼을 때:
        /// ① 본 → 직전 정상본 복사 중: 본 파일은 그대로(직전 정상본만 깨질 수 있음 — 로드는 본 파일을 쓴다).
        /// ② 임시 → 본 파일 덮어쓰기 중: 본 파일이 깨질 수 있으나 ①에서 만든 직전 정상본이 이전 세대를 가진다.
        /// ③ 임시 파일 삭제 중: 남은 임시 파일은 로드하지 않고 다음 저장이 덮어쓴다.
        /// 즉 <b>한 번의 중단으로 잃는 것은 이번 저장 한 세대</b>이고, 본 파일과 직전 정상본이 동시에 깨지는 단계는 없다.
        /// 단 backupPath가 null(본 파일이 이미 손상)이면 ②의 중단은 기존 직전 정상본(더 이전 세대)으로 돌아간다.
        /// Windows 외 플랫폼에서 실제로 실행해 확인하지는 않았다.
        /// </summary>
        public static void ReplaceByCopy(string sourcePath, string destinationPath, string backupPath)
        {
            if (backupPath != null) WriteBytesFlushed(backupPath, File.ReadAllBytes(destinationPath));
            WriteBytesFlushed(destinationPath, File.ReadAllBytes(sourcePath));
            File.Delete(sourcePath);
        }

        private static void WriteBytesFlushed(string path, byte[] bytes)
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
            // OS 캐시까지 내린다. 교체 직후 전원이 끊겨도 내용 없는 파일이 남지 않게.
            stream.Flush(true);
        }
    }
}

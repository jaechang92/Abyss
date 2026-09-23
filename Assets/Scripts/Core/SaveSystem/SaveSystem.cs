using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace SaveSystem_Core
{
    /// <summary>
    /// 범용 JSON 기반 저장 시스템
    /// 파일 I/O만 담당하며 데이터 구조는 프로젝트에서 정의
    /// 한 저장 슬롯 = 같은 폴더의 본 파일(로드 1순위) · <c>.prev</c> 직전 정상본(로드 2순위, 갱신 때 교체 직전 본 파일)
    /// · <c>.tmp</c> 쓰는 중인 내용(끝까지 썼는지 알 수 없어 <b>로드하지 않는다</b>).
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        /// <summary>직전 정상본 / 임시 파일 접미사.</summary>
        public const string BACKUP_SUFFIX = ".prev";
        public const string TEMP_SUFFIX = ".tmp";

        /// <summary>
        /// 파일 하나를 여는 최대 시도 횟수. 백신·동기화 도구의 짧은 잠금만 넘기고 그 이상은
        /// <see cref="SaveFileStatus.Inaccessible"/>로 드러낸다 — 로드 오류를 숨기는 무제한 재시도는 하지 않는다.
        /// 최악의 지연은 (시도-1) × <see cref="READ_RETRY_DELAY_MS"/>ms이며, 잠금이 걸린 경우에만 생긴다.
        /// </summary>
        public const int READ_ATTEMPTS = 3;
        public const int READ_RETRY_DELAY_MS = 30;

        private static SaveSystem instance;
        public static SaveSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<SaveSystem>();
                    if (instance == null)
                    {
                        var go = new GameObject("[SaveSystem]");
                        instance = go.AddComponent<SaveSystem>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        public static bool HasInstance => instance != null;

        // null이면 Application.persistentDataPath. 테스트가 사용자 세이브 폴더를 건드리지 않게 바꾼다.
        private string saveDirectoryOverride;
        private ISaveFileOperations fileOperations = SaveFileOperations.Default;

        /// <summary><b>테스트 전용</b> — 사용자 세이브 대신 격리된 폴더를 쓴다. null/빈 문자열이면 기본 폴더로.</summary>
        public void SetSaveDirectoryOverride(string directory) =>
            saveDirectoryOverride = string.IsNullOrEmpty(directory) ? null : directory;

        /// <summary><b>테스트 전용</b> — 쓰기·교체를 원하는 단계에서 실패시키는 구현으로 바꾼다. null이면 기본 구현으로.</summary>
        public void SetFileOperations(ISaveFileOperations operations) =>
            fileOperations = operations ?? SaveFileOperations.Default;

        /// <summary>현재 저장 폴더 대체값(없으면 null). 격리 장치가 끝날 때 이전 값으로 되돌리려고 읽는다.</summary>
        public string SaveDirectoryOverride => saveDirectoryOverride;

        /// <summary>현재 파일 조작 구현. 격리 장치가 끝날 때 이전 값으로 되돌리려고 읽는다.</summary>
        public ISaveFileOperations FileOperations => fileOperations;

        // ====== 이벤트 ======

        /// <summary>
        /// 저장 완료 시 발생
        /// </summary>
        public event Action<string> OnSaved;

        /// <summary>
        /// 불러오기 완료 시 발생
        /// </summary>
        public event Action<string> OnLoaded;

        /// <summary>
        /// 저장 실패 시 발생
        /// </summary>
        public event Action<string> OnSaveFailed;

        /// <summary>
        /// 불러오기 실패 시 발생
        /// </summary>
        public event Action<string> OnLoadFailed;

        // ====== Unity 생명주기 ======

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                Debug.Log($"[SaveSystem] 초기화 완료. 저장 경로: {Application.persistentDataPath}");
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        // ====== 저장 ======

        /// <summary>
        /// 데이터를 JSON 파일로 저장. 본 파일을 직접 덮어쓰지도, 먼저 지우지도 않는다.
        /// ① 같은 폴더의 임시 파일에 끝까지 쓰고 디스크에 내린다. ② 최초 저장(본 파일 없음)은 임시 파일을
        /// 본 파일 이름으로 옮긴다. ③ 갱신은 임시 파일로 본 파일을 교체하며 교체 직전 본 파일을 직전 정상본으로
        /// 남긴다 — 단 본 파일이 읽히지 않으면 직전 정상본으로 돌리지 않는다(깨진 파일이 멀쩡한 직전 정상본을
        /// 밀어내면 복구할 곳이 사라진다. 손상 증거 보관은 로드한 쪽, 예: MetaSaveService의 몫).
        /// 실패하면 본 파일·직전 정상본은 저장 전 그대로다(교체 도중 끊겨 본 파일이 사라져도 직전 정상본이 남는다).
        /// 기존 본 파일을 <b>열지 못하면</b>(<see cref="SaveFileStatus.Inaccessible"/>) 저장하지 않는다 —
        /// 확인하지 못한 최신본을 덮지 않는다. <see cref="OnSaved"/>는 본 파일 교체가 끝난 뒤에만 발생한다.
        /// </summary>
        /// <typeparam name="T">저장할 데이터 타입</typeparam>
        /// <param name="data">저장할 데이터</param>
        /// <param name="fileName">파일명 (확장자 포함)</param>
        /// <param name="prettyPrint">JSON 가독성 형식 여부</param>
        public bool Save<T>(T data, string fileName, bool prettyPrint = true)
        {
            if (data == null)
            {
                Debug.LogError("[SaveSystem] Save(): data가 null입니다.");
                OnSaveFailed?.Invoke("data is null");
                return false;
            }

            string filePath = GetFilePath(fileName);
            string tempPath = GetTempFilePath(fileName);

            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint);

                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                var primaryStatus = TryReadFile<T>(filePath, out _);
                if (primaryStatus == SaveFileStatus.Inaccessible)
                    throw new IOException($"본 파일을 열지 못해(잠금·권한) 내용을 확인할 수 없다 — 덮어쓰지 않는다: {filePath}");

                fileOperations.WriteAllText(tempPath, json);

                if (primaryStatus == SaveFileStatus.Missing)
                {
                    fileOperations.Move(tempPath, filePath);
                }
                else
                {
                    bool isPrimaryReadable = primaryStatus == SaveFileStatus.Loaded;
                    if (!isPrimaryReadable)
                        Debug.LogWarning($"[SaveSystem] 본 파일이 손상돼 직전 정상본으로 돌리지 않고 교체한다: {filePath}");
                    fileOperations.Replace(tempPath, filePath, isPrimaryReadable ? GetBackupFilePath(fileName) : null);
                }
            }
            catch (Exception e)
            {
                // 임시 파일은 로드 후보가 아니라 남겨 둘 이유가 없다. 본 파일·직전 정상본은 건드리지 않는다.
                DeleteQuietly(tempPath);
                string errorMsg = $"저장 실패: {e.Message}";
                Debug.LogError($"[SaveSystem] {errorMsg}");
                OnSaveFailed?.Invoke(errorMsg);
                return false;
            }

            // try 밖에서 알린다 — 구독자 예외가 이미 끝난 저장을 실패로 뒤집지 않게.
            Debug.Log($"[SaveSystem] 저장 완료: {filePath}");
            OnSaved?.Invoke(filePath);
            return true;
        }

        /// <summary>
        /// 데이터를 JSON 문자열로 직렬화
        /// </summary>
        public string Serialize<T>(T data, bool prettyPrint = false)
        {
            if (data == null) return null;
            return JsonUtility.ToJson(data, prettyPrint);
        }

        // ====== 불러오기 ======

        /// <summary>
        /// JSON 파일에서 데이터 불러오기. 본 파일만 읽는다 — 직전 정상본 복구가 필요하면 <see cref="LoadWithBackup{T}"/>.
        /// </summary>
        /// <typeparam name="T">불러올 데이터 타입</typeparam>
        /// <param name="fileName">파일명 (확장자 포함)</param>
        /// <returns>불러온 데이터 (실패 시 default)</returns>
        public T Load<T>(string fileName)
        {
            string filePath = GetFilePath(fileName);

            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[SaveSystem] 파일이 존재하지 않습니다: {filePath}");
                OnLoadFailed?.Invoke($"File not found: {filePath}");
                return default;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                T data = JsonUtility.FromJson<T>(json);

                Debug.Log($"[SaveSystem] 불러오기 완료: {filePath}");
                OnLoaded?.Invoke(filePath);
                return data;
            }
            catch (Exception e)
            {
                string errorMsg = $"불러오기 실패: {e.Message}";
                Debug.LogError($"[SaveSystem] {errorMsg}");
                OnLoadFailed?.Invoke(errorMsg);
                return default;
            }
        }

        /// <summary>
        /// 본 파일 → 직전 정상본 순으로 불러온다. ① 본 파일이 읽히면 그것을 쓰고 직전 정상본은 열지 않는다
        /// (더 오래된 데이터가 정상 최신본을 이기지 않는다). ② 본 파일이 없거나 <b>손상</b>(Unreadable)일 때만 직전 정상본.
        /// 본 파일을 <b>열지 못하면</b>(Inaccessible) 직전 정상본으로 대신하지 않고 멈춘다(backupStatus = NotChecked).
        /// ③ 임시 파일은 어떤 경우에도 읽지 않는다. 파일을 지우거나 고치지 않는다 —
        /// 손상 파일 보관·새 세이브 시작·저장 보류는 호출자가 두 상태를 보고 판단한다.
        /// </summary>
        /// <returns>읽은 데이터. 쓸 수 있는 파일이 없으면 default.</returns>
        public T LoadWithBackup<T>(string fileName, out SaveFileStatus primaryStatus, out SaveFileStatus backupStatus)
        {
            string filePath = GetFilePath(fileName);
            backupStatus = SaveFileStatus.NotChecked;

            primaryStatus = TryReadFile(filePath, out T data);
            if (primaryStatus == SaveFileStatus.Loaded)
            {
                Debug.Log($"[SaveSystem] 불러오기 완료: {filePath}");
                OnLoaded?.Invoke(filePath);
                return data;
            }

            if (primaryStatus == SaveFileStatus.Inaccessible)
            {
                // 멀쩡한 최신본일 수 있다. 직전 정상본을 현재값으로 채택하면 다음 저장이 그 옛 값으로 최신본을 덮는다.
                Debug.LogError($"[SaveSystem] 본 파일을 열지 못했다(잠금·권한, {READ_ATTEMPTS}회 시도) — 직전 정상본으로 대신하지 않는다: {filePath}");
                OnLoadFailed?.Invoke($"Inaccessible: {filePath}");
                return default;
            }

            string backupPath = GetBackupFilePath(fileName);
            backupStatus = TryReadFile(backupPath, out data);
            if (backupStatus == SaveFileStatus.Loaded)
            {
                Debug.LogWarning($"[SaveSystem] 본 파일({primaryStatus})을 쓸 수 없어 직전 정상본을 불러왔다: {backupPath}");
                OnLoaded?.Invoke(backupPath);
                return data;
            }

            bool isNothingSaved = primaryStatus == SaveFileStatus.Missing && backupStatus == SaveFileStatus.Missing;
            if (isNothingSaved) Debug.Log($"[SaveSystem] 저장 파일 없음: {filePath}");
            else Debug.LogError($"[SaveSystem] 불러오기 실패 — 본 파일 {primaryStatus}, 직전 정상본 {backupStatus}: {filePath}");
            OnLoadFailed?.Invoke(isNothingSaved ? $"File not found: {filePath}" : $"{backupStatus}: {filePath}");
            return default;
        }

        /// <summary>
        /// 파일 하나를 읽어 역직렬화한다. <b>여는 단계의 실패와 내용의 손상을 가른다</b> —
        /// 열지 못함(잠금·권한 등 I/O 예외)은 최대 <see cref="READ_ATTEMPTS"/>회 시도 후 Inaccessible,
        /// 읽었으나 빈 내용·역직렬화 실패는 Unreadable(재시도하지 않는다 — 다시 읽어도 같다).
        ///
        /// <b><see cref="File.Exists"/>로 먼저 거르지 않는다</b> — 그 API는 접근이 거부돼 확인조차 못 한 경우에도
        /// false를 돌려주므로, 선검사를 두면 권한 실패가 "파일 없음"으로 둔갑해 새 세이브로 시작하게 된다.
        /// 없다는 판정은 실제로 열어 보고 FileNotFound·DirectoryNotFound가 났을 때만 내린다.
        /// </summary>
        private SaveFileStatus TryReadFile<T>(string path, out T data)
        {
            data = default;
            string json;
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    json = fileOperations.ReadAllText(path);
                    break;
                }
                catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException)
                {
                    return SaveFileStatus.Missing;
                }
                catch (Exception e)
                {
                    if (attempt >= READ_ATTEMPTS)
                    {
                        Debug.LogWarning($"[SaveSystem] 파일을 열지 못했다({attempt}회): {path} — {e.Message}");
                        return SaveFileStatus.Inaccessible;
                    }
                    Thread.Sleep(READ_RETRY_DELAY_MS);
                }
            }

            if (string.IsNullOrWhiteSpace(json)) return SaveFileStatus.Unreadable;
            try
            {
                data = JsonUtility.FromJson<T>(json);
            }
            catch (Exception)
            {
                data = default;
                return SaveFileStatus.Unreadable;
            }
            return data == null ? SaveFileStatus.Unreadable : SaveFileStatus.Loaded;
        }

        /// <summary>
        /// JSON 문자열에서 데이터 역직렬화
        /// </summary>
        public T Deserialize<T>(string json)
        {
            if (string.IsNullOrEmpty(json)) return default;
            return JsonUtility.FromJson<T>(json);
        }

        /// <summary>
        /// 파일이 존재하면 데이터 불러오기, 없으면 새 인스턴스 반환
        /// </summary>
        public T LoadOrCreate<T>(string fileName) where T : new()
        {
            string filePath = GetFilePath(fileName);

            if (File.Exists(filePath))
            {
                return Load<T>(fileName);
            }

            Debug.Log($"[SaveSystem] 새 데이터 생성: {fileName}");
            return new T();
        }

        // ====== 파일 관리 ======

        /// <summary>
        /// 저장 파일 존재 확인
        /// </summary>
        public bool FileExists(string fileName)
        {
            return File.Exists(GetFilePath(fileName));
        }

        /// <summary>
        /// 저장 파일 삭제. 직전 정상본·임시 파일도 함께 지운다 — 본 파일만 지우면
        /// 다음 <see cref="LoadWithBackup{T}"/>가 직전 정상본을 되살린다.
        /// </summary>
        public bool DeleteFile(string fileName)
        {
            string filePath = GetFilePath(fileName);

            try
            {
                string backupPath = GetBackupFilePath(fileName);
                bool hadAny = File.Exists(filePath) || File.Exists(backupPath);
                if (File.Exists(filePath)) File.Delete(filePath);
                if (File.Exists(backupPath)) File.Delete(backupPath);
                DeleteQuietly(GetTempFilePath(fileName));

                if (hadAny) Debug.Log($"[SaveSystem] 파일 삭제 완료: {filePath}");
                else Debug.LogWarning($"[SaveSystem] 삭제할 파일이 존재하지 않습니다: {filePath}");
                return hadAny;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 파일 삭제 실패: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 파일 정보 가져오기
        /// </summary>
        public string GetFileInfo(string fileName)
        {
            string filePath = GetFilePath(fileName);

            if (!File.Exists(filePath))
            {
                return "파일이 존재하지 않습니다.";
            }

            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                return $"경로: {filePath}\n크기: {fileInfo.Length} bytes\n수정: {fileInfo.LastWriteTime}";
            }
            catch (Exception e)
            {
                return $"파일 정보 읽기 실패: {e.Message}";
            }
        }

        /// <summary>
        /// 모든 저장 파일 목록 가져오기
        /// </summary>
        public string[] GetAllSaveFiles(string extension = ".json")
        {
            try
            {
                return Directory.GetFiles(SaveDirectory, $"*{extension}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 파일 목록 가져오기 실패: {e.Message}");
                return Array.Empty<string>();
            }
        }

        // ====== 유틸리티 ======

        /// <summary>
        /// 파일 전체 경로 가져오기
        /// </summary>
        public string GetFilePath(string fileName)
        {
            return Path.Combine(SaveDirectory, fileName);
        }

        /// <summary>직전 정상본 전체 경로(<c>{파일명}.prev</c>).</summary>
        public string GetBackupFilePath(string fileName) => GetFilePath(fileName) + BACKUP_SUFFIX;

        /// <summary>임시 파일 전체 경로(<c>{파일명}.tmp</c>). 교체가 같은 볼륨 안에서 일어나도록 본 파일과 같은 폴더다.</summary>
        public string GetTempFilePath(string fileName) => GetFilePath(fileName) + TEMP_SUFFIX;

        /// <summary>
        /// 저장 디렉토리 경로
        /// </summary>
        public string SaveDirectory => saveDirectoryOverride ?? Application.persistentDataPath;

        private static void DeleteQuietly(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] 임시 파일 정리 실패(다음 저장이 덮어쓴다): {e.Message}");
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }

    /// <summary>
    /// 저장 데이터 기본 클래스 (선택적 사용)
    /// </summary>
    [Serializable]
    public abstract class SaveDataBase
    {
        /// <summary>
        /// 저장 시간 (UTC)
        /// </summary>
        public string saveTimeUtc;

        /// <summary>
        /// 저장 버전
        /// </summary>
        public int version = 1;

        /// <summary>
        /// 저장 시간 업데이트
        /// </summary>
        public void UpdateSaveTime()
        {
            saveTimeUtc = DateTime.UtcNow.ToString("O");
        }

        /// <summary>
        /// 저장 시간 가져오기
        /// </summary>
        public DateTime GetSaveTime()
        {
            return DateTime.TryParse(saveTimeUtc, out var time) ? time : DateTime.MinValue;
        }
    }
}

using System;
using System.IO;
using UnityEngine;

namespace SaveSystem_Core
{
    /// <summary>
    /// 범용 JSON 기반 저장 시스템
    /// 파일 I/O만 담당하며 데이터 구조는 프로젝트에서 정의
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
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
        /// 데이터를 JSON 파일로 저장
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

            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint);
                File.WriteAllText(filePath, json);

                Debug.Log($"[SaveSystem] 저장 완료: {filePath}");
                OnSaved?.Invoke(filePath);
                return true;
            }
            catch (Exception e)
            {
                string errorMsg = $"저장 실패: {e.Message}";
                Debug.LogError($"[SaveSystem] {errorMsg}");
                OnSaveFailed?.Invoke(errorMsg);
                return false;
            }
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
        /// JSON 파일에서 데이터 불러오기
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
        /// 저장 파일 삭제
        /// </summary>
        public bool DeleteFile(string fileName)
        {
            string filePath = GetFilePath(fileName);

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Debug.Log($"[SaveSystem] 파일 삭제 완료: {filePath}");
                    return true;
                }
                else
                {
                    Debug.LogWarning($"[SaveSystem] 삭제할 파일이 존재하지 않습니다: {filePath}");
                    return false;
                }
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
                return Directory.GetFiles(Application.persistentDataPath, $"*{extension}");
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
            return Path.Combine(Application.persistentDataPath, fileName);
        }

        /// <summary>
        /// 저장 디렉토리 경로
        /// </summary>
        public string SaveDirectory => Application.persistentDataPath;

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

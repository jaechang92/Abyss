using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CodeConvention.Editor
{
    public static class ConventionFileEdit
    {
        private static string JournalPath => Path.GetFullPath("Library/CodeConventionBackups/last-edit.json");
        public static bool CanRestore => File.Exists(JournalPath);

        [Serializable]
        private sealed class EditRecord
        {
            public string Path;
            public string Backup;
            public string Applied;
        }

        public static string ReadSource(string path)
        {
            RequireAssetScript(path);
            return Decode(File.ReadAllBytes(path));
        }

        [Serializable]
        private sealed class BatchRecord
        {
            public EditRecord[] Records;
        }

        public static void Apply(string path, ConventionRenamePlan plan)
        {
            ApplyBatch(new Dictionary<string, ConventionRenamePlan> { { path, plan } });
        }

        public static void ApplyBatch(IReadOnlyDictionary<string, ConventionRenamePlan> plans)
        {
            if (plans == null || plans.Count == 0) throw new InvalidOperationException("적용할 변경이 없습니다.");
            var originals = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var updates = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            // 모든 파일을 먼저 검증한다. 하나라도 오래된 미리보기면 아무 파일도 변경하지 않는다.
            foreach (var item in plans)
            {
                string path = System.IO.Path.GetFullPath(item.Key);
                RequireAssetScript(path);
                byte[] original = File.ReadAllBytes(path);
                if (item.Value == null || Decode(original) != item.Value.Original)
                    throw new InvalidOperationException("미리보기 후 파일이 바뀌었습니다. 다시 미리보기를 만드세요: " + path);
                bool hasBom = original.Length >= 3 && original[0] == 0xef && original[1] == 0xbb && original[2] == 0xbf;
                var encoding = new UTF8Encoding(hasBom, true);
                originals.Add(path, original);
                updates.Add(path, encoding.GetPreamble().Concat(encoding.GetBytes(item.Value.Updated)).ToArray());
            }
            string directory = Path.GetDirectoryName(JournalPath);
            Directory.CreateDirectory(directory);
            var records = new List<EditRecord>();
            foreach (var item in originals)
            {
                string id = Guid.NewGuid().ToString("N");
                var record = new EditRecord { Path = item.Key,
                    Backup = Path.Combine(directory, id + ".before"), Applied = Path.Combine(directory, id + ".after") };
                File.WriteAllBytes(record.Backup, item.Value);
                File.WriteAllBytes(record.Applied, updates[item.Key]);
                records.Add(record);
            }
            string previousJournal = File.Exists(JournalPath) ? File.ReadAllText(JournalPath) : null;
            File.WriteAllText(JournalPath, JsonUtility.ToJson(new BatchRecord { Records = records.ToArray() }), new UTF8Encoding(false));
            var written = new List<string>();
            try
            {
                foreach (var item in updates)
                {
                    if (!File.ReadAllBytes(item.Key).SequenceEqual(originals[item.Key]))
                        throw new IOException("적용 도중 파일이 변경되었습니다: " + item.Key);
                    ReplaceFile(item.Key, item.Value);
                    written.Add(item.Key);
                }
            }
            catch (Exception error)
            {
                bool isRecovered = true;
                foreach (string path in written.AsEnumerable().Reverse())
                {
                    try
                    {
                        if (!File.ReadAllBytes(path).SequenceEqual(updates[path])) { isRecovered = false; continue; }
                        ReplaceFile(path, originals[path]);
                    }
                    catch (Exception) { isRecovered = false; }
                }
                if (isRecovered)
                {
                    if (previousJournal == null) File.Delete(JournalPath);
                    else File.WriteAllText(JournalPath, previousJournal, new UTF8Encoding(false));
                }
                throw new IOException(error.Message + (isRecovered ? "\n이번 적용은 취소되었습니다." :
                    "\n일부 파일 복원이 필요합니다. Library/CodeConventionBackups의 원본을 확인하세요."), error);
            }
        }

        public static string[] RestoreBatch()
        {
            string json = File.ReadAllText(JournalPath);
            var batch = JsonUtility.FromJson<BatchRecord>(json);
            // 이전 버전의 단일 파일 백업도 계속 복원할 수 있다.
            var records = batch?.Records ?? new[] { JsonUtility.FromJson<EditRecord>(json) };
            var originals = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var expected = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in records)
            {
                if (record == null) throw new InvalidOperationException("복원 기록을 읽을 수 없습니다.");
                RequireAssetScript(record.Path);
                string directory = Path.GetDirectoryName(JournalPath) + Path.DirectorySeparatorChar;
                if (!Path.GetFullPath(record.Backup).StartsWith(directory, StringComparison.OrdinalIgnoreCase) ||
                    !Path.GetFullPath(record.Applied).StartsWith(directory, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("백업 경로가 올바르지 않습니다.");
                byte[] before = File.ReadAllBytes(record.Backup);
                byte[] after = File.ReadAllBytes(record.Applied);
                byte[] current = File.ReadAllBytes(record.Path);
                // 중단된 적용/복원의 재시도도 가능하되, 사용자 후속 편집은 덮어쓰지 않는다.
                if (!current.SequenceEqual(after) && !current.SequenceEqual(before))
                    throw new InvalidOperationException("적용 이후 파일이 수정되어 전체 복원을 중단합니다: " + record.Path);
                originals.Add(record.Path, before);
                expected.Add(record.Path, current);
            }
            foreach (var item in originals)
            {
                if (!File.ReadAllBytes(item.Key).SequenceEqual(expected[item.Key]))
                    throw new IOException("복원 도중 파일이 변경되었습니다. 다시 확인하세요: " + item.Key);
                if (!item.Value.SequenceEqual(expected[item.Key])) ReplaceFile(item.Key, item.Value);
            }
            File.Delete(JournalPath);
            return originals.Keys.ToArray();
        }

        private static string Decode(byte[] bytes)
        {
            int offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
            string text = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
            if (text.Contains("\0")) throw new InvalidOperationException("UTF-8 코드 파일만 자동 수정할 수 있습니다.");
            return text;
        }

        private static void RequireAssetScript(string path)
        {
            string root = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(fullPath), ".cs", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("현재 프로젝트 Assets 안의 C# 파일만 수정할 수 있습니다.");
        }

        private static void ReplaceFile(string path, byte[] bytes)
        {
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temp, bytes);
                File.Replace(temp, path, null);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }
}

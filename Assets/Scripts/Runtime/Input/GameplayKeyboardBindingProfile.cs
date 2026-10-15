using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.Input
{
    public enum KeyBindingError { None, Conflict, Reserved, Invalid, SaveFailed, Unavailable }

    public sealed class KeyboardBindingRow
    {
        public string BindingId { get; }
        public string ActionName { get; }
        public string PartName { get; }
        public string DisplayKey { get; }

        public KeyboardBindingRow(string id, string action, string part, string path)
        {
            BindingId = id;
            ActionName = action;
            PartName = part;
            DisplayKey = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }
    }

    /// <summary>원본 복제·허용 목록·저장 트랜잭션. 저장 delegate로 디스크 없는 테스트가 가능하다.</summary>
    public sealed class GameplayKeyboardBindingProfile : IDisposable
    {
        [Serializable] private sealed class SavedEntries { public Entry[] entries; }
        [Serializable] private sealed class Entry { public string bindingId; public string path; }
        private static readonly HashSet<string> AllowedActions = new HashSet<string>
        { "Move", "Jump", "Dash", "Attack", "AttackHeavy", "Skill1", "Skill2", "FormSwap", "Interact" };
        private static readonly HashSet<string> ReservedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "escape", "enter", "numpadEnter", "tab" };
        private readonly InputActionAsset preview;
        private readonly InputActionAsset sourceAsset;
        private readonly Func<string, bool> save;
        private readonly List<int> editable = new List<int>();
        private readonly Dictionary<string, string> overrides = new Dictionary<string, string>();
        private readonly List<KeyboardBindingRow> rows = new List<KeyboardBindingRow>();
        private readonly IReadOnlyList<KeyboardBindingRow> rowView;
        public IReadOnlyList<KeyboardBindingRow> Rows => rowView;
        public event Action OnBindingsChanged;

        public GameplayKeyboardBindingProfile(InputActionAsset source, string saved, Func<string, bool> saveCallback)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            rowView = rows.AsReadOnly();
            sourceAsset = source;
            preview = UnityEngine.Object.Instantiate(source);
            preview.Disable();
            save = saveCallback;
            var map = preview.FindActionMap("Player");
            if (map == null) throw new ArgumentException("Player map missing", nameof(source));
            for (int i = 0; i < map.bindings.Count; i++)
            {
                var binding = map.bindings[i];
                if (!binding.isComposite && AllowedActions.Contains(binding.action) &&
                    binding.path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase) &&
                    (binding.action != "Move" || binding.isPartOfComposite)) editable.Add(i);
            }
            LoadValidated(saved);
            RebuildRows();
        }

        public bool TrySetKey(string bindingId, string controlPath, out KeyBindingError error)
        {
            error = KeyBindingError.Invalid;
            var map = preview.FindActionMap("Player");
            int index = editable.FindIndex(i => map.bindings[i].id.ToString() == bindingId);
            if (index < 0 || !TryCanonicalPath(controlPath, out string path)) return false;
            if (ReservedKeys.Contains(path.Substring(11))) { error = KeyBindingError.Reserved; return false; }
            int bindingIndex = editable[index];
            for (int i = 0; i < map.bindings.Count; i++)
            {
                if (i != bindingIndex && !map.bindings[i].isComposite &&
                    string.Equals(map.bindings[i].effectivePath, path, StringComparison.OrdinalIgnoreCase))
                { error = KeyBindingError.Conflict; return false; }
            }
            var candidate = new Dictionary<string, string>(overrides) { [bindingId] = path };
            if (string.Equals(path, map.bindings[bindingIndex].path, StringComparison.OrdinalIgnoreCase)) candidate.Remove(bindingId);
            return Commit(candidate, out error);
        }

        public bool ResetKeyboard(out KeyBindingError error) => Commit(new Dictionary<string, string>(), out error);

        public void ApplyTo(InputActionAsset actions)
        {
            if (actions == sourceAsset) return;
            var map = actions?.FindActionMap("Player");
            if (map == null) return;
            // ApplyBindingOverride supports enabled maps; changing map activation
            // here would interfere with pause/draft ownership and cached actions.
            foreach (int sourceIndex in editable)
            {
                var source = preview.FindActionMap("Player").bindings[sourceIndex];
                for (int i = 0; i < map.bindings.Count; i++)
                {
                    if (map.bindings[i].id != source.id) continue;
                    string id = source.id.ToString();
                    if (overrides.TryGetValue(id, out string path)) map.ApplyBindingOverride(i, new InputBinding { overridePath = path });
                    else map.ApplyBindingOverride(i, default(InputBinding));
                    break;
                }
            }
        }

        private bool Commit(Dictionary<string, string> candidate, out KeyBindingError error)
        {
            error = KeyBindingError.SaveFailed;
            string json = Serialize(candidate);
            try { if (save == null || !save(json)) return false; }
            catch (Exception) { return false; }
            overrides.Clear();
            foreach (var entry in candidate) overrides.Add(entry.Key, entry.Value);
            ApplyTo(preview);
            RebuildRows();
            error = KeyBindingError.None;
            OnBindingsChanged?.Invoke();
            return true;
        }

        private void RebuildRows()
        {
            rows.Clear();
            var map = preview.FindActionMap("Player");
            foreach (int i in editable)
            {
                var binding = map.bindings[i];
                rows.Add(new KeyboardBindingRow(binding.id.ToString(), binding.action,
                    binding.isPartOfComposite ? binding.name : string.Empty, binding.effectivePath));
            }
        }

        private void LoadValidated(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 16384) return;
            try
            {
                var saved = JsonUtility.FromJson<SavedEntries>(json);
                if (saved?.entries == null || saved.entries.Length > editable.Count) return;
                var map = preview.FindActionMap("Player");
                var candidate = new Dictionary<string, string>();
                foreach (var entry in saved.entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.bindingId) || candidate.ContainsKey(entry.bindingId) ||
                        !editable.Exists(i => map.bindings[i].id.ToString() == entry.bindingId) ||
                        !TryCanonicalPath(entry.path, out string path) || ReservedKeys.Contains(path.Substring(11))) return;
                    candidate.Add(entry.bindingId, path);
                }
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < map.bindings.Count; i++)
                {
                    var binding = map.bindings[i];
                    string path = candidate.TryGetValue(binding.id.ToString(), out string replacement) ? replacement : binding.path;
                    if (!binding.isComposite && path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase) && !used.Add(path)) return;
                }
                foreach (var entry in candidate) overrides.Add(entry.Key, entry.Value);
                ApplyTo(preview);
            }
            catch (Exception) { overrides.Clear(); }
        }

        private static bool TryCanonicalPath(string path, out string canonical)
        {
            canonical = null;
            if (string.IsNullOrEmpty(path) || !path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase)) return false;
            string key = path.Substring(11);
            var layout = InputSystem.LoadLayout("Keyboard");
            foreach (var control in layout.controls)
            {
                if (string.Equals(control.layout.ToString(), "Key", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(control.name.ToString(), key, StringComparison.OrdinalIgnoreCase))
                { canonical = "<Keyboard>/" + control.name; return true; }
            }
            return false;
        }

        private static string Serialize(Dictionary<string, string> values)
        {
            if (values.Count == 0) return string.Empty;
            var ids = new List<string>(values.Keys);
            ids.Sort(StringComparer.Ordinal);
            var entries = new List<Entry>();
            foreach (string id in ids) entries.Add(new Entry { bindingId = id, path = values[id] });
            return JsonUtility.ToJson(new SavedEntries { entries = entries.ToArray() });
        }

        public void Dispose()
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(preview);
            else UnityEngine.Object.DestroyImmediate(preview);
        }
    }
}

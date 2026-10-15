using System;
using System.Collections.Generic;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Flow;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Abyss.Runtime.Input
{
    /// <summary>기본 키 변경 진입점. 공유 원본은 보존하고 각 런타임 입력 사본에 적용한다.</summary>
    public static class GameplayKeyBindings
    {
        private static GameplayKeyBindingsConfig config;
        private static GameplayKeyboardBindingProfile profile;
        private static MetaSettings loadedSettings;
        private static string loadedJson;
        private static readonly HashSet<InputActionAsset> runtimeAssets = new HashSet<InputActionAsset>();
        private static readonly HashSet<InputActionAsset> ownedPlayerAssets = new HashSet<InputActionAsset>();
        private static readonly KeyboardBindingRow[] EmptyRows = Array.Empty<KeyboardBindingRow>();
        private static bool isApplying;
        public static event Action OnBindingsChanged;
        public static IReadOnlyList<KeyboardBindingRow> Rows => EnsureProfile() ? profile.Rows : EmptyRows;

        public static string GetDisplayKey(string actionName, string partName = "")
        {
            foreach (var row in Rows)
                if (row.ActionName == actionName && row.PartName == partName) return row.DisplayKey;
            return string.Empty;
        }

        public static bool TrySetKey(string bindingId, string controlPath, out KeyBindingError error)
        {
            error = KeyBindingError.Unavailable;
            return EnsureProfile() && profile.TrySetKey(bindingId, controlPath, out error);
        }

        public static bool ResetKeyboard(out KeyBindingError error)
        {
            error = KeyBindingError.Unavailable;
            return EnsureProfile() && profile.ResetKeyboard(out error);
        }

        public static void ApplyTo(InputActionAsset actions)
        {
            if (actions == null || !EnsureProfile() || actions == config.SourceActions || isApplying) return;
            isApplying = true;
            try { runtimeAssets.Add(actions); profile.ApplyTo(actions); }
            finally { isApplying = false; }
        }

        private static bool EnsureProfile()
        {
            if (config == null) config = Resources.Load<GameplayKeyBindingsConfig>("GameplayKeyBindings");
            if (config == null || config.SourceActions == null) return false;
            var settings = MetaSaveService.Instance.Current?.settings;
            string saved = settings?.keyboardBindingOverrides;
            if (profile != null && ReferenceEquals(settings, loadedSettings) && saved == loadedJson) return true;
            bool isReload = profile != null;
            profile?.Dispose();
            loadedSettings = settings;
            loadedJson = saved;
            profile = new GameplayKeyboardBindingProfile(config.SourceActions, loadedJson,
                json => MetaSaveService.Instance.TrySaveKeyboardBindings(json));
            profile.OnBindingsChanged += PublishChanges;
            if (isReload) PublishChanges();
            return true;
        }

        /// <summary>테스트 가드가 메타 메모리를 복원한 뒤 디스크 접근 없이 캐시를 비운다.</summary>
        public static void InvalidateCache()
        {
            profile?.Dispose();
            profile = null;
            loadedSettings = null;
            loadedJson = null;
        }

        /// <summary>메타 재로드·테스트 격리 복원 후 실제 사본까지 갱신한다.</summary>
        public static void ReloadFromSettings()
        {
            profile?.Dispose();
            profile = null;
            if (EnsureProfile()) PublishChanges();
        }

        private static void PublishChanges()
        {
            loadedSettings = MetaSaveService.Instance.Current?.settings;
            loadedJson = loadedSettings?.keyboardBindingOverrides;
            runtimeAssets.RemoveWhere(asset => asset == null);
            foreach (var asset in new List<InputActionAsset>(runtimeAssets)) ApplyTo(asset);
            AttachPlayers();
            OnBindingsChanged?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            InputSystem.onActionChange -= HandleActionChange;
            profile?.Dispose();
            profile = null;
            config = null;
            loadedSettings = null;
            loadedJson = null;
            runtimeAssets.Clear();
            foreach (var asset in ownedPlayerAssets) if (asset != null) UnityEngine.Object.Destroy(asset);
            ownedPlayerAssets.Clear();
            isApplying = false;
            OnBindingsChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            InputSystem.onActionChange += HandleActionChange;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => AttachPlayers(true);

        private static void AttachPlayers(bool isSceneLoad = false)
        {
            if (!EnsureProfile()) return;
            InputRouter router = InputRouter.GetInstanceSafe();
            bool isRunScene = HasActiveRunFlow();
            var activeAssets = new HashSet<InputActionAsset>();
            if (router != null && !isRunScene) router.DetachPlayerActions();
            foreach (var player in PlayerInput.all)
            {
                var actions = player.actions;
                if (actions == null || actions.FindActionMap("Player") == null) continue;
                if (actions == config.SourceActions)
                {
                    // First PlayerInput may retain the imported asset; clone before
                    // Start-time action caches are populated in the loaded scene.
                    actions = UnityEngine.Object.Instantiate(actions);
                    player.actions = actions;
                    ownedPlayerAssets.Add(actions);
                }
                ApplyTo(actions);
                activeAssets.Add(actions);
                if (router != null && isRunScene && player.gameObject.scene == SceneManager.GetActiveScene())
                    router.UsePlayerActions(actions);
            }
            if (isSceneLoad && isRunScene && router != null) router.SwitchMode(InputRouter.InputMode.Gameplay);
            foreach (var asset in new List<InputActionAsset>(ownedPlayerAssets))
            {
                if (asset != null && !activeAssets.Contains(asset))
                {
                    runtimeAssets.Remove(asset);
                    UnityEngine.Object.Destroy(asset);
                    ownedPlayerAssets.Remove(asset);
                }
            }
        }

        private static void HandleActionChange(object changed, InputActionChange change)
        {
            if (profile == null || isApplying || (change != InputActionChange.ActionMapEnabled && change != InputActionChange.ActionEnabled)) return;
            var actions = changed is InputActionMap map ? map.asset : (changed as InputAction)?.actionMap?.asset;
            ApplyTo(actions);
        }

        private static bool HasActiveRunFlow()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.activeInHierarchy && root.GetComponentInChildren<GameFlowController>() != null) return true;
            return false;
        }
    }
}

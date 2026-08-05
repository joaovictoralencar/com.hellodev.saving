using Cysharp.Threading.Tasks;
using HelloDev.Saving.Interfaces;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using UnityEngine.Events;
using Logger = HelloDev.Logging.Logger;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace HelloDev.Saving.Core
{
    /// <summary>
    /// Self-initializing MonoBehaviour wrapper around <see cref="UnifiedSaveManager"/>.
    /// Configures the global <see cref="SaveSerializerService"/> and
    /// <see cref="SaveProviderService"/> and exposes Save/Load for a
    /// configurable test slot.
    ///
    /// This is a standalone setup for testing the save framework and does not
    /// integrate with any bootstrap/DI system. If the project adopts one
    /// later, replace self-initialization with that system's lifecycle hooks.
    ///
    /// [DefaultExecutionOrder(-1000)] ensures this component's Awake/OnEnable
    /// run before other scene scripts' (e.g. ExampleSavableComponent),
    /// since Instance/Manager must exist before scene objects try to
    /// self-register in their own Awake. Registration order relative to
    /// loading no longer matters functionally (see SaveModule.LastLoadedState),
    /// but Instance/Manager still need to exist before anything reads them.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class UnifiedSaveManagerBehaviour : MonoBehaviour
    {
        #region Serialized Fields

#if ODIN_INSPECTOR
        [TabGroup("Main", "Settings")]
        [BoxGroup("Main/Settings/Provider Configuration")]
#else
        [Header("Provider Configuration")]
#endif
        [SerializeField]
        [Tooltip("Subdirectory within Application.persistentDataPath.")]
        private string saveSubdirectory = "Saves";

#if ODIN_INSPECTOR
        [BoxGroup("Main/Settings/Provider Configuration")]
#endif
        [SerializeField]
        [Tooltip("File extension for save files.")]
        private string fileExtension = ".json";

#if ODIN_INSPECTOR
        [BoxGroup("Main/Settings/Provider Configuration")]
#endif
        [SerializeField]
        [Tooltip("If true, saved JSON is formatted for readability (recommended while testing).")]
        private bool prettyPrint = true;

#if ODIN_INSPECTOR
        [BoxGroup("Main/Settings/Lifecycle")]
#else
        [Header("Lifecycle")]
#endif
        [SerializeField]
        [Tooltip("If true, this manager persists across scene loads.")]
        private bool persistent = true;

#if ODIN_INSPECTOR
        [BoxGroup("Main/Settings/Test Slot")]
#else
        [Header("Test Slot")]
#endif
        [SerializeField]
        [Tooltip("Slot key used by SaveTestSlot/LoadTestSlot and auto-load.")]
        private string testSlotKey = "test_save";

#if ODIN_INSPECTOR
        [BoxGroup("Main/Settings/Test Slot")]
#endif
        [SerializeField]
        [Tooltip("If true, automatically loads the test slot on startup.")]
        private bool autoLoadOnStart;

        #endregion

        #region Events

        /// <summary>Fired before a save operation starts.</summary>
        [HideInInspector] public UnityEvent<string> OnBeforeSave = new();

        /// <summary>Fired after a save operation completes.</summary>
        [HideInInspector] public UnityEvent<string, bool> OnAfterSave = new();

        /// <summary>Fired before a load operation starts.</summary>
        [HideInInspector] public UnityEvent<string> OnBeforeLoad = new();

        /// <summary>Fired after a load operation completes.</summary>
        [HideInInspector] public UnityEvent<string, bool> OnAfterLoad = new();

        #endregion

        /// <summary>
        /// Active instance, available once <see cref="Awake"/> has run.
        /// Only one should exist for the whole game (persistent).
        /// </summary>
        public static UnifiedSaveManagerBehaviour Instance { get; private set; }

        /// <summary>
        /// The underlying framework manager. Null until initialization completes.
        /// </summary>
        public IUnifiedSaveManager Manager { get; private set; }

        /// <summary>
        /// The save scheduler.
        /// </summary>
        public SaveScheduler Scheduler { get; private set; }

        public AutoSaveController AutoSaveController { get; private set; }

        /// <summary>
        /// True once provider/serializer are configured and the manager
        /// is ready for modules/savables to register.
        /// </summary>
#if ODIN_INSPECTOR
        [TabGroup("Main", "Diagnostics & Tools")]
        [BoxGroup("Main/Diagnostics & Tools/Runtime Status")]
        [ShowInInspector, ReadOnly]
#endif
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// The slot most recently saved to or loaded from via this behaviour.
        /// Updated automatically whenever <see cref="SaveAsync(string)"/> or
        /// <see cref="LoadAsync(string)"/> is called, and used as the default
        /// target for the parameterless <see cref="SaveAsync()"/>/<see cref="LoadAsync()"/>
        /// overloads and for <c>autoSaveOnDestroy</c>.
        /// </summary>
#if ODIN_INSPECTOR
        [BoxGroup("Main/Diagnostics & Tools/Runtime Status")]
        [ShowInInspector, ReadOnly]
#endif
        public string ActiveSlot { get; private set; }

#if ODIN_INSPECTOR
        [BoxGroup("Main/Diagnostics & Tools/Runtime Status")]
        [ShowInInspector, ReadOnly]
#endif
        public int RegisteredModuleCount => Manager?.Modules.Count ?? 0;

#if ODIN_INSPECTOR
        [BoxGroup("Main/Diagnostics & Tools/Runtime Status")]
        [ShowInInspector, ReadOnly]
#endif
        public int RegisteredSavableCount => Manager?.Modules?.Sum(m => m.Savables?.Count ?? 0) ?? 0;

        /// <summary>
        /// List of registered module IDs for quick inspection.
        /// </summary>
#if ODIN_INSPECTOR
        [BoxGroup("Main/Diagnostics & Tools/Runtime Status")]
        [ShowInInspector, ReadOnly]
#endif
        public System.Collections.Generic.List<string> RegisteredModuleIds => Manager?.Modules?.Select(m => m.ModuleId).ToList() ?? new System.Collections.Generic.List<string>();

        // The full module list is kept for code access but hidden from the inspector.
#if ODIN_INSPECTOR
        [HideInInspector]
#endif
        public System.Collections.Generic.IReadOnlyList<ISaveModule> RegisteredModules => Manager?.Modules?.ToList() ?? new System.Collections.Generic.List<ISaveModule>();

#if ODIN_INSPECTOR
        [BoxGroup("Main/Diagnostics & Tools/Runtime Status")]
        [ShowInInspector, ReadOnly]
#endif
        public string PersistentDataPath => Application.persistentDataPath;

        private bool _shutdownSaveTriggered;

#if ODIN_INSPECTOR
        [BoxGroup("Main/Settings/Autosave")]
#else
[Header("Autosave")]
#endif
        [SerializeField]
        [Tooltip("Slot to save to when autosave is triggered.")]
        private bool enableAutoSave;

#if ODIN_INSPECTOR
        [BoxGroup("Main/Settings/Autosave")]
#endif
        [SerializeField]
        [Tooltip("Delay before an autosave executes after being requested.")]
        private float autoSaveDelay = 5f;

        public float AutoSaveDelay => autoSaveDelay;
        public bool EnableAutoSave => enableAutoSave;

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Logger.LogWarning("Save", "Duplicate UnifiedSaveManagerBehaviour found; destroying this one.");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (persistent)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private async void OnEnable()
        {
            if (IsInitialized)
                return;

            await InitializeCoreAsync();
        }

        private void OnDestroy()
        {
            Scheduler?.Dispose();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Initialization

        private async UniTask InitializeCoreAsync()
        {
            Logger.Log("Save", "UnifiedSaveManagerBehaviour initializing...");

            SaveSerializerService.SetSerializer(new JsonSaveSerializer(prettyPrint));
            SaveProviderService.SetProvider(new JsonSaveProvider(saveSubdirectory, fileExtension, prettyPrint));

            Manager = new UnifiedSaveManager();

            Scheduler = new SaveScheduler(Manager, autoSaveDelay);

            Scheduler.SaveStarted += slot => OnBeforeSave?.Invoke(slot);
            Scheduler.SaveCompleted += (slot, success) => OnAfterSave?.Invoke(slot, success);
            Scheduler.LoadStarted += slot => OnBeforeLoad?.Invoke(slot);
            Scheduler.LoadCompleted += (slot, success) => OnAfterLoad?.Invoke(slot, success);

            IsInitialized = true;
            Logger.Log("Save", "UnifiedSaveManagerBehaviour initialized.");

            if (autoLoadOnStart && !string.IsNullOrEmpty(testSlotKey))
            {
                bool success = await LoadAsync(testSlotKey);

                if (!success)
                {
                    Logger.LogWarning("Save", $"Auto-load from '{testSlotKey}' failed or no save was found.");
                }
            }
        }

        #endregion

        #region Save / Load

        /// <summary>
        /// Saves to the given slot. Updates <see cref="ActiveSlot"/> to <paramref name="slot"/>.
        /// </summary>
        public async UniTask<bool> SaveAsync(string slot)
        {
            if (!IsInitialized)
            {
                Logger.LogError("Save", "Cannot save before initialization completes.");
                return false;
            }

            ActiveSlot = slot;

            return await Scheduler.SaveAsync(slot);
        }

        /// <summary>
        /// Saves to <see cref="ActiveSlot"/>.
        /// </summary>
        public UniTask<bool> SaveAsync()
        {
            if (string.IsNullOrEmpty(ActiveSlot))
            {
                Logger.LogError("Save", "Cannot save: no ActiveSlot is set.");
                return UniTask.FromResult(false);
            }

            return SaveAsync(ActiveSlot);
        }

        /// <summary>
        /// Loads from the given slot. Updates <see cref="ActiveSlot"/> to <paramref name="slot"/>.
        /// Safe to call more than once (e.g. a newly loaded scene re-requesting
        /// load so its own just-registered savables pick up matching data) —
        /// repeated calls for the same slot reuse the cached state instead of
        /// re-reading from disk unless <paramref name="forceReload"/> is true.
        /// </summary>
        public async UniTask<bool> LoadAsync(string slot, bool forceReload = false)
        {
            if (!IsInitialized)
            {
                Logger.LogError("Save", "Cannot load before initialization completes.");
                return false;
            }

            ActiveSlot = slot;
            if (enableAutoSave)
            {
                AutoSaveController ??= new AutoSaveController(Scheduler, ActiveSlot, autoSaveDelay);
                AutoSaveController.Start();
            }

            return await Scheduler.LoadAsync(slot, forceReload);
        }

        /// <summary>
        /// Loads from <see cref="ActiveSlot"/>.
        /// </summary>
        public UniTask<bool> LoadAsync(bool forceReload = false)
        {
            if (string.IsNullOrEmpty(ActiveSlot))
            {
                Logger.LogError("Save", "Cannot load: no ActiveSlot is set.");
                return UniTask.FromResult(false);
            }

            return LoadAsync(ActiveSlot, forceReload);
        }

        public void RequestAutoSave()
        {
            if (!IsInitialized)
            {
                Logger.LogWarning("Save", "Cannot request autosave before initialization.");
                return;
            }

            if (string.IsNullOrEmpty(ActiveSlot))
            {
                Logger.LogWarning("Save", "Cannot autosave: no ActiveSlot is set.");
                return;
            }

            Scheduler.RequestAutoSave(ActiveSlot);
        }

        /// <summary>
        /// Saves to the configured test slot. Hook this up to a UI Button for
        /// quick manual testing.
        /// </summary>
#if ODIN_INSPECTOR
        [BoxGroup("Main/Diagnostics & Tools/Test Actions")]
        [ButtonGroup("Main/Diagnostics & Tools/Test Actions/Buttons")]
        [Button(ButtonSizes.Large)]
        [GUIColor(0.6f, 0.9f, 0.6f)] // Soft Green
#endif
        public void SaveTestSlot()
        {
            SaveAsync(testSlotKey).Forget();
        }

        /// <summary>
        /// Loads from the configured test slot. Hook this up to a UI Button for
        /// quick manual testing.
        /// </summary>
#if ODIN_INSPECTOR
        [ButtonGroup("Main/Diagnostics & Tools/Test Actions/Buttons")]
        [Button(ButtonSizes.Large)]
        [GUIColor(0.6f, 0.8f, 1f)] // Soft Blue
#endif
        public void LoadTestSlot()
        {
            LoadAsync(testSlotKey).Forget();
        }

        #endregion

        #region File Utilities

#if ODIN_INSPECTOR
        [BoxGroup("Main/Diagnostics & Tools/File Operations")]
        [Button(ButtonSizes.Medium)]
#endif
        public void OpenSaveFolder()
        {
            string path = Path.Combine(Application.persistentDataPath, saveSubdirectory);
            Directory.CreateDirectory(path); // ensure it exists

#if UNITY_STANDALONE_WIN
            Process.Start("explorer.exe", path.Replace("/", "\\"));
#elif UNITY_STANDALONE_OSX
            Process.Start("open", path);
#elif UNITY_STANDALONE_LINUX
            Process.Start("xdg-open", path);
#else
            Application.OpenURL("file://" + path);
#endif
        }

#if ODIN_INSPECTOR
        [BoxGroup("Main/Diagnostics & Tools/File Operations")]
        [Button(ButtonSizes.Medium)]
        [GUIColor(1f, 0.6f, 0.6f)] // Soft Red
#endif
        public async void DeleteTestSaveAsync()
        {
            if (!IsInitialized)
            {
                Logger.LogWarning("Save", "Cannot delete test save: manager not initialized.");
                return;
            }

            bool success = await SaveProviderService.Provider.DeleteAsync(testSlotKey);
            if (success)
                Logger.Log("Save", $"Test save slot '{testSlotKey}' deleted successfully.");
            else
                Logger.LogWarning("Save", $"Failed to delete test save slot '{testSlotKey}'.");
        }

#if ODIN_INSPECTOR
        [BoxGroup("Main/Diagnostics & Tools/File Operations")]
        [Button(ButtonSizes.Medium)]
        [GUIColor(1f, 0.6f, 0.6f)] // Soft Red
#endif
        public async void DeleteActiveSaveAsync()
        {
            if (string.IsNullOrEmpty(ActiveSlot))
            {
                Logger.LogWarning("Save", "Cannot delete active save: ActiveSlot is null or empty.");
                return;
            }

            bool success = await SaveProviderService.Provider.DeleteAsync(ActiveSlot);
            if (success)
                Logger.Log("Save", $"Active save slot '{ActiveSlot}' deleted successfully.");
            else
                Logger.LogWarning("Save", $"Failed to delete active save slot '{ActiveSlot}'.");
        }

        #endregion
    }
}
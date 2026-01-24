using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HelloDev.Bootstrap;
using HelloDev.Logging;
using HelloDev.Utils;
using UnityEngine;
using UnityEngine.Events;
using Logger = HelloDev.Logging.Logger;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace HelloDev.Saving
{
    /// <summary>
    /// Central manager that coordinates saving/loading of all registered ISaveableSystem implementations.
    /// Produces a single unified save file per slot containing all system snapshots.
    /// Implements IBootstrapInitializable for coordinated initialization (priority 50).
    /// </summary>
    public class UnifiedSaveManager : MonoBehaviour, IBootstrapInitializable
    {
        #region Serialized Fields

#if ODIN_INSPECTOR
        [Title("Configuration")]
        [Required]
#else
        [Header("Configuration")]
#endif
        [SerializeField]
        [Tooltip("Global save system settings. Required.")]
        private SaveSystemSettings_SO settings;

#if ODIN_INSPECTOR
        [Title("Initialization")]
        [ToggleLeft]
        [InfoBox("Disable when using GameBootstrap for coordinated initialization.")]
#else
        [Header("Initialization")]
#endif
        [SerializeField]
        [Tooltip("If true, self-initializes in OnEnable. Disable when using GameBootstrap.")]
        private bool selfInitialize = true;

#if ODIN_INSPECTOR
        [ToggleLeft]
#endif
        [SerializeField]
        [Tooltip("If true, this manager persists across scene loads.")]
        private bool persistent = true;

#if ODIN_INSPECTOR
        [Title("Auto Save/Load")]
#else
        [Header("Auto Save/Load")]
#endif
        [SerializeField]
        [Tooltip("Default slot key used for auto-save/load operations.")]
        private string defaultSlotKey = "autosave";

        [SerializeField]
        [Tooltip("If true, automatically loads from the default slot on startup.")]
        private bool autoLoadOnStart;

        [SerializeField]
        [Tooltip("If true, automatically saves to the default slot when the application quits.")]
        private bool autoSaveOnQuit;

        [SerializeField]
        [Tooltip("If true, automatically saves when the application loses focus (useful for mobile).")]
        private bool autoSaveOnPause;

        [SerializeField]
        [Tooltip("If greater than 0, automatically saves at this interval in seconds.")]
        [Min(0f)]
        private float autoSaveInterval;

        #endregion

        #region Private Fields

        private readonly List<ISaveableSystem> _registeredSystems = new();
        private GameContext _context;
        private bool _isInitialized;
        private float _autoSaveTimer;

        #endregion

        #region Events

        /// <summary>
        /// Fired before a save operation starts.
        /// </summary>
        [HideInInspector]
        public UnityEvent<string> OnBeforeSave = new();

        /// <summary>
        /// Fired after a save operation completes.
        /// </summary>
        [HideInInspector]
        public UnityEvent<string, bool> OnAfterSave = new();

        /// <summary>
        /// Fired before a load operation starts.
        /// </summary>
        [HideInInspector]
        public UnityEvent<string> OnBeforeLoad = new();

        /// <summary>
        /// Fired after a load operation completes.
        /// </summary>
        [HideInInspector]
        public UnityEvent<string, bool> OnAfterLoad = new();

        /// <summary>
        /// Fired when a system registers.
        /// </summary>
        [HideInInspector]
        public UnityEvent<ISaveableSystem> OnSystemRegistered = new();

        /// <summary>
        /// Fired when a system unregisters.
        /// </summary>
        [HideInInspector]
        public UnityEvent<ISaveableSystem> OnSystemUnregistered = new();

        #endregion

        #region Properties

        /// <summary>
        /// Gets the save system settings.
        /// </summary>
        public SaveSystemSettings_SO Settings => settings;

        /// <summary>
        /// Gets all registered saveable systems.
        /// </summary>
        public IReadOnlyList<ISaveableSystem> RegisteredSystems => _registeredSystems;

        /// <summary>
        /// Gets whether a save provider has been configured.
        /// </summary>
        public bool HasProvider => SaveService.IsConfigured;

        /// <summary>
        /// Gets the default slot key for auto-save/load.
        /// </summary>
        public string DefaultSlotKey => defaultSlotKey;

        #endregion

        #region IBootstrapInitializable

        /// <summary>
        /// Whether this manager should self-initialize.
        /// </summary>
        public bool SelfInitialize
        {
            get => selfInitialize;
            set => selfInitialize = value;
        }

        /// <summary>
        /// Priority 50 - Core services phase. Runs early so other systems can register.
        /// </summary>
        public int InitializationPriority => 50;

        /// <summary>
        /// Whether this manager has completed initialization.
        /// </summary>
        bool IBootstrapInitializable.IsInitialized => _isInitialized;

        /// <summary>
        /// Receives the game context from GameBootstrap.
        /// </summary>
        /// <param name="context">The game context for service registration.</param>
        public void ReceiveContext(GameContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Initializes the save manager and configures the global SaveService.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            Logger.Log(LogSystems.Save, "UnifiedSaveManager starting initialization...");

            if (settings == null)
            {
                Logger.LogError(LogSystems.Save, "SaveSystemSettings_SO not assigned. Cannot initialize.");
                return;
            }

            // Configure global SaveService with our settings
            settings.ConfigureSaveService();
            Logger.Log(LogSystems.Save, $"SaveService configured: {settings.SaveSubdirectory}/{settings.FileExtension}");

            // Self-register to context
            _context?.Register<UnifiedSaveManager>(this);

            _isInitialized = true;
            _autoSaveTimer = autoSaveInterval;

            Logger.Log(LogSystems.Save, "UnifiedSaveManager initialized.");

            // Auto-load AFTER all bootstrap systems are ready (so all snapshot providers are registered)
            if (autoLoadOnStart && !string.IsNullOrEmpty(defaultSlotKey))
            {
                if (GameBootstrap.IsReady)
                {
                    // Bootstrap already complete, load immediately
                    await AutoLoadAsync();
                }
                else
                {
                    // Subscribe to bootstrap complete event
                    GameBootstrap.OnBootstrapComplete += OnBootstrapCompleteAutoLoad;
                }
            }
        }

        /// <summary>
        /// Handler for auto-load after bootstrap completes.
        /// </summary>
        private async void OnBootstrapCompleteAutoLoad()
        {
            GameBootstrap.OnBootstrapComplete -= OnBootstrapCompleteAutoLoad;
            await AutoLoadAsync();
        }

        /// <summary>
        /// Shuts down the save manager.
        /// </summary>
        public void Shutdown()
        {
            _context?.Unregister<UnifiedSaveManager>();
            _registeredSystems.Clear();
            _isInitialized = false;
            Logger.Log(LogSystems.Save, "UnifiedSaveManager shutdown.");
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (persistent)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            if (selfInitialize && !_isInitialized)
            {
                _ = InitializeAsync();
            }
        }

        private void Update()
        {
            if (!_isInitialized || autoSaveInterval <= 0f) return;

            _autoSaveTimer -= Time.deltaTime;
            if (_autoSaveTimer <= 0f)
            {
                _autoSaveTimer = autoSaveInterval;
                _ = AutoSaveAsync("interval");
            }
        }

        private void OnApplicationQuit()
        {
            if (_isInitialized && autoSaveOnQuit && !string.IsNullOrEmpty(defaultSlotKey))
            {
                AutoSaveSync("quit");
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (_isInitialized && autoSaveOnPause && pauseStatus && !string.IsNullOrEmpty(defaultSlotKey))
            {
                _ = AutoSaveAsync("pause");
            }
        }

        #endregion

        #region System Registration

        /// <summary>
        /// Registers a saveable system. Systems are saved/loaded in priority order.
        /// </summary>
        /// <param name="system">The system to register.</param>
        public void RegisterSystem(ISaveableSystem system)
        {
            if (system == null)
            {
                Logger.LogWarning(LogSystems.Save, "Cannot register null system.");
                return;
            }

            if (_registeredSystems.Any(s => s.SystemKey == system.SystemKey))
            {
                Logger.LogWarning(LogSystems.Save, $"System '{system.SystemKey}' already registered. Skipping.");
                return;
            }

            _registeredSystems.Add(system);
            _registeredSystems.Sort((a, b) => a.SavePriority.CompareTo(b.SavePriority));

            OnSystemRegistered?.Invoke(system);
            Logger.Log(LogSystems.Save, $"Registered saveable system: {system.SystemKey} (priority: {system.SavePriority})");
        }

        /// <summary>
        /// Unregisters a saveable system.
        /// </summary>
        /// <param name="system">The system to unregister.</param>
        public void UnregisterSystem(ISaveableSystem system)
        {
            if (system == null) return;

            bool removed = _registeredSystems.RemoveAll(s => s.SystemKey == system.SystemKey) > 0;
            if (removed)
            {
                OnSystemUnregistered?.Invoke(system);
                Logger.Log(LogSystems.Save, $"Unregistered saveable system: {system.SystemKey}");
            }
        }

        /// <summary>
        /// Gets a registered system by key.
        /// </summary>
        /// <param name="systemKey">The system key.</param>
        /// <returns>The system, or null if not found.</returns>
        public ISaveableSystem GetSystem(string systemKey)
        {
            return _registeredSystems.FirstOrDefault(s => s.SystemKey == systemKey);
        }

        #endregion

        #region Snapshot Operations

        /// <summary>
        /// Captures all system snapshots into a unified snapshot.
        /// </summary>
        /// <returns>A unified snapshot containing all system data.</returns>
        public UnifiedSnapshot CaptureUnifiedSnapshot()
        {
            var snapshot = new UnifiedSnapshot
            {
                Version = settings.CurrentVersion,
                Timestamp = DateTime.UtcNow.ToString("O")
            };

            foreach (var system in _registeredSystems)
            {
                try
                {
                    system.OnBeforeSave();
                    var systemSnapshot = system.CaptureSnapshot();

                    if (systemSnapshot != null)
                    {
                        var entry = new SystemSnapshotEntry
                        {
                            Key = system.SystemKey,
                            TypeName = system.SnapshotType.AssemblyQualifiedName,
                            JsonData = JsonUtility.ToJson(systemSnapshot, settings.PrettyPrint)
                        };
                        snapshot.Systems.Add(entry);

                        Logger.LogVerbose(LogSystems.Save, $"Captured snapshot for system: {system.SystemKey}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(LogSystems.Save, $"Failed to capture {system.SystemKey}: {ex.Message}");
                }
            }

            Logger.Log(LogSystems.Save, $"Captured unified snapshot with {snapshot.Systems.Count} systems");
            return snapshot;
        }

        /// <summary>
        /// Restores all systems from a unified snapshot.
        /// </summary>
        /// <param name="snapshot">The snapshot to restore from.</param>
        /// <returns>True if all systems restored successfully.</returns>
        public bool RestoreUnifiedSnapshot(UnifiedSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Logger.LogWarning(LogSystems.Save, "Cannot restore null snapshot.");
                return false;
            }

            // Notify all systems before load
            foreach (var system in _registeredSystems)
            {
                system.OnBeforeLoad();
            }

            bool allSuccess = true;
            int restoredCount = 0;

            foreach (var system in _registeredSystems)
            {
                var entry = snapshot.FindSystem(system.SystemKey);
                if (entry == null)
                {
                    Logger.LogVerbose(LogSystems.Save, $"No data for system '{system.SystemKey}' in snapshot.");
                    system.OnAfterLoad(false);
                    continue;
                }

                try
                {
                    var type = Type.GetType(entry.TypeName);
                    if (type == null)
                    {
                        Logger.LogError(LogSystems.Save, $"Cannot find type: {entry.TypeName}");
                        system.OnAfterLoad(false);
                        allSuccess = false;
                        continue;
                    }

                    var systemSnapshot = JsonUtility.FromJson(entry.JsonData, type);
                    bool success = system.RestoreSnapshot(systemSnapshot);
                    system.OnAfterLoad(success);

                    if (success)
                    {
                        restoredCount++;
                        Logger.LogVerbose(LogSystems.Save, $"Restored snapshot for system: {system.SystemKey}");
                    }
                    else
                    {
                        allSuccess = false;
                        Logger.LogWarning(LogSystems.Save, $"System '{system.SystemKey}' reported restore failure.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(LogSystems.Save, $"Failed to restore {system.SystemKey}: {ex.Message}");
                    system.OnAfterLoad(false);
                    allSuccess = false;
                }
            }

            Logger.Log(LogSystems.Save, $"Restored {restoredCount}/{_registeredSystems.Count} systems from unified snapshot");
            return allSuccess;
        }

        #endregion

        #region Save/Load Operations

        /// <summary>
        /// Saves all registered systems to the specified slot.
        /// </summary>
        /// <param name="slotKey">The save slot identifier.</param>
        /// <returns>True if save was successful.</returns>
        public async Task<bool> SaveAsync(string slotKey)
        {
            if (!SaveService.IsConfigured)
            {
                Logger.LogError(LogSystems.Save, "No provider configured. Cannot save.");
                return false;
            }

            Logger.Log(LogSystems.Save, $"Saving to slot: {slotKey}");
            OnBeforeSave?.Invoke(slotKey);

            var snapshot = CaptureUnifiedSnapshot();
            snapshot.Metadata.SlotKey = slotKey;
            snapshot.Metadata.Timestamp = snapshot.Timestamp;

            bool success = await SaveService.Provider.SaveAsync(slotKey, snapshot);

            // Notify systems after save
            foreach (var system in _registeredSystems)
            {
                system.OnAfterSave(success);
            }

            OnAfterSave?.Invoke(slotKey, success);
            Logger.Log(LogSystems.Save, $"Save to '{slotKey}': {(success ? "success" : "failed")}");

            return success;
        }

        /// <summary>
        /// Loads all registered systems from the specified slot.
        /// </summary>
        /// <param name="slotKey">The save slot identifier.</param>
        /// <returns>True if load was successful.</returns>
        public async Task<bool> LoadAsync(string slotKey)
        {
            if (!SaveService.IsConfigured)
            {
                Logger.LogError(LogSystems.Save, "No provider configured. Cannot load.");
                return false;
            }

            Logger.Log(LogSystems.Save, $"Loading from slot: {slotKey}");
            OnBeforeLoad?.Invoke(slotKey);

            var snapshot = await SaveService.Provider.LoadAsync<UnifiedSnapshot>(slotKey);
            if (snapshot == null)
            {
                Logger.LogWarning(LogSystems.Save, $"No save found at '{slotKey}'.");
                OnAfterLoad?.Invoke(slotKey, false);
                return false;
            }

            bool success = RestoreUnifiedSnapshot(snapshot);

            OnAfterLoad?.Invoke(slotKey, success);
            Logger.Log(LogSystems.Save, $"Load from '{slotKey}': {(success ? "success" : "partial/failed")}");

            return success;
        }

        /// <summary>
        /// Checks if a save slot exists.
        /// </summary>
        /// <param name="slotKey">The save slot identifier.</param>
        /// <returns>True if the slot exists.</returns>
        public async Task<bool> SaveExistsAsync(string slotKey)
        {
            if (!SaveService.IsConfigured) return false;
            return await SaveService.Provider.ExistsAsync(slotKey);
        }

        /// <summary>
        /// Deletes a save slot.
        /// </summary>
        /// <param name="slotKey">The save slot identifier.</param>
        /// <returns>True if deletion was successful.</returns>
        public async Task<bool> DeleteSaveAsync(string slotKey)
        {
            if (!SaveService.IsConfigured) return false;

            bool success = await SaveService.Provider.DeleteAsync(slotKey);
            Logger.Log(LogSystems.Save, $"Delete '{slotKey}': {(success ? "success" : "failed")}");

            return success;
        }

        /// <summary>
        /// Gets metadata for a save slot without loading the full snapshot.
        /// </summary>
        /// <param name="slotKey">The save slot identifier.</param>
        /// <returns>The metadata, or null if not found.</returns>
        public async Task<UnifiedSnapshotMetadata> GetMetadataAsync(string slotKey)
        {
            if (!SaveService.IsConfigured) return null;

            var snapshot = await SaveService.Provider.LoadAsync<UnifiedSnapshot>(slotKey);
            return snapshot?.Metadata;
        }

        #endregion

        #region Auto Save/Load

        private async Task AutoLoadAsync()
        {
            if (string.IsNullOrEmpty(defaultSlotKey))
            {
                Logger.LogWarning(LogSystems.Save, "Auto-load enabled but no slot specified.");
                return;
            }

            bool exists = await SaveExistsAsync(defaultSlotKey);
            if (!exists)
            {
                Logger.Log(LogSystems.Save, $"No save file found for slot '{defaultSlotKey}', skipping auto-load.");
                return;
            }

            Logger.Log(LogSystems.Save, $"Auto-loading from slot '{defaultSlotKey}'...");
            bool success = await LoadAsync(defaultSlotKey);
            if (!success)
            {
                Logger.LogWarning(LogSystems.Save, $"Auto-load from '{defaultSlotKey}' failed.");
            }
        }

        private async Task AutoSaveAsync(string trigger)
        {
            if (string.IsNullOrEmpty(defaultSlotKey))
            {
                Logger.LogWarning(LogSystems.Save, "Auto-save triggered but no slot specified.");
                return;
            }

            Logger.Log(LogSystems.Save, $"Auto-saving to slot '{defaultSlotKey}' (trigger: {trigger})...");
            bool success = await SaveAsync(defaultSlotKey);

            if (success)
            {
                Logger.Log(LogSystems.Save, $"Auto-save to '{defaultSlotKey}' successful.");
            }
            else
            {
                Logger.LogWarning(LogSystems.Save, $"Auto-save to '{defaultSlotKey}' failed.");
            }
        }

        private void AutoSaveSync(string trigger)
        {
            if (string.IsNullOrEmpty(defaultSlotKey))
            {
                Logger.LogWarning(LogSystems.Save, "Auto-save triggered but no slot specified.");
                return;
            }

            Logger.Log(LogSystems.Save, $"Auto-saving to slot '{defaultSlotKey}' (trigger: {trigger})...");

            // Capture snapshot and save synchronously for quit scenarios
            var snapshot = CaptureUnifiedSnapshot();
            if (snapshot != null)
            {
                snapshot.Metadata.SlotKey = defaultSlotKey;
                snapshot.Metadata.Timestamp = snapshot.Timestamp;

                var task = SaveService.Provider.SaveAsync(defaultSlotKey, snapshot);
                task.Wait();

                if (task.Result)
                {
                    Logger.Log(LogSystems.Save, $"Auto-save to '{defaultSlotKey}' successful.");
                }
                else
                {
                    Logger.LogWarning(LogSystems.Save, $"Auto-save to '{defaultSlotKey}' failed.");
                }
            }
        }

        #endregion

        #region Debug

#if ODIN_INSPECTOR && UNITY_EDITOR
        private const string DEBUG_SLOT = "unified_debug_save";

        [Title("Debug - Status")]
        [ShowInInspector, ReadOnly]
        [PropertyOrder(199)]
        private bool IsInitialized => _isInitialized;

        [ShowInInspector, ReadOnly]
        [PropertyOrder(200)]
        private bool ProviderConfigured => SaveService.IsConfigured;

        [ShowInInspector, ReadOnly]
        [PropertyOrder(202)]
        private int RegisteredSystemCount => _registeredSystems.Count;

        [ShowInInspector, ReadOnly]
        [PropertyOrder(203)]
        private string RegisteredSystemKeys => string.Join(", ", _registeredSystems.Select(s => s.SystemKey));

        [ShowInInspector, ReadOnly]
        [PropertyOrder(204)]
        [ShowIf("@autoSaveInterval > 0")]
        private float TimeUntilNextAutoSave => _autoSaveTimer;

        [ShowInInspector, ReadOnly]
        [PropertyOrder(205)]
        private string AutoSaveConfig => $"Load:{(autoLoadOnStart ? "Y" : "N")} Quit:{(autoSaveOnQuit ? "Y" : "N")} Pause:{(autoSaveOnPause ? "Y" : "N")} Interval:{(autoSaveInterval > 0 ? $"{autoSaveInterval}s" : "Off")}";

        [Title("Debug - Operations")]
        [Button("Quick Save", ButtonSizes.Medium)]
        [PropertyOrder(210)]
        [GUIColor(0.4f, 0.8f, 0.4f)]
        private async void DebugQuickSave()
        {
            if (!SaveService.IsConfigured)
            {
                Logger.LogError(LogSystems.Save, "No provider configured");
                return;
            }

            var success = await SaveAsync(DEBUG_SLOT);
            Debug.Log($"<color={(success ? "#90EE90" : "#FF6B6B")}>Unified save: {(success ? "success" : "failed")}</color>");
        }

        [Button("Quick Load", ButtonSizes.Medium)]
        [PropertyOrder(211)]
        [GUIColor(0.4f, 0.6f, 0.9f)]
        private async void DebugQuickLoad()
        {
            if (!SaveService.IsConfigured)
            {
                Logger.LogError(LogSystems.Save, "No provider configured");
                return;
            }

            var success = await LoadAsync(DEBUG_SLOT);
            Debug.Log($"<color={(success ? "#90EE90" : "#FF6B6B")}>Unified load: {(success ? "success" : "failed")}</color>");
        }

        [Button("Log Registered Systems", ButtonSizes.Medium)]
        [PropertyOrder(212)]
        private void DebugLogSystems()
        {
            Debug.Log($"<color=#A8D8EA><b>=== REGISTERED SYSTEMS ({_registeredSystems.Count}) ===</b></color>");
            foreach (var system in _registeredSystems)
            {
                Debug.Log($"  [{system.SavePriority}] {system.SystemKey} ({system.SnapshotType.Name})");
            }
        }

        [Button("Open Save Folder", ButtonSizes.Medium)]
        [PropertyOrder(213)]
        [GUIColor(0.8f, 0.8f, 0.4f)]
        private void DebugOpenSaveFolder()
        {
            if (settings == null)
            {
                Logger.LogError(LogSystems.Save, "No settings configured");
                return;
            }

            string path = settings.SaveDirectoryPath;
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            UnityEditor.EditorUtility.RevealInFinder(path);
        }

        [Button("Delete Debug Save", ButtonSizes.Medium)]
        [PropertyOrder(220)]
        [GUIColor(0.9f, 0.4f, 0.4f)]
        private async void DebugDeleteSave()
        {
            if (!SaveService.IsConfigured)
            {
                Logger.LogError(LogSystems.Save, "No provider configured");
                return;
            }

            var success = await DeleteSaveAsync(DEBUG_SLOT);
            Debug.Log($"<color={(success ? "#90EE90" : "#FF6B6B")}>Delete: {(success ? "success" : "failed")}</color>");
        }
#endif

        #endregion
    }
}

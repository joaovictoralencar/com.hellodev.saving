using UnityEngine;
using UnityEngine.Events;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace HelloDev.Saving
{
    /// <summary>
    /// Global configuration for the unified save system.
    /// Create one instance and reference it from UnifiedSaveManager.
    /// All save-related settings are centralized here including optional slot management.
    /// </summary>
    [CreateAssetMenu(fileName = "SaveSystemSettings", menuName = "HelloDev/Settings/Save System Settings")]
    public class SaveSystemSettings_SO : ScriptableObject
    {
        #region Provider Configuration

#if ODIN_INSPECTOR
        [Title("Provider Configuration")]
#else
        [Header("Provider Configuration")]
#endif
        [SerializeField]
        [Tooltip("Subdirectory within Application.persistentDataPath for save files.")]
        private string saveSubdirectory = "Saves";

        [SerializeField]
        [Tooltip("File extension for save files (e.g., '.save', '.json').")]
        private string fileExtension = ".save";

        [SerializeField]
        [Tooltip("If true, JSON output is formatted for readability. Larger files but easier to debug.")]
        private bool prettyPrint = true;

        #endregion

        #region Versioning

#if ODIN_INSPECTOR
        [Title("Versioning")]
#else
        [Header("Versioning")]
#endif
        [SerializeField]
        [Tooltip("Current save format version. Increment when making breaking changes to enable migration.")]
        private int currentVersion = 1;

        #endregion

        #region Migration

#if ODIN_INSPECTOR
        [Title("Migration")]
#else
        [Header("Migration")]
#endif
        [SerializeField]
        [Tooltip("If true, automatically migrates legacy per-system saves to unified format on load.")]
        private bool autoMigrateLegacySaves = true;

        [SerializeField]
        [Tooltip("If true, deletes legacy save files after successful migration to unified format.")]
        private bool deleteLegacyAfterMigration = false;

        #endregion

        #region Slot Configuration

#if ODIN_INSPECTOR
        [Title("Slot Configuration")]
        [ToggleLeft]
#else
        [Header("Slot Configuration")]
#endif
        [SerializeField]
        [Tooltip("Enable slot-based saves (e.g., save-0, save-1). Disable for single-file saves.")]
        private bool useSaveSlots = true;

#if ODIN_INSPECTOR
        [ShowIf("useSaveSlots")]
#endif
        [SerializeField]
        [Tooltip("Maximum number of save slots available (1-indexed in UI, 0-indexed internally).")]
        [Min(1)]
        private int maxSlots = 3;

#if ODIN_INSPECTOR
        [ShowIf("useSaveSlots")]
#endif
        [SerializeField]
        [Tooltip("Prefix for manual save slot names (e.g., 'save' -> 'save-0', 'save-1').")]
        private string manualSavePrefix = "save";

#if ODIN_INSPECTOR
        [ShowIf("useSaveSlots")]
#endif
        [SerializeField]
        [Tooltip("Prefix for autosave slot names (e.g., 'autosave' -> 'autosave-0', 'autosave-1').")]
        private string autosavePrefix = "autosave";

        #endregion

        #region Runtime State

        /// <summary>
        /// The currently active slot index (0-based). -1 means no slot is active.
        /// </summary>
        private int _currentSlotIndex = -1;

        #endregion

        #region Slot Events

        /// <summary>
        /// Fired when the active slot changes. Parameters: (previousIndex, newIndex)
        /// </summary>
        [System.NonSerialized]
        public UnityEvent<int, int> OnSlotChanged = new();

        #endregion

        #region Properties

        /// <summary>
        /// Subdirectory within Application.persistentDataPath for save files.
        /// </summary>
        public string SaveSubdirectory => saveSubdirectory;

        /// <summary>
        /// File extension for save files.
        /// </summary>
        public string FileExtension => fileExtension;

        /// <summary>
        /// Whether JSON output is formatted for readability.
        /// </summary>
        public bool PrettyPrint => prettyPrint;

        /// <summary>
        /// Current save format version for migration support.
        /// </summary>
        public int CurrentVersion => currentVersion;

        /// <summary>
        /// Whether to automatically migrate legacy saves.
        /// </summary>
        public bool AutoMigrateLegacySaves => autoMigrateLegacySaves;

        /// <summary>
        /// Whether to delete legacy files after migration.
        /// </summary>
        public bool DeleteLegacyAfterMigration => deleteLegacyAfterMigration;

        /// <summary>
        /// Gets the full save directory path.
        /// </summary>
        public string SaveDirectoryPath => System.IO.Path.Combine(Application.persistentDataPath, saveSubdirectory);

        // Slot Properties

        /// <summary>
        /// Gets whether slot-based saves are enabled.
        /// </summary>
        public bool UseSaveSlots => useSaveSlots;

        /// <summary>
        /// Gets the maximum number of slots.
        /// </summary>
        public int MaxSlots => maxSlots;

        /// <summary>
        /// Gets the current slot index (0-based). Returns -1 if no slot is active.
        /// </summary>
        public int CurrentSlotIndex => _currentSlotIndex;

        /// <summary>
        /// Returns true if a slot is currently active.
        /// </summary>
        public bool HasActiveSlot => useSaveSlots && _currentSlotIndex >= 0;

        /// <summary>
        /// Gets the current manual save slot key (e.g., "save-1").
        /// Returns null if slots disabled or no slot active.
        /// </summary>
        public string CurrentManualSlotKey =>
            HasActiveSlot ? GetManualSlotKey(_currentSlotIndex) : null;

        /// <summary>
        /// Gets the current autosave slot key (e.g., "autosave-1").
        /// Returns null if slots disabled or no slot active.
        /// </summary>
        public string CurrentAutosaveSlotKey =>
            HasActiveSlot ? GetAutosaveSlotKey(_currentSlotIndex) : null;

        #endregion

        #region Methods

        /// <summary>
        /// Creates a JsonSaveProvider configured with these settings.
        /// </summary>
        /// <returns>A new ISaveProvider instance configured with this settings.</returns>
        public ISaveProvider CreateProvider()
        {
            return new JsonSaveProvider(saveSubdirectory, fileExtension, prettyPrint);
        }

        /// <summary>
        /// Configures the global SaveService with a provider using these settings.
        /// Call this once at application startup.
        /// </summary>
        public void ConfigureSaveService()
        {
            SaveService.SetProvider(CreateProvider());
        }

        /// <summary>
        /// Gets the full save directory path. Alias for SaveDirectoryPath.
        /// </summary>
        /// <returns>The full path to the save directory.</returns>
        public string GetSavePath()
        {
            return SaveDirectoryPath;
        }

        #endregion

        #region Slot Methods

        /// <summary>
        /// Sets the active slot index. Called when loading a save or starting new game.
        /// </summary>
        /// <param name="slotIndex">The slot index (0-based). Use -1 to clear active slot.</param>
        public void SetActiveSlot(int slotIndex)
        {
            if (!useSaveSlots)
            {
                Debug.LogWarning("[SaveSystemSettings] Slot system is disabled.");
                return;
            }

            if (slotIndex < -1 || slotIndex >= maxSlots)
            {
                Debug.LogWarning($"[SaveSystemSettings] Invalid slot index: {slotIndex}. Must be -1 to {maxSlots - 1}.");
                return;
            }

            int previousIndex = _currentSlotIndex;
            _currentSlotIndex = slotIndex;

            if (previousIndex != slotIndex)
            {
                OnSlotChanged?.Invoke(previousIndex, slotIndex);
            }
        }

        /// <summary>
        /// Clears the active slot (sets to -1).
        /// </summary>
        public void ClearActiveSlot()
        {
            SetActiveSlot(-1);
        }

        /// <summary>
        /// Gets the manual save slot key for a specific index.
        /// </summary>
        /// <param name="slotIndex">The slot index (0-based).</param>
        /// <returns>The slot key (e.g., "save-0", "save-1").</returns>
        public string GetManualSlotKey(int slotIndex)
        {
            if (!useSaveSlots) return manualSavePrefix;

            if (slotIndex < 0 || slotIndex >= maxSlots)
            {
                Debug.LogWarning($"[SaveSystemSettings] Invalid slot index: {slotIndex}. Must be 0 to {maxSlots - 1}.");
                return null;
            }
            return $"{manualSavePrefix}-{slotIndex}";
        }

        /// <summary>
        /// Gets the autosave slot key for a specific index.
        /// </summary>
        /// <param name="slotIndex">The slot index (0-based).</param>
        /// <returns>The autosave slot key (e.g., "autosave-0", "autosave-1").</returns>
        public string GetAutosaveSlotKey(int slotIndex)
        {
            if (!useSaveSlots) return autosavePrefix;

            if (slotIndex < 0 || slotIndex >= maxSlots)
            {
                Debug.LogWarning($"[SaveSystemSettings] Invalid slot index: {slotIndex}. Must be 0 to {maxSlots - 1}.");
                return null;
            }
            return $"{autosavePrefix}-{slotIndex}";
        }

        /// <summary>
        /// Checks if a slot index is valid.
        /// </summary>
        /// <param name="slotIndex">The slot index to check.</param>
        /// <returns>True if the index is valid (0 to maxSlots-1).</returns>
        public bool IsValidSlotIndex(int slotIndex)
        {
            return useSaveSlots && slotIndex >= 0 && slotIndex < maxSlots;
        }

        #endregion

        #region Lifecycle

        private void OnEnable()
        {
            // Reset runtime state when entering play mode
            _currentSlotIndex = -1;
        }

        #endregion

        #region Debug

#if ODIN_INSPECTOR && UNITY_EDITOR
        [Title("Debug")]
        [ShowInInspector, ReadOnly]
        [PropertyOrder(100)]
        private string FullSavePath => SaveDirectoryPath;

        [ShowInInspector, ReadOnly]
        [PropertyOrder(101)]
        private bool ProviderConfigured => SaveService.IsConfigured;

        [Button("Open Save Folder", ButtonSizes.Medium)]
        [PropertyOrder(110)]
        private void OpenSaveFolder()
        {
            string path = SaveDirectoryPath;
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            UnityEditor.EditorUtility.RevealInFinder(path);
        }

        [Button("Configure SaveService Now", ButtonSizes.Medium)]
        [PropertyOrder(111)]
        [GUIColor(0.4f, 0.8f, 0.4f)]
        private void DebugConfigureSaveService()
        {
            ConfigureSaveService();
            Debug.Log($"SaveService configured with: {saveSubdirectory}/{fileExtension}");
        }

        [Title("Slot Debug (Runtime)")]
        [ShowInInspector, ReadOnly]
        [ShowIf("useSaveSlots")]
        [PropertyOrder(120)]
        private int DebugCurrentSlotIndex => _currentSlotIndex;

        [ShowInInspector, ReadOnly]
        [ShowIf("useSaveSlots")]
        [PropertyOrder(121)]
        private bool DebugHasActiveSlot => HasActiveSlot;

        [ShowInInspector, ReadOnly]
        [ShowIf("useSaveSlots")]
        [PropertyOrder(122)]
        private string DebugCurrentManualSlotKey => CurrentManualSlotKey ?? "(none)";

        [ShowInInspector, ReadOnly]
        [ShowIf("useSaveSlots")]
        [PropertyOrder(123)]
        private string DebugCurrentAutosaveSlotKey => CurrentAutosaveSlotKey ?? "(none)";

        [Button("Set Slot 0")]
        [ButtonGroup("SlotButtons")]
        [ShowIf("useSaveSlots")]
        [PropertyOrder(130)]
        private void DebugSetSlot0() => SetActiveSlot(0);

        [Button("Set Slot 1")]
        [ButtonGroup("SlotButtons")]
        [ShowIf("useSaveSlots")]
        private void DebugSetSlot1() => SetActiveSlot(1);

        [Button("Set Slot 2")]
        [ButtonGroup("SlotButtons")]
        [ShowIf("useSaveSlots")]
        private void DebugSetSlot2() => SetActiveSlot(2);

        [Button("Clear Slot")]
        [ButtonGroup("SlotButtons")]
        [ShowIf("useSaveSlots")]
        private void DebugClearSlot() => ClearActiveSlot();
#endif

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using HelloDev.Saving.Data;
using HelloDev.Saving.Interfaces;
using Logger = HelloDev.Logging.Logger;

namespace HelloDev.Saving.Core
{
    /// <summary>
    /// Default implementation of <see cref="IUnifiedSaveManager"/>.
    ///
    /// Coordinates registered <see cref="ISaveModule"/>s and builds/restores the
    /// root <see cref="SaveState"/>. The root snapshot is handed directly to
    /// <see cref="SaveProviderService"/>, which is responsible for its own
    /// storage-level serialization (e.g. <c>JsonUtility</c> for a file-based
    /// provider). Per-savable payloads inside each module still go through
    /// <see cref="SaveSerializerService"/> (see <see cref="SaveModule"/>),
    /// since those require runtime-type-aware serialization that a provider's
    /// generic <c>SaveAsync&lt;T&gt;</c> alone can't do.
    /// </summary>
    public sealed class UnifiedSaveManager : IUnifiedSaveManager
    {
        private readonly Dictionary<string, ISaveModule> _modules = new();

        private Func<SaveMetadata> _metadataProvider;

        /// <summary>
        /// Most recently loaded root state, kept so modules created
        /// after a load has already happened (e.g. a scene object whose
        /// Awake fired late) can still pick up their data on registration.
        /// Replaced wholesale on every successful <see cref="LoadAsync"/> call.
        /// </summary>
        private SaveState _lastLoadedSlotState;

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public event Action Initialized;

        /// <inheritdoc/>
        public bool IsLoaded { get; private set; }

        /// <inheritdoc/>
        public string ActiveSlot { get; private set; }

        /// <inheritdoc/>
        public IReadOnlyCollection<ISaveModule> Modules => _modules.Values;

        private static IUnifiedSaveManager _instance;
        public static IUnifiedSaveManager Instance => _instance;

        /// <inheritdoc/>
        public event Action<string> SaveStarted;

        /// <inheritdoc/>
        public event Action<string, bool> SaveCompleted;

        /// <inheritdoc/>
        public event Action<string> LoadStarted;

        /// <inheritdoc/>
        public event Action<string, bool> LoadCompleted;

        public UnifiedSaveManager()
        {
            IsInitialized = true;
            Initialized?.Invoke();
        }

        /// <summary>
        /// Registers a callback invoked right before a save capture begins,
        /// used to populate <see cref="SaveMetadata"/> (e.g. play time, player
        /// name, current location). SlotId and Timestamp are set by the
        /// manager afterwards and do not need to be provided.
        /// </summary>
        public void SetMetadataProvider(Func<SaveMetadata> metadataProvider)
        {
            _metadataProvider = metadataProvider;
        }

        /// <inheritdoc/>
        public UniTask<ISaveModule> RegisterModule(string moduleId)
        {
            if (_modules.TryGetValue(moduleId, out ISaveModule existing))
            {
                return UniTask.FromResult(existing);
            }

            SaveModule module = new()
            {
                ModuleId = moduleId
            };

            _modules.Add(moduleId, module);

            SaveModuleState pendingState = _lastLoadedSlotState?.FindModule(moduleId);

            if (pendingState != null)
            {
                module.LastLoadedState = pendingState;
                Logger.LogVerbose("Save", $"Module '{moduleId}': created with {pendingState.Entries.Count} pending entrie(s) from last load.");
            }

            Logger.Log("Save", $"Registered module '{moduleId}'.");

            return UniTask.FromResult<ISaveModule>(module);
        }

        /// <inheritdoc/>
        public ISaveModule UnregisterModule(string moduleId)
        {
            _modules.TryGetValue(moduleId, out ISaveModule module);
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            if (_modules.Remove(module.ModuleId))
            {
                Logger.Log("Save", $"Unregistered module '{module.ModuleId}'.");
            }

            return module;
        }

        /// <inheritdoc/>
        public bool TryGetSaveable(string saveId, out ISavable savable)
        {
            foreach (ISaveModule module in _modules.Values)
            {
                foreach (ISavable candidate in module.Savables)
                {
                    if (candidate.SaveId == saveId)
                    {
                        savable = candidate;
                        return true;
                    }
                }
            }

            savable = null;
            return false;
        }

        /// <inheritdoc/>
        public async UniTask<bool> SaveAsync(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot))
                throw new ArgumentException("Slot must not be null or empty.", nameof(slot));

            SaveStarted?.Invoke(slot);

            bool success;

            try
            {
                string timestamp = DateTime.UtcNow.ToString("O");

                SaveMetadata metadata = _metadataProvider?.Invoke() ?? new SaveMetadata();
                metadata.SlotId = slot;
                metadata.Timestamp = timestamp;

                SaveState state = new()
                {
                    Timestamp = timestamp,
                    Metadata = metadata
                };

                foreach (ISaveModule module in _modules.Values)
                {
                    SaveModuleState moduleState = await module.SaveAsync();
                    state.Modules.Add(moduleState);
                }

                success = await SaveProviderService.Provider.SaveAsync(slot, state);

                Logger.Log("Save", $"Saved slot '{slot}': {state.Modules.Count} module(s), {state.Modules.Sum(m => m.Entries.Count)} entrie(s) total.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Save", $"Exception while saving slot '{slot}': {ex.Message}");
                success = false;
            }

            SaveCompleted?.Invoke(slot, success);
            return success;
        }

        /// <inheritdoc/>
        public async UniTask<bool> LoadAsync(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot))
                throw new ArgumentException("Slot must not be null or empty.", nameof(slot));

            LoadStarted?.Invoke(slot);

            bool success;

            try
            {
                bool exists = await SaveProviderService.Provider.ExistsAsync(slot);

                if (!exists)
                {
                    Logger.LogWarning("Save", $"No save data found for slot '{slot}'.");
                    LoadCompleted?.Invoke(slot, false);
                    return false;
                }

                SaveState state = await SaveProviderService.Provider.LoadAsync<SaveState>(slot);

                if (state == null)
                {
                    Logger.LogError("Save", $"Failed to load save state for slot '{slot}'.");
                    LoadCompleted?.Invoke(slot, false);
                    return false;
                }

                _lastLoadedSlotState = state;

                Logger.LogVerbose("Save", $"Slot '{slot}': read {state.Modules.Count} module(s) from disk.");

                success = true;

                foreach (ISaveModule module in _modules.Values)
                {
                    SaveModuleState moduleState = state.FindModule(module.ModuleId);

                    if (moduleState == null)
                    {
                        Logger.LogVerbose("Save", $"Module '{module.ModuleId}': no data found in slot '{slot}'.");
                        continue;
                    }

                    bool moduleSuccess = await module.LoadAsync(moduleState);
                    success &= moduleSuccess;
                }

                foreach (SaveModuleState moduleState in state.Modules)
                {
                    if (!_modules.ContainsKey(moduleState.ModuleId))
                    {
                        Logger.LogVerbose("Save", $"Module '{moduleState.ModuleId}': not registered yet, will apply once it registers.");
                    }
                }

                if (success)
                {
                    ActiveSlot = slot;
                    IsLoaded = true;
                }

                Logger.Log("Save", $"Loaded slot '{slot}'.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Save", $"Exception while loading slot '{slot}': {ex.Message}");
                success = false;
            }

            LoadCompleted?.Invoke(slot, success);
            return success;
        }
    }
}
// SaveModule.cs
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HelloDev.Saving.Data;
using HelloDev.Saving.Interfaces;
using Logger = HelloDev.Logging.Logger;

namespace HelloDev.Saving.Core
{
    /// <summary>
    /// Base implementation of <see cref="ISaveModule"/>.
    /// Handles registration and restoration of saveables while
    /// remaining independent from the underlying save provider.
    /// </summary>
    public class SaveModule : ISaveModule
    {
        private readonly Dictionary<string, ISavable> _savables = new();
        
        /// <inheritdoc/>
        public string ModuleId { get; set; }

        /// <inheritdoc/>
        public IReadOnlyCollection<ISavable> Savables => _savables.Values;

        /// <inheritdoc/>
        public SaveModuleState LastLoadedState { get; set; }
        
        /// <inheritdoc/>
        public virtual async UniTask<SaveModuleState> SaveAsync()
        {
            SaveModuleState saveModuleState = new()
            {
                ModuleId = ModuleId
            };

            foreach (ISavable savable in _savables.Values)
            {
                object state = savable.SaveState();

                string payload = await SaveSerializerService.Serializer.SerializeAsync(state);

                saveModuleState.Entries.Add(new SaveEntry
                {
                    SaveId = savable.SaveId,
                    Payload = payload
                });
            }

            Logger.LogVerbose("Save", $"Module '{ModuleId}': captured {saveModuleState.Entries.Count} entrie(s).");

            return saveModuleState;
        }

        /// <inheritdoc/>
        public virtual async UniTask<bool> LoadAsync(SaveModuleState saveModuleState)
        {
            if (saveModuleState == null)
                return false;

            LastLoadedState = saveModuleState;

            int appliedCount = 0;

            foreach (SaveEntry entry in saveModuleState.Entries)
            {
                if (!_savables.TryGetValue(entry.SaveId, out ISavable savable))
                    continue;

                object state = await SaveSerializerService.Serializer.DeserializeAsync(entry.Payload, savable.StateType);
                await savable.LoadState(state);

                appliedCount++;
                Logger.LogVerbose("Save", $"Module '{ModuleId}': restored '{entry.SaveId}'.");
            }

            int unconsumed = saveModuleState.Entries.Count - appliedCount;

            if (unconsumed > 0)
            {
                Logger.LogWarning("Save", $"Module '{ModuleId}': {unconsumed} save entrie(s) had no matching registered savable (stale or not yet spawned).");
            }

            Logger.LogVerbose("Save", $"Module '{ModuleId}': applied {appliedCount}/{saveModuleState.Entries.Count} entrie(s).");

            return true;
        }

        /// <inheritdoc/>
        public async UniTask RegisterSavable(ISavable savable)
        {
            if (savable == null) throw new ArgumentNullException();

            if (!_savables.TryAdd(savable.SaveId, savable))
            {
                throw new InvalidOperationException($"A savable with id '{savable.SaveId}' is already registered in module '{ModuleId}'.");
            }

            if (LastLoadedState == null)
            {
                Logger.LogVerbose("Save", $"Module '{ModuleId}': registered '{savable.SaveId}' - no loaded state yet.");
                return;
            }

            SaveEntry entry = LastLoadedState.FindEntry(savable.SaveId);

            if (entry == null)
            {
                Logger.LogVerbose("Save", $"Module '{ModuleId}': registered '{savable.SaveId}' - no matching save entry, using defaults.");
                return;
            }

            object state = await SaveSerializerService.Serializer.DeserializeAsync(entry.Payload, savable.StateType);
            await savable.LoadState(state);

            Logger.LogVerbose("Save", $"Module '{ModuleId}': registered '{savable.SaveId}' and restored from loaded state.");
        }

        /// <inheritdoc/>
        public bool UnregisterSavable(ISavable savable)
        {
            return _savables.Remove(savable.SaveId);
        }

        /// <summary>
        /// Attempts to retrieve a registered savable.
        /// </summary>
        protected bool TryGetSavable(string saveId, out ISavable savable)
        {
            return _savables.TryGetValue(saveId, out savable);
        }
    }
}
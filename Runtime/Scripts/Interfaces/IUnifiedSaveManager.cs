using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace HelloDev.Saving.Interfaces
{
    /// <summary>
    /// Central coordinator responsible for saving and loading
    /// registered modules and savable objects.
    /// </summary>
    public interface IUnifiedSaveManager
    {
        /// <summary>
        /// True once this manager instance is ready to be used
        /// (modules/savables may register).
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Fired once, when this manager instance becomes ready to use.
        /// </summary>
        event Action Initialized;

        /// <summary>
        /// Indicates whether a save slot has been loaded.
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// Currently loaded slot.
        /// </summary>
        string ActiveSlot { get; }

        /// <summary>
        /// Registered save modules.
        /// </summary>
        IReadOnlyCollection<ISaveModule> Modules { get; }

        /// <summary>
        /// Registers a save module and initializes it. If a module with
        /// this id already exists, the existing instance is returned.
        /// </summary>
        UniTask<ISaveModule> RegisterModule(string moduleId);

        /// <summary>
        /// Removes a save module.
        /// </summary>
        ISaveModule UnregisterModule(string moduleId);

        /// <summary>
        /// Finds a savable object by identifier.
        /// </summary>
        bool TryGetSaveable(string saveId, out ISavable savable);

        /// <summary>
        /// Saves all registered modules.
        /// </summary>
        UniTask<bool> SaveAsync(string slot);

        /// <summary>
        /// Loads all registered modules.
        /// </summary>
        UniTask<bool> LoadAsync(string slot);


        /// <summary>
        /// Fired before saving starts.
        /// </summary>
        event Action<string> SaveStarted;

        /// <summary>
        /// Fired after saving completes.
        /// </summary>
        event Action<string, bool> SaveCompleted;

        /// <summary>
        /// Fired before loading starts.
        /// </summary>
        event Action<string> LoadStarted;

        /// <summary>
        /// Fired after loading completes.
        /// </summary>
        event Action<string, bool> LoadCompleted;
    }
}
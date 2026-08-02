using System;
using Cysharp.Threading.Tasks;
using HelloDev.Logging;
using UnityEngine;
using Logger = HelloDev.Logging.Logger;

namespace HelloDev.Saving
{
    /// <summary>
    /// Generic base class for MonoBehaviour-based savable systems.
    /// Handles auto-registration with UnifiedSaveManager and reduces boilerplate code.
    /// 
    /// This class automatically:
    /// - Implements all ISaveableSystem interface methods with sensible defaults
    /// - Provides type-safe Capture/Restore methods (no casting needed)
    /// - Registers with UnifiedSaveManager on Awake
    /// - Unregisters on OnDestroy
    /// - Handles all the boilerplate so you only write what's unique to your system
    /// </summary>
    /// <typeparam name="TSnapshot">The snapshot type this system produces. Must be a [Serializable] class.</typeparam>
    public abstract class SaveableSystem<TSnapshot> : MonoBehaviour, ISaveableSystem
        where TSnapshot : class
    {
        #region Private Fields

        private UnifiedSaveManager _saveManager;

        #endregion

        #region ISaveableSystem Implementation

        /// <summary>
        /// Unique key identifying this system in the save file.
        /// Convention: lowercase, no spaces (e.g., "player", "inventory", "quests").
        /// </summary>
        public abstract string SystemKey { get; }

        /// <summary>
        /// Priority for save/restore operations. Lower numbers execute first.
        /// Default is 100. Override to change execution order.
        /// 
        /// Suggested ranges:
        /// - 0-99: Core systems (world state, flags)
        /// - 100-199: Data systems (quests, tutorials, inventory)
        /// - 200+: Gameplay systems (UI state, camera position)
        /// </summary>
        public virtual int SavePriority => 100;

        /// <summary>
        /// The Type of the snapshot class this system produces.
        /// Automatically set to TSnapshot - do not override.
        /// </summary>
        public Type SnapshotType => typeof(TSnapshot);

        /// <summary>
        /// Captures the current state as a serializable snapshot.
        /// This is the type-erased version called by UnifiedSaveManager.
        /// Override the type-safe Capture() method instead.
        /// </summary>
        object ISaveableSystem.CaptureSnapshot() => Capture();

        /// <summary>
        /// Restores state from a previously captured snapshot.
        /// This is the type-erased version called by UnifiedSaveManager.
        /// Override the type-safe Restore() method instead.
        /// </summary>
        async UniTask<bool> ISaveableSystem.RestoreSnapshot(object snapshot)
        {
            if (snapshot is TSnapshot typed)
            {
                return await Restore(typed);
            }

            Logger.LogWarning("Save", 
                $"[{SystemKey}] Invalid snapshot type: {snapshot?.GetType().Name ?? "null"}. Expected: {typeof(TSnapshot).Name}", 
                this);
            return false;
        }

        /// <summary>
        /// Called before a save operation starts.
        /// Override to prepare data or pause systems during save.
        /// </summary>
        public virtual void OnBeforeSave() { }

        /// <summary>
        /// Called after a save operation completes.
        /// Override to handle post-save logic.
        /// </summary>
        /// <param name="success">Whether the save operation succeeded.</param>
        public virtual void OnAfterSave(bool success) { }

        /// <summary>
        /// Called before a load operation starts.
        /// Override to clear current state or prepare for restoration.
        /// </summary>
        public virtual void OnBeforeLoad() { }

        /// <summary>
        /// Called after a load operation completes.
        /// Override to refresh UI or trigger post-load events.
        /// </summary>
        /// <param name="success">Whether the load operation succeeded.</param>
        public virtual void OnAfterLoad(bool success) { }

        #endregion

        #region Type-Safe Abstract Methods

        /// <summary>
        /// Capture current state into a type-safe snapshot.
        /// This method is called by the save system when saving.
        /// Return null if there's nothing to save.
        /// </summary>
        /// <returns>A snapshot object, or null if nothing to save.</returns>
        protected abstract TSnapshot Capture();

        /// <summary>
        /// Restore state from a type-safe snapshot.
        /// This method is called by the save system when loading.
        /// The snapshot parameter is guaranteed to be of type TSnapshot (no casting needed).
        /// </summary>
        /// <param name="snapshot">The snapshot to restore from.</param>
        /// <returns>True if restoration succeeded, false otherwise.</returns>
        protected abstract UniTask<bool> Restore(TSnapshot snapshot);

        #endregion

        #region Auto-Registration

        /// <summary>
        /// Automatically registers this system with UnifiedSaveManager on Awake.
        /// Override this method if you need custom Awake logic, but make sure to call base.Awake().
        /// </summary>
        protected virtual void Awake()
        {
            AutoRegister();
        }

        /// <summary>
        /// Automatically unregisters this system from UnifiedSaveManager on destroy.
        /// Override this method if you need custom cleanup logic, but make sure to call base.OnDestroy().
        /// </summary>
        protected virtual void OnDestroy()
        {
            AutoUnregister();
        }

        /// <summary>
        /// Attempts to find and register with a UnifiedSaveManager in the scene.
        /// Called automatically during Awake.
        /// </summary>
        private void AutoRegister()
        {
            _saveManager = FindObjectOfType<UnifiedSaveManager>();
            
            if (_saveManager != null)
            {
                _saveManager.RegisterSystem(this);
                Logger.LogVerbose("Save", $"[{SystemKey}] Auto-registered with UnifiedSaveManager", this);
            }
            else
            {
                Logger.LogWarning("Save", 
                    $"[{SystemKey}] No UnifiedSaveManager found in scene. This system will not be saved.", 
                    this);
            }
        }

        /// <summary>
        /// Unregisters this system from the UnifiedSaveManager.
        /// Called automatically during OnDestroy.
        /// </summary>
        private void AutoUnregister()
        {
            if (_saveManager != null)
            {
                _saveManager.UnregisterSystem(this);
                Logger.LogVerbose("Save", $"[{SystemKey}] Auto-unregistered from UnifiedSaveManager", this);
                _saveManager = null;
            }
        }

        #endregion

        #region Manual Registration Helpers

        /// <summary>
        /// Manually registers this system with a specific UnifiedSaveManager.
        /// Useful if you need to register before Awake or with a specific manager instance.
        /// </summary>
        /// <param name="manager">The save manager to register with.</param>
        public void ManualRegister(UnifiedSaveManager manager)
        {
            if (manager == null)
            {
                Logger.LogWarning("Save", $"[{SystemKey}] Cannot register: manager is null", this);
                return;
            }

            if (_saveManager != null && _saveManager != manager)
            {
                Logger.LogWarning("Save", 
                    $"[{SystemKey}] Already registered with a different manager. Unregister first.", 
                    this);
                return;
            }

            _saveManager = manager;
            _saveManager.RegisterSystem(this);
            Logger.Log("Save", $"[{SystemKey}] Manually registered with UnifiedSaveManager", this);
        }

        /// <summary>
        /// Manually unregisters this system from its current UnifiedSaveManager.
        /// </summary>
        public void ManualUnregister()
        {
            AutoUnregister();
        }

        /// <summary>
        /// Returns true if this system is currently registered with a save manager.
        /// </summary>
        public bool IsRegistered => _saveManager != null;

        #endregion
    }
}
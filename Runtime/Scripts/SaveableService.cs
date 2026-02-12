using System;
using HelloDev.Logging;
using UnityEngine;
using Logger = HelloDev.Logging.Logger;

namespace HelloDev.Saving
{
    /// <summary>
    /// Generic base class for non-MonoBehaviour saveable systems.
    /// Use this for services, managers, or any pure C# class that needs to be saved.
    /// 
    /// Unlike SaveableSystem (MonoBehaviour), this class requires manual registration/unregistration
    /// with UnifiedSaveManager since it doesn't have Unity lifecycle events.
    /// 
    /// This class automatically:
    /// - Implements all ISaveableSystem interface methods with sensible defaults
    /// - Provides type-safe Capture/Restore methods (no casting needed)
    /// - Tracks registration state
    /// - Handles all the boilerplate so you only write what's unique to your system
    /// </summary>
    /// <typeparam name="TSnapshot">The snapshot type this system produces. Must be a [Serializable] class.</typeparam>
    public abstract class SaveableService<TSnapshot> : ISaveableSystem
        where TSnapshot : class
    {
        #region Private Fields

        private UnifiedSaveManager _registeredManager;

        #endregion

        #region ISaveableSystem Implementation

        /// <summary>
        /// Unique key identifying this system in the save file.
        /// Convention: lowercase, no spaces (e.g., "economy", "settings", "analytics").
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
        bool ISaveableSystem.RestoreSnapshot(object snapshot)
        {
            if (snapshot is TSnapshot typed)
            {
                return Restore(typed);
            }

            Debug.LogWarning($"[{SystemKey}] Invalid snapshot type: {snapshot?.GetType().Name ?? "null"}. Expected: {typeof(TSnapshot).Name}");
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
        protected abstract bool Restore(TSnapshot snapshot);

        #endregion

        #region Registration

        /// <summary>
        /// Registers this service with a UnifiedSaveManager.
        /// Must be called after instantiating your service for it to be saved/loaded.
        /// </summary>
        /// <param name="manager">The save manager to register with.</param>
        public void Register(UnifiedSaveManager manager)
        {
            if (manager == null)
            {
                Debug.LogWarning($"[{SystemKey}] Cannot register: manager is null");
                return;
            }

            if (_registeredManager != null)
            {
                Debug.LogWarning($"[{SystemKey}] Already registered with a save manager. Unregister first or call Register again to switch.");
                return;
            }

            manager.RegisterSystem(this);
            _registeredManager = manager;
            Logger.LogVerbose(LogSystems.Save, $"[{SystemKey}] Registered with UnifiedSaveManager");
        }

        /// <summary>
        /// Unregisters this service from its UnifiedSaveManager.
        /// Call this when disposing/shutting down your service to prevent memory leaks.
        /// </summary>
        public void Unregister()
        {
            if (_registeredManager != null)
            {
                _registeredManager.UnregisterSystem(this);
                _registeredManager = null;
                Logger.LogVerbose(LogSystems.Save, $"[{SystemKey}] Unregistered from UnifiedSaveManager");
            }
        }

        /// <summary>
        /// Returns true if this service is currently registered with a save manager.
        /// </summary>
        public bool IsRegistered => _registeredManager != null;

        #endregion
    }
}
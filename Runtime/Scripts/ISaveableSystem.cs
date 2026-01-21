using System;

namespace HelloDev.Saving
{
    /// <summary>
    /// Interface for systems that can be saved/loaded by the unified save system.
    /// Implement this to make your system saveable and register it with UnifiedSaveManager.
    /// </summary>
    /// <example>
    /// public class InventorySaveableSystem : MonoBehaviour, ISaveableSystem
    /// {
    ///     public string SystemKey => "inventory";
    ///     public int SavePriority => 120;
    ///     public Type SnapshotType => typeof(InventorySnapshot);
    ///
    ///     public object CaptureSnapshot() => inventoryManager.CaptureSnapshot();
    ///     public bool RestoreSnapshot(object snapshot) => inventoryManager.RestoreSnapshot((InventorySnapshot)snapshot);
    /// }
    /// </example>
    public interface ISaveableSystem
    {
        /// <summary>
        /// Unique key identifying this system in the save file.
        /// Convention: lowercase, no spaces (e.g., "quests", "tutorials", "inventory").
        /// This key is used as the dictionary key in the unified save file.
        /// </summary>
        string SystemKey { get; }

        /// <summary>
        /// Priority for save/restore operations. Lower numbers execute first.
        /// Use this to ensure dependencies are restored before dependents.
        ///
        /// Suggested ranges:
        /// - 0-99: Core systems (world state, flags)
        /// - 100-199: Data systems (quests, tutorials, inventory)
        /// - 200+: Gameplay systems (UI state, camera position)
        /// </summary>
        int SavePriority { get; }

        /// <summary>
        /// The Type of the snapshot class this system produces.
        /// Used for deserialization when loading.
        /// Must be a [Serializable] class compatible with JsonUtility.
        /// </summary>
        Type SnapshotType { get; }

        /// <summary>
        /// Captures the current state as a serializable snapshot.
        /// The returned object must be of SnapshotType and serializable via JsonUtility.
        /// </summary>
        /// <returns>A snapshot object, or null if nothing to save.</returns>
        object CaptureSnapshot();

        /// <summary>
        /// Restores state from a previously captured snapshot.
        /// </summary>
        /// <param name="snapshot">The snapshot to restore from. Cast to your SnapshotType.</param>
        /// <returns>True if restoration succeeded, false otherwise.</returns>
        bool RestoreSnapshot(object snapshot);

        /// <summary>
        /// Called before a save operation starts.
        /// Use this to prepare data or pause systems during save.
        /// </summary>
        void OnBeforeSave();

        /// <summary>
        /// Called after a save operation completes.
        /// </summary>
        /// <param name="success">Whether the save operation succeeded.</param>
        void OnAfterSave(bool success);

        /// <summary>
        /// Called before a load operation starts.
        /// Use this to clear current state or prepare for restoration.
        /// </summary>
        void OnBeforeLoad();

        /// <summary>
        /// Called after a load operation completes.
        /// Use this to refresh UI or trigger post-load events.
        /// </summary>
        /// <param name="success">Whether the load operation succeeded.</param>
        void OnAfterLoad(bool success);
    }
}

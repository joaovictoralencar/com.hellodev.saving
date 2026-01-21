using System;
using System.Collections.Generic;

namespace HelloDev.Saving
{
    /// <summary>
    /// Container for all system snapshots. This is the root object saved to disk.
    /// Contains versioning info, metadata, and a list of system-specific snapshots.
    /// </summary>
    /// <example>
    /// JSON structure:
    /// {
    ///   "Version": 1,
    ///   "Timestamp": "2026-01-18T14:30:00Z",
    ///   "Systems": [
    ///     { "Key": "quests", "TypeName": "...", "JsonData": "{...}" },
    ///     { "Key": "tutorials", "TypeName": "...", "JsonData": "{...}" }
    ///   ],
    ///   "Metadata": { "SlotKey": "save-0", ... }
    /// }
    /// </example>
    [Serializable]
    public class UnifiedSnapshot
    {
        /// <summary>
        /// Save format version for migration support.
        /// Increment this when making breaking changes to the save format.
        /// </summary>
        public int Version = 1;

        /// <summary>
        /// UTC timestamp when the snapshot was captured (ISO 8601 format).
        /// </summary>
        public string Timestamp;

        /// <summary>
        /// List of system snapshots, each containing the key, type, and serialized data.
        /// Using a list instead of Dictionary for JsonUtility compatibility.
        /// </summary>
        public List<SystemSnapshotEntry> Systems = new();

        /// <summary>
        /// Metadata for quick access without loading full snapshot.
        /// Useful for save slot UI display.
        /// </summary>
        public UnifiedSnapshotMetadata Metadata = new();

        /// <summary>
        /// Finds a system entry by key.
        /// </summary>
        /// <param name="systemKey">The system key to find.</param>
        /// <returns>The entry, or null if not found.</returns>
        public SystemSnapshotEntry FindSystem(string systemKey)
        {
            return Systems.Find(s => s.Key == systemKey);
        }

        /// <summary>
        /// Checks if a system exists in this snapshot.
        /// </summary>
        /// <param name="systemKey">The system key to check.</param>
        /// <returns>True if the system exists.</returns>
        public bool HasSystem(string systemKey)
        {
            return Systems.Exists(s => s.Key == systemKey);
        }
    }

    /// <summary>
    /// Entry for a single system's snapshot data.
    /// Stores the serialized snapshot as a JSON string to preserve type info during deserialization.
    /// </summary>
    [Serializable]
    public class SystemSnapshotEntry
    {
        /// <summary>
        /// Unique key identifying this system (e.g., "quests", "tutorials").
        /// Matches ISaveableSystem.SystemKey.
        /// </summary>
        public string Key;

        /// <summary>
        /// Assembly-qualified type name for deserialization.
        /// Used to reconstruct the correct type when loading.
        /// </summary>
        public string TypeName;

        /// <summary>
        /// The snapshot serialized as a JSON string.
        /// This nested serialization allows each system to have its own structure.
        /// </summary>
        public string JsonData;
    }

    /// <summary>
    /// Metadata stored alongside the snapshot for UI display and quick access.
    /// This data can be loaded without deserializing the full snapshot.
    /// </summary>
    [Serializable]
    public class UnifiedSnapshotMetadata
    {
        /// <summary>
        /// The save slot key this snapshot was saved to.
        /// </summary>
        public string SlotKey;

        /// <summary>
        /// UTC timestamp when saved (ISO 8601 format).
        /// </summary>
        public string Timestamp;

        /// <summary>
        /// Total play time in seconds (game-specific, set by game code).
        /// </summary>
        public float PlayTimeSeconds;

        /// <summary>
        /// Player name or character name (game-specific).
        /// </summary>
        public string PlayerName;

        /// <summary>
        /// Current location or level name (game-specific).
        /// </summary>
        public string Location;

        /// <summary>
        /// Game-specific custom data as a JSON string.
        /// Use this for any additional metadata your game needs.
        /// </summary>
        public string CustomData;
    }
}

using System;
using System.Collections.Generic;

namespace HelloDev.Saving.Data
{
    /// <summary>
    /// Represents a logical group of savable objects.
    ///
    /// Examples:
    /// - Currency
    /// - World
    /// - Player
    /// - Quests
    /// </summary>
    [Serializable]
    public class SaveModuleState
    {
        /// <summary>
        /// Unique module identifier.
        /// Example: "currency", "world", "player".
        /// </summary>
        public string ModuleId;

        /// <summary>
        /// Save format version.
        /// Used for future migration support.
        /// </summary>
        public int Version = 1;

        /// <summary>
        /// Entries belonging to this module.
        /// </summary>
        public List<SaveEntry> Entries = new();


        /// <summary>
        /// Finds an entry by save id.
        /// </summary>
        public SaveEntry FindEntry(string saveId)
        {
            return Entries.Find(x => x.SaveId == saveId);
        }


        /// <summary>
        /// Checks whether this module contains an object.
        /// </summary>
        public bool HasEntry(string saveId)
        {
            return Entries.Exists(x => x.SaveId == saveId);
        }
    }
}
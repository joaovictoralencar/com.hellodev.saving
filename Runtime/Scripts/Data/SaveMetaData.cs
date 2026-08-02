using System;

namespace HelloDev.Saving.Data
{
    /// <summary>
    /// General information about a save slot.
    ///
    /// Designed to be readable without loading gameplay data.
    /// </summary>
    [Serializable]
    public class SaveMetadata
    {
        /// <summary>
        /// Slot identifier.
        /// </summary>
        public string SlotId;


        /// <summary>
        /// UTC timestamp of the save.
        /// </summary>
        public string Timestamp;


        /// <summary>
        /// Total play time in seconds.
        /// </summary>
        public float PlayTimeSeconds;


        /// <summary>
        /// Optional player display name.
        /// </summary>
        public string PlayerName;


        /// <summary>
        /// Current world/location identifier.
        /// </summary>
        public string Location;
    }
}
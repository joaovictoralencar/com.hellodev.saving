using System;
using System.Collections.Generic;

namespace HelloDev.Saving.Data
{
    /// <summary>
    /// Root save container.
    /// Represents the complete state of a save slot.
    /// </summary>
    [Serializable]
    public class SaveState
    {
        /// <summary>
        /// Save format version.
        /// Used for future migration support.
        /// </summary>
        public int Version = 1;

        /// <summary>
        /// UTC timestamp when this snapshot was created.
        /// </summary>
        public string Timestamp;

        /// <summary>
        /// General metadata associated with this save.
        /// </summary>
        public SaveMetadata Metadata = new();

        /// <summary>
        /// Collection of save modules.
        /// Modules group related savable objects.
        /// </summary>
        public List<SaveModuleState> Modules = new();


        /// <summary>
        /// Finds a module by identifier.
        /// </summary>
        public SaveModuleState FindModule(string moduleId)
        {
            return Modules.Find(x => x.ModuleId == moduleId);
        }


        /// <summary>
        /// Checks if a module exists.
        /// </summary>
        public bool HasModule(string moduleId)
        {
            return Modules.Exists(x => x.ModuleId == moduleId);
        }
    }
}
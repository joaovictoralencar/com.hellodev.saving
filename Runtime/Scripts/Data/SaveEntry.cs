using System;
using HelloDev.Saving.Interfaces;

namespace HelloDev.Saving.Data
{
    /// <summary>
    /// Serialized state of a single savable object.
    /// </summary>
    [Serializable]
    public class SaveEntry
    {
        /// <summary>
        /// Stable identifier of the savable.
        /// Matches <see cref="ISavable.SaveId"/>.
        /// </summary>
        public string SaveId;

        /// <summary>
        /// Serialized snapshot payload.
        ///
        /// The payload format is determined by the active
        /// <see cref="ISaveSerializer"/>.
        /// </summary>
        public string Payload;
    }
}
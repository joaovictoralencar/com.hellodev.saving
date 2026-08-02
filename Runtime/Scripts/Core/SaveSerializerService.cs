using Cysharp.Threading.Tasks;
using HelloDev.Saving.Interfaces;
using Logger = HelloDev.Logging.Logger;

namespace HelloDev.Saving.Core
{
    /// <summary>
    /// Static service providing access to the current save serializer.
    /// Set the serializer once during application startup.
    ///
    /// This follows the same pattern as <see cref="SaveService"/>,
    /// allowing serialization to be swapped without changing
    /// the save framework.
    /// </summary>
    public static class SaveSerializerService
    {
        private static ISaveSerializer _serializer;

        /// <summary>
        /// Gets the active serializer.
        /// Returns a null serializer if none has been configured.
        /// </summary>
        public static ISaveSerializer Serializer => _serializer ?? NullSaveSerializer.Instance;

        /// <summary>
        /// Returns true if a serializer has been configured.
        /// </summary>
        public static bool IsConfigured => _serializer != null;

        /// <summary>
        /// Sets the serializer used by the save framework.
        /// </summary>
        public static void SetSerializer(ISaveSerializer serializer)
        {
            _serializer = serializer;
        }

        /// <summary>
        /// Removes the current serializer.
        /// </summary>
        public static void ClearSerializer()
        {
            _serializer = null;
        }
    }

    /// <summary>
    /// Fallback serializer used when no serializer has been configured.
    /// All operations safely return empty values.
    /// </summary>
    internal sealed class NullSaveSerializer : ISaveSerializer
    {
        public static readonly NullSaveSerializer Instance = new();

        private NullSaveSerializer() { }

        public UniTask<string> SerializeAsync<T>(T snapshot)
        {
            Logger.LogWarning("Save", "No save serializer configured.");
            return UniTask.FromResult(string.Empty);
        }

        public UniTask<T> DeserializeAsync<T>(string payload)
        {
            Logger.LogWarning("Save", "No save serializer configured.");
            return UniTask.FromResult(default(T));
        }

        public UniTask<object> DeserializeAsync(string payload, System.Type snapshotType)
        {
            Logger.LogWarning("Save", "No save serializer configured.");
            return UniTask.FromResult<object>(null);
        }
    }
}
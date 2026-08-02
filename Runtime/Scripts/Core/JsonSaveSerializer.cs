using System;
using Cysharp.Threading.Tasks;
using HelloDev.Saving.Interfaces;
using UnityEngine;
using Logger = HelloDev.Logging.Logger;

namespace HelloDev.Saving.Core
{
    /// <summary>
    /// Default serializer implementation based on Unity's JsonUtility.
    ///
    /// This serializer is suitable for most Unity projects and produces
    /// JSON payloads compatible with Unity's serialization system.
    ///
    /// For projects requiring more advanced serialization features
    /// (polymorphism, dictionaries, interfaces, etc.),
    /// implement <see cref="ISaveSerializer"/> using another serializer
    /// such as Newtonsoft.Json, Odin Serializer, MessagePack, or a custom solution.
    /// </summary>
    public class JsonSaveSerializer : ISaveSerializer
    {
        private readonly bool _prettyPrint;

        /// <summary>
        /// Creates a new JSON serializer.
        /// </summary>
        /// <param name="prettyPrint">
        /// If true, generated JSON is formatted for readability.
        /// </param>
        public JsonSaveSerializer(bool prettyPrint = false)
        {
            _prettyPrint = prettyPrint;
        }

        /// <inheritdoc/>
        public UniTask<string> SerializeAsync<T>(T snapshot)
        {
            try
            {
                if (snapshot == null)
                    return UniTask.FromResult(string.Empty);

                string payload = JsonUtility.ToJson(snapshot, _prettyPrint);

                return UniTask.FromResult(payload);
            }
            catch (Exception ex)
            {
                Logger.LogError("Save", $"Failed to serialize '{typeof(T).Name}': {ex.Message}");
                return UniTask.FromResult(string.Empty);
            }
        }

        /// <inheritdoc/>
        public UniTask<T> DeserializeAsync<T>(string payload)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(payload)) return UniTask.FromResult(default(T));

                T snapshot = JsonUtility.FromJson<T>(payload);

                return UniTask.FromResult(snapshot);
            }
            catch (Exception ex)
            {
                Logger.LogError("Save", $"Failed to deserialize '{typeof(T).Name}': {ex.Message}");
                return UniTask.FromResult(default(T));
            }
        }

        /// <inheritdoc/>
        public UniTask<object> DeserializeAsync(string payload, Type snapshotType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(payload))
                    return UniTask.FromResult<object>(null);

                object snapshot = JsonUtility.FromJson(payload, snapshotType);

                return UniTask.FromResult(snapshot);
            }
            catch (Exception ex)
            {
                Logger.LogError("Save", $"Failed to deserialize '{snapshotType.Name}': {ex.Message}");

                return UniTask.FromResult<object>(null);
            }
        }
    }
}
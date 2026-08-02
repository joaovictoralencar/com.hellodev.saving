using System;
using Cysharp.Threading.Tasks;

namespace HelloDev.Saving.Interfaces
{
    /// <summary>
    /// Serializes and deserializes snapshot objects used by the save system.
    ///
    /// Implement this interface to integrate with any serialization format,
    /// such as JSON, binary, encrypted data, MessagePack, or custom serializers.
    ///
    /// The save system depends only on this abstraction and does not assume
    /// how snapshot data is encoded.
    /// </summary>
    public interface ISaveSerializer
    {
        /// <summary>
        /// Serializes a snapshot into a provider-independent payload.
        /// </summary>
        /// <typeparam name="T">
        /// Type of the snapshot to serialize.
        /// </typeparam>
        /// <param name="snapshot">
        /// Snapshot instance to serialize.
        /// </param>
        /// <returns>
        /// Serialized payload.
        /// </returns>
        UniTask<string> SerializeAsync<T>(T snapshot);

        /// <summary>
        /// Deserializes a snapshot from a serialized payload.
        /// </summary>
        /// <typeparam name="T">
        /// Expected snapshot type.
        /// </typeparam>
        /// <param name="payload">
        /// Serialized payload.
        /// </param>
        /// <returns>
        /// Deserialized snapshot.
        /// </returns>
        UniTask<T> DeserializeAsync<T>(string payload);

        /// <summary>
        /// Deserializes a snapshot when its type is only known at runtime.
        /// </summary>
        /// <param name="payload">
        /// Serialized payload.
        /// </param>
        /// <param name="snapshotType">
        /// Type of snapshot to reconstruct.
        /// </param>
        /// <returns>
        /// Deserialized snapshot instance.
        /// </returns>
        UniTask<object> DeserializeAsync(string payload, Type snapshotType);
    }
}
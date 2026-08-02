using Cysharp.Threading.Tasks;

namespace HelloDev.Saving
{
    /// <summary>
    /// Interface for save data providers. Implement this to integrate with your
    /// preferred save system (JSON files, PlayerPrefs, Easy Save 3, cloud saves, etc.).
    ///
    /// The SaveService uses this interface to save/load data without being coupled
    /// to a specific storage implementation.
    /// </summary>
    public interface ISaveProvider
    {
        /// <summary>
        /// Saves data asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of data to save (must be serializable).</typeparam>
        /// <param name="key">Unique identifier for this save data.</param>
        /// <param name="data">The data to save.</param>
        /// <returns>True if save was successful.</returns>
        UniTask<bool> SaveAsync<T>(string key, T data);

        /// <summary>
        /// Loads data asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of data to load.</typeparam>
        /// <param name="key">Unique identifier for the save data.</param>
        /// <returns>The loaded data, or default(T) if not found or failed.</returns>
        UniTask<T> LoadAsync<T>(string key);

        /// <summary>
        /// Checks if save data exists for the given key.
        /// </summary>
        /// <param name="key">Unique identifier to check.</param>
        /// <returns>True if data exists for this key.</returns>
        UniTask<bool> ExistsAsync(string key);

        /// <summary>
        /// Deletes save data for the given key.
        /// </summary>
        /// <param name="key">Unique identifier to delete.</param>
        /// <returns>True if deletion was successful or key didn't exist.</returns>
        UniTask<bool> DeleteAsync(string key);

        /// <summary>
        /// Gets all saved keys, optionally filtered by prefix.
        /// </summary>
        /// <param name="prefix">Optional prefix to filter keys (e.g., "quest.", "inventory.").</param>
        /// <returns>Array of matching keys.</returns>
        UniTask<string[]> GetKeysAsync(string prefix = null);
    }
}

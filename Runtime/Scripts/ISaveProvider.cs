using System.Threading.Tasks;

namespace HelloDev.Saving
{
    /// <summary>
    /// Interface for save data providers. Implement this to integrate with your
    /// preferred save system (JSON files, PlayerPrefs, Easy Save 3, cloud saves, etc.).
    ///
    /// The SaveService uses this interface to save/load data without being coupled
    /// to a specific storage implementation.
    /// </summary>
    /// <example>
    /// // Example: JSON file implementation
    /// public class JsonSaveProvider : ISaveProvider
    /// {
    ///     public async Task&lt;bool&gt; SaveAsync&lt;T&gt;(string key, T data)
    ///     {
    ///         var json = JsonUtility.ToJson(data, true);
    ///         await File.WriteAllTextAsync($"{key}.json", json);
    ///         return true;
    ///     }
    /// }
    ///
    /// // Example: Easy Save 3 implementation
    /// public class ES3SaveProvider : ISaveProvider
    /// {
    ///     public Task&lt;bool&gt; SaveAsync&lt;T&gt;(string key, T data)
    ///     {
    ///         ES3.Save(key, data);
    ///         return Task.FromResult(true);
    ///     }
    /// }
    /// </example>
    public interface ISaveProvider
    {
        /// <summary>
        /// Saves data asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of data to save (must be serializable).</typeparam>
        /// <param name="key">Unique identifier for this save data.</param>
        /// <param name="data">The data to save.</param>
        /// <returns>True if save was successful.</returns>
        Task<bool> SaveAsync<T>(string key, T data);

        /// <summary>
        /// Loads data asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of data to load.</typeparam>
        /// <param name="key">Unique identifier for the save data.</param>
        /// <returns>The loaded data, or default(T) if not found or failed.</returns>
        Task<T> LoadAsync<T>(string key);

        /// <summary>
        /// Checks if save data exists for the given key.
        /// </summary>
        /// <param name="key">Unique identifier to check.</param>
        /// <returns>True if data exists for this key.</returns>
        Task<bool> ExistsAsync(string key);

        /// <summary>
        /// Deletes save data for the given key.
        /// </summary>
        /// <param name="key">Unique identifier to delete.</param>
        /// <returns>True if deletion was successful or key didn't exist.</returns>
        Task<bool> DeleteAsync(string key);

        /// <summary>
        /// Gets all saved keys, optionally filtered by prefix.
        /// </summary>
        /// <param name="prefix">Optional prefix to filter keys (e.g., "quest.", "inventory.").</param>
        /// <returns>Array of matching keys.</returns>
        /// <example>
        /// // Get all quest saves
        /// var questKeys = await provider.GetKeysAsync("quest.");
        ///
        /// // Get all saves
        /// var allKeys = await provider.GetKeysAsync();
        /// </example>
        Task<string[]> GetKeysAsync(string prefix = null);
    }
}

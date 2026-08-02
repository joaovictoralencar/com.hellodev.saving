using Cysharp.Threading.Tasks;
using Logger = HelloDev.Logging.Logger;

namespace HelloDev.Saving.Core
{
    /// <summary>
    /// Static service providing access to the current save provider.
    /// Set the provider once during application startup.
    ///
    /// This follows the same pattern as <see cref="Core.SaveSerializerService"/>,
    /// allowing the storage backend to be swapped without changing
    /// the save framework.
    /// </summary>
    public static class SaveProviderService
    {
        private static ISaveProvider _provider;

        /// <summary>
        /// Gets the active provider.
        /// Returns a null provider if none has been configured.
        /// </summary>
        public static ISaveProvider Provider => _provider ?? NullSaveProvider.Instance;

        /// <summary>
        /// Returns true if a provider has been configured.
        /// </summary>
        public static bool IsConfigured => _provider != null;

        /// <summary>
        /// Sets the provider used by the save framework.
        /// </summary>
        public static void SetProvider(ISaveProvider provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// Removes the current provider.
        /// </summary>
        public static void ClearProvider()
        {
            _provider = null;
        }
    }

    /// <summary>
    /// Fallback provider used when no provider has been configured.
    /// All operations safely return empty/failed values.
    /// </summary>
    internal sealed class NullSaveProvider : ISaveProvider
    {
        public static readonly NullSaveProvider Instance = new();

        private NullSaveProvider() { }

        public UniTask<bool> SaveAsync<T>(string key, T data)
        {
            Logger.LogWarning("Save", "No save provider configured.");
            return UniTask.FromResult(false);
        }

        public UniTask<T> LoadAsync<T>(string key)
        {
            Logger.LogWarning("Save", "No save provider configured.");
            return UniTask.FromResult(default(T));
        }

        public UniTask<bool> ExistsAsync(string key)
        {
            Logger.LogWarning("Save", "No save provider configured.");
            return UniTask.FromResult(false);
        }

        public UniTask<bool> DeleteAsync(string key)
        {
            Logger.LogWarning("Save", "No save provider configured.");
            return UniTask.FromResult(false);
        }

        public UniTask<string[]> GetKeysAsync(string prefix = null)
        {
            Logger.LogWarning("Save", "No save provider configured.");
            return UniTask.FromResult(System.Array.Empty<string>());
        }
    }
}
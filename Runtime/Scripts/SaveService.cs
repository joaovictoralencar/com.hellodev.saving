using System;
using System.Threading.Tasks;
using HelloDev.Logging;
using UnityEngine;
using Logger = HelloDev.Logging.Logger;

namespace HelloDev.Saving
{
    /// <summary>
    /// Static service providing access to the current save provider.
    /// Set the provider once at application startup.
    ///
    /// This follows the same pattern as TweenService - a static service with
    /// a pluggable provider that can be implemented by any save system.
    /// </summary>
    /// <example>
    /// // At application startup (e.g., in a bootstrap script)
    /// SaveService.SetProvider(new JsonSaveProvider());
    ///
    /// // Usage anywhere in the application
    /// await SaveService.Provider.SaveAsync("player.inventory", inventoryData);
    /// var inventory = await SaveService.Provider.LoadAsync&lt;InventoryData&gt;("player.inventory");
    /// </example>
    public static class SaveService
    {
        private static ISaveProvider _provider;

        /// <summary>
        /// Gets the current save provider. Returns a NullSaveProvider if none is set.
        /// </summary>
        public static ISaveProvider Provider => _provider ?? NullSaveProvider.Instance;

        /// <summary>
        /// Returns true if a save provider has been configured.
        /// </summary>
        public static bool IsConfigured => _provider != null;

        /// <summary>
        /// Sets the save provider to use throughout the application.
        /// Call this once at application startup (e.g., in a bootstrap script).
        /// </summary>
        /// <param name="provider">The save provider implementation to use.</param>
        public static void SetProvider(ISaveProvider provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// Clears the current provider (useful for testing or shutdown).
        /// </summary>
        public static void ClearProvider()
        {
            _provider = null;
        }
    }

    /// <summary>
    /// A no-op save provider that does nothing. Used as fallback when no provider is configured.
    /// All operations return safe default values.
    /// </summary>
    internal class NullSaveProvider : ISaveProvider
    {
        public static readonly NullSaveProvider Instance = new();

        private NullSaveProvider() { }

        public Task<bool> SaveAsync<T>(string key, T data)
        {
            Logger.LogWarning(LogSystems.Save, "No provider configured. Save operation ignored.");
            return Task.FromResult(false);
        }

        public Task<T> LoadAsync<T>(string key)
        {
            Logger.LogWarning(LogSystems.Save, "No provider configured. Load operation returned default.");
            return Task.FromResult(default(T));
        }

        public Task<bool> ExistsAsync(string key)
        {
            return Task.FromResult(false);
        }

        public Task<bool> DeleteAsync(string key)
        {
            return Task.FromResult(true);
        }

        public Task<string[]> GetKeysAsync(string prefix = null)
        {
            return Task.FromResult(Array.Empty<string>());
        }
    }
}

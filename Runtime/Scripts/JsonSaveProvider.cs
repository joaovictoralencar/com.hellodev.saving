using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HelloDev.Logging;
using UnityEngine;
using Logger = HelloDev.Logging.Logger;

namespace HelloDev.Saving
{
    /// <summary>
    /// Default save provider that uses JSON files in Unity's persistent data path.
    /// This is a simple implementation suitable for development and single-player games.
    ///
    /// For production games, consider implementing your own ISaveProvider for:
    /// - Cloud saves (Steam Cloud, PlayStation, Xbox, etc.)
    /// - Encrypted saves
    /// - Binary serialization for smaller files
    /// - Integration with third-party save systems (Easy Save 3, etc.)
    /// </summary>
    /// <example>
    /// // Basic usage
    /// SaveService.SetProvider(new JsonSaveProvider());
    ///
    /// // Custom directory and extension
    /// SaveService.SetProvider(new JsonSaveProvider(
    ///     subdirectory: "MySaves",
    ///     fileExtension: ".sav",
    ///     prettyPrint: true
    /// ));
    /// </example>
    public class JsonSaveProvider : ISaveProvider
    {
        private readonly string _saveDirectory;
        private readonly string _fileExtension;
        private readonly bool _prettyPrint;

        /// <summary>
        /// Creates a new JSON file save provider.
        /// </summary>
        /// <param name="subdirectory">Subdirectory within Application.persistentDataPath (default: "Saves").</param>
        /// <param name="fileExtension">File extension for save files (default: ".json").</param>
        /// <param name="prettyPrint">If true, JSON output is formatted for readability.</param>
        public JsonSaveProvider(
            string subdirectory = "Saves",
            string fileExtension = ".json",
            bool prettyPrint = false)
        {
            _saveDirectory = Path.Combine(Application.persistentDataPath, subdirectory);
            _fileExtension = fileExtension.StartsWith(".") ? fileExtension : "." + fileExtension;
            _prettyPrint = prettyPrint;

            EnsureDirectoryExists();
        }

        /// <inheritdoc/>
        public Task<bool> SaveAsync<T>(string key, T data)
        {
            try
            {
                EnsureDirectoryExists();

                string filePath = GetFilePath(key);
                string json = JsonUtility.ToJson(data, _prettyPrint);

                File.WriteAllText(filePath, json);

                Logger.LogVerbose(LogSystems.Save, $"Saved: {key}");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Logger.LogError(LogSystems.Save, $"Save failed for '{key}': {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc/>
        public Task<T> LoadAsync<T>(string key)
        {
            try
            {
                string filePath = GetFilePath(key);

                if (!File.Exists(filePath))
                {
                    Logger.LogWarning(LogSystems.Save, $"File not found: {key}");
                    return Task.FromResult(default(T));
                }

                string json = File.ReadAllText(filePath);
                T data = JsonUtility.FromJson<T>(json);

                Logger.LogVerbose(LogSystems.Save, $"Loaded: {key}");
                return Task.FromResult(data);
            }
            catch (Exception ex)
            {
                Logger.LogError(LogSystems.Save, $"Load failed for '{key}': {ex.Message}");
                return Task.FromResult(default(T));
            }
        }

        /// <inheritdoc/>
        public Task<bool> ExistsAsync(string key)
        {
            string filePath = GetFilePath(key);
            return Task.FromResult(File.Exists(filePath));
        }

        /// <inheritdoc/>
        public Task<bool> DeleteAsync(string key)
        {
            try
            {
                string filePath = GetFilePath(key);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Logger.LogVerbose(LogSystems.Save, $"Deleted: {key}");
                }

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Logger.LogError(LogSystems.Save, $"Delete failed for '{key}': {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc/>
        public Task<string[]> GetKeysAsync(string prefix = null)
        {
            try
            {
                if (!Directory.Exists(_saveDirectory))
                {
                    return Task.FromResult(Array.Empty<string>());
                }

                var files = Directory.GetFiles(_saveDirectory, $"*{_fileExtension}");
                var keys = files
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .Where(k => string.IsNullOrEmpty(prefix) || k.StartsWith(prefix))
                    .ToArray();

                return Task.FromResult(keys);
            }
            catch (Exception ex)
            {
                Logger.LogError(LogSystems.Save, $"GetKeys failed: {ex.Message}");
                return Task.FromResult(Array.Empty<string>());
            }
        }

        /// <summary>
        /// Gets the full file path for a save key.
        /// Handles key sanitization for safe file names.
        /// </summary>
        private string GetFilePath(string key)
        {
            // Replace dots with underscores for file safety, but preserve the key structure
            string safeKey = key.Replace("/", "_").Replace("\\", "_");
            return Path.Combine(_saveDirectory, $"{safeKey}{_fileExtension}");
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_saveDirectory))
            {
                Directory.CreateDirectory(_saveDirectory);
            }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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
    public class JsonSaveProvider : ISaveProvider
    {
        private readonly string _saveDirectory;
        private readonly string _fileExtension;
        private readonly bool _prettyPrint;
        private readonly JsonSerializerSettings _jsonSettings;

        /// <summary>
        /// Creates a new JSON file save provider using Newtonsoft.Json.
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

            // Configure Newtonsoft settings – you can adjust these to your needs
            _jsonSettings = new JsonSerializerSettings
            {
                Formatting = prettyPrint ? Formatting.Indented : Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,           // optional: omit nulls to reduce file size
                MissingMemberHandling = MissingMemberHandling.Error,    // strict contract (throws on unknown fields)
                Error = (obj, args) =>
                {
                    // Log deserialization errors but continue (handles per-field failures)
                    Logger.LogError("Save", $"JSON error: {args.ErrorContext.Error.Message}");
                    args.ErrorContext.Handled = true;
                },
                Converters =
                {
                    new StringEnumConverter() // saves enums as strings, not integers
                }
            };

            EnsureDirectoryExists();
        }

        /// <inheritdoc/>
        public UniTask<bool> SaveAsync<T>(string key, T data)
        {
            try
            {
                EnsureDirectoryExists();

                string filePath = GetFilePath(key);
                string json = JsonConvert.SerializeObject(data, _jsonSettings);

                File.WriteAllText(filePath, json);

                Logger.LogVerbose("Save", $"Saved: {key}");
                return UniTask.FromResult(true);
            }
            catch (Exception ex)
            {
                Logger.LogError("Save", $"Save failed for '{key}': {ex.Message}");
                return UniTask.FromResult(false);
            }
        }

        /// <inheritdoc/>
        public UniTask<T> LoadAsync<T>(string key)
        {
            try
            {
                string filePath = GetFilePath(key);

                if (!File.Exists(filePath))
                {
                    Logger.LogWarning("Save", $"File not found: {key}");
                    return UniTask.FromResult(default(T));
                }

                string json = File.ReadAllText(filePath);
                T data = JsonConvert.DeserializeObject<T>(json, _jsonSettings);

                Logger.LogVerbose("Save", $"Loaded: {key}");
                return UniTask.FromResult(data);
            }
            catch (Exception ex)
            {
                Logger.LogError("Save", $"Load failed for '{key}': {ex.Message}");
                return UniTask.FromResult(default(T));
            }
        }

        /// <inheritdoc/>
        public UniTask<bool> ExistsAsync(string key)
        {
            string filePath = GetFilePath(key);
            return UniTask.FromResult(File.Exists(filePath));
        }

        /// <inheritdoc/>
        public UniTask<bool> DeleteAsync(string key)
        {
            try
            {
                string filePath = GetFilePath(key);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Logger.LogVerbose("Save", $"Deleted: {key}");
                }

                return UniTask.FromResult(true);
            }
            catch (Exception ex)
            {
                Logger.LogError("Save", $"Delete failed for '{key}': {ex.Message}");
                return UniTask.FromResult(false);
            }
        }

        /// <inheritdoc/>
        public UniTask<string[]> GetKeysAsync(string prefix = null)
        {
            try
            {
                if (!Directory.Exists(_saveDirectory))
                {
                    return UniTask.FromResult(Array.Empty<string>());
                }

                var files = Directory.GetFiles(_saveDirectory, $"*{_fileExtension}");
                var keys = files
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .Where(k => string.IsNullOrEmpty(prefix) || k.StartsWith(prefix))
                    .ToArray();

                return UniTask.FromResult(keys);
            }
            catch (Exception ex)
            {
                Logger.LogError("Save", $"GetKeys failed: {ex.Message}");
                return UniTask.FromResult(Array.Empty<string>());
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
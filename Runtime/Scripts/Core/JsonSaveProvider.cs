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
    public class JsonSaveProvider : ISaveProvider
    {
        private readonly string _saveDirectory;
        private readonly string _fileExtension;
        private readonly bool _prettyPrint;
        private readonly JsonSerializerSettings _jsonSettings;

        public JsonSaveProvider(
            string subdirectory = "Saves",
            string fileExtension = ".json",
            bool prettyPrint = false)
        {
            _saveDirectory = Path.Combine(Application.persistentDataPath, subdirectory);
            _fileExtension = fileExtension.StartsWith(".") ? fileExtension : "." + fileExtension;

            _jsonSettings = new JsonSerializerSettings
            {
                Formatting = prettyPrint ? Formatting.Indented : Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Error,
                Error = (obj, args) =>
                {
                    Logger.LogError("Save", $"JSON error: {args.ErrorContext.Error.Message}");
                    args.ErrorContext.Handled = true;
                },
                Converters =
                {
                    new StringEnumConverter()
                }
            };

            EnsureDirectoryExists();
        }

        public async UniTask<bool> SaveAsync<T>(string key, T data)
        {
            try
            {
                EnsureDirectoryExists();

                string filePath = GetFilePath(key);
                string tempFilePath = filePath + ".tmp";

                string json = await SerializeAsync(data);

                await WriteFileAsync(tempFilePath, json);

                ReplaceFile(tempFilePath, filePath);

                Logger.LogVerbose("Save", $"Saved: {key}");

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Save", $"Save failed for '{key}': {ex}");
                return false;
            }
        }

        public async UniTask<T> LoadAsync<T>(string key)
        {
            try
            {
                string filePath = GetFilePath(key);

                if (!File.Exists(filePath))
                {
                    Logger.LogWarning("Save", $"File not found: {key}");
                    return default;
                }

                string json = await ReadFileAsync(filePath);

                T data = await DeserializeAsync<T>(json);

                Logger.LogVerbose("Save", $"Loaded: {key}");

                return data;
            }
            catch (Exception ex)
            {
                Logger.LogError("Save", $"Load failed for '{key}': {ex}");
                return default;
            }
        }

        public UniTask<bool> ExistsAsync(string key)
        {
            string filePath = GetFilePath(key);
            return UniTask.FromResult(File.Exists(filePath));
        }

        public UniTask<bool> DeleteAsync(string key)
        {
            try
            {
                string filePath = GetFilePath(key);

                if (File.Exists(filePath))
                    File.Delete(filePath);

                string tempFile = filePath + ".tmp";

                if (File.Exists(tempFile))
                    File.Delete(tempFile);

                Logger.LogVerbose("Save", $"Deleted: {key}");

                return UniTask.FromResult(true);
            }
            catch (Exception ex)
            {
                Logger.LogError("Save", $"Delete failed for '{key}': {ex.Message}");
                return UniTask.FromResult(false);
            }
        }

        public UniTask<string[]> GetKeysAsync(string prefix = null)
        {
            try
            {
                if (!Directory.Exists(_saveDirectory))
                    return UniTask.FromResult(Array.Empty<string>());

                string[] files = Directory.GetFiles(_saveDirectory, $"*{_fileExtension}");

                string[] keys = files
                    .Select(Path.GetFileNameWithoutExtension)
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

        private async UniTask<string> SerializeAsync<T>(T data)
        {
            return await UniTask.RunOnThreadPool(() => JsonConvert.SerializeObject(data, _jsonSettings));
        }

        private async UniTask<T> DeserializeAsync<T>(string json)
        {
            return await UniTask.RunOnThreadPool(() =>
            {
                return JsonConvert.DeserializeObject<T>(json, _jsonSettings);
            });
        }

        private static async UniTask WriteFileAsync(string path, string contents)
        {
            await File.WriteAllTextAsync(path, contents);
        }

        private static async UniTask<string> ReadFileAsync(string path)
        {
            return await File.ReadAllTextAsync(path);
        }

        private static void ReplaceFile(string tempPath, string destinationPath)
        {
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);

            File.Move(tempPath, destinationPath);
        }

        private string GetFilePath(string key)
        {
            string safeKey = key.Replace("/", "_").Replace("\\", "_");
            return Path.Combine(_saveDirectory, $"{safeKey}{_fileExtension}");
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_saveDirectory))
                Directory.CreateDirectory(_saveDirectory);
        }
    }
}
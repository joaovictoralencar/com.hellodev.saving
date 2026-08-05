using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HelloDev.Saving.Interfaces;
using Logger = HelloDev.Logging.Logger;

namespace HelloDev.Saving.Core
{
    /// <summary>
    /// Coordinates save and load requests.
    /// Ensures only one save/load operation runs at a time and provides
    /// a safe entry point for autosave requests.
    /// </summary>
    public sealed class SaveScheduler : IDisposable
    {
        private readonly IUnifiedSaveManager _manager;
        private readonly SemaphoreSlim _operationLock = new(1, 1);

        private CancellationTokenSource _autoSaveCancellation;
        private string _autoSaveSlot;

        private readonly float AutoSaveDelay;

        public bool IsSaving { get; private set; }

        public bool IsLoading { get; private set; }

        public bool IsBusy => IsSaving || IsLoading;

        public event Action<string> SaveStarted;
        public event Action<string, bool> SaveCompleted;

        public event Action<string> LoadStarted;
        public event Action<string, bool> LoadCompleted;

        public SaveScheduler(IUnifiedSaveManager manager, float autoSaveDelay)
        {
            AutoSaveDelay = autoSaveDelay;
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        /// <summary>
        /// Immediately executes a save.
        /// Manual saves should use this method.
        /// </summary>
        public async UniTask<bool> SaveAsync(string slot)
        {
            await _operationLock.WaitAsync();

            try
            {
                IsSaving = true;

                SaveStarted?.Invoke(slot);

                bool success = await _manager.SaveAsync(slot);

                SaveCompleted?.Invoke(slot, success);

                return success;
            }
            catch (Exception ex)
            {
                Logger.LogError("Save", $"Unexpected exception while saving '{slot}': {ex}");

                SaveCompleted?.Invoke(slot, false);

                return false;
            }
            finally
            {
                IsSaving = false;
                _operationLock.Release();
            }
        }

        /// <summary>
        /// Immediately executes a load.
        /// </summary>
        public async UniTask<bool> LoadAsync(string slot)
        {
            await _operationLock.WaitAsync();

            try
            {
                IsLoading = true;

                LoadStarted?.Invoke(slot);

                bool success = await _manager.LoadAsync(slot);

                LoadCompleted?.Invoke(slot, success);

                return success;
            }
            catch (Exception ex)
            {
                Logger.LogError("Save", $"Unexpected exception while loading '{slot}': {ex}");

                LoadCompleted?.Invoke(slot, false);

                return false;
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        /// <summary>
        /// Requests an autosave.
        /// Multiple requests during the delay period are merged into one save.
        /// </summary>
        public void RequestAutoSave(string slot)
        {
            _autoSaveSlot = slot;

            _autoSaveCancellation?.Cancel();
            _autoSaveCancellation?.Dispose();

            _autoSaveCancellation = new CancellationTokenSource();

            AutoSaveDelayedAsync(_autoSaveCancellation.Token).Forget();
        }

        private async UniTaskVoid AutoSaveDelayedAsync(CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(AutoSaveDelay),
                    cancellationToken: cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                    return;

                await SaveAsync(_autoSaveSlot);
            }
            catch (OperationCanceledException)
            {
                // Expected when another autosave request resets the timer.
            }
            catch (Exception ex)
            {
                Logger.LogError("Save", $"Autosave failed: {ex}");
            }
        }

        public void Dispose()
        {
            _autoSaveCancellation?.Cancel();
            _autoSaveCancellation?.Dispose();

            _operationLock.Dispose();
        }
    }
}
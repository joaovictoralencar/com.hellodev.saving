using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Save tasks currently in flight, keyed by slot. Lets concurrent
        /// SaveAsync(slot) callers for the same slot (e.g. several systems
        /// requesting a save the same frame) share one real save instead of
        /// each queuing up behind <see cref="_operationLock"/> for a
        /// separate, redundant save. Populated with a <c>.Preserve()</c>'d
        /// UniTask so every caller can safely await the same instance.
        /// </summary>
        private readonly Dictionary<string, UniTask<bool>> _inFlightSaves = new();

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
        /// Executes a save. If a save for <paramref name="slot"/> is already
        /// in flight, returns that same pending task instead of starting a
        /// second one. Manual saves should use this method.
        /// </summary>
        public UniTask<bool> SaveAsync(string slot)
        {
            if (_inFlightSaves.TryGetValue(slot, out UniTask<bool> inFlight))
            {
                Logger.LogVerbose("Save", $"Slot '{slot}': save already in-flight, sharing pending result.");
                return inFlight;
            }

            UniTask<bool> task = SaveInternalAsync(slot).Preserve();
            _inFlightSaves[slot] = task;
            return task;
        }

        private async UniTask<bool> SaveInternalAsync(string slot)
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
                _inFlightSaves.Remove(slot);
            }
        }

        /// <summary>
        /// Immediately executes a load.
        /// </summary>
        public async UniTask<bool> LoadAsync(string slot, bool forceReload = false)
        {
            await _operationLock.WaitAsync();

            try
            {
                IsLoading = true;

                LoadStarted?.Invoke(slot);

                bool success = await _manager.LoadAsync(slot, forceReload);

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
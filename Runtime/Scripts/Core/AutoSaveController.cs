using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace HelloDev.Saving.Core
{
    public sealed class AutoSaveController
    {
        private readonly SaveScheduler _scheduler;
        private readonly string _slot;
        private readonly float _interval;

        private CancellationTokenSource _cts;
        
        bool hasStarted;

        public AutoSaveController(SaveScheduler scheduler, string slot, float interval)
        {
            _scheduler = scheduler;
            _slot = slot;
            _interval = interval;
        }

        public void Start()
        {
            if (hasStarted)
                return;
            
            _cts = new CancellationTokenSource();
            RunLoopAsync(_cts.Token).Forget();
            hasStarted = true;
        }

        public void Stop()
        {
            _cts?.Cancel();
            hasStarted = false;
        }

        private async UniTask RunLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_interval), cancellationToken: token);

                await _scheduler.SaveAsync(_slot);
            }
        }
    }
}
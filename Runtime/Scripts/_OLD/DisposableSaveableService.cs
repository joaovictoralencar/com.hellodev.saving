using UnityEngine;

using System;
using HelloDev.Logging;
using Logger = HelloDev.Logging.Logger;

namespace HelloDev.Saving
{
    /// <summary>
    /// Generic base class for non-MonoBehaviour savable systems that implement IDisposable.
    /// Automatically unregisters from the save manager when disposed.
    /// 
    /// This class is useful for:
    /// - Services with deterministic lifetime management
    /// - Systems that need explicit cleanup
    /// - Using with 'using' statements for automatic disposal
    /// 
    /// This class automatically:
    /// - Implements IDisposable with proper dispose pattern
    /// - Unregisters from save manager on disposal
    /// - Prevents multiple disposal
    /// - Provides type-safe Capture/Restore methods
    /// </summary>
    /// <typeparam name="TSnapshot">The snapshot type this system produces. Must be a [Serializable] class.</typeparam>
    public abstract class DisposableSaveableService<TSnapshot> : SaveableService<TSnapshot>, IDisposable
        where TSnapshot : class
    {
        #region Private Fields

        private bool _disposed;

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes this service and unregisters from the save manager.
        /// Safe to call multiple times - subsequent calls are ignored.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Dispose(true);
            GC.SuppressFinalize(this);
            _disposed = true;
        }

        /// <summary>
        /// Performs disposal of managed and unmanaged resources.
        /// Override this to add custom cleanup logic.
        /// Always call base.Dispose(disposing) in your override.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unregister from save manager
                Unregister();
                
                Logger.LogVerbose("Save", $"[{SystemKey}] Disposed");
            }
        }

        /// <summary>
        /// Finalizer to ensure unregistration even if Dispose() is not called.
        /// Note: Relying on the finalizer is not recommended - always call Dispose() explicitly.
        /// </summary>
        ~DisposableSaveableService()
        {
            if (!_disposed)
            {
                Logger.LogWarning("Save", 
                    $"[{SystemKey}] Finalizer called without Dispose(). Always call Dispose() explicitly.");
                Dispose(false);
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns true if this service has been disposed.
        /// </summary>
        public bool IsDisposed => _disposed;

        #endregion

        #region Helper Methods

        /// <summary>
        /// Throws ObjectDisposedException if this service has been disposed.
        /// Call this at the start of any public method to prevent use after disposal.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if service has been disposed.</exception>
        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    GetType().Name,
                    $"Cannot use {SystemKey} after it has been disposed.");
            }
        }

        #endregion
    }
}
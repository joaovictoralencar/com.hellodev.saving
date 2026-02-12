using System.Threading.Tasks;
using HelloDev.Bootstrap;
using HelloDev.Logging;
using HelloDev.Utils;
using UnityEngine;
using Logger = HelloDev.Logging.Logger;

namespace HelloDev.Saving
{
    /// <summary>
    /// Generic base class for non-MonoBehaviour saveable systems that participate in bootstrap initialization.
    /// Combines SaveableService manual registration with IBootstrapInitializable lifecycle.
    /// 
    /// This class automatically:
    /// - Implements IBootstrapInitializable for coordinated initialization
    /// - Registers with GameContext
    /// - Registers with UnifiedSaveManager from context
    /// - Provides type-safe Capture/Restore methods
    /// - Handles all boilerplate
    /// 
    /// Use this for services/managers that need to initialize alongside other bootstrap systems.
    /// Unlike SaveableService, this handles registration automatically via Bootstrap.
    /// </summary>
    /// <typeparam name="TSnapshot">The snapshot type this system produces. Must be a [Serializable] class.</typeparam>
    public abstract class BootstrappedSaveableService<TSnapshot> : SaveableService<TSnapshot>, IBootstrapInitializable
        where TSnapshot : class
    {
        #region Private Fields

        private bool _isInitialized;
        private GameContext _context;

        #endregion

        #region IBootstrapInitializable Implementation

        /// <summary>
        /// Whether this service has completed initialization.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Whether this service should self-initialize if not managed by GameBootstrap.
        /// Set to false when using GameBootstrap for coordinated initialization.
        /// </summary>
        public virtual bool SelfInitialize { get; set; } = false;

        /// <summary>
        /// Receives the game context from GameBootstrap.
        /// Stores the context and registers with UnifiedSaveManager.
        /// 
        /// Override this to register your service in the context for retrieval elsewhere:
        /// <code>
        /// public override void ReceiveContext(GameContext context)
        /// {
        ///     base.ReceiveContext(context);
        ///     context.Register&lt;EconomyManager&gt;(this);  // Now retrievable via TryGet
        /// }
        /// </code>
        /// </summary>
        /// <param name="context">The game context for service registration.</param>
        public virtual void ReceiveContext(GameContext context)
        {
            if (context == null)
            {
                Debug.LogWarning($"[{SystemKey}] Received null context");
                return;
            }

            _context = context;

            // Auto-register with save manager from context
            if (_context.TryGet(out UnifiedSaveManager saveManager))
            {
                Register(saveManager);
                Logger.LogVerbose(LogSystems.Save, 
                    $"[{SystemKey}] Auto-registered with UnifiedSaveManager from context");
            }
            else
            {
                Debug.LogWarning($"[{SystemKey}] No UnifiedSaveManager found in context. " +
                    "This service will not be saved unless manually registered.");
            }
        }

        /// <summary>
        /// Initializes this saveable service.
        /// Called by GameBootstrap during initialization phase.
        /// Override to add custom initialization logic, but always call base.InitializeAsync().
        /// </summary>
        /// <returns>A task representing the initialization operation.</returns>
        public virtual Task InitializeAsync()
        {
            if (_isInitialized)
            {
                Debug.LogWarning($"[{SystemKey}] Already initialized");
                return Task.CompletedTask;
            }

            Logger.LogVerbose(LogSystems.Save, $"[{SystemKey}] Initializing...");
            _isInitialized = true;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Shuts down this saveable service.
        /// Unregisters from save manager and clears context reference.
        /// Override to add custom shutdown logic, but always call base.Shutdown().
        /// 
        /// Note: Does not unregister from context automatically. If you manually registered
        /// to context in ReceiveContext, you should unregister here:
        /// <code>
        /// public override void Shutdown()
        /// {
        ///     Context?.Unregister&lt;EconomyManager&gt;();
        ///     base.Shutdown();
        /// }
        /// </code>
        /// </summary>
        public virtual void Shutdown()
        {
            Unregister();

            if (_context != null)
            {
                _context = null;
            }

            _isInitialized = false;
            Logger.LogVerbose(LogSystems.Save, $"[{SystemKey}] Shutdown complete");
        }

        #endregion

        #region Protected Helpers

        /// <summary>
        /// Gets the GameContext this service was registered with.
        /// Returns null if ReceiveContext hasn't been called yet.
        /// </summary>
        protected GameContext Context => _context;

        #endregion
    }
}
using System.Threading.Tasks;
using HelloDev.Bootstrap;
using HelloDev.Logging;
using HelloDev.Utils;
using UnityEngine;
using Logger = HelloDev.Logging.Logger;

namespace HelloDev.Saving
{
    /// <summary>
    /// Generic base class for MonoBehaviour-based saveable systems that participate in bootstrap initialization.
    /// Combines SaveableSystem auto-registration with IBootstrapInitializable lifecycle.
    /// 
    /// This class automatically:
    /// - Implements IBootstrapInitializable for coordinated initialization
    /// - Registers with GameContext
    /// - Registers with UnifiedSaveManager from context (preferred over scene search)
    /// - Provides type-safe Capture/Restore methods
    /// - Handles all boilerplate
    /// 
    /// Use this when your saveable system needs to initialize alongside other bootstrap systems.
    /// </summary>
    /// <typeparam name="TSnapshot">The snapshot type this system produces. Must be a [Serializable] class.</typeparam>
    public abstract class BootstrappedSaveableSystem<TSnapshot> : SaveableSystem<TSnapshot>, IBootstrapInitializable
        where TSnapshot : class
    {
        #region Private Fields

        private bool _isInitialized;
        private GameContext _context;

        #endregion

        #region IBootstrapInitializable Implementation

        /// <summary>
        /// Whether this system has completed initialization.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Whether this system should self-initialize if not managed by GameBootstrap.
        /// Set to false when using GameBootstrap for coordinated initialization.
        /// </summary>
        public virtual bool SelfInitialize { get; set; } = false;

        /// <summary>
        /// Receives the game context from GameBootstrap.
        /// Stores the context and registers with UnifiedSaveManager.
        /// 
        /// Override this to register your system in the context for retrieval elsewhere:
        /// <code>
        /// public override void ReceiveContext(GameContext context)
        /// {
        ///     base.ReceiveContext(context);
        ///     context.Register&lt;PlayerTutorial&gt;(this);  // Now retrievable via TryGet
        /// }
        /// </code>
        /// </summary>
        /// <param name="context">The game context for service registration.</param>
        public virtual void ReceiveContext(GameContext context)
        {
            if (context == null)
            {
                Logger.LogWarning("Save", $"[{SystemKey}] Received null context", this);
                return;
            }

            _context = context;

            // Register with save manager from context (preferred over scene search)
            if (_context.TryGet(out UnifiedSaveManager saveManager))
            {
                ManualRegister(saveManager);
                Logger.LogVerbose("Save", 
                    $"[{SystemKey}] Registered with UnifiedSaveManager from context", 
                    this);
            }
            else
            {
                Logger.LogWarning("Save", 
                    $"[{SystemKey}] No UnifiedSaveManager in context. Falling back to scene search.", 
                    this);
            }
        }

        /// <summary>
        /// Initializes this saveable system.
        /// Called by GameBootstrap during initialization phase.
        /// Override to add custom initialization logic, but always call base.InitializeAsync().
        /// </summary>
        /// <returns>A task representing the initialization operation.</returns>
        public virtual Task InitializeAsync()
        {
            if (_isInitialized)
            {
                Logger.LogWarning("Save", $"[{SystemKey}] Already initialized", this);
                return Task.CompletedTask;
            }

            Logger.LogVerbose("Save", $"[{SystemKey}] Initializing...", this);
            _isInitialized = true;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Shuts down this saveable system.
        /// Unregisters from save manager and clears context reference.
        /// Override to add custom shutdown logic, but always call base.Shutdown().
        /// 
        /// Note: Does not unregister from context automatically. If you manually registered
        /// to context in ReceiveContext, you should unregister here:
        /// <code>
        /// public override void Shutdown()
        /// {
        ///     Context?.Unregister&lt;PlayerTutorial&gt;();
        ///     base.Shutdown();
        /// }
        /// </code>
        /// </summary>
        public virtual void Shutdown()
        {
            if (_context != null)
            {
                if (_context.TryGet(out UnifiedSaveManager saveManager))
                {
                    saveManager.UnregisterSystem(this);
                }
                
                _context = null;
            }

            _isInitialized = false;
            Logger.LogVerbose("Save", $"[{SystemKey}] Shutdown complete", this);
        }

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Override of base Awake to prevent double-registration when using Bootstrap.
        /// Auto-registration is skipped if SelfInitialize is false.
        /// </summary>
        protected override void Awake()
        {
            // Skip auto-registration since bootstrap will handle it via ReceiveContext
            if (SelfInitialize)
            {
                base.Awake();
            }
        }

        #endregion

        #region Protected Helpers

        /// <summary>
        /// Gets the GameContext this system was registered with.
        /// Returns null if ReceiveContext hasn't been called yet.
        /// </summary>
        protected GameContext Context => _context;

        #endregion
    }
}
using HelloDev.Utils;
using UnityEngine;
using UnityEngine.Events;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace HelloDev.Saving
{
    /// <summary>
    /// ScriptableObject locator for UnifiedSaveManager.
    /// Acts as a decoupled access point that any asset can reference.
    /// The UnifiedSaveManager registers itself with this locator on enable.
    /// </summary>
    /// <remarks>
    /// Usage:
    /// 1. Create a single UnifiedSaveLocator_SO asset in your project
    /// 2. Assign it to UnifiedSaveManager's "Locator" field
    /// 3. Reference the same asset anywhere you need save/load access
    /// 4. Access functionality via locator.Manager.MethodName()
    /// </remarks>
    [CreateAssetMenu(fileName = "UnifiedSaveLocator", menuName = "HelloDev/Locators/Unified Save Locator")]
    public class UnifiedSaveLocator_SO : LocatorBase_SO
    {
        #region Private Fields

        private UnifiedSaveManager _manager;

        #endregion

        #region LocatorBase_SO Implementation

        /// <inheritdoc/>
        public override bool IsAvailable => _manager != null;

        /// <inheritdoc/>
        public override void PrepareForBootstrap()
        {
            _manager = null;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the registered manager instance.
        /// </summary>
        public UnifiedSaveManager Manager => _manager;

        /// <summary>
        /// Gets whether a save provider has been configured via SaveService.SetProvider().
        /// </summary>
        public bool HasProvider => SaveService.IsConfigured;

        #endregion

        #region Events

        /// <summary>
        /// Fired when a manager registers with this locator.
        /// </summary>
        [System.NonSerialized]
        public UnityEvent OnManagerRegistered = new();

        /// <summary>
        /// Fired when a manager unregisters from this locator.
        /// </summary>
        [System.NonSerialized]
        public UnityEvent OnManagerUnregistered = new();

        /// <summary>
        /// Fired before a save operation starts. Proxied from manager.
        /// </summary>
        [System.NonSerialized]
        public UnityEvent<string> OnBeforeSave = new();

        /// <summary>
        /// Fired after a save operation completes. Proxied from manager.
        /// </summary>
        [System.NonSerialized]
        public UnityEvent<string, bool> OnAfterSave = new();

        /// <summary>
        /// Fired before a load operation starts. Proxied from manager.
        /// </summary>
        [System.NonSerialized]
        public UnityEvent<string> OnBeforeLoad = new();

        /// <summary>
        /// Fired after a load operation completes. Proxied from manager.
        /// </summary>
        [System.NonSerialized]
        public UnityEvent<string, bool> OnAfterLoad = new();

        #endregion

        #region Registration

        /// <summary>
        /// Registers a UnifiedSaveManager with this locator.
        /// Called by UnifiedSaveManager.OnEnable().
        /// </summary>
        public void Register(UnifiedSaveManager manager)
        {
            if (manager == null) return;

            if (_manager != null && _manager != manager)
            {
                Debug.LogWarning($"[UnifiedSaveLocator] Replacing existing manager. Old: {_manager.name}, New: {manager.name}");
            }

            _manager = manager;
            OnManagerRegistered?.Invoke();
        }

        /// <summary>
        /// Unregisters a UnifiedSaveManager from this locator.
        /// Called by UnifiedSaveManager.OnDisable().
        /// </summary>
        public void Unregister(UnifiedSaveManager manager)
        {
            if (_manager == manager)
            {
                _manager = null;
                OnManagerUnregistered?.Invoke();
            }
        }

        #endregion

        #region Debug

#if ODIN_INSPECTOR && UNITY_EDITOR
        [Title("Debug")]
        [ShowInInspector, ReadOnly]
        [PropertyOrder(100)]
        private bool ManagerRegistered => IsAvailable;

        [ShowInInspector, ReadOnly]
        [PropertyOrder(101)]
        private string ManagerName => _manager != null ? _manager.name : "(none)";

        [ShowInInspector, ReadOnly]
        [PropertyOrder(102)]
        private bool ProviderConfigured => SaveService.IsConfigured;

        [ShowInInspector, ReadOnly]
        [PropertyOrder(103)]
        private int RegisteredSystems => _manager?.RegisteredSystems.Count ?? 0;
#endif

        #endregion
    }
}

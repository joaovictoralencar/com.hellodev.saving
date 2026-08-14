using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using HelloDev.Saving.Interfaces;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEditor;
using UnityEngine;
using Logger = HelloDev.Logging.Logger;

namespace HelloDev.Saving.Core
{
    /// <summary>
    /// Base implementation of <see cref="ISavable"/>.
    /// Provides type-safe capture and restore methods while exposing
    /// the non-generic interface required by the save system.
    /// </summary>
    /// <typeparam name="TState">
    /// Serializable snapshot type produced by this savable.
    /// </typeparam>
    public abstract class SavableMonoBehaviour<TState> : MonoBehaviour, ISavable where TState : class
    {
#if ODIN_INSPECTOR
        [FoldoutGroup("Save Settings")]
#endif
        [SerializeField, HideInInspector] private string _saveId;

#if ODIN_INSPECTOR
        [FoldoutGroup("Save Settings")]
        [LabelText("Save/Load Enabled")]
        [Tooltip("When off, this savable never registers with the save module and is completely invisible to it.")]
#endif
        [SerializeField] private bool _saveLoadEnabled = true;

#if ODIN_INSPECTOR
        [FoldoutGroup("Save Settings")]
#endif
        [SerializeField] bool _loadAfterRegister = true;

        private ISaveModule _module;

        #region Properties

        /// <inheritdoc/>
        public string SaveId => _saveId;

        /// <summary>
        /// When false, this savable skips registration entirely and never
        /// participates in save/load for its module.
        /// </summary>
        public bool SaveLoadEnabled => _saveLoadEnabled;

#if ODIN_INSPECTOR
        [ShowInInspector, ReadOnly]
        [PropertyOrder(-100)]
        [LabelText("Save Id")]
        private string DebugSaveId => _saveId;
#endif

        public abstract string ModuleId { get; protected set; }

        /// <summary>
        /// Implement as a getter-only expression body (e.g.
        /// <c>=> UnifiedSaveManagerBehaviour.Instance.Manager;</c>)
        /// rather than a field initializer.
        /// </summary>
        public abstract IUnifiedSaveManager SaveManager { get; }

        /// <inheritdoc/>
        public Type StateType => typeof(TState);

        #endregion

        #region Save Interface

        object ISavable.SaveState()
        {
            Logger.Log("Save", $"Saving state for {gameObject.name}[{typeof(TState).FullName}]", gameObject);
            return SaveState();
        }

        async UniTask ISavable.LoadState(object state)
        {
            if (state is not TState snapshot)
                throw new InvalidOperationException($"Expected snapshot of type '{typeof(TState).FullName}', received '{state?.GetType().FullName ?? "null"}'.");


            Logger.Log("Save", $"Loading state for {gameObject.name}[{typeof(TState).FullName}]", gameObject);

            await LoadState(snapshot);
            OnAfterLoadState();
        }
        
        protected virtual void OnAfterLoadState()
        {
        }
        

        /// <summary>
        /// Captures the current state.
        /// </summary>
        protected abstract TState SaveState();

        /// <summary>
        /// Restores the current state.
        /// </summary>
        protected abstract UniTask LoadState(TState state);

        #endregion

        #region Registration Lifecycle

        protected virtual UniTask OnBeforeRegisterAsync()
        {
            return UniTask.CompletedTask;
        }

        protected virtual UniTask OnAfterRegisterAsync()
        {
            return UniTask.CompletedTask;
        }

        protected virtual async void Awake()
        {
#if UNITY_EDITOR
            EnsureUniqueSaveId();
#endif

            // Flag is off: skip registration entirely, this savable is
            // invisible to the save module for the rest of its lifetime.
            if (!_saveLoadEnabled)
            {
                Logger.LogVerbose("Save", $"Save/Load disabled for {gameObject.name}[{typeof(TState).FullName}], skipping registration", gameObject);
                return;
            }

            await OnBeforeRegisterAsync();

            _module = await SaveManager.RegisterModule(ModuleId);
            await _module.RegisterSavable(this);
            await OnAfterRegisterAsync();
            
            if (_loadAfterRegister)
                await SaveManager.LoadActiveSlotAsync();
        }

        protected virtual void OnDestroy()
        {
            _module?.UnregisterSavable(this);
        }

        #endregion

        protected async UniTask<bool> SaveOnActiveSlotAsync()
        {
            return await SaveManager.SaveActiveSlotAsync();
        }

#if UNITY_EDITOR

        #region Editor

        private void OnValidate()
        {
            EnsureUniqueSaveId();
        }

        private void EnsureUniqueSaveId()
        {
            // Never assign IDs to prefab assets.
            if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
                return;

            bool changed = false;

            // Generate an ID if one doesn't exist.
            if (string.IsNullOrWhiteSpace(_saveId))
            {
                _saveId = Guid.NewGuid().ToString("N");
                changed = true;
            }

            // Check for duplicate IDs among all scene ISavable objects.
            bool hasDuplicate = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                .OfType<ISavable>()
                .Where(s =>
                {
                    var mb = s as MonoBehaviour;
                    return mb != null &&
                           !EditorUtility.IsPersistent(mb) &&
                           mb.gameObject.scene.IsValid();
                })
                .Any(s => !ReferenceEquals(s, this) && s.SaveId == _saveId);

            if (hasDuplicate)
            {
                _saveId = Guid.NewGuid().ToString("N");
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(this);
        }

        #endregion

#endif
    }
}
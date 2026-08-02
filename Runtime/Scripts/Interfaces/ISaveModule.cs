// ISaveModule.cs
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HelloDev.Saving.Data;

namespace HelloDev.Saving.Interfaces
{
    /// <summary>
    /// Represents a group of savable objects.
    /// 
    /// Modules allow save data to be organized into logical sections
    /// instead of storing hundreds of independent entries.
    /// </summary>
    public interface ISaveModule
    {
        /// <summary>
        /// Unique identifier of this module.
        /// Example: "economy", "world", "player".
        /// </summary>
        string ModuleId { get; set; }
        
        /// <summary>
        /// Gets saveables owned by this module.
        /// </summary>
        IReadOnlyCollection<ISavable> Savables { get; }

        /// <summary>
        /// The most recently loaded state for this module, if any.
        /// Set whenever <see cref="LoadAsync"/> runs for this module,
        /// or handed to a newly-created module by the manager if a slot
        /// was already loaded before this module existed. Used so that
        /// savables registering after a load has already happened can
        /// immediately restore themselves in <see cref="RegisterSavable"/>.
        /// </summary>
        SaveModuleState LastLoadedState { get; set; }

        /// <summary>
        /// Captures all savables inside this module.
        /// </summary>
        UniTask<SaveModuleState> SaveAsync();

        /// <summary>
        /// Restores all currently-registered savables inside this module.
        /// </summary>
        UniTask<bool> LoadAsync(SaveModuleState saveModuleState);
        
        /// <summary>
        /// Registers a savable inside this module. If this module already
        /// has a <see cref="LastLoadedState"/>, the savable is restored
        /// immediately if a matching entry is found.
        /// </summary>
        UniTask RegisterSavable(ISavable savable);
        
        /// <summary>
        /// Unregisters a savable inside this module.
        /// </summary>
        bool UnregisterSavable(ISavable savable);
    }
}
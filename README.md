# HelloDev Saving

Unified save system for Unity games. Provides a centralized manager that coordinates saving/loading across multiple game systems via a modular interface.

## Features

- **UnifiedSaveManager** - Central coordinator for all save/load operations
- **ISaveableSystem** - Interface for systems that need persistence (quests, inventory, etc.)
- **ISaveProvider** - Interface for different storage backends (JSON, binary, cloud)
- **JsonSaveProvider** - Built-in JSON file persistence with pretty-print option
- **Slot-based Saving** - Multiple save slots with metadata
- **Auto-save Support** - Configurable auto-save intervals
- **Locator Pattern** - Decoupled access via UnifiedSaveLocator_SO
- **Bootstrap Integration** - Works with GameBootstrap for coordinated initialization

## Getting Started

### 1. Install the Package

**Via Package Manager (Local):**
1. Open Unity Package Manager (Window > Package Manager)
2. Click "+" > "Add package from disk"
3. Navigate to this folder and select `package.json`

**Dependencies:** Ensure `com.hellodev.utils` is installed.

### 2. Create Required Assets

1. Right-click in Project window
2. Create **HelloDev > Saving > Save System Settings** - Configure paths, auto-save, etc.
3. Create **HelloDev > Locators > Unified Save Locator** - For decoupled access

### 3. Set Up UnifiedSaveManager

Add `UnifiedSaveManager` component to a GameObject (typically on a persistent manager object):

```csharp
// The manager is configured via inspector:
// - Settings: SaveSystemSettings_SO asset
// - Locator: UnifiedSaveLocator_SO asset
// - Self Initialize: true for standalone, false when using GameBootstrap
```

### 4. Implement ISaveableSystem

Any system that needs persistence implements `ISaveableSystem`:

```csharp
using HelloDev.Saving;
using System.Collections.Generic;

public class InventoryManager : ISaveableSystem
{
    public string SystemId => "inventory";
    public int SavePriority => 50;  // Lower = saves first

    private List<string> _items = new();

    public object CaptureSnapshot()
    {
        // Return serializable data
        return new InventorySnapshot { Items = _items.ToList() };
    }

    public void RestoreSnapshot(object snapshot)
    {
        if (snapshot is InventorySnapshot data)
        {
            _items = data.Items.ToList();
        }
    }

    public void ResetToDefault()
    {
        _items.Clear();
    }
}

[System.Serializable]
public class InventorySnapshot
{
    public List<string> Items;
}
```

### 5. Register Systems with the Manager

```csharp
using HelloDev.Saving;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private UnifiedSaveLocator_SO saveLocator;

    private InventoryManager _inventory;

    void Start()
    {
        _inventory = new InventoryManager();

        if (saveLocator.IsAvailable)
        {
            saveLocator.Manager.RegisterSystem(_inventory);
        }
    }
}
```

### 6. Save and Load

```csharp
// Save to current slot
await saveLocator.Manager.SaveAsync();

// Save to specific slot
await saveLocator.Manager.SaveAsync(slotIndex: 1);

// Load from current slot
await saveLocator.Manager.LoadAsync();

// Load from specific slot
await saveLocator.Manager.LoadAsync(slotIndex: 1);

// Check if slot has data
bool hasSave = saveLocator.Manager.HasSaveData(slotIndex: 0);

// Delete save data
saveLocator.Manager.DeleteSave(slotIndex: 0);
```

## Architecture

```
UnifiedSaveManager (Coordinator)
    │
    ├── ISaveProvider (Storage Backend)
    │   └── JsonSaveProvider (default)
    │
    └── ISaveableSystem[] (Registered Systems)
        ├── QuestManager
        ├── InventoryManager
        └── SettingsManager
```

The manager:
1. Collects snapshots from all registered `ISaveableSystem` implementations
2. Packages them into a `UnifiedSnapshot` with metadata
3. Passes to `ISaveProvider` for persistence

## API Reference

### UnifiedSaveManager
| Member | Description |
|--------|-------------|
| `RegisterSystem(ISaveableSystem)` | Register a system for save/load |
| `UnregisterSystem(ISaveableSystem)` | Remove a system |
| `SaveAsync(int slot)` | Save all systems to slot |
| `LoadAsync(int slot)` | Load all systems from slot |
| `HasSaveData(int slot)` | Check if slot has data |
| `DeleteSave(int slot)` | Delete save at slot |
| `GetSlotMetadata(int slot)` | Get save metadata |
| `CurrentSlot` | Active save slot index |
| `IsSaving` | True during save operation |
| `IsLoading` | True during load operation |

### ISaveableSystem
| Member | Description |
|--------|-------------|
| `SystemId` | Unique identifier for this system |
| `SavePriority` | Order for save/load (lower = first) |
| `CaptureSnapshot()` | Return serializable state |
| `RestoreSnapshot(object)` | Restore from saved state |
| `ResetToDefault()` | Reset to initial state |

### ISaveProvider
| Member | Description |
|--------|-------------|
| `SaveAsync(slot, data)` | Persist data to storage |
| `LoadAsync(slot)` | Retrieve data from storage |
| `DeleteAsync(slot)` | Remove data from storage |
| `ExistsAsync(slot)` | Check if data exists |

## Dependencies

- com.hellodev.utils (1.4.0+)

## Changelog

### v1.0.0
- Initial release as standalone package
- Extracted from com.hellodev.utils

## License

MIT License

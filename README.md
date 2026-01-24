# HelloDev Saving

Unified save system for Unity games. Provides a centralized manager that coordinates saving/loading across multiple game systems via a modular interface.

## Features

- **UnifiedSaveManager** - Central coordinator for all save/load operations
- **ISaveableSystem** - Interface for systems that need persistence (quests, inventory, etc.)
- **ISaveProvider** - Interface for different storage backends (JSON, binary, cloud)
- **JsonSaveProvider** - Built-in JSON file persistence with pretty-print option
- **Slot-based Saving** - Multiple save slots with metadata
- **Auto-save Support** - Configurable auto-save on quit, pause, or interval
- **GameContext Integration** - Self-registers with bootstrap's GameContext for decoupled access
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
2. Create **HelloDev > Saving > Save System Settings** - Configure paths, file extension, etc.

### 3. Set Up UnifiedSaveManager

Add `UnifiedSaveManager` component to a GameObject (typically on a persistent manager object):

```csharp
// The manager is configured via inspector:
// - Settings: SaveSystemSettings_SO asset
// - Self Initialize: true for standalone, false when using GameBootstrap
// - Auto-save options: on quit, on pause, or interval-based
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

Systems can register with the save manager during bootstrap via GameContext:

```csharp
using HelloDev.Saving;
using HelloDev.Utils;
using UnityEngine;

public class InventoryManager : MonoBehaviour, IBootstrapInitializable, ISaveableSystem
{
    private GameContext _context;

    public void ReceiveContext(GameContext context) => _context = context;

    public Task InitializeAsync()
    {
        // Access the save manager from context and register
        if (_context.TryGet<UnifiedSaveManager>(out var saveManager))
        {
            saveManager.RegisterSystem(this);
        }
        return Task.CompletedTask;
    }

    // ISaveableSystem implementation...
}
```

Or register via direct reference:

```csharp
[SerializeField] private UnifiedSaveManager saveManager;

void Start()
{
    saveManager.RegisterSystem(new InventorySystem());
}
```

### 6. Save and Load

```csharp
// Get save manager from context or direct reference
var saveManager = _context.Get<UnifiedSaveManager>();

// Save to a slot
await saveManager.SaveAsync("slot_1");

// Load from a slot
await saveManager.LoadAsync("slot_1");

// Check if slot has data
bool exists = await saveManager.SaveExistsAsync("slot_1");

// Delete save data
await saveManager.DeleteSaveAsync("slot_1");

// Get slot metadata
var metadata = await saveManager.GetMetadataAsync("slot_1");
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
| `SaveAsync(string slotKey)` | Save all systems to slot |
| `LoadAsync(string slotKey)` | Load all systems from slot |
| `SaveExistsAsync(string slotKey)` | Check if slot has data |
| `DeleteSaveAsync(string slotKey)` | Delete save at slot |
| `GetMetadataAsync(string slotKey)` | Get save metadata |
| `RegisteredSystems` | Read-only list of registered systems |
| `DefaultSlotKey` | Default slot for auto-save/load |
| `HasProvider` | True if save provider is configured |
| `ReceiveContext(GameContext)` | Called by bootstrap for service registration |

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

## License

MIT License

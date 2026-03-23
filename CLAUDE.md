# CLAUDE.md

This file provides guidance to Claude Code when working with this repository.

## Project

[ALPHA] Pantheum — Starcraft-inspired RTS, Unity 6 (6000.3.6f1), URP 17.3.0.

## Key Packages

- URP 17.3.0 — all shaders/materials must be URP-compatible
- Input System 1.18.0 — use new Input System only (no legacy `Input.*` API)
- AI Navigation 2.0.9 — `NavMeshAgent` for all unit movement
- Unity Test Framework 1.6.0 — tests run via Window → Test Runner (no CLI)

## Folder Structure

```
Assets/Scripts/
├── Core/
│   ├── GameManager.cs          # Singleton: game state, pausing
│   ├── ResourceManager.cs      # Gold/Mana global totals + deposit logic
│   └── SupplyManager.cs        # Global supply cap (combat units only)
│
├── Buildings/
│   ├── BuildingBase.cs         # Abstract: health, tier, BuildingType enum, ISelectable
│   ├── BuildingManager.cs      # Singleton: building count registry, limit queries
│   ├── Castle.cs               # Worker registry (max 15), Castle tier (T1/T2/T3)
│   ├── Barracks.cs             # Produces Knight (T1), Archer (T2)
│   ├── Academy.cs              # Produces Mage (T1), Valkyrie (T2)
│   ├── Blacksmith.cs           # Upgrade logic
│   ├── House.cs                # Provides supply capacity
│   └── ResourceNode.cs         # Base for GoldMine / ManaExtractor
│
├── Construction/
│   ├── BuildingPlacer.cs       # Ghost preview, NavMesh-valid placement
│   └── ConstructionSite.cs     # Parallel health+progress tracking, worker slot
│
├── Units/
│   ├── UnitBase.cs             # Health, NavMeshAgent wrapper, ISelectable
│   ├── WorkerController.cs     # FSM: Idle/MovingToResource/Harvesting/
│   │                           #       Returning/MovingToBuild/Building
│   ├── CombatUnit.cs           # Attack, target acquisition
│   └── UnitProduction.cs       # Queue + cooldown timer (used by Barracks/Academy)
│
├── Selection/
│   ├── SelectionManager.cs     # Drag-box + click; maintains selected list
│   └── Selectable.cs           # MonoBehaviour component; fires OnSelected/OnDeselected
│
└── UI/
    ├── IHealthDisplay.cs       # Interface: void UpdateHealth(float current, float max)
    ├── IProgressDisplay.cs     # Interface: void UpdateProgress(float t)  // 0..1
    ├── IWorkerCountDisplay.cs  # Interface: void UpdateWorkerCount(int current, int max)
    ├── TempWorldUI.cs          # IMGUI implementation of all three interfaces
    └── SelectionPanel.cs       # Bottom HUD: command buttons, stats for selection
```

## Architecture

### BuildingManager — Tracking Limits

`BuildingManager` is a Singleton that tracks building counts per `BuildingType` and
provides limit queries scaling with Castle tier counts.

```
BuildingManager (Singleton)
  - Dictionary<BuildingType, int> _counts
  - List<Castle>[] _castlesByTier          // index 0=T1, 1=T2, 2=T3

  + void Register(BuildingBase b)
  + void Unregister(BuildingBase b)
  + int GetCastleCount(int tier)
  + int GetLimit(BuildingType type)
      // Returns: baseLimits[type] * GetCastleCount(requiredTier[type])
      // Castle itself: returns int.MaxValue
  + bool CanPlace(BuildingType type)
      // counts[type] < GetLimit(type)
  + bool TierRequirementMet(BuildingType type)
      // GetCastleCount(requiredTier[type]) > 0
```

- `BuildingBase.Awake()` calls `BuildingManager.Instance.Register(this)`
- `BuildingBase.OnDestroy()` calls `BuildingManager.Instance.Unregister(this)`
- T2 building limits scale with `GetCastleCount(tier: 2)` (mixed-tier rule)
- `BuildingManager.UpdateCastleTier(castle, oldTier)` moves a Castle between tier lists on upgrade

### Building Placement Requirements (RequiredTier)

| Building      | Castle tier needed to PLACE |
|---------------|-----------------------------|
| Castle        | T1                          |
| Barracks      | T1                          |
| House         | T1                          |
| GoldMine      | T1                          |
| ManaExtractor | T1                          |
| Academy       | **T2**                      |
| Blacksmith    | **T2**                      |

### Building Tier Upgrades

All upgrades cost gold (configurable per prefab via `Upgrade Cost` in Inspector).
`Castle Req For Tier 2/3` fields gate upgrades behind a minimum Castle tier.

| Building      | Max Tier | T1→T2 req    | T2→T3 req    | Effect of upgrade                        |
|---------------|----------|--------------|--------------|------------------------------------------|
| Castle        | 3        | none         | none         | Unlocks Academy/Blacksmith (T2), enables T2/T3 upgrades for other buildings |
| Barracks      | 2        | Castle T2    | —            | Unlocks Archer training                  |
| Academy       | 2        | Castle T3    | —            | Unlocks Valkyrie training                |
| Blacksmith    | 2        | Castle T3    | —            | (future: stronger upgrades)              |
| House         | 3        | Castle T2    | Castle T3    | (future: more supply per tier)           |
| GoldMine      | 3        | Castle T2    | Castle T3    | (future: more gold per trip per tier)    |
| ManaExtractor | 3        | Castle T2    | Castle T3    | (future: more mana per trip per tier)    |

**Inspector setup per prefab** (`BuildingBase` header "Tier Upgrade"):
- `Max Tier` — maximum tier this building can reach
- `Upgrade Cost` — gold cost per upgrade step
- `Castle Req For Tier 2` — minimum Castle tier needed to upgrade to T2 (0 = no requirement)
- `Castle Req For Tier 3` — minimum Castle tier needed to upgrade to T3 (0 = no requirement)

### WorkerController — FSM

```
enum WorkerState { Idle, MovingToResource, Harvesting, MovingToDeposit,
                   Depositing, MovingToBuild, Building }
```

- `homeBase` (Castle) is assigned at spawn and **never changes**
- Multiple Workers can share one `ConstructionSite`; each calls `site.Tick(deltaTime)`
- Workers are **not** part of the global Supply Cap

### Health/Progress UI — Decoupling

Three pure interfaces in `UI/`:
- `IHealthDisplay` — updated by `BuildingBase` and `UnitBase` on health change
- `IProgressDisplay` — updated by `ConstructionSite` each frame while building
- `IWorkerCountDisplay` — updated by `Castle` on every worker register/unregister

`TempWorldUI` implements all three via `OnGUI()` (Unity IMGUI). Each consumer holds:

```csharp
[SerializeField] private MonoBehaviour _healthDisplayProvider;
private IHealthDisplay _healthDisplay; // resolved in Awake via as-cast
```

To replace the UI later: implement the interfaces in a new component, swap the
Inspector reference — zero changes to game logic.

### Castle Worker Registry

- Each Castle tracks its own Workers independently (max 15)
- Renders "X / 15" world-space label **unconditionally** (not gated on selection)
- 16th Worker spawn is rejected by the Castle before it is created

### ConstructionSite — Parallel Tracking

- Tracks two parallel floats: `currentHealth` (0 → maxHealth) and `buildProgress` (0 → 1)
- Both advance at the same rate while a Worker is actively building
- Displays a Health Bar **and** a Progress Bar simultaneously
- On `buildProgress == 1`: replace site with finished building at full health, Worker → Idle

## Out of Scope

No animations/VFX, no audio, no multiplayer, no complex pathfinding, no UI polish.

## Verification Checklist

1. Place a Castle → "0 / 15" label renders without selecting it
2. Spawn 15 Workers → label reads "15 / 15"; 16th Worker is rejected
3. Place a construction site → Health Bar and Progress Bar both advance simultaneously
4. Build completes → Health is full, Progress bar disappears, Worker returns to Idle
5. Place T2 building → blocked when only T1 Castles exist
6. Build a T2 Castle → T2 building limit unlocks
7. Produce a Knight → Supply Cap increments; produce a Worker → it does NOT

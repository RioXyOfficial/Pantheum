# Pantheum — Documentation des Scripts

> RTS inspiré de Starcraft — Unity 6 URP · Mirror (LAN) · Input System · AI Navigation

---

## Structure des dossiers

```
Assets/Scripts/
├── Core/           Singletons globaux (état jeu, ressources, grille, caméra)
├── Buildings/      Logique de tous les bâtiments
├── Construction/   Placement interactif + chantier de construction
├── Units/          Toutes les unités (workers, combattants)
├── Selection/      Sélection souris (clic + drag-box + commandes)
├── UI/             HUD, panneaux, interfaces d'affichage world-space
└── Network/        Multijoueur Mirror LAN
```

---

## Core/

### `Faction.cs`
Enum simple : **`Player`** et **`Enemy`**. Utilisé partout pour identifier l'appartenance d'une unité ou d'un bâtiment.

---

### `GameManager.cs`
**Singleton** — état global du jeu.
- Pause / reprise via `Time.timeScale`.

---

### `ResourceManager.cs`
**Singleton** — gestion de l'or et du mana en **solo**.
En multijoueur, délègue toutes les opérations à `ActiveNetworkPlayer` (static).
- `SpendGold / DepositGold / SpendMana / DepositMana`

---

### `SupplyManager.cs`
**Singleton** — cap de supply globale (unités de combat uniquement, pas les Workers).
- `AddCapacity / RemoveCapacity / TryUseSupply / ReleaseSupply / HasSupply`

---

### `GridSystem.cs`
**Singleton** — grille 2D pour le placement des bâtiments.
Stocke les cellules occupées dans un `HashSet`.
- `Snap(pos, size)` — accroche au centre de cellule
- `CanPlace / Occupy / Release`
- Visualisation debug de la grille via GL lines.

---

### `RTSCamera.cs`
Contrôle de caméra RTS :
- Pan clavier (ZQSD) + scroll bords écran
- Zoom molette
- Pan clic molette (drag)
- `CenterOn(Vector3)` — centrage sur une position (utilisé au spawn)

---

### `GameBootstrap.cs`
Spawn des **Castles initiaux** et des **Workers de départ** pour chaque joueur.
- `GetPivotToBottomOffset(prefab)` — calcule l'offset Y pivot→bas via `Renderer.bounds` pour poser les bâtiments correctement sur le sol.
- Appelle `NetworkServer.Spawn` avec `connectionToClient` pour l'autorité correcte.

---

## Buildings/

### `BuildingBase.cs`
Classe **abstraite** dont héritent tous les bâtiments. Implémente `ISelectable`.
- Système de santé : `TakeDamage`, `SetHealthFull`
- Système de tier : `CurrentTier`, `Upgrade()` (coût or + tier Castle requis), visuels par tier
- Enregistrement grille via `GridSystem`
- Faction : `SetFaction`, `Faction`
- NavMeshObstacle automatique
- `StartConstruction()` / `CompleteConstruction()`
- `IsRegistered` — flag pour `BuildingManager`

---

### `BuildingManager.cs`
**Singleton** — registre des bâtiments par type et par faction.
- Limites basées sur le nombre de Castles par tier (ex : Barracks → 1 slot T1, 2 slots T2/T3)
- `CanPlace(type)` / `TierRequirementMet(type)` / `GetLimit(type)` / `GetCastleCount(tier)`
- `RebuildCastleTierLists()` — reconstruit les listes après un changement de tier

---

### `Castle.cs`
Bâtiment de base — produit des **Workers** (max 15).
- Registre interne des workers : `TryRegisterWorker / UnregisterWorker`
- File de production worker avec timer
- Affiche "X / 15" en world-space via `IWorkerCountDisplay` (toujours visible)
- En multi : synchronise le compteur via `NetworkFactionSync.SetWorkerCount`
- `ApplyWorkerCountDisplay(int)` — appelé par le hook SyncVar sur les clients

---

### `Barracks.cs`
Produit **Knight** (T1) et **Archer** (T2+). Utilise `UnitProduction`.

---

### `Academy.cs`
Produit **Mage** (T1) et **Valkyrie** (T2+). Utilise `UnitProduction`.

---

### `Blacksmith.cs`
Upgrades **attaque** et **armure** (max niveau 5 chacun, 150 or/niveau).
- Niveaux statiques partagés entre tous les Blacksmiths.
- Reset au niveau 0 quand le dernier Blacksmith est détruit.

---

### `House.cs`
Fournit de la **supply** (5 au T1, jusqu'à 12 au T3).
Ajoute/retire la capacité via `SupplyManager` en fonction du tier et de l'état de construction.

---

### `ResourceNode.cs`
Nœud de ressource — base pour `GoldMine` et `ManaExtractor`.
- Ressource par récolte : 10 (T1) → 30 (T3)
- Durée de récolte : 3 secondes
- Les Workers récoltent en cycle : déplacement → récolte → dépôt → retour

---

## Construction/

### `BuildingPlacer.cs`
Gère le **mode placement interactif**.
- `BeginPlacement(type, prefab, cost, workers)` — démarre le ghost
- `CreateGhost(prefab)` — clone le prefab, supprime NetworkBehaviour/NetworkIdentity, désactive colliders et MonoBehaviours. Calcule `_ghostPivotToBottom` via `sharedMesh.bounds.min.y` pour aligner visuellement le ghost sur le sol.
- `Update` — suit la souris, colorie le ghost (vert/rouge), appelle `TryPlace(ghostPos)` au clic gauche (ghostPos inclut l'offset Y)
- `TryPlace` :
  - **Client pur** → envoie `CmdPlaceBuilding` avec la position corrigée
  - **Hôte/Solo** → instancie directement, active `ConstructionSite`, fait `NetworkServer.Spawn`
- Utilise `GetComponent<ConstructionSite>()` — le composant **doit exister sur le prefab** (désactivé par défaut)

---

### `ConstructionSite.cs`
`NetworkBehaviour` — **chantier actif** sur un bâtiment en cours de construction.

SyncVars :
- `_currentHealth` — vie qui monte de 0 à max
- `_buildProgress` — progression 0→1 (hook → `UpdateRealVisual`)
- `_constructionComplete` — hook → ré-active les composants du bâtiment fini

Visuels (locaux, non-networked) :
- `_ghostVisual` — clone bleu transparent du bâtiment
- `_realVisual` — clone qui grandit depuis le bas (`ScaleFromBottom`)

Logique serveur uniquement :
- `Tick(dt)` — avancé par les Workers (`if (!NetworkServer.active) return`)
- `SnapToGround()` — Physics.Raycast + `_pivotToBottom` pour corriger la position Y
- `GridSystem.Occupy` — seulement si `!_gridOccupied`

Sur les clients : activé par `NetworkFactionSync.OnConstructionStateChanged` quand `_underConstruction=true`. Utilise `_fallbackBuildingPrefab` (assigné dans l'Inspector sur chaque prefab) si `_buildingPrefab` est null.

---

## Units/

### `UnitBase.cs`
Classe **abstraite `NetworkBehaviour`** — base de toutes les unités. Implémente `ISelectable`.
- `_currentHealth` → `[SyncVar]` avec hook affichage
- `NavMeshAgent` activé **uniquement serveur** (`OnStartServer`), désactivé sur clients
- `IsClientOnly` — guard statique (`NetworkClient.active && !NetworkServer.active`) pour bloquer le FSM côté client pur
- `MoveTo / StopMoving / TakeDamage / RestoreHealth / HasArrived`
- `OnDeath` → `NetworkServer.Destroy`

---

### `CombatUnit.cs`
Hérite de `UnitBase`. Système de **combat**.
- Attributs : dégâts, portée d'attaque, portée de détection, cooldown
- Bonus attaque/armure depuis `Blacksmith.AttackLevel / ArmorLevel`
- Ciblage unités ET bâtiments ennemis
- Attack-move avec scan automatique toutes les 0.5s
- `OrderMove / OrderAttackMove / SetTarget / SetBuildingTarget / ScanForTarget`

---

### `Knight.cs`
Hérite de `CombatUnit`. Aucune logique spéciale — stats dans l'Inspector du prefab.

### `Archer.cs`
Idem Knight.

---

### `Mage.cs`
Hérite de `CombatUnit`. Override `PerformAttack()` → **dégâts AoE** dans un rayon de 2.5m autour de la cible.

---

### `Valkyrie.cs`
Hérite de `CombatUnit`. Override `PerformAttack()` → **cleave** (toutes les unités ennemies à portée) + **lifesteal** (30% des dégâts en soin).

---

### `WorkerController.cs`
Hérite de `UnitBase`. **Machine à états** complexe.

États :
```
Idle → MovingToResource → Harvesting → MovingToDeposit → Depositing
     → MovingToBuild → Building
```
- `homeBase` (Castle) fixé à la création, jamais changé
- Récolte en cycles automatiques
- Construction : seul le worker primaire `Tick()` le chantier
- `AssignToHarvest / AssignToConstruction / OrderMove`
- En multi : `ResourceManager.ActiveNetworkPlayer` mis à jour avant chaque dépôt

---

### `UnitProduction.cs`
Gère la **file de production** des bâtiments (Barracks, Academy).
- `ProductionEntry` : prefab, coût or, isCombatUnit, coût supply
- File max configurable (défaut 5)
- Pre-applique `NavMeshAgent.baseOffset` au Y de spawn avant `NetworkServer.Spawn` (évite les unités dans le sol)
- Sync réseau : queue count, isProducing, timer, maxQueue (via `NetworkFactionSync`)

---

### `test.cs`
Stub vide : `PositionDebug : NetworkBehaviour` — **à supprimer**.

---

## Selection/

### `Selectable.cs`
Composant sur chaque objet sélectionnable. Registre statique de tous les sélectionnables.
- Events : `OnSelectedEvent / OnDeselectedEvent`

---

### `SelectionManager.cs`
**Singleton** — sélection par clic et drag-box.
- Clic simple, Shift = ajout à la sélection
- Bâtiments : désélectionne tout le reste
- Empêche la sélection d'unités/bâtiments ennemis
- Clic droit : move, attack-move (+ touche A), assignation ressource, assignation construction, attaque bâtiment ennemi
- Calcul d'offset de formation pour déplacer plusieurs unités

---

### `SelectionIndicator.cs`
Affiche des **anneaux verts** sous les unités/bâtiments sélectionnés. Maillage dynamique avec dégradé de couleur.

---

## UI/

### `IHealthDisplay.cs`
Interface : `UpdateHealth(float current, float max)`

### `IProgressDisplay.cs`
Interface : `UpdateProgress(float t)` — t ∈ [0..1]

### `IWorkerCountDisplay.cs`
Interface : `UpdateWorkerCount(int current, int max)`

---

### `TempWorldUI.cs`
Implémente les 3 interfaces via **IMGUI** (`OnGUI`).
- Barres de vie, progression, compteur Workers en world-space au-dessus des objets
- Largeur : 67px bâtiments, 33px unités

---

### `SelectionPanel.cs`
**HUD bas** — s'adapte à la sélection courante.
- Barre ressources (or/mana/supply) — lit `PlayerNetworkController.Gold/Mana` en multi
- Stats de l'unité/bâtiment sélectionné
- Résumé multi-sélection (ex: "3 Knights, 2 Workers")
- Boutons upgrade bâtiment (avec vérification tier + or)
- Boutons entraînement (Knight, Archer, Mage, Valkyrie)
- Boutons Blacksmith (upgrade attaque/armure)
- Bouton spawn Worker
- Bouton annulation construction

---

### `BuildingMenu.cs`
Boutons de **construction** dans l'UI.
- `CanBuild(type)` — vérifie tier requis, limite bâtiments, or disponible
- `GetEffectiveCost(type)` — premier GoldMine gratuit (0 or)
- `BeginBuild(type)` — délègue à `BuildingPlacer` avec le worker assigné
- Lit `PlayerNetworkController.LocalPlayer.Gold` en multi, `ResourceManager.Gold` en solo

---

## Network/

### `PantheumNetworkManager.cs`
Hérite de `NetworkManager` (Mirror). Un seul dans la scène sur le GameObject **Network**.
- `OnServerAddPlayer` — instancie `PlayerController`, assigne faction (`Player` = 1er, `Enemy` = 2ème)
- Déclenche `GameBootstrap` dès que 2 joueurs sont connectés

---

### `PlayerNetworkController.cs`
`NetworkBehaviour` — **un par joueur** (sur le prefab `PlayerController`).

SyncVars : `_faction`, `_gold`, `_mana`

`LocalPlayer` — référence statique au joueur local (utilisée partout).

Tous les `[Command]` (client → serveur) :
| Command | Action |
|---|---|
| `CmdPlaceBuilding` | Valide, dépense l'or, instancie `ConstructionSite`, spawn réseau |
| `CmdSpawnWorker` | Appelle `Castle.SpawnWorker()` |
| `CmdTrainKnight/Archer/Mage/Valkyrie` | Appelle `UnitProduction.Enqueue` |
| `CmdMoveUnit` | `UnitBase.MoveTo` |
| `CmdAttackMove` | `CombatUnit.OrderAttackMove` |
| `CmdOrderWorkerMove` | `WorkerController.OrderMove` |
| `CmdAssignWorkerHarvest/Build` | Assignation worker |
| `CmdUpgradeBuilding` | `BuildingBase.Upgrade` |
| `CmdSetAttackTarget/AttackBuilding` | `CombatUnit.SetTarget/SetBuildingTarget` |
| `CmdUpgradeAttack/Armor` | `Blacksmith.UpgradeAttack/Armor` |
| `CmdDemolish` | `BuildingBase.Demolish` |
| `CmdCancelConstruction` | `ConstructionSite.CancelConstruction` |

---

### `NetworkFactionSync.cs`
`NetworkBehaviour` sur **chaque bâtiment et unité**. Centralise tous les SyncVars objets.

| SyncVar | Rôle |
|---|---|
| `_faction` | Faction de l'objet → `SetFaction` sur tous les enfants |
| `_underConstruction` | Active/désactive les composants + active `ConstructionSite` sur clients |
| `_buildingTier` | Tier du bâtiment → `ApplyTier` sur `BuildingBase` |
| `_workerCount` | Compteur workers du Castle → `ApplyWorkerCountDisplay` |
| `_syncWorkerQueue/Producing/Timer` | État production workers (Castle) |
| `_syncUnitQueue/Producing/Timer/MaxQueue` | État production unités (Barracks/Academy) |

---

### `NetworkLobbyUI.cs`
UI de lobby simple en IMGUI.
- Bouton **Host** (démarre en hôte)
- Champ IP + bouton **Join** (connexion client)
- Statut de connexion + bouton **Disconnect**

---

## Prefabs — Composants requis

### Bâtiments (Barracks, Academy, House, GoldMine…)
```
BuildingBase (sous-classe)
NetworkIdentity
NetworkTransformUnreliable   (ServerToClient + Observers)
NetworkFactionSync
ConstructionSite             ← DÉSACTIVÉ par défaut
                               _fallbackBuildingPrefab = ce prefab lui-même
```

### Unités (Knight, Archer, Worker…)
```
UnitBase (sous-classe)
NetworkIdentity
NetworkTransformUnreliable   (ServerToClient + Observers)
```

### PlayerController
```
PlayerNetworkController
NetworkIdentity
```

### Network (GameObject scène)
```
PantheumNetworkManager
KcpTransport                 ← transport Mirror
NetworkManagerHUD            ← optionnel (UI debug Host/Join)
```

---

## Flux multijoueur résumé

```
Host démarre
 └─ PantheumNetworkManager.OnServerAddPlayer ×2
      └─ Faction.Player (hôte) / Faction.Enemy (client)
           └─ GameBootstrap.SpawnCastles + Workers

Joueur place un bâtiment
  Hôte  → BuildingPlacer.TryPlace(ghostPos) → active ConstructionSite → NetworkServer.Spawn
  Client → BuildingPlacer.TryPlace → CmdPlaceBuilding(ghostPos) → serveur fait tout

Worker construit
  WorkerController.Update → ConstructionSite.Tick (serveur uniquement)
  SyncVar _buildProgress → hook OnBuildProgressChanged → UpdateRealVisual (tous clients)
  SyncVar _constructionComplete → hook → ré-active bâtiment, supprime ConstructionSite

Ressources
  Solo  : ResourceManager.Gold/Mana (local)
  Multi : PlayerNetworkController._gold/_mana (SyncVar, serveur autoritaire)
```

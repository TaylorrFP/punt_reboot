# Networking Architecture

## Overview

This document outlines the networking architecture for Punt, covering the flow from main menu through lobby and into gameplay.

## Lobby Types

The game supports different lobby types, each with different flows for getting into a match:

| Type | Entry Flow | Team Assignment | Mid-Match Join |
|------|------------|-----------------|----------------|
| **Custom** | Host creates lobby, players browse/join | Host assigns teams | Configurable |
| **Ranked** | Players queue, matchmaking finds game | Server assigns by MMR | Spectator only |
| **Private** | Invite-only, tournament/scrim use | Host or external system | Configurable |

Despite different entry flows, **all lobby types use the same GameSession structure** once in a match. The lobby type determines *how* GameSession gets populated, not *what* it contains.

## Core Components

### NetworkManager
**Location:** `Core/NetworkManager.cs`

Handles connection lifecycle and lobby management. Implements `Component.INetworkListener`.

**Responsibilities:**
- Creating/joining/leaving lobbies
- Searching for available lobbies
- Tracking connection state (`NetworkState`)
- Firing events when players join/leave
- Persists across scene loads (`DontDestroyOnLoad`)

**Network States:**
| State | Description |
|-------|-------------|
| `None` | Not connected, browsing main menu |
| `CreatingLobby` | On create/join screen, preparing to create or join |
| `InLobby` | Lobby active, players connecting, configuring teams/settings |
| `StartingGame` | Host started game, loading into game scene |

**Key Events:**
- `OnStateChanged(oldState, newState)` - Network state transitions
- `OnPlayerJoined(Connection)` - Player completed handshake
- `OnPlayerLeft(Connection)` - Player disconnected
- `OnLobbiesUpdated` - Lobby search results ready

---

### GameSession
**Location:** `Core/GameSession.cs`

Shared data container for lobby and game state. Single source of truth for team assignments and game settings.

**Responsibilities:**
- Storing team assignments (which players are on which team)
- Storing game settings (match duration, cooldowns, rules)
- Storing map selection
- Syncing all data to clients via `[Sync]` properties
- Persists across scene loads (`DontDestroyOnLoad`)

**Key Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `LobbyType` | `LobbyType` | Custom, Ranked, or Private |
| `TeamAssignments` | `NetDictionary<long, TeamSide>` | Maps SteamId to team |
| `MatchDuration` | `float` | Match length in seconds |
| `PieceCooldown` | `float` | Cooldown between piece flicks |
| `MapName` | `string` | Selected map identifier |
| `AllowMidMatchJoin` | `bool` | Whether players can join during gameplay |
| `MidMatchJoinTeam` | `TeamSide` | Default team for mid-match joiners (usually Spectator) |

**Why GameSession Exists:**
- Avoids duplicating team/settings data between "lobby manager" and "game manager"
- Data configured in lobby flows naturally into gameplay
- Single source of truth eliminates sync issues

---

### MatchController
**Location:** `Match/MatchController.cs`

In-game state machine for match flow. Only active during gameplay scenes.

**Responsibilities:**
- Managing match state transitions (Intro → Playing → GoalScored → etc.)
- Coordinating with MatchClock and ScoreTracker
- Reading team assignments and settings from GameSession

**Match States:**
| State | Description |
|-------|-------------|
| `None` | Initial state |
| `Intro` | Cinematic fly-in |
| `WaitingForPlayers` | Waiting for all players to load |
| `Countdown` | 3, 2, 1... |
| `Playing` | Main gameplay, clock running |
| `GoalScored` | Freeze, replay, reset |
| `Overtime` | Sudden death if draw |
| `Results` | Final scores screen |

---

## Data Flow

```
┌─────────────────────────────────────────────────────────────┐
│                       GameSession                           │
│  • TeamAssignments (NetDictionary<long, TeamSide>)          │
│  • MatchDuration, PieceCooldown, MapName                    │
│  • [Sync(SyncFlags.FromHost)] - host controls, all receive  │
└─────────────────────────────────────────────────────────────┘
                    ▲                       │
                    │ writes                │ reads
                    │                       ▼
┌───────────────────┴───────┐   ┌───────────────────────────┐
│      Lobby Phase          │   │      Game Phase           │
│  • CreateCustomGame.razor │   │  • MatchController        │
│  • Host assigns teams     │   │  • PlayerController       │
│  • Host configures settings│   │  • ScoreTracker          │
└───────────────────────────┘   └───────────────────────────┘
                    ▲
                    │ manages connections
                    │
┌───────────────────┴───────────────────────────────────────┐
│                     NetworkManager                         │
│  • CreateLobby / JoinLobby / LeaveLobby                   │
│  • INetworkListener (OnActive, OnDisconnected)            │
│  • NetworkState tracking                                   │
└───────────────────────────────────────────────────────────┘
```

---

## Connection Flow

### Creating a Lobby (Host)

```
1. User navigates to Create Game screen
   └─> NetworkManager.SetState(CreatingLobby)

2. User configures settings and clicks "Create Lobby"
   └─> NetworkManager.CreateLobby(maxPlayers, privacy, name)
   └─> NetworkManager.SetState(InLobby)
   └─> GameSession initializes with default settings

3. Host appears in lobby UI
   └─> NetworkManager.OnActive(hostConnection)
   └─> UI reads Connection.All to display players

4. Host assigns teams via drag-drop
   └─> GameSession.TeamAssignments[steamId] = team
   └─> Synced to all connected clients

5. Host clicks "Start Game"
   └─> NetworkManager.StartGame()
   └─> NetworkManager.SetState(StartingGame)
   └─> Scene loads, MatchController reads GameSession
```

### Joining a Lobby (Client)

```
1. User browses lobbies
   └─> NetworkManager.SearchLobbies()
   └─> UI displays NetworkManager.AvailableLobbies

2. User selects and joins lobby
   └─> NetworkManager.JoinLobby(lobby)
   └─> NetworkManager.SetState(InLobby)

3. Client completes handshake
   └─> Host's NetworkManager.OnActive(clientConnection)
   └─> Client receives synced GameSession data
   └─> Client UI shows current team assignments

4. Host starts game
   └─> Client receives NetworkState.StartingGame
   └─> Scene loads, client's MatchController reads GameSession
```

### Mid-Match Joining

Players may join while a match is in progress (if `AllowMidMatchJoin` is true):

```
1. Player joins during active match
   └─> NetworkManager.OnActive(connection)
   └─> NetworkState is already StartingGame or InGame

2. Determine team assignment
   └─> If GameSession.TeamAssignments already has this SteamId:
       └─> Use existing assignment (reconnection case)
   └─> Else:
       └─> Assign to GameSession.MidMatchJoinTeam (default: Spectator)
       └─> Or auto-balance to team with fewer players (if configured)

3. Spawn player into match
   └─> MatchController.OnPlayerJoined(connection) handles spawning
   └─> PlayerController created with team from GameSession
   └─> Player receives current match state (scores, time remaining)
```

### Ranked Queue Flow

Ranked matchmaking is a separate system that eventually populates a GameSession:

```
1. Player enters ranked queue
   └─> UI shows queue status, estimated wait time
   └─> Matchmaking server groups players by MMR/region

2. Match found
   └─> Server creates lobby, assigns all players
   └─> Server populates GameSession:
       └─> LobbyType = Ranked
       └─> TeamAssignments (balanced by MMR)
       └─> Settings (standard ranked rules)
       └─> AllowMidMatchJoin = false (or Spectator only)

3. Players connect to match
   └─> Client receives pre-populated GameSession
   └─> No lobby phase - straight to game loading
   └─> MatchController starts match

4. Post-match
   └─> Results submitted to ranking server
   └─> Players returned to queue or main menu
```

---

## Sync Strategy

### Host-Controlled Properties

Using `[Sync(SyncFlags.FromHost)]` for data that only the host should modify:

```csharp
[Sync(SyncFlags.FromHost)]
public NetDictionary<long, TeamSide> TeamAssignments { get; set; }

[Sync(SyncFlags.FromHost)]
public float MatchDuration { get; set; }
```

### Client Requests

When clients need to modify host-controlled data (e.g., changing their own team):

```csharp
[Rpc.Host]
public void RequestTeamChange(long steamId, TeamSide requestedTeam)
{
    // Validate: can only change own team
    if (Rpc.Caller.SteamId != steamId) return;

    // Validate: slot must be available
    // ... validation logic ...

    TeamAssignments[steamId] = requestedTeam;
}
```

### Change Detection

Using `[Change]` attribute for UI updates:

```csharp
[Sync(SyncFlags.FromHost), Change(nameof(OnTeamAssignmentsChanged))]
public NetDictionary<long, TeamSide> TeamAssignments { get; set; }

private void OnTeamAssignmentsChanged(...)
{
    OnTeamsChanged?.Invoke(); // UI subscribes to refresh
}
```

---

## Scene Structure

### Menu Scene
- **NetworkManager** (singleton, DontDestroyOnLoad)
- **GameSession** (singleton, DontDestroyOnLoad)
- **PartyManager** (singleton, DontDestroyOnLoad)
- Menu UI components

### Game Scene
- **MatchController** (reads from GameSession)
- **MatchClock**
- **ScoreTracker**
- **PlayerController** (one per player, reads team from GameSession)
- Game UI (Hud.razor)

---

## Future Considerations

### Reconnection Handling
- Player disconnects mid-game
- GameSession retains their team assignment by SteamId
- On reconnect within time window:
  - `OnActive` fires, existing TeamAssignment found
  - Respawn to same team, restore state
- On reconnect after timeout (ranked):
  - May count as forfeit/abandon
  - Penalties applied via ranking server

### Spectators
- `TeamSide.Spectator` in TeamAssignments
- No PlayerController spawned
- Spectator camera system with player/ball follow
- Can join mid-match without affecting gameplay

### Ranked Infrastructure (Future)
The ranked queue system is **separate infrastructure** from GameSession:
- Matchmaking server handles queue management
- Groups players by MMR, region, party size
- Creates game server and populates GameSession
- Tracks match results and updates rankings
- GameSession itself is agnostic - it just holds the data

### Private/Tournament Lobbies
- External bracket systems can create lobbies via API
- Team assignments may come from tournament organizer
- Custom rulesets for competitive play
- Integration with streaming/spectator systems

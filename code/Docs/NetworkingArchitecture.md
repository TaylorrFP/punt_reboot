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
- Firing events when players join/leave
- Persists across scene loads (`DontDestroyOnLoad`)

**Key Events:**
- `OnPlayerJoined(Connection)` - Player completed handshake
- `OnPlayerLeft(Connection)` - Player disconnected
- `OnLobbiesUpdated` - Lobby search results ready

**Note:** NetworkManager does NOT track session state - that responsibility belongs to GameSession.

---

### GameSession
**Location:** `Core/GameSession.cs`

Shared data container for lobby and game state. Single source of truth for team assignments, session state, and game settings.

**Responsibilities:**
- Tracking session state (None, CustomLobby, Matchmaking, InGame, etc.)
- Storing team assignments (which players are on which team)
- Storing game settings (match duration, cooldowns, rules) - *future*
- Syncing all data to clients via `[Sync]` properties
- Persists across scene loads (`DontDestroyOnLoad`)

**Session States:**
| State | Description |
|-------|-------------|
| `None` | Not in any networked session, browsing menus |
| `CustomLobby` | In a custom game lobby - host can configure teams and settings |
| `Matchmaking` | In ranked matchmaking queue, waiting for a match |
| `MatchFound` | Match found in ranked queue, connecting to game server |
| `InGame` | Actively in a game (loaded into game scene) |

**Key Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `State` | `SessionState` | Current session state (synced from host) |
| `TeamAssignments` | `NetDictionary<long, TeamSide>` | Maps SteamId to team |
| `AllowMidMatchJoin` | `bool` | Whether players can join during gameplay |
| `MidMatchJoinTeam` | `TeamSide` | Default team for mid-match joiners (usually Spectator) |

**Key Events:**
- `OnStateChanged(oldState, newState)` - Session state transitions
- `OnTeamsChanged` - Team assignments modified

**Why GameSession Owns Session State:**
- Allows differentiating between `CustomLobby` and `Matchmaking` even when `Networking.IsActive` is true for both
- State is synced to clients automatically via `[Sync(SyncFlags.FromHost)]`
- Single source of truth for "what kind of session are we in?"

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
│  • State (SessionState - synced from host)                  │
│  • TeamAssignments (NetDictionary<long, TeamSide>)          │
│  • AllowMidMatchJoin, MidMatchJoinTeam                      │
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
│  • SearchLobbies                                           │
└───────────────────────────────────────────────────────────┘
```

---

## Connection Flow

### Creating a Lobby (Host)

```
1. User navigates to Create Game screen

2. User configures settings and clicks "Create Lobby"
   └─> NetworkManager.CreateLobby(maxPlayers, privacy, name)
   └─> GameSession.State = CustomLobby

3. Host appears in lobby UI
   └─> NetworkManager.OnActive(hostConnection)
   └─> UI reads Connection.All to display players

4. Host assigns teams via drag-drop
   └─> GameSession.TeamAssignments[steamId] = team
   └─> Synced to all connected clients

5. Host clicks "Start Game"
   └─> NetworkManager.StartGame()
   └─> GameSession.State = InGame
   └─> Scene loads, MatchController reads GameSession
```

### Joining a Lobby (Client)

```
1. User browses lobbies
   └─> NetworkManager.SearchLobbies()
   └─> UI displays NetworkManager.AvailableLobbies

2. User selects and joins lobby
   └─> NetworkManager.JoinLobby(lobby)

3. Client completes handshake
   └─> Host's NetworkManager.OnActive(clientConnection)
   └─> Client receives synced GameSession (State = CustomLobby)
   └─> Client UI shows current team assignments

4. Host starts game
   └─> Client receives GameSession.State = InGame
   └─> Scene loads, client's MatchController reads GameSession
```

### Mid-Match Joining

Players may join while a match is in progress (if `AllowMidMatchJoin` is true):

```
1. Player joins during active match
   └─> NetworkManager.OnActive(connection)
   └─> GameSession.State is already InGame

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
   └─> GameSession.State = Matchmaking
   └─> UI shows queue status, estimated wait time
   └─> Matchmaking server groups players by MMR/region

2. Match found
   └─> GameSession.State = MatchFound
   └─> Server creates lobby, assigns all players
   └─> Server populates GameSession:
       └─> TeamAssignments (balanced by MMR)
       └─> AllowMidMatchJoin = false (or Spectator only)

3. Players connect to match
   └─> Client receives pre-populated GameSession
   └─> GameSession.State = InGame
   └─> No lobby phase - straight to game loading
   └─> MatchController starts match

4. Post-match
   └─> Results submitted to ranking server
   └─> GameSession.State = None
   └─> Players returned to queue or main menu
```

---

## Sync Strategy

### Host-Controlled Properties

Using `[Sync(SyncFlags.FromHost)]` for data that only the host should modify:

```csharp
[Sync(SyncFlags.FromHost)]
public SessionState State { get; set; }

[Sync(SyncFlags.FromHost)]
public NetDictionary<long, TeamSide> TeamAssignments { get; set; }
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
[Sync(SyncFlags.FromHost), Change(nameof(OnSessionStateChanged))]
public SessionState State { get; set; }

private void OnSessionStateChanged(SessionState oldState, SessionState newState)
{
    OnStateChanged?.Invoke(oldState, newState); // UI subscribes to refresh
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

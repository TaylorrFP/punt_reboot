using Sandbox;
using Sandbox.Network;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents what kind of networked session we're currently in.
/// </summary>
public enum SessionState
{
	/// <summary>
	/// Not in any networked session, browsing menus.
	/// </summary>
	None,

	/// <summary>
	/// In a custom game lobby - host can configure teams and settings.
	/// </summary>
	CustomLobby,

	/// <summary>
	/// In ranked matchmaking queue, waiting for a match.
	/// </summary>
	Matchmaking,

	/// <summary>
	/// Match found in ranked queue, connecting to game server.
	/// </summary>
	MatchFound,

	/// <summary>
	/// Actively in a game (loaded into game scene).
	/// </summary>
	InGame
}

/// <summary>
/// Shared data container for lobby and game state.
/// Single source of truth for team assignments and session state - persists across scene loads.
/// </summary>
public sealed class GameSession : SingletonComponent<GameSession>
{
	// =========================================================================
	// SESSION STATE
	// =========================================================================

	/// <summary>
	/// Current session state. Host-controlled, synced to all clients.
	/// </summary>
	[Property, Sync( SyncFlags.FromHost ), Change( nameof( OnSessionStateChanged ) )]
	public SessionState State { get; set; } = SessionState.None;

	/// <summary>
	/// The name of the current lobby. Host-controlled, synced to all clients.
	/// </summary>
	[Property, Sync( SyncFlags.FromHost )]
	public string LobbyName { get; set; } = "";

	/// <summary>
	/// Fired when the session state changes.
	/// </summary>
	public event Action<SessionState, SessionState> OnStateChanged;

	private void OnSessionStateChanged( SessionState oldState, SessionState newState )
	{
		Log.Info( $"[GameSession] State changed: {oldState} -> {newState}" );
		OnStateChanged?.Invoke( oldState, newState );
	}

	/// <summary>
	/// Convenience property: are we currently in a lobby (custom or matchmaking)?
	/// </summary>
	public bool IsInLobby => State == SessionState.CustomLobby || State == SessionState.Matchmaking;

	/// <summary>
	/// Convenience property: are we in an active game?
	/// </summary>
	public bool IsInGame => State == SessionState.InGame;

	// =========================================================================
	// TEAM ASSIGNMENTS
	// =========================================================================

	/// <summary>
	/// Team assignments keyed by Connection.Id. Host-controlled, synced to all clients.
	/// Uses Guid instead of SteamId to support local testing instances with same Steam account.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public NetDictionary<Guid, TeamSide> TeamAssignments { get; set; } = new();

	/// <summary>
	/// Whether players can join during an active match.
	/// </summary>
	[Property, Sync( SyncFlags.FromHost )]
	public bool AllowMidMatchJoin { get; set; } = true;

	/// <summary>
	/// Default team for players joining mid-match.
	/// </summary>
	[Property, Sync( SyncFlags.FromHost )]
	public TeamSide MidMatchJoinTeam { get; set; } = TeamSide.Spectator;

	/// <summary>
	/// Fired when team assignments change.
	/// </summary>
	public event Action OnTeamsChanged;

	protected override void OnAwake()
	{
		base.OnAwake();
		GameObject.Flags = GameObjectFlags.DontDestroyOnLoad;
	}

	/// <summary>
	/// Assign a player to a team. Host only.
	/// </summary>
	public void AssignTeam( Guid connectionId, TeamSide team )
	{
		if ( !Networking.IsHost )
		{
			Log.Warning( "[GameSession] Only the host can assign teams directly" );
			return;
		}

		var oldTeam = GetTeam( connectionId );
		TeamAssignments[connectionId] = team;

		var connection = Connection.All.FirstOrDefault( c => c.Id == connectionId );
		var displayName = connection?.DisplayName ?? connectionId.ToString();
		Log.Info( $"[GameSession] Assigned {displayName} to {team} (was {oldTeam})" );
		OnTeamsChanged?.Invoke();
	}

	/// <summary>
	/// Remove a player from team assignments (on disconnect).
	/// </summary>
	public void RemovePlayer( Guid connectionId )
	{
		if ( !Networking.IsHost )
			return;

		if ( TeamAssignments.Remove( connectionId ) )
		{
			Log.Info( $"[GameSession] Removed {connectionId} from team assignments" );
			OnTeamsChanged?.Invoke();
		}
	}

	/// <summary>
	/// Get a player's current team assignment.
	/// </summary>
	public TeamSide GetTeam( Guid connectionId )
	{
		return TeamAssignments.TryGetValue( connectionId, out var team ) ? team : TeamSide.None;
	}

	/// <summary>
	/// Get a player's current team assignment by connection.
	/// </summary>
	public TeamSide GetTeam( Connection connection )
	{
		return GetTeam( connection.Id );
	}

	/// <summary>
	/// Get all players on a specific team.
	/// </summary>
	public IEnumerable<Guid> GetPlayersOnTeam( TeamSide team )
	{
		return TeamAssignments.Where( kvp => kvp.Value == team ).Select( kvp => kvp.Key );
	}

	/// <summary>
	/// Get the count of players on a specific team.
	/// </summary>
	public int GetTeamCount( TeamSide team )
	{
		return TeamAssignments.Count( kvp => kvp.Value == team );
	}

	/// <summary>
	/// Clear all team assignments. Useful when returning to lobby or resetting.
	/// </summary>
	public void ClearTeams()
	{
		if ( !Networking.IsHost )
			return;

		TeamAssignments.Clear();
		Log.Info( "[GameSession] Cleared all team assignments" );
		OnTeamsChanged?.Invoke();
	}

	/// <summary>
	/// Request to change your own team. Clients call this, host processes it.
	/// </summary>
	[Rpc.Host]
	public void RequestTeamChange( Guid connectionId, TeamSide requestedTeam )
	{
		// Validate: can only change own team
		if ( Rpc.Caller.Id != connectionId )
		{
			Log.Warning( $"[GameSession] {Rpc.Caller.DisplayName} tried to change someone else's team" );
			return;
		}

		AssignTeam( connectionId, requestedTeam );
	}

	// =========================================================================
	// DEBUG / INSPECTOR
	// =========================================================================

	[Property] public bool ShowDebugInfo { get; set; } = true;

	[Property, ReadOnly, Title( "Red Team" ), Group( "Teams" )]
	public string DebugRedTeam => GetTeamDebugString( TeamSide.Red );

	[Property, ReadOnly, Title( "Blue Team" ), Group( "Teams" )]
	public string DebugBlueTeam => GetTeamDebugString( TeamSide.Blue );

	[Property, ReadOnly, Title( "Spectators" ), Group( "Teams" )]
	public string DebugSpectators => GetTeamDebugString( TeamSide.Spectator );

	private string GetTeamDebugString( TeamSide team )
	{
		if ( TeamAssignments == null || TeamAssignments.Count == 0 )
			return "(empty)";

		var players = TeamAssignments
			.Where( kvp => kvp.Value == team )
			.Select( kvp =>
			{
				var connection = Connection.All.FirstOrDefault( c => c.Id == kvp.Key );
				return connection?.DisplayName ?? $"Id:{kvp.Key}";
			} )
			.ToList();

		return players.Count > 0 ? string.Join( ", ", players ) : "(empty)";
	}

	protected override void OnUpdate()
	{
		if ( ShowDebugInfo )
		{
			DrawDebugInfo();
		}
	}

	private void DrawDebugInfo()
	{
		var x = Screen.Width - 1050;
		var y = 450f;
		var lineHeight = 25f;

		Gizmo.Draw.Color = Color.Black;

		Gizmo.Draw.ScreenText( "=== GameSession ===", new Vector2( x, y ), "roboto", 24, TextFlag.RightCenter );
		y += lineHeight + 5;

		Gizmo.Draw.ScreenText( $"State: {State}", new Vector2( x, y ), "roboto", 20, TextFlag.RightCenter );
		y += lineHeight;

		if ( !string.IsNullOrEmpty( LobbyName ) )
		{
			Gizmo.Draw.ScreenText( $"Lobby: {LobbyName}", new Vector2( x, y ), "roboto", 20, TextFlag.RightCenter );
			y += lineHeight;
		}

		Gizmo.Draw.ScreenText( $"AllowMidMatchJoin: {AllowMidMatchJoin}", new Vector2( x, y ), "roboto", 20, TextFlag.RightCenter );
		y += lineHeight;

		Gizmo.Draw.ScreenText( $"MidMatchJoinTeam: {MidMatchJoinTeam}", new Vector2( x, y ), "roboto", 20, TextFlag.RightCenter );
		y += lineHeight + 5;

		Gizmo.Draw.ScreenText( $"Team Assignments ({TeamAssignments.Count}):", new Vector2( x, y ), "roboto", 20, TextFlag.RightCenter );
		y += lineHeight;

		foreach ( var kvp in TeamAssignments )
		{
			var connection = Connection.All.FirstOrDefault( c => c.Id == kvp.Key );
			var name = connection?.DisplayName ?? $"Id:{kvp.Key}";
			var teamColor = kvp.Value switch
			{
				TeamSide.Red => "RED",
				TeamSide.Blue => "BLUE",
				TeamSide.Spectator => "SPEC",
				_ => "NONE"
			};

			Gizmo.Draw.ScreenText( $"  {name}: {teamColor}", new Vector2( x, y ), "roboto", 18, TextFlag.RightCenter );
			y += lineHeight - 3;
		}
	}
}

using Sandbox;
using Sandbox.Network;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Shared data container for lobby and game state.
/// Single source of truth for team assignments - persists across scene loads.
/// </summary>
public sealed class GameSession : SingletonComponent<GameSession>
{
	/// <summary>
	/// Team assignments keyed by SteamId. Host-controlled, synced to all clients.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public NetDictionary<long, TeamSide> TeamAssignments { get; set; } = new();

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
	public void AssignTeam( long steamId, TeamSide team )
	{
		if ( !Networking.IsHost )
		{
			Log.Warning( "[GameSession] Only the host can assign teams directly" );
			return;
		}

		var oldTeam = GetTeam( steamId );
		TeamAssignments[steamId] = team;

		Log.Info( $"[GameSession] Assigned {steamId} to {team} (was {oldTeam})" );
		OnTeamsChanged?.Invoke();
	}

	/// <summary>
	/// Remove a player from team assignments (on disconnect).
	/// </summary>
	public void RemovePlayer( long steamId )
	{
		if ( !Networking.IsHost )
			return;

		if ( TeamAssignments.Remove( steamId ) )
		{
			Log.Info( $"[GameSession] Removed {steamId} from team assignments" );
			OnTeamsChanged?.Invoke();
		}
	}

	/// <summary>
	/// Get a player's current team assignment.
	/// </summary>
	public TeamSide GetTeam( long steamId )
	{
		return TeamAssignments.TryGetValue( steamId, out var team ) ? team : TeamSide.None;
	}

	/// <summary>
	/// Get a player's current team assignment by connection.
	/// </summary>
	public TeamSide GetTeam( Connection connection )
	{
		return GetTeam( connection.SteamId );
	}

	/// <summary>
	/// Get all players on a specific team.
	/// </summary>
	public IEnumerable<long> GetPlayersOnTeam( TeamSide team )
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
	public void RequestTeamChange( long steamId, TeamSide requestedTeam )
	{
		// Validate: can only change own team
		if ( Rpc.Caller.SteamId != steamId )
		{
			Log.Warning( $"[GameSession] {Rpc.Caller.DisplayName} tried to change someone else's team" );
			return;
		}

		AssignTeam( steamId, requestedTeam );
	}

	// =========================================================================
	// DEBUG / INSPECTOR
	// =========================================================================

	[Property] public bool ShowDebugInfo { get; set; } = true;

	/// <summary>
	/// Debug display of current team assignments for the inspector.
	/// </summary>
	[Property, ReadOnly, Title( "Current Teams" )]
	public string DebugTeamAssignments => GetDebugTeamString();

	private string GetDebugTeamString()
	{
		if ( TeamAssignments == null || TeamAssignments.Count == 0 )
			return "(empty)";

		var lines = new List<string>();

		foreach ( var kvp in TeamAssignments )
		{
			// Try to find the connection to get display name
			var connection = Connection.All.FirstOrDefault( c => c.SteamId == kvp.Key );
			var name = connection?.DisplayName ?? $"SteamId:{kvp.Key}";
			lines.Add( $"{name} -> {kvp.Value}" );
		}

		return string.Join( "\n", lines );
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

		Gizmo.Draw.ScreenText( $"AllowMidMatchJoin: {AllowMidMatchJoin}", new Vector2( x, y ), "roboto", 20, TextFlag.RightCenter );
		y += lineHeight;

		Gizmo.Draw.ScreenText( $"MidMatchJoinTeam: {MidMatchJoinTeam}", new Vector2( x, y ), "roboto", 20, TextFlag.RightCenter );
		y += lineHeight + 5;

		Gizmo.Draw.ScreenText( $"Team Assignments ({TeamAssignments.Count}):", new Vector2( x, y ), "roboto", 20, TextFlag.RightCenter );
		y += lineHeight;

		foreach ( var kvp in TeamAssignments )
		{
			var connection = Connection.All.FirstOrDefault( c => c.SteamId == kvp.Key );
			var name = connection?.DisplayName ?? $"ID:{kvp.Key}";
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

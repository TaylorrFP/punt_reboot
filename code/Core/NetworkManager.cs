using Sandbox;
using Sandbox.Network;
using System;
using System.Collections.Generic;

/// <summary>
/// Handles connection lifecycle and lobby management.
/// Session state is managed by GameSession, not here.
/// </summary>
public sealed class NetworkManager : SingletonComponent<NetworkManager>, Component.INetworkListener
{
	[Property] public bool ShowDebugInfo { get; set; } = true;

	/// <summary>
	/// Fired when the lobby list is updated after a search
	/// </summary>
	public event Action OnLobbiesUpdated;

	/// <summary>
	/// Fired when a player joins the current lobby/game
	/// </summary>
	public event Action<Connection> OnPlayerJoined;

	/// <summary>
	/// Fired when a player leaves the current lobby/game
	/// </summary>
	public event Action<Connection> OnPlayerLeft;

	/// <summary>
	/// List of available lobbies from the last search
	/// </summary>
	public IReadOnlyList<LobbyInformation> AvailableLobbies => _availableLobbies;
	private List<LobbyInformation> _availableLobbies = new();

	/// <summary>
	/// Whether we're currently searching for lobbies
	/// </summary>
	public bool IsSearchingLobbies { get; private set; }

	/// <summary>
	/// Current lobby members (all connected players)
	/// </summary>
	public IReadOnlyList<Connection> LobbyMembers => Connection.All;

	protected override void OnAwake()
	{
		base.OnAwake();
		GameObject.Flags = GameObjectFlags.DontDestroyOnLoad;
	}

	// =========================================================================
	// INetworkListener Implementation
	// =========================================================================

	/// <summary>
	/// Called on the host when someone successfully joins and completes handshake
	/// </summary>
	public void OnActive( Connection connection )
	{
		Log.Info( $"[NetworkManager] Player active: {connection.DisplayName}" );
		OnPlayerJoined?.Invoke( connection );
	}

	/// <summary>
	/// Called when a client disconnects from the server
	/// </summary>
	public void OnDisconnected( Connection connection )
	{
		Log.Info( $"[NetworkManager] Player disconnected: {connection.DisplayName}" );
		OnPlayerLeft?.Invoke( connection );
	}

	// =========================================================================
	// Lobby Operations
	// =========================================================================

	/// <summary>
	/// Create a new lobby and set session state to CustomLobby.
	/// </summary>
	public void CreateLobby( int maxPlayers = 8, LobbyPrivacy privacy = LobbyPrivacy.Private, string name = "My Lobby Name" )
	{
		Log.Info( $"[NetworkManager] Creating lobby: {name}, MaxPlayers: {maxPlayers}, Privacy: {privacy}" );

		Networking.CreateLobby( new LobbyConfig()
		{
			MaxPlayers = maxPlayers,
			Privacy = privacy,
			Name = name,
			Hidden = false,
			DestroyWhenHostLeaves = false
		} );

		// Update GameSession state
		if ( GameSession.Instance != null )
		{
			GameSession.Instance.State = SessionState.CustomLobby;
		}
	}

	/// <summary>
	/// Join an existing lobby.
	/// </summary>
	public void JoinLobby( LobbyInformation lobby )
	{
		Log.Info( $"[NetworkManager] Joining lobby: {lobby.Name}" );
		Networking.Connect( lobby.LobbyId );

		// State will be synced from host's GameSession once connected
	}

	/// <summary>
	/// Leave the current lobby/game.
	/// </summary>
	public void LeaveLobby()
	{
		Log.Info( "[NetworkManager] Leaving lobby" );
		Networking.Disconnect();

		// Reset GameSession state
		if ( GameSession.Instance != null )
		{
			GameSession.Instance.State = SessionState.None;
			GameSession.Instance.ClearTeams();
		}
	}

	/// <summary>
	/// Start the game. Host only.
	/// </summary>
	public void StartGame()
	{
		if ( !Networking.IsHost )
		{
			Log.Warning( "[NetworkManager] Only the host can start the game" );
			return;
		}

		var session = GameSession.Instance;
		if ( session == null || session.State != SessionState.CustomLobby )
		{
			Log.Warning( "[NetworkManager] Can only start game from CustomLobby state" );
			return;
		}

		Log.Info( "[NetworkManager] Starting game..." );
		session.State = SessionState.InGame;

		// TODO: Load game scene
	}

	/// <summary>
	/// Search for available lobbies.
	/// </summary>
	public async void SearchLobbies()
	{
		if ( IsSearchingLobbies )
			return;

		IsSearchingLobbies = true;
		OnLobbiesUpdated?.Invoke();
		Log.Info( "[NetworkManager] Searching for lobbies..." );

		try
		{
			var lobbies = await Networking.QueryLobbies();
			_availableLobbies.Clear();
			_availableLobbies.AddRange( lobbies );
			Log.Info( $"[NetworkManager] Found {_availableLobbies.Count} lobbies" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[NetworkManager] Failed to search lobbies: {e.Message}" );
		}
		finally
		{
			IsSearchingLobbies = false;
			OnLobbiesUpdated?.Invoke();
		}
	}

	// =========================================================================
	// Debug
	// =========================================================================

	protected override void OnUpdate()
	{
		if ( ShowDebugInfo )
		{
			DrawDebugInfo();
		}
	}

	private void DrawDebugInfo()
	{
		Gizmo.Draw.Color = Color.Black;

		var x = Screen.Width - 1050;
		var y = 200f;
		var lineHeight = 28f;

		Gizmo.Draw.ScreenText( "=== NetworkManager ===", new Vector2( x, y ), "roboto", 24, TextFlag.RightCenter );
		y += lineHeight + 5;

		Gizmo.Draw.ScreenText( $"Active: {Networking.IsActive}", new Vector2( x, y ), "roboto", 20, TextFlag.RightCenter );
		y += lineHeight;

		if ( Networking.IsActive )
		{
			var hostInfo = Networking.IsHost ? "YES (You)" : "NO";
			Gizmo.Draw.ScreenText( $"Is Host: {hostInfo}", new Vector2( x, y ), "roboto", 20, TextFlag.RightCenter );
			y += lineHeight;

			if ( Connection.Host != null )
			{
				Gizmo.Draw.ScreenText( $"Host: {Connection.Host.DisplayName}", new Vector2( x, y ), "roboto", 20, TextFlag.RightCenter );
				y += lineHeight;
			}

			Gizmo.Draw.ScreenText( $"Players ({Connection.All.Count}):", new Vector2( x, y ), "roboto", 20, TextFlag.RightCenter );
			y += lineHeight;

			foreach ( var connection in Connection.All )
			{
				var tags = new List<string>();
				if ( connection.IsHost ) tags.Add( "HOST" );
				if ( connection == Connection.Local ) tags.Add( "YOU" );
				var tagStr = tags.Count > 0 ? $"[{string.Join( ", ", tags )}]" : "";
				var ping = connection.Ping > 0 ? $"({connection.Ping}ms)" : "";

				Gizmo.Draw.ScreenText( $"  {connection.DisplayName} {tagStr} {ping}", new Vector2( x, y ), "roboto", 18, TextFlag.RightCenter );
				y += lineHeight - 5;
			}
		}
		else
		{
			Gizmo.Draw.ScreenText( "Not connected", new Vector2( x, y ), "roboto", 20, TextFlag.RightCenter );
		}
	}
}

using Sandbox;
using Sandbox.Network;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public enum NetworkState
{
	/// <summary>
	/// Not in any network session, in the menu preparing to create/join a lobby
	/// </summary>
	CreatingLobby,

	/// <summary>
	/// Lobby is created and active, players can connect, teams can be assigned, game settings configured
	/// </summary>
	InLobby,

	/// <summary>
	/// Host has started the game, all players are loading into the game scene
	/// </summary>
	StartingGame
}

public sealed class NetworkManager : SingletonComponent<NetworkManager>, Component.INetworkListener
{
	[Property] public bool showDebugInfo { get; set; } = true;

	/// <summary>
	/// Current state of the network loop
	/// </summary>
	public NetworkState CurrentState { get; private set; } = NetworkState.CreatingLobby;

	/// <summary>
	/// Fired when the network state changes
	/// </summary>
	public event Action<NetworkState, NetworkState> OnStateChanged;

	/// <summary>
	/// Fired when the lobby list is updated after a search
	/// </summary>
	public event Action OnLobbiesUpdated;

	/// <summary>
	/// Fired when a player joins the current lobby
	/// </summary>
	public event Action<Connection> OnPlayerJoined;

	/// <summary>
	/// Fired when a player leaves the current lobby
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

		// Make this GameObject persist across scene loads
		GameObject.Flags = GameObjectFlags.DontDestroyOnLoad;
	}

	/// <summary>
	/// Called on the host when someone successfully joins and completes handshake (including local player)
	/// </summary>
	public void OnActive( Connection connection )
	{
		Log.Info( $"[Network] Player joined and is active: {connection.DisplayName}" );
		OnPlayerJoined?.Invoke( connection );
	}

	/// <summary>
	/// Called when a client disconnects from the server
	/// </summary>
	public void OnDisconnected( Connection connection )
	{
		Log.Info( $"[Network] Player disconnected: {connection.DisplayName}" );
		OnPlayerLeft?.Invoke( connection );
	}

	/// <summary>
	/// Transition to a new network state
	/// </summary>
	public void SetState( NetworkState newState )
	{
		if ( CurrentState == newState )
			return;

		var oldState = CurrentState;
		CurrentState = newState;

		Log.Info( $"[Network] State changed: {oldState} -> {newState}" );
		OnStateChanged?.Invoke( oldState, newState );
	}

	public void CreateLobby( int maxPlayers = 8, LobbyPrivacy privacy = LobbyPrivacy.Private, string name = "My Lobby Name" )
	{
		Networking.CreateLobby( new LobbyConfig()
		{
			MaxPlayers = maxPlayers,
			Privacy = privacy,
			Name = name
		} );

		SetState( NetworkState.InLobby );
	}

	public void LeaveLobby()
	{
		Networking.Disconnect();
		SetState( NetworkState.CreatingLobby );
	}

	public void StartGame()
	{
		if ( !Networking.IsHost )
		{
			Log.Warning( "[Network] Only the host can start the game" );
			return;
		}

		if ( CurrentState != NetworkState.InLobby )
		{
			Log.Warning( "[Network] Can only start game from InLobby state" );
			return;
		}

		SetState( NetworkState.StartingGame );
	}

	public async void SearchLobbies()
	{
		if ( IsSearchingLobbies )
			return;

		IsSearchingLobbies = true;
		Log.Info( "[Network] Searching for lobbies..." );

		try
		{
			var lobbies = await Networking.QueryLobbies();
			_availableLobbies.Clear();
			_availableLobbies.AddRange( lobbies );

			Log.Info( $"[Network] Found {_availableLobbies.Count} lobbies" );
			OnLobbiesUpdated?.Invoke();
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Network] Failed to search lobbies: {e.Message}" );
		}
		finally
		{
			IsSearchingLobbies = false;
		}
	}

	public void JoinLobby( LobbyInformation lobby )
	{
		Log.Info( $"[Network] Joining lobby: {lobby.Name}" );

		Networking.Connect( lobby.LobbyId );
		SetState( NetworkState.InLobby );
	}

	protected override void OnUpdate()
	{	if( showDebugInfo ) { DrawNetworkDebugInfo(); }
	}

	private void DrawNetworkDebugInfo()
	{
		Gizmo.Draw.Color = Color.Black;

		// Position on right side
		var x = Screen.Width - 1050;
		var networkY = 200f;
		var networkLineHeight = 30f;

		Gizmo.Draw.ScreenText( "=== Networking Info ===", new Vector2( x, networkY ), "roboto", 28, TextFlag.RightCenter );
		networkY += networkLineHeight + 10;

		Gizmo.Draw.ScreenText( $"State: {CurrentState}", new Vector2( x, networkY ), "roboto", 24, TextFlag.RightCenter );
		networkY += networkLineHeight;

		Gizmo.Draw.ScreenText( $"Networking Active: {Networking.IsActive}", new Vector2( x, networkY ), "roboto", 24, TextFlag.RightCenter );
		networkY += networkLineHeight;

		if ( Networking.IsActive )
		{
			// Host information
			var hostInfo = Networking.IsHost ? "YES (You)" : "NO";
			Gizmo.Draw.ScreenText( $"Is Host: {hostInfo}", new Vector2( x, networkY ), "roboto", 24, TextFlag.RightCenter );
			networkY += networkLineHeight;

			// Host connection info
			if ( Connection.Host != null )
			{
				Gizmo.Draw.ScreenText( $"Host: {Connection.Host.DisplayName} (ID: {Connection.Host.Id})", new Vector2( x, networkY ), "roboto", 24, TextFlag.RightCenter );
				networkY += networkLineHeight;
			}

			// Connected players list
			if ( Connection.All.Count > 0 )
			{
				Gizmo.Draw.ScreenText( $"Connected Players ({Connection.All.Count}):", new Vector2( x, networkY ), "roboto", 24, TextFlag.RightCenter );
				networkY += networkLineHeight;

				foreach ( var connection in Connection.All )
				{
					var isHost = connection.IsHost ? "[HOST]" : "";
					var isSelf = connection == Connection.Local ? "[YOU]" : "";
					var ping = connection.Ping > 0 ? $"({connection.Ping}ms)" : "";
					var playerInfo = $"  - {connection.DisplayName} {isHost}{isSelf} {ping}";
					Gizmo.Draw.ScreenText( playerInfo, new Vector2( x, networkY ), "roboto", 20, TextFlag.RightCenter );
					networkY += networkLineHeight - 5;
				}
			}
			else
			{
				Gizmo.Draw.ScreenText( "No players connected", new Vector2( x, networkY ), "roboto", 24, TextFlag.RightCenter );
				networkY += networkLineHeight;
			}
		}
		else
		{
			Gizmo.Draw.ScreenText( "Not in a network session", new Vector2( x, networkY ), "roboto", 24, TextFlag.RightCenter );
		}
	}
}

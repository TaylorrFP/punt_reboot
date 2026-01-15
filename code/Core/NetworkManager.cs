using Sandbox;
using Sandbox.Network;
using System;

public sealed class NetworkManager : SingletonComponent<NetworkManager>
{
	[Property] public bool showDebugInfo { get; set; } = true;


	protected override void OnAwake()
	{
		base.OnAwake();

		// Make this GameObject persist across scene loads
		GameObject.Flags = GameObjectFlags.DontDestroyOnLoad;
	}

	public void CreateLobby( int maxPlayers = 8, LobbyPrivacy privacy = LobbyPrivacy.Private, string name = "My Lobby Name" )
	{


		Networking.CreateLobby( new LobbyConfig()
		{
			MaxPlayers = maxPlayers,
			Privacy = privacy,
			Name = name
		} );

	}

	public void SearchLobbies()
	{
		


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

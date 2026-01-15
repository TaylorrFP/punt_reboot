using Sandbox;
using System;

public sealed class PartyManager : SingletonComponent<PartyManager>
{
	[Property] public bool showDebugInfo { get; set; } = true;

	private PartyRoom _currentParty;

	protected override void OnAwake()
	{
		base.OnAwake();

		// Make this GameObject persist across scene loads
		GameObject.Flags = GameObjectFlags.DontDestroyOnLoad;
	}



	protected override void OnUpdate()
	{

		// Check if the party has changed (joined, left, or invited)
		var currentParty = PartyRoom.Current;

		if ( currentParty != _currentParty )
		{
			// Unsubscribe from old party
			if ( _currentParty != null )
			{
				_currentParty.OnJoin -= HandlePlayerJoined;
				_currentParty.OnLeave -= HandlePlayerLeft;
				_currentParty.OnChatMessage -= HandleChatMessage;
				Log.Info( "[Party] Left party or party changed" );
			}

			// Subscribe to new party
			if ( currentParty != null )
			{
				currentParty.OnJoin += HandlePlayerJoined;
				currentParty.OnLeave += HandlePlayerLeft;
				currentParty.OnChatMessage += HandleChatMessage;
				Log.Info( "[Party] Joined party!" );
			}

			_currentParty = currentParty;
		}


		if ( showDebugInfo ) { DrawPartyDebugInfo(); }
		// Debug drawing
		
	}

	private void DrawPartyDebugInfo()
	{
		Gizmo.Draw.Color = Color.Black;

		var myPartyRoom = PartyRoom.Current;

		// Position on right side, middle vertically
		var startY = Screen.Height / 2 - 100;
		var x = Screen.Width - 1050;

		var y = startY;
		var lineHeight = 30f;

		if ( myPartyRoom is null )
		{
			Gizmo.Draw.ScreenText( "Not in a party", new Vector2( x, startY ), "roboto", 24, TextFlag.RightCenter );
			return;
		}

		Gizmo.Draw.ScreenText( $"=== Party Debug Info ===", new Vector2( x, y ), "roboto", 28, TextFlag.RightCenter );
		y += lineHeight + 10;

		Gizmo.Draw.ScreenText( $"Members: {myPartyRoom.Members.Count()}", new Vector2( x, y ), "roboto", 24, TextFlag.RightCenter );
		y += lineHeight;

		Gizmo.Draw.ScreenText( $"Owner: {myPartyRoom.Owner.Name ?? "Unknown"}", new Vector2( x, y ), "roboto", 24, TextFlag.RightCenter );
		y += lineHeight;

		Gizmo.Draw.ScreenText( $"Owner ID: {myPartyRoom.Owner.Id}", new Vector2( x, y ), "roboto", 24, TextFlag.RightCenter );
		y += lineHeight;

		// List all members
		Gizmo.Draw.ScreenText( "Member List:", new Vector2( x, y ), "roboto", 24, TextFlag.RightCenter );
		y += lineHeight;

		foreach ( var member in myPartyRoom.Members )
		{
			var playingStatus = member.IsPlayingThisGame ? "ONLINE" : "OFFLINE";
			var memberInfo = $"  - {member.Name} [{playingStatus}] (ID: {member.Id})";
			Gizmo.Draw.ScreenText( memberInfo, new Vector2( x, y ), "roboto", 20, TextFlag.RightCenter );
			y += lineHeight - 5;
		}
	}

	private void HandlePlayerJoined( Friend friend )
	{
		Log.Info( $"[Party] {friend.Name} joined the party!" );
	}

	private void HandlePlayerLeft( Friend friend )
	{
		Log.Info( $"[Party] {friend.Name} left the party!" );
		
	}

	private void HandleChatMessage( Friend friend, string message )
	{
		Log.Info( $"[Party Chat] {friend.Name}: {message}" );
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		// Unsubscribe from PartyRoom events when component is destroyed
		if ( _currentParty != null )
		{
			_currentParty.OnJoin -= HandlePlayerJoined;
			_currentParty.OnLeave -= HandlePlayerLeft;
			_currentParty.OnChatMessage -= HandleChatMessage;
		}
	}
}

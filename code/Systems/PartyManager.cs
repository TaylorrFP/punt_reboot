using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace Punt.Systems;

/// <summary>
/// Manages party/lobby functionality for the game.
/// Provides a centralized way to handle party members, invites, and party state.
/// This is a global singleton that persists across scene loads.
/// </summary>
public sealed class PartyManager
{
	/// <summary>
	/// Singleton instance of the PartyManager
	/// </summary>
	public static PartyManager Instance { get; private set; }

	/// <summary>
	/// List of all party members (including local player)
	/// </summary>
	public List<PartyMember> Members { get; private set; } = new();

	/// <summary>
	/// The local player's party member info
	/// </summary>
	public PartyMember LocalMember => Members.FirstOrDefault( m => m.IsLocal );

	/// <summary>
	/// Is the local player the party leader?
	/// </summary>
	public bool IsPartyLeader => LocalMember?.IsLeader ?? false;

	/// <summary>
	/// Current party size
	/// </summary>
	public int PartySize => Members.Count;

	/// <summary>
	/// Maximum party size
	/// </summary>
	public int MaxPartySize { get; set; } = 4;

	/// <summary>
	/// Event fired when party members change
	/// </summary>
	public event System.Action OnPartyChanged;

	private PartyManager()
	{
		// Private constructor for singleton
		InitializeLocalPlayer();
	}

	/// <summary>
	/// Initialize the PartyManager singleton
	/// </summary>
	public static void Initialize()
	{
		if ( Instance == null )
		{
			Instance = new PartyManager();
			Log.Info( "PartyManager initialized" );
		}
	}

	void InitializeLocalPlayer()
	{
		var localConnection = Connection.Local;
		if ( localConnection != null )
		{
			var localMember = new PartyMember
			{
				SteamId = localConnection.SteamId,
				DisplayName = localConnection.DisplayName,
				IsLocal = true,
				IsLeader = true // Solo player is always leader
			};

			Members.Add( localMember );
			Log.Info( $"Initialized local player: {localMember.DisplayName}" );
		}
	}

	/// <summary>
	/// Add a player to the party
	/// </summary>
	public bool AddMember( ulong steamId, string displayName )
	{
		if ( PartySize >= MaxPartySize )
		{
			Log.Warning( "Party is full!" );
			return false;
		}

		if ( Members.Any( m => m.SteamId == steamId ) )
		{
			Log.Warning( $"Player {displayName} is already in the party" );
			return false;
		}

		var member = new PartyMember
		{
			SteamId = steamId,
			DisplayName = displayName,
			IsLocal = false,
			IsLeader = false
		};

		Members.Add( member );
		Log.Info( $"Added {displayName} to party" );
		OnPartyChanged?.Invoke();
		return true;
	}

	/// <summary>
	/// Remove a player from the party
	/// </summary>
	public bool RemoveMember( ulong steamId )
	{
		var member = Members.FirstOrDefault( m => m.SteamId == steamId );
		if ( member == null || member.IsLocal )
		{
			return false;
		}

		Members.Remove( member );
		Log.Info( $"Removed {member.DisplayName} from party" );
		OnPartyChanged?.Invoke();
		return true;
	}

	/// <summary>
	/// Get all party members except the local player
	/// </summary>
	public IEnumerable<PartyMember> GetOtherMembers()
	{
		return Members.Where( m => !m.IsLocal );
	}

	/// <summary>
	/// Clear all party members except local player
	/// </summary>
	public void LeaveParty()
	{
		var localMember = LocalMember;
		Members.Clear();
		if ( localMember != null )
		{
			Members.Add( localMember );
			localMember.IsLeader = true;
		}
		Log.Info( "Left party" );
		OnPartyChanged?.Invoke();
	}

	/// <summary>
	/// Sync party members from current lobby connections
	/// </summary>
	public void SyncFromConnections()
	{
		if ( !Networking.IsActive )
			return;

		// Get all connections in the current lobby
		var connections = Connection.All;

		// Remove members who are no longer connected
		var disconnectedMembers = Members.Where( m =>
			!m.IsLocal && !connections.Any( c => c.SteamId == m.SteamId )
		).ToList();

		foreach ( var member in disconnectedMembers )
		{
			RemoveMember( member.SteamId );
		}

		// Add new connections
		foreach ( var connection in connections )
		{
			if ( connection.IsHost )
				continue; // Skip the local connection

			if ( !Members.Any( m => m.SteamId == connection.SteamId ) )
			{
				AddMember( connection.SteamId, connection.DisplayName );
			}
		}
	}
}

/// <summary>
/// Represents a party member
/// </summary>
public class PartyMember
{
	public ulong SteamId { get; set; }
	public string DisplayName { get; set; }
	public bool IsLocal { get; set; }
	public bool IsLeader { get; set; }

	public string GetAvatarUrl()
	{
		return $"avatar:{SteamId}";
	}
}

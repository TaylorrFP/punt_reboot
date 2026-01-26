using Sandbox;
using System.Collections.Generic;

namespace Punt.Match
{
	/// <summary>
	/// Handles spawning and resetting punt pieces based on formations.
	/// Formations are per-team and positioned on opposite ends of the pitch.
	/// Formation coordinates are normalized to each team's half only (0.0 = goal line, 1.0 = center line).
	/// </summary>
	public sealed class PieceSpawner : Component
	{
		/// <summary>
		/// Formation for the Blue team (spawns in their half).
		/// </summary>
		[Property]
		public Formation BlueFormation { get; set; }

		/// <summary>
		/// Formation for the Red team (spawns in their half).
		/// </summary>
		[Property]
		public Formation RedFormation { get; set; }

		/// <summary>
		/// Prefab GameObject template for spawning pieces. Should have a PuntPiece component.
		/// </summary>
		[Property]
		public GameObject PiecePrefab { get; set; }

		/// <summary>
		/// Pitch bounds component to get world dimensions.
		/// </summary>
		[Property]
		public PitchGenerator Pitch { get; set; }

		// Cache spawned pieces for quick reset/cleanup
		private List<PuntPiece> SpawnedPieces { get; set; } = new();

		/// <summary>
		/// Spawn all pieces based on team formations.
		/// Only executes on host in networked games. Can be forced for editor/singleplayer.
		/// </summary>
		public void SpawnPieces( FormationVariant blueVariant = FormationVariant.Attacking, bool forceSpawn = false )
		{
			// In networked games, only host can spawn. Can be forced for editor/singleplayer.
			if ( Networking.IsActive && !Networking.IsHost && !forceSpawn )
				return;

			ClearPieces();

			// Get pitch bounds
			GetPitchBounds( out var pitchMin, out var pitchMax );

			// Red team always uses opposite variant from Blue
			var redVariant = blueVariant == FormationVariant.Attacking
				? FormationVariant.Defending
				: FormationVariant.Attacking;

			// Spawn Blue team pieces
			if ( BlueFormation != null )
			{
				SpawnTeamPieces( BlueFormation, TeamSide.Blue, pitchMin, pitchMax, isBlueTeam: true, blueVariant );
			}

			// Spawn Red team pieces
			if ( RedFormation != null )
			{
				SpawnTeamPieces( RedFormation, TeamSide.Red, pitchMin, pitchMax, isBlueTeam: false, redVariant );
			}
		}

		/// <summary>
		/// Reset all pieces to their formation positions.
		/// Used after goals to restart the round.
		/// Only executes on host in networked games. Can be forced for editor/singleplayer.
		/// </summary>
		public void ResetPieces( FormationVariant blueVariant = FormationVariant.Defending, bool forceReset = false )
		{
			// In networked games, only host can reset. Can be forced for editor/singleplayer.
			if ( Networking.IsActive && !Networking.IsHost && !forceReset )
				return;

			GetPitchBounds( out var pitchMin, out var pitchMax );

			// Red team always uses opposite variant from Blue
			var redVariant = blueVariant == FormationVariant.Attacking
				? FormationVariant.Defending
				: FormationVariant.Attacking;

			var bluePositions = CalculateWorldPositions( BlueFormation, pitchMin, pitchMax, isBlueTeam: true, blueVariant );
			var redPositions = CalculateWorldPositions( RedFormation, pitchMin, pitchMax, isBlueTeam: false, redVariant );

			int blueIndex = 0;
			int redIndex = 0;

			foreach ( var piece in SpawnedPieces )
			{
				if ( piece == null )
					continue;

				if ( piece.Team == TeamSide.Blue && blueIndex < bluePositions.Count )
				{
					piece.WorldPosition = bluePositions[blueIndex++];
					piece.Rigidbody.Velocity = Vector3.Zero;
					piece.Rigidbody.AngularVelocity = Vector3.Zero;
					piece.State = PieceState.Ready;
				}
				else if ( piece.Team == TeamSide.Red && redIndex < redPositions.Count )
				{
					piece.WorldPosition = redPositions[redIndex++];
					piece.Rigidbody.Velocity = Vector3.Zero;
					piece.Rigidbody.AngularVelocity = Vector3.Zero;
					piece.State = PieceState.Ready;
				}
			}
		}

		private void SpawnTeamPieces( Formation formation, TeamSide team, Vector3 pitchMin, Vector3 pitchMax, bool isBlueTeam, FormationVariant variant )
		{
			if ( formation == null || PiecePrefab == null )
				return;

			var positions = CalculateWorldPositions( formation, pitchMin, pitchMax, isBlueTeam, variant );

			foreach ( var worldPos in positions )
			{
				// Clone the prefab GameObject
				var pieceObject = PiecePrefab.Clone( worldPos );

				// Get the PuntPiece component from the cloned object
				var puntPiece = pieceObject.Components.Get<PuntPiece>();
				if ( puntPiece != null )
				{
					puntPiece.Team = team;
					puntPiece.SetTeamMaterialGroups();
					puntPiece.State = PieceState.Ready;

					// Spawn the networked object
					pieceObject.NetworkSpawn();

					SpawnedPieces.Add( puntPiece );
				}
			}
		}

		private List<Vector3> CalculateWorldPositions( Formation formation, Vector3 pitchMin, Vector3 pitchMax, bool isBlueTeam, FormationVariant variant )
		{
			if ( formation == null )
				return new List<Vector3>();

			var normalizedPositions = formation.GetPositions( variant );
			var positions = new List<Vector3>();
			var pitchWidth = pitchMax.x - pitchMin.x;
			var pitchLength = pitchMax.y - pitchMin.y;
			var halfLength = pitchLength / 2f; // Only use half the pitch
			var pitchHeight = pitchMin.z; // Ground level

			foreach ( var normalizedPos in normalizedPositions )
			{
				// X: center-aligned, scale around pitch center
				var worldX = pitchMin.x + (normalizedPos.x * pitchWidth);

				// Y: formation goes from goal line (0.0) to halfway line (1.0)
				// This ensures pieces never spawn on the wrong side
				float worldY;
				if ( isBlueTeam )
				{
					// Blue team: goal at bottom (pitchMin.y), center line above
					// 0.0 -> pitchMin.y (goal line), 1.0 -> pitchCenterY (center line)
					worldY = pitchMin.y + (normalizedPos.y * halfLength);
				}
				else
				{
					// Red team: goal at top (pitchMax.y), center line below
					// 0.0 -> pitchMax.y (goal line), 1.0 -> pitchCenterY (center line)
					worldY = pitchMax.y - (normalizedPos.y * halfLength);
				}

				positions.Add( new Vector3( worldX, worldY, pitchHeight ) );
			}

			return positions;
		}

		private void GetPitchBounds( out Vector3 min, out Vector3 max )
		{
			if ( Pitch != null )
			{
				// Get pitch dimensions from PitchGenerator
				var pitchCenter = Pitch.WorldPosition;
				var halfWidth = Pitch.PitchWidth / 2f;
				var halfLength = Pitch.PitchLength / 2f;

				// Pitch is centered at WorldPosition, extending equally in all directions
				min = pitchCenter + new Vector3( -halfWidth, -halfLength, 0 );
				max = pitchCenter + new Vector3( halfWidth, halfLength, Pitch.WallHeight );
			}
			else
			{
				// Default bounds if no pitch component
				// Pitch dimensions: 100x150, wall height 50, centered at origin
				min = new Vector3( -50, -75, 0 );
				max = new Vector3( 50, 75, 50 );
			}
		}

		private void ClearPieces()
		{
			foreach ( var piece in SpawnedPieces )
			{
				if ( piece != null )
				{
					piece.GameObject?.Destroy();
				}
			}
			SpawnedPieces.Clear();
		}

	}
}

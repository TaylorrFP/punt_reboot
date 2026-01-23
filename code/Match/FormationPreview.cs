using Sandbox;

namespace Punt.Match
{
	/// <summary>
	/// Editor-only component for previewing formation positions in the scene.
	/// Drag your Formation asset here and it will show real-time gizmos as you edit it.
	/// </summary>
	public sealed class FormationPreview : Component
	{
		[Property]
		public Formation Formation { get; set; }

		[Property]
		public PitchGenerator Pitch { get; set; }


		protected override void DrawGizmos()
		{
			if ( Formation == null || Pitch == null )
				return;

			GetPitchBounds( out var pitchMin, out var pitchMax );


				if ( Formation.AttackingPositions.Count > 0 || Formation.DefendingPositions.Count > 0 )
				{
					Formation.DrawDebugGizmos( pitchMin, pitchMax, isBlueTeam: true, FormationVariant.Attacking, Color.Blue );
					Formation.DrawDebugGizmos( pitchMin, pitchMax, isBlueTeam: false, FormationVariant.Defending, Color.Red );
				}



		}

		private void GetPitchBounds( out Vector3 min, out Vector3 max )
		{
			if ( Pitch != null )
			{
				var pitchCenter = Pitch.WorldPosition;
				var halfWidth = Pitch.PitchWidth / 2f;
				var halfLength = Pitch.PitchLength / 2f;

				min = pitchCenter + new Vector3( -halfWidth, -halfLength, 0 );
				max = pitchCenter + new Vector3( halfWidth, halfLength, Pitch.WallHeight );
			}
			else
			{
				// Default bounds if no pitch component
				min = new Vector3( -50, -75, 0 );
				max = new Vector3( 50, 75, 50 );
			}
		}
	}
}

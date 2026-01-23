using Sandbox;
using System.Collections.Generic;

namespace Punt
{
	public enum FormationVariant
	{
		Attacking,
		Defending
	}

	[AssetType( Name = "Punt Formation", Extension = "form", Category = "game" )]
	public partial class Formation : GameResource
	{
		public List<Vector2> AttackingPositions { get; set; } = new();
		public List<Vector2> DefendingPositions { get; set; } = new();

		public List<Vector2> GetPositions( FormationVariant variant ) => variant switch
		{
			FormationVariant.Attacking => AttackingPositions,
			FormationVariant.Defending => DefendingPositions,
			_ => new List<Vector2>()
		};

		public void DrawDebugGizmos( Vector3 pitchMin, Vector3 pitchMax, bool isBlueTeam, FormationVariant variant, Color color )
		{
			var normalizedPositions = GetPositions( variant );
			if ( normalizedPositions.Count == 0 )
				return;

			var pitchWidth = pitchMax.x - pitchMin.x;
			var pitchLength = pitchMax.y - pitchMin.y;
			var halfLength = pitchLength / 2f;
			var pitchHeight = pitchMin.z + 5;

			foreach ( var normalizedPos in normalizedPositions )
			{
				var worldX = pitchMin.x + (normalizedPos.x * pitchWidth);
				float worldY;
				if ( isBlueTeam )
				{
					worldY = pitchMin.y + (normalizedPos.y * halfLength);
				}
				else
				{
					worldY = pitchMax.y - (normalizedPos.y * halfLength);
				}

				var worldPos = new Vector3( worldX, worldY, pitchHeight );
				Gizmo.Draw.Color = color;
				Gizmo.Draw.SolidSphere( worldPos, 20 );
			}
		}

		public static IReadOnlyList<Formation> All => _all;
		internal static List<Formation> _all = new();

		protected override void PostLoad()
		{
			base.PostLoad();
			if ( !_all.Contains( this ) )
				_all.Add( this );
		}

		protected override Bitmap CreateAssetTypeIcon( int width, int height )
		{
			return CreateSimpleAssetTypeIcon( "account_tree", width, height, "#6db3f2", "black" );
		}
	}
}

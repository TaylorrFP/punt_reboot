using Punt;
using Sandbox;

/// <summary>
/// Temporary balance override component. Add to scene to override physics and flick settings globally.
/// Delete this component to revert all changes.
/// All values are optional - leave at 0 to skip overriding that property.
/// </summary>
public sealed class BalanceOverride : Component
{
	[Property, Group( "Time" )]
	public bool OverrideTimeScale { get; set; } = false;

	[Property, Group( "Time" ), ShowIf( nameof( OverrideTimeScale ), true ), Range( 0.1f, 3f ), Step( 0.1f )]
	public float TimeScale { get; set; } = 1f;

	[Property, Group( "Flick Settings" )]
	public bool OverrideFlickSettings { get; set; } = false;

	[Property, Group( "Flick Settings" ), ShowIf( nameof( OverrideFlickSettings ), true )]
	public float MinFlickForce { get; set; } = 0f;

	[Property, Group( "Flick Settings" ), ShowIf( nameof( OverrideFlickSettings ), true )]
	public float MaxFlickForce { get; set; } = 0f;

	[Property, Group( "Pieces - Physics" )]
	public bool OverridePiecePhysics { get; set; } = false;

	[Property, Group( "Pieces - Physics" ), ShowIf( nameof( OverridePiecePhysics ), true )]
	public float PieceLinearDamping { get; set; } = -1f;

	[Property, Group( "Pieces - Physics" ), ShowIf( nameof( OverridePiecePhysics ), true )]
	public float PieceAngularDamping { get; set; } = -1f;

	[Property, Group( "Pieces - Physics" ), ShowIf( nameof( OverridePiecePhysics ), true )]
	public float PieceMass { get; set; } = -1f;

	[Property, Group( "Pieces - Physics" ), ShowIf( nameof( OverridePiecePhysics ), true )]
	public float PieceCenterOfMassZ { get; set; } = float.NaN;

	[Property, Group( "Pieces - Surface" )]
	public bool OverridePieceSurface { get; set; } = false;

	[Property, Group( "Pieces - Surface" ), ShowIf( nameof( OverridePieceSurface ), true )]
	public float PieceFriction { get; set; } = -1f;

	[Property, Group( "Pieces - Surface" ), ShowIf( nameof( OverridePieceSurface ), true )]
	public float PieceElasticity { get; set; } = -1f;

	[Property, Group( "Pieces - Surface" ), ShowIf( nameof( OverridePieceSurface ), true )]
	public float PieceRollingResistance { get; set; } = -1f;

	[Property, Group( "Ball - Physics" )]
	public bool OverrideBallPhysics { get; set; } = false;

	[Property, Group( "Ball - Physics" ), ShowIf( nameof( OverrideBallPhysics ), true )]
	public float BallLinearDamping { get; set; } = -1f;

	[Property, Group( "Ball - Physics" ), ShowIf( nameof( OverrideBallPhysics ), true )]
	public float BallAngularDamping { get; set; } = -1f;

	[Property, Group( "Ball - Physics" ), ShowIf( nameof( OverrideBallPhysics ), true )]
	public float BallMass { get; set; } = -1f;

	[Property, Group( "Ball - Surface" )]
	public bool OverrideBallSurface { get; set; } = false;

	[Property, Group( "Ball - Surface" ), ShowIf( nameof( OverrideBallSurface ), true )]
	public float BallFriction { get; set; } = -1f;

	[Property, Group( "Ball - Surface" ), ShowIf( nameof( OverrideBallSurface ), true )]
	public float BallElasticity { get; set; } = -1f;

	[Property, Group( "Ball - Surface" ), ShowIf( nameof( OverrideBallSurface ), true )]
	public float BallRollingResistance { get; set; } = -1f;

	[Property, Group( "Pitch - Walls" )]
	public bool OverrideWallSurface { get; set; } = false;

	[Property, Group( "Pitch - Walls" ), ShowIf( nameof( OverrideWallSurface ), true )]
	public float WallFriction { get; set; } = -1f;

	[Property, Group( "Pitch - Walls" ), ShowIf( nameof( OverrideWallSurface ), true )]
	public float WallElasticity { get; set; } = -1f;

	[Property, Group( "Pitch - Walls" ), ShowIf( nameof( OverrideWallSurface ), true )]
	public float WallRollingResistance { get; set; } = -1f;

	[Property, Group( "Pitch - Floor" )]
	public bool OverrideFloorSurface { get; set; } = false;

	[Property, Group( "Pitch - Floor" ), ShowIf( nameof( OverrideFloorSurface ), true )]
	public float FloorFriction { get; set; } = -1f;

	[Property, Group( "Pitch - Floor" ), ShowIf( nameof( OverrideFloorSurface ), true )]
	public float FloorElasticity { get; set; } = -1f;

	[Property, Group( "Pitch - Floor" ), ShowIf( nameof( OverrideFloorSurface ), true )]
	public float FloorRollingResistance { get; set; } = -1f;

	protected override void OnAwake()
	{
		base.OnAwake();
		ApplyOverrides();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		// Re-apply every frame to catch runtime changes and new pieces
		ApplyOverrides();
	}

	private void ApplyOverrides()
	{
		// Override time scale
		if ( OverrideTimeScale )
		{
			Scene.TimeScale = TimeScale;
		}

		// Override flick settings
		if ( OverrideFlickSettings )
		{
			var players = Scene.GetAllComponents<PlayerController>();
			foreach ( var player in players )
			{
				if ( MinFlickForce > 0 )
					player.MinFlickForce = MinFlickForce;
				if ( MaxFlickForce > 0 )
					player.MaxFlickForce = MaxFlickForce;
			}
		}

		// Override piece physics
		if ( OverridePiecePhysics )
		{
			var pieces = Scene.GetAllComponents<PuntPiece>();
			foreach ( var piece in pieces )
			{
				if ( piece.Rigidbody == null ) continue;

				if ( PieceLinearDamping >= 0 )
					piece.Rigidbody.LinearDamping = PieceLinearDamping;
				if ( PieceAngularDamping >= 0 )
					piece.Rigidbody.AngularDamping = PieceAngularDamping;
				if ( PieceMass >= 0 )
					piece.Rigidbody.MassOverride = PieceMass;
				if ( !float.IsNaN( PieceCenterOfMassZ ) )
				{
					var centerOfMass = piece.Rigidbody.MassCenterOverride;
					piece.Rigidbody.MassCenterOverride = new Vector3( centerOfMass.x, centerOfMass.y, PieceCenterOfMassZ );
				}
			}
		}

		// Override piece surface properties (base collider only)
		if ( OverridePieceSurface )
		{
			var pieces = Scene.GetAllComponents<PuntPiece>();
			foreach ( var piece in pieces )
			{
				// Find the ModelCollider (base collider) - skip capsule colliders
				var colliders = piece.GetComponentsInChildren<Collider>();
				foreach ( var collider in colliders )
				{
					if ( collider is ModelCollider )
					{
						if ( PieceFriction >= 0 )
							collider.Friction = PieceFriction;
						if ( PieceElasticity >= 0 )
							collider.Elasticity = PieceElasticity;
						if ( PieceRollingResistance >= 0 )
							collider.RollingResistance = PieceRollingResistance;
						break; // Only apply to first ModelCollider
					}
				}
			}
		}

		// Override ball physics
		if ( OverrideBallPhysics )
		{
			var balls = Scene.GetAllComponents<PuntBall>();
			foreach ( var ball in balls )
			{
				if ( ball.Rigidbody == null ) continue;

				if ( BallLinearDamping >= 0 )
					ball.Rigidbody.LinearDamping = BallLinearDamping;
				if ( BallAngularDamping >= 0 )
					ball.Rigidbody.AngularDamping = BallAngularDamping;
				if ( BallMass >= 0 )
					ball.Rigidbody.MassOverride = BallMass;
			}
		}

		// Override ball surface properties
		if ( OverrideBallSurface )
		{
			var balls = Scene.GetAllComponents<PuntBall>();
			foreach ( var ball in balls )
			{
				var colliders = ball.GetComponentsInChildren<Collider>();
				foreach ( var collider in colliders )
				{
					if ( BallFriction >= 0 )
						collider.Friction = BallFriction;
					if ( BallElasticity >= 0 )
						collider.Elasticity = BallElasticity;
					if ( BallRollingResistance >= 0 )
						collider.RollingResistance = BallRollingResistance;
				}
			}
		}

		// Override wall surface properties
		if ( OverrideWallSurface )
		{
			var wallObjects = Scene.FindAllWithTag( "wall" );
			foreach ( var wallObj in wallObjects )
			{
				var colliders = wallObj.Components.GetAll<Collider>();
				foreach ( var collider in colliders )
				{
					if ( WallFriction >= 0 )
						collider.Friction = WallFriction;
					if ( WallElasticity >= 0 )
						collider.Elasticity = WallElasticity;
					if ( WallRollingResistance >= 0 )
						collider.RollingResistance = WallRollingResistance;
				}
			}
		}

		// Override floor surface properties
		if ( OverrideFloorSurface )
		{
			var floorObjects = Scene.FindAllWithTag( "floor" );
			foreach ( var floorObj in floorObjects )
			{
				var colliders = floorObj.Components.GetAll<Collider>();
				foreach ( var collider in colliders )
				{
					if ( FloorFriction >= 0 )
						collider.Friction = FloorFriction;
					if ( FloorElasticity >= 0 )
						collider.Elasticity = FloorElasticity;
					if ( FloorRollingResistance >= 0 )
						collider.RollingResistance = FloorRollingResistance;
				}
			}
		}
	}
}

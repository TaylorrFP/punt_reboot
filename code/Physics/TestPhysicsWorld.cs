using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Creates and manages an isolated physics world with test bodies.
/// Can clone physics shapes from existing PuntPieces or create simple test spheres.
/// </summary>
public sealed class TestPhysicsWorld : Component
{
	[Property, Group( "Physics World" )]
	public Vector3 Gravity { get; set; } = new Vector3( 0, 0, -800f );

	[Property, Group( "Punt Pieces" )]
	public List<PuntPiece> SourcePieces { get; set; } = new();

	[Property, Group( "Punt Pieces" )]
	public bool UsePuntPieceShapes { get; set; } = true;

	[Property, Group( "Test Bodies" )]
	public int NumberOfBodies { get; set; } = 3;

	[Property, Group( "Test Bodies" )]
	public float BodyMass { get; set; } = 1f;

	[Property, Group( "Test Bodies" )]
	public float BodyRadius { get; set; } = 5f;

	[Property, Group( "Test Bodies" )]
	public Vector3 InitialVelocity { get; set; } = new Vector3( 50f, 0, 0 );

	[Property, Group( "Floor" )]
	public bool CreateFloor { get; set; } = true;

	[Property, Group( "Floor" )]
	public Vector3 FloorSize { get; set; } = new Vector3( 500f, 500f, 10f );

	[Property, Group( "Floor" )]
	public float FloorOffset { get; set; } = -50f;

	[Property, Group( "Debug" )]
	public bool ShowDebug { get; set; } = true;

	[Property, Group( "Debug" )]
	public Color DebugColor { get; set; } = Color.Green;

	[Property, Group( "Debug" )]
	public Color CapsuleColor { get; set; } = Color.Cyan;

	private PhysicsWorld physicsWorld;
	private List<PhysicsBody> testBodies = new();
	private PhysicsBody floorBody;

	// Store shape info for debug drawing
	private struct BodyShapeInfo
	{
		public PhysicsBody Body;
		public float CapsuleRadius;
		public Vector3 CapsuleStart;
		public Vector3 CapsuleEnd;
	}
	private List<BodyShapeInfo> bodyShapeInfos = new();

	protected override void OnStart()
	{
		CreatePhysicsWorld();
		SpawnTestBodies();
	}

	protected override void OnFixedUpdate()
	{
		if ( physicsWorld == null )
			return;

		// Step the physics world at fixed timestep
		physicsWorld.Step( Time.Delta );
	}

	protected override void OnUpdate()
	{
		// Draw debug visualization
		if ( ShowDebug )
		{
			DrawDebugBodies();
			DrawDebugFloor();
		}
	}

	protected override void DrawGizmos()
	{
		if ( ShowDebug )
		{
			DrawDebugBodies();
			DrawDebugFloor();
		}
	}

	[Button]
	public void RespawnBodies()
	{
		ClearBodies();
		SpawnTestBodies();
	}

	private void CreatePhysicsWorld()
	{
		if ( physicsWorld != null )
			physicsWorld.Delete();

		physicsWorld = new PhysicsWorld();
		physicsWorld.Gravity = Gravity;
	}

	private void ClearBodies()
	{
		testBodies.Clear();
		bodyShapeInfos.Clear();
		floorBody = null;
	}

	private void SpawnTestBodies()
	{
		ClearBodies();

		if ( UsePuntPieceShapes && SourcePieces != null && SourcePieces.Count > 0 )
		{
			SpawnFromPuntPieces();
		}
		else
		{
			SpawnSimpleSpheres();
		}

		// Create floor if enabled
		if ( CreateFloor )
			CreateFloorBody();
	}

	private void SpawnFromPuntPieces()
	{
		foreach ( var piece in SourcePieces )
		{
			if ( piece == null || !piece.IsValid )
				continue;

			var body = new PhysicsBody( physicsWorld );
			var shapeInfo = new BodyShapeInfo { Body = body };

			// Read capsule collider from the piece
			var capsuleCollider = piece.Components.Get<CapsuleCollider>();
			if ( capsuleCollider != null )
			{
				shapeInfo.CapsuleRadius = capsuleCollider.Radius;
				shapeInfo.CapsuleStart = capsuleCollider.Start;
				shapeInfo.CapsuleEnd = capsuleCollider.End;

				body.AddCapsuleShape( capsuleCollider.Start, capsuleCollider.End, capsuleCollider.Radius, true );
			}

			// Enable physics simulation
			body.Enabled = true;
			body.MotionEnabled = true;
			body.GravityEnabled = true;

			// Get mass from the original piece's rigidbody if available
			var sourceMass = piece.Rigidbody?.Mass ?? BodyMass;
			body.Mass = sourceMass;
			body.RebuildMass();

			// Position at the piece's current position
			body.Position = piece.WorldPosition;
			body.Rotation = piece.WorldRotation;

			// Give initial velocity
			body.Velocity = InitialVelocity;

			testBodies.Add( body );
			bodyShapeInfos.Add( shapeInfo );
		}
	}

	private void SpawnSimpleSpheres()
	{
		// Create test spheres
		for ( int i = 0; i < NumberOfBodies; i++ )
		{
			var body = new PhysicsBody( physicsWorld );

			// Add sphere shape to the body
			body.AddSphereShape( new Sphere( Vector3.Zero, BodyRadius ) );

			var shapeInfo = new BodyShapeInfo { Body = body };

			// Enable physics simulation
			body.Enabled = true;
			body.MotionEnabled = true;
			body.GravityEnabled = true;

			// Set mass and rebuild from shape
			body.Mass = BodyMass;
			body.RebuildMass();

			// Position bodies in a line with some variation
			var xOffset = (i - NumberOfBodies / 2f) * 30f;
			var yOffset = i * 10f;
			body.Position = WorldPosition + new Vector3( xOffset, yOffset, 100f + i * 20f );

			// Give them initial velocity
			body.Velocity = new Vector3( (i % 2 == 0 ? 1 : -1) * InitialVelocity.x, InitialVelocity.y, InitialVelocity.z );

			testBodies.Add( body );
			bodyShapeInfos.Add( shapeInfo );
		}
	}

	private void CreateFloorBody()
	{
		floorBody = new PhysicsBody( physicsWorld );

		// Add box shape to the floor body
		var halfExtents = FloorSize / 2f;
		floorBody.AddBoxShape( new BBox( -halfExtents, halfExtents ), Rotation.Identity, false );

		// Configure as static body
		floorBody.Mass = 0; // Static body (mass 0)
		floorBody.Enabled = true;
		floorBody.MotionEnabled = false; // Static bodies don't move
		floorBody.GravityEnabled = false;

		floorBody.Position = WorldPosition + Vector3.Up * FloorOffset;
	}

	private void DrawDebugBodies()
	{
		if ( testBodies == null || testBodies.Count == 0 )
			return;

		for ( int i = 0; i < testBodies.Count; i++ )
		{
			var body = testBodies[i];
			if ( body == null )
				continue;

			var position = body.Position;
			var rotation = body.Rotation;

			// Check if we have shape info for this body
			if ( i < bodyShapeInfos.Count )
			{
				var shapeInfo = bodyShapeInfos[i];

				// Draw capsule if present (radius > 0 means we have capsule data)
				if ( shapeInfo.CapsuleRadius > 0 )
				{
					Gizmo.Draw.Color = CapsuleColor;
					DrawCapsule( position, rotation, shapeInfo.CapsuleStart, shapeInfo.CapsuleEnd, shapeInfo.CapsuleRadius );
				}
				else
				{
					// Fallback to sphere
					Gizmo.Draw.Color = DebugColor;
					Gizmo.Draw.LineSphere( position, BodyRadius );
				}
			}
			else
			{
				// Fallback to sphere
				Gizmo.Draw.Color = DebugColor;
				Gizmo.Draw.LineSphere( position, BodyRadius );
			}

			// Draw velocity vector
			if ( body.Velocity.Length > 0.1f )
			{
				Gizmo.Draw.Color = Color.Yellow;
				Gizmo.Draw.Line( position, position + body.Velocity.Normal * 20f );
			}
		}
	}

	private void DrawCapsule( Vector3 bodyPosition, Rotation bodyRotation, Vector3 localStart, Vector3 localEnd, float radius )
	{
		// Transform local capsule points to world space
		var worldStart = bodyPosition + bodyRotation * localStart;
		var worldEnd = bodyPosition + bodyRotation * localEnd;

		// Draw the two end spheres
		Gizmo.Draw.LineSphere( worldStart, radius );
		Gizmo.Draw.LineSphere( worldEnd, radius );

		// Draw connecting lines along the capsule
		var direction = (worldEnd - worldStart).Normal;
		var perpendicular = Vector3.Cross( direction, Vector3.Up ).Normal;
		if ( perpendicular.Length < 0.01f )
			perpendicular = Vector3.Cross( direction, Vector3.Forward ).Normal;
		var perpendicular2 = Vector3.Cross( direction, perpendicular ).Normal;

		// Draw 4 lines connecting the spheres
		for ( int j = 0; j < 4; j++ )
		{
			float angle = j * MathF.PI * 0.5f;
			var offset = (perpendicular * MathF.Cos( angle ) + perpendicular2 * MathF.Sin( angle )) * radius;
			Gizmo.Draw.Line( worldStart + offset, worldEnd + offset );
		}
	}

	private void DrawDebugFloor()
	{
		if ( !CreateFloor || floorBody == null )
			return;

		var floorPosition = floorBody.Position;
		var halfExtents = FloorSize / 2f;

		Gizmo.Draw.Color = Color.Blue.WithAlpha( 0.3f );
		Gizmo.Transform = new Transform( floorPosition, Rotation.Identity );
		Gizmo.Draw.LineBBox( new BBox( -halfExtents, halfExtents ) );
		Gizmo.Transform = new Transform();
	}

	protected override void OnDestroy()
	{
		if ( physicsWorld != null )
		{
			physicsWorld.Delete();
			physicsWorld = null;
		}
	}
}

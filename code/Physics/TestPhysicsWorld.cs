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

	[Property, Group( "Punt Pieces" )]
	public PlayerController PlayerController { get; set; }

	[Property, Group( "Floor" )]
	public bool CreateFloor { get; set; } = true;

	[Property, Group( "Floor" )]
	public Vector3 FloorSize { get; set; } = new Vector3( 500f, 500f, 10f );

	[Property, Group( "Floor" )]
	public float FloorOffset { get; set; } = -50f;

	[Property, Group( "Floor" )]
	public BalanceOverride BalanceOverride { get; set; }

	[Property, Group( "Debug" )]
	public bool ShowDebug { get; set; } = true;

	[Property, Group( "Debug" )]
	public Color DebugColor { get; set; } = Color.Green;

	[Property, Group( "Debug" )]
	public Color CapsuleColor { get; set; } = Color.Cyan;

	[Property, Group( "Debug" )]
	public Color HullColor { get; set; } = Color.Magenta;

	[Property, Group( "Prediction" )]
	public int MaxPredictionSteps { get; set; } = 500;

	[Property, Group( "Prediction" )]
	public float PredictionTimestep { get; set; } = 1f / 60f;

	[Property, Group( "Prediction" )]
	public float StopVelocityThreshold { get; set; } = 1f;

	private PhysicsWorld physicsWorld;
	private List<PhysicsBody> testBodies = new();
	private PhysicsBody floorBody;

	// Store shape info for debug drawing
	private struct BodyShapeInfo
	{
		public PhysicsBody Body;
		public PuntPiece SourcePiece;
		public float CapsuleRadius;
		public Vector3 CapsuleStart;
		public Vector3 CapsuleEnd;
		public bool HasHull;
		public List<Line[]> HullLines; // Store lines for each hull
	}
	private List<BodyShapeInfo> bodyShapeInfos = new();

	// Track drag start position to prevent jitter
	private PuntPiece currentDragPiece;
	private Vector3 dragStartPosition;

	protected override void OnStart()
	{
		CreatePhysicsWorld();
		SpawnTestBodies();
	}

	protected override void OnUpdate()
	{
		if ( physicsWorld == null )
			return;

		// Run prediction simulation while dragging
		if ( PlayerController != null && PlayerController.IsDragging && PlayerController.SelectedPiece != null )
		{
			var piece = PlayerController.SelectedPiece;

			// Lock in the start position when drag begins
			if ( currentDragPiece != piece )
			{
				currentDragPiece = piece;
				dragStartPosition = piece.WorldPosition;
			}

			RunPredictionSimulation( piece, PlayerController.CurrentFlickVector );
		}
		else
		{
			// Clear drag tracking when not dragging
			currentDragPiece = null;
		}

		// Draw debug visualization
		if ( ShowDebug )
		{
			DrawDebugBodies();
			DrawDebugFloor();
		}
	}

	private void RunPredictionSimulation( PuntPiece piece, Vector3 flickVelocity )
	{
		// Find the test body that corresponds to this piece
		BodyShapeInfo? targetInfo = null;
		foreach ( var info in bodyShapeInfos )
		{
			if ( info.SourcePiece == piece )
			{
				targetInfo = info;
				break;
			}
		}

		if ( !targetInfo.HasValue )
			return;

		var body = targetInfo.Value.Body;

		// Reset body to locked start position with upright rotation
		body.Position = dragStartPosition;
		body.Rotation = Rotation.Identity;
		body.Velocity = flickVelocity;
		body.AngularVelocity = Vector3.Zero;

		// Step physics until the body stops or we hit max iterations
		for ( int i = 0; i < MaxPredictionSteps; i++ )
		{
			physicsWorld.Step( PredictionTimestep );

			// Force angular velocity to zero to prevent any rotation
			body.AngularVelocity = Vector3.Zero;

			// Check if body has come to rest
			if ( body.Velocity.Length < StopVelocityThreshold )
			{
				break;
			}
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
			var shapeInfo = new BodyShapeInfo
			{
				Body = body,
				SourcePiece = piece
			};

			// Get source surface properties from the piece's colliders
			// Note: PhysicsShape only supports Friction - elasticity/restitution not available
			float sourceFriction = 0.5f;

			// Read capsule collider from the piece
			var capsuleCollider = piece.Components.Get<CapsuleCollider>();
			if ( capsuleCollider != null )
			{
				shapeInfo.CapsuleRadius = capsuleCollider.Radius;
				shapeInfo.CapsuleStart = capsuleCollider.Start;
				shapeInfo.CapsuleEnd = capsuleCollider.End;

				var shape = body.AddCapsuleShape( capsuleCollider.Start, capsuleCollider.End, capsuleCollider.Radius, true );

				// Copy surface properties from the capsule collider (nullable)
				sourceFriction = capsuleCollider.Friction ?? 0.5f;

				if ( shape != null )
				{
					shape.Friction = sourceFriction;
				}
			}

			// Read hull from ModelCollider
			var modelCollider = piece.Components.Get<ModelCollider>();
			if ( modelCollider != null && modelCollider.Model != null )
			{
				var model = modelCollider.Model;
				shapeInfo.HullLines = new List<Line[]>();

				// Get surface properties from the model collider (nullable)
				sourceFriction = modelCollider.Friction ?? 0.5f;

				// Try to get hull parts from the model's physics data
				foreach ( var part in model.Physics.Parts )
				{
					// BodyPart has Hulls and Meshes collections
					foreach ( var hull in part.Hulls )
					{
						var shape = body.AddShape( hull, new Transform(), true );

						// Apply surface properties to the shape
						if ( shape != null )
						{
							shape.Friction = sourceFriction;
						}

						// Store hull lines for debug drawing
						var lines = hull.GetLines()?.ToArray();
						if ( lines != null && lines.Length > 0 )
						{
							shapeInfo.HullLines.Add( lines );
						}
					}

					foreach ( var mesh in part.Meshes )
					{
						var shape = body.AddShape( mesh, new Transform(), true, true );

						// Apply surface properties to the shape
						if ( shape != null )
						{
							shape.Friction = sourceFriction;
						}
					}
				}

				shapeInfo.HasHull = shapeInfo.HullLines.Count > 0;
			}

			// Enable physics simulation
			body.Enabled = true;
			body.MotionEnabled = true;
			body.GravityEnabled = true;

			// Copy physics properties from the source rigidbody
			var sourceRb = piece.Rigidbody;
			if ( sourceRb != null )
			{
				// Mass (use MassOverride if set, otherwise use calculated Mass)
				body.Mass = sourceRb.MassOverride > 0 ? sourceRb.MassOverride : sourceRb.Mass;

				// Linear and angular damping
				body.LinearDamping = sourceRb.LinearDamping;
				body.AngularDamping = sourceRb.AngularDamping;

				// Mass center override
				body.LocalMassCenter = sourceRb.MassCenterOverride;
				body.OverrideMassCenter = true;
			}

			body.RebuildMass();

			// Lock all rotation
			body.Locking = new PhysicsLock
			{
				Pitch = true,
				Roll = true,
				Yaw = true
			};

			// Position at the piece's current position
			body.Position = piece.WorldPosition;
			body.Rotation = piece.WorldRotation;

			testBodies.Add( body );
			bodyShapeInfos.Add( shapeInfo );
		}
	}

	private void CreateFloorBody()
	{
		floorBody = new PhysicsBody( physicsWorld );

		// Add box shape to the floor body
		var halfExtents = FloorSize / 2f;
		var shape = floorBody.AddBoxShape( new BBox( -halfExtents, halfExtents ), Rotation.Identity, false );

		// Copy surface properties from BalanceOverride if available
		if ( shape != null && BalanceOverride != null && BalanceOverride.OverrideFloorSurface )
		{
			if ( BalanceOverride.FloorFriction >= 0 )
				shape.Friction = BalanceOverride.FloorFriction;
		}

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

				// Draw hull lines if present
				if ( shapeInfo.HasHull && shapeInfo.HullLines != null )
				{
					Gizmo.Draw.Color = HullColor;
					foreach ( var hullLines in shapeInfo.HullLines )
					{
						DrawHullLines( position, rotation, hullLines );
					}
				}

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

	private void DrawHullLines( Vector3 bodyPosition, Rotation bodyRotation, Line[] lines )
	{
		if ( lines == null || lines.Length == 0 )
			return;

		foreach ( var line in lines )
		{
			// Transform line endpoints to world space
			var worldStart = bodyPosition + bodyRotation * line.Start;
			var worldEnd = bodyPosition + bodyRotation * line.End;
			Gizmo.Draw.Line( worldStart, worldEnd );
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

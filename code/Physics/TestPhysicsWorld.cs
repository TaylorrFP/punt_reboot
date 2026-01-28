using Sandbox;
using System.Collections.Generic;

/// <summary>
/// Creates and manages an isolated physics world with test bodies.
/// Spawns spheres with physics enabled in a separate physics world and draws them.
/// </summary>
public sealed class TestPhysicsWorld : Component
{
	[Property, Group( "Physics World" )]
	public Vector3 Gravity { get; set; } = new Vector3( 0, 0, -800f );

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
	public bool ShowDebugSpheres { get; set; } = true;

	[Property, Group( "Debug" )]
	public Color DebugColor { get; set; } = Color.Green;

	private PhysicsWorld physicsWorld;
	private List<PhysicsBody> testBodies = new();
	private PhysicsBody floorBody;

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
		if ( ShowDebugSpheres )
		{
			DrawDebugBodies();
			DrawDebugFloor();
		}
	}

	protected override void DrawGizmos()
	{
		if ( ShowDebugSpheres )
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
		floorBody = null;
	}

	private void SpawnTestBodies()
	{
		ClearBodies();

		// Create test spheres
		for ( int i = 0; i < NumberOfBodies; i++ )
		{
			var body = new PhysicsBody( physicsWorld );

			// Add sphere shape to the body
			body.AddSphereShape( new Sphere( Vector3.Zero, BodyRadius ) );

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
		}

		// Create floor if enabled
		if ( CreateFloor )
			CreateFloorBody();
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

		Gizmo.Draw.Color = DebugColor;

		foreach ( var body in testBodies )
		{
			if ( body == null )
				continue;

			var position = body.Position;

			// Draw a sphere at the body position
			Gizmo.Draw.LineSphere( position, BodyRadius );

			// Draw velocity vector
			if ( body.Velocity.Length > 0.1f )
			{
				Gizmo.Draw.Color = Color.Yellow;
				Gizmo.Draw.Line( position, position + body.Velocity.Normal * 20f );
				Gizmo.Draw.Color = DebugColor;
			}
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
		Gizmo.Transform = global::Transform.Zero;
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

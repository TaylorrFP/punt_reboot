using Sandbox;
using System;
using System.Collections.Generic;

/// <summary>
/// Generates colliders for a rectangular pitch with beveled corners and goals at each end.
/// The pitch lies on the XY plane with goals on the Y axis (top and bottom).
/// </summary>
public sealed class PitchGenerator : Component
{
	#region Properties - Pitch Dimensions

	private float pitchWidth = 100f;
	/// <summary>Width of the pitch along the X axis.</summary>
	[Property, Group( "Pitch" )]
	public float PitchWidth
	{
		get => pitchWidth;
		set { pitchWidth = value; OnPropertyChanged(); }
	}

	private float pitchLength = 150f;
	/// <summary>Length of the pitch along the Y axis (where goals are located).</summary>
	[Property, Group( "Pitch" )]
	public float PitchLength
	{
		get => pitchLength;
		set { pitchLength = value; OnPropertyChanged(); }
	}

	private float bevelAmount = 20f;
	/// <summary>Radius of the beveled corners.</summary>
	[Property, Group( "Pitch" )]
	public float BevelAmount
	{
		get => bevelAmount;
		set { bevelAmount = value; OnPropertyChanged(); }
	}

	private int bevelResolution = 8;
	/// <summary>Number of segments used to render each beveled corner.</summary>
	[Property, Group( "Pitch" )]
	public int BevelResolution
	{
		get => bevelResolution;
		set { bevelResolution = value; OnPropertyChanged(); }
	}

	#endregion

	#region Properties - Wall Dimensions

	private float wallThickness = 10f;
	/// <summary>Thickness of all walls, goal posts, and crossbars.</summary>
	[Property, Group( "Walls" )]
	public float WallThickness
	{
		get => wallThickness;
		set { wallThickness = value; OnPropertyChanged(); }
	}

	private float wallHeight = 50f;
	/// <summary>Height of the perimeter walls.</summary>
	[Property, Group( "Walls" )]
	public float WallHeight
	{
		get => wallHeight;
		set { wallHeight = value; OnPropertyChanged(); }
	}

	#endregion

	#region Properties - Goal Dimensions

	private float goalWidth = 40f;
	/// <summary>Width of the goal opening.</summary>
	[Property, Group( "Goals" )]
	public float GoalWidth
	{
		get => goalWidth;
		set { goalWidth = value; OnPropertyChanged(); }
	}

	private float goalHeight = 30f;
	/// <summary>Height of the goal (crossbar height).</summary>
	[Property, Group( "Goals" )]
	public float GoalHeight
	{
		get => goalHeight;
		set { goalHeight = value; OnPropertyChanged(); }
	}

	private float goalDepth = 20f;
	/// <summary>Depth of the goal enclosure behind the goal line.</summary>
	[Property, Group( "Goals" )]
	public float GoalDepth
	{
		get => goalDepth;
		set { goalDepth = value; OnPropertyChanged(); }
	}

	#endregion

	#region Properties - Debug Visualization

	[Property, Group( "Debug" )]
	public bool DebugDraw { get; set; } = false;

	[Property, Group( "Debug" ), ShowIf( nameof( DebugDraw ), true )]
	public bool ShowWallsDebug { get; set; } = true;

	[Property, Group( "Debug" ), ShowIf( nameof( DebugDraw ), true )]
	public bool ShowGoalsDebug { get; set; } = true;

	[Property, Group( "Debug" ), ShowIf( nameof( DebugDraw ), true )]
	public bool ShowGoalEnclosureDebug { get; set; } = true;

	[Property, Group( "Debug" ), ShowIf( nameof( DebugDraw ), true )]
	public bool ShowCeilingDebug { get; set; } = false;

	[Property, Group( "Debug" ), ShowIf( nameof( DebugDraw ), true )]
	public bool ShowGoalVolumeDebug { get; set; } = false;

	#endregion

	#region Private Fields

	private const int CircleSegments = 16;

	private readonly List<Collider> wallColliders = new();
	private readonly List<GameObject> goalObjects = new();
	private GameObject collidersContainer;
	private bool isInitialized = false;

	/// <summary>The inner width of the goal volume, accounting for wall thickness on each side.</summary>
	private float GoalVolumeWidth => GoalWidth - WallThickness;

	#endregion

	#region Lifecycle

	protected override void OnStart()
	{
		isInitialized = true;
		GeneratePitchColliders();
	}

	protected override void OnEnabled()
	{
		isInitialized = true;
		GeneratePitchColliders();
	}

	protected override void OnUpdate()
	{
		if ( DebugDraw )
			DrawPitch();
	}

	protected override void DrawGizmos()
	{
		if ( DebugDraw )
			DrawPitch();
	}

	private void OnPropertyChanged()
	{
		if ( !isInitialized || !GameObject.IsValid )
			return;

		GeneratePitchColliders();
	}

	#endregion

	#region Collider Generation

	private void GeneratePitchColliders()
	{
		// Clear existing colliders
		if ( collidersContainer != null && collidersContainer.IsValid )
			collidersContainer.Destroy();

		wallColliders.Clear();
		goalObjects.Clear();

		collidersContainer = new GameObject
		{
			Name = "Colliders",
			Parent = GameObject,
			WorldPosition = WorldPosition
		};

		var (vertices, goalLineIndices) = GenerateVertices();

		// Create wall colliders for each edge segment (skip goal openings)
		for ( int i = 0; i < vertices.Count; i++ )
		{
			var nextIndex = (i + 1) % vertices.Count;
			if ( !IsGoalLine( i, nextIndex, goalLineIndices ) )
				CreateWallCollider( vertices[i], vertices[nextIndex] );
		}

		// Create goal structures at each goal opening
		for ( int goalIndex = 0; goalIndex < goalLineIndices.Count; goalIndex++ )
		{
			var (leftIdx, rightIdx) = goalLineIndices[goalIndex];
			var leftPost = vertices[leftIdx];
			var rightPost = vertices[rightIdx];

			CreateGoalPost( leftPost );
			CreateGoalPost( rightPost );
			CreateCrossbar( leftPost, rightPost );
			CreateAboveCrossbarWall( leftPost, rightPost );
			CreateGoalVolume( leftPost, rightPost, goalIndex );
			CreateGoalEnclosure( leftPost, rightPost, goalIndex );
		}

		CreateCeiling();
	}

	private void CreateWallCollider( Vector3 start, Vector3 end )
	{
		var center = (start + end) / 2f;
		var direction = (end - start);
		var length = direction.Length;
		direction = direction.Normal;

		// Normal points inward toward pitch center
		var normal = new Vector3( direction.y, -direction.x, 0 );
		var angle = MathF.Atan2( direction.y, direction.x ) * (180f / MathF.PI);

		var wallObj = new GameObject
		{
			Name = "PitchWall",
			Parent = collidersContainer,
			WorldPosition = center + normal * (WallThickness / 2f) + Vector3.Up * (WallHeight / 2f),
			WorldRotation = Rotation.FromYaw( angle )
		};

		var collider = wallObj.Components.Create<BoxCollider>();
		collider.Scale = new Vector3( length, WallThickness, WallHeight );
		wallColliders.Add( collider );
	}

	private void CreateCeiling()
	{
		var ceilingObj = new GameObject
		{
			Name = "Ceiling",
			Parent = collidersContainer,
			WorldPosition = WorldPosition + Vector3.Up * (WallHeight + WallThickness / 2f)
		};

		var collider = ceilingObj.Components.Create<BoxCollider>();
		collider.Scale = new Vector3( PitchWidth, PitchLength, WallThickness );
		goalObjects.Add( ceilingObj );
	}

	private void CreateGoalPost( Vector3 position )
	{
		var yDirection = GetGoalDirection( position );
		var insetPosition = position + new Vector3( 0, yDirection * (WallThickness / 2f), 0 );

		var postObj = new GameObject
		{
			Name = "GoalPost",
			Parent = collidersContainer,
			WorldPosition = insetPosition
		};

		var collider = postObj.Components.Create<CapsuleCollider>();
		collider.Radius = WallThickness / 2f;
		collider.Start = Vector3.Zero;
		collider.End = new Vector3( 0, 0, GoalHeight + WallThickness * 0.5f );
		goalObjects.Add( postObj );
	}

	private void CreateCrossbar( Vector3 leftPost, Vector3 rightPost )
	{
		var center = (leftPost + rightPost) / 2f;
		var yDirection = GetGoalDirection( leftPost );
		var offsetCenter = center + new Vector3( 0, yDirection * (WallThickness / 2f), 0 );

		var crossbarObj = new GameObject
		{
			Name = "Crossbar",
			Parent = collidersContainer,
			WorldPosition = offsetCenter.WithZ( GoalHeight + WallThickness / 2f )
		};

		var collider = crossbarObj.Components.Create<CapsuleCollider>();
		collider.Radius = WallThickness / 2f;
		collider.Start = new Vector3( -GoalWidth / 2f, 0, 0 );
		collider.End = new Vector3( GoalWidth / 2f, 0, 0 );
		goalObjects.Add( crossbarObj );
	}

	private void CreateAboveCrossbarWall( Vector3 leftPost, Vector3 rightPost )
	{
		var crossbarCenterZ = GoalHeight + WallThickness * 0.5f;
		var wallAboveHeight = WallHeight - crossbarCenterZ;

		if ( wallAboveHeight <= 0 )
			return;

		var center = (leftPost + rightPost) / 2f;
		var yDirection = GetGoalDirection( leftPost );
		var offsetCenter = center + new Vector3( 0, yDirection * (WallThickness / 2f), 0 );

		var wallObj = new GameObject
		{
			Name = "AboveCrossbarWall",
			Parent = collidersContainer,
			WorldPosition = offsetCenter.WithZ( crossbarCenterZ + wallAboveHeight / 2f )
		};

		var collider = wallObj.Components.Create<BoxCollider>();
		collider.Scale = new Vector3( GoalWidth, WallThickness, wallAboveHeight );
		goalObjects.Add( wallObj );
	}

	private void CreateGoalVolume( Vector3 leftPost, Vector3 rightPost, int goalIndex )
	{
		var center = (leftPost + rightPost) / 2f;
		var yDirection = GetGoalDirection( leftPost );
		var volumeCenter = center + new Vector3( 0, yDirection * (GoalDepth / 2f + WallThickness / 2f), GoalHeight / 2f );

		var goalVolumeObj = new GameObject
		{
			Name = goalIndex == 0 ? "GoalVolume_Top" : "GoalVolume_Bottom",
			Parent = collidersContainer,
			WorldPosition = volumeCenter
		};

		var collider = goalVolumeObj.Components.Create<BoxCollider>();
		collider.Scale = new Vector3( GoalVolumeWidth, GoalDepth, GoalHeight );
		collider.IsTrigger = true;
		goalObjects.Add( goalVolumeObj );
	}

	private void CreateGoalEnclosure( Vector3 leftPost, Vector3 rightPost, int goalIndex )
	{
		var center = (leftPost + rightPost) / 2f;
		var yDirection = GetGoalDirection( leftPost );
		var sideName = goalIndex == 0 ? "Top" : "Bottom";
		var goalStartY = center.y + yDirection * (WallThickness / 2f);
		var sideWallOffset = GoalVolumeWidth / 2f + WallThickness / 2f;

		// Back wall
		CreateGoalEnclosureWall(
			$"GoalBack_{sideName}",
			new Vector3( center.x, goalStartY + yDirection * (GoalDepth + WallThickness / 2f), GoalHeight / 2f ),
			new Vector3( GoalVolumeWidth, WallThickness, GoalHeight )
		);

		// Ceiling
		CreateGoalEnclosureWall(
			$"GoalCeiling_{sideName}",
			new Vector3( center.x, goalStartY + yDirection * (GoalDepth / 2f), GoalHeight + WallThickness / 2f ),
			new Vector3( GoalVolumeWidth, GoalDepth, WallThickness )
		);

		// Side walls
		var sideWallY = goalStartY + yDirection * (GoalDepth / 2f);
		var sideScale = new Vector3( WallThickness, GoalDepth, GoalHeight );

		CreateGoalEnclosureWall(
			$"GoalSideRight_{sideName}",
			new Vector3( center.x + sideWallOffset, sideWallY, GoalHeight / 2f ),
			sideScale
		);

		CreateGoalEnclosureWall(
			$"GoalSideLeft_{sideName}",
			new Vector3( center.x - sideWallOffset, sideWallY, GoalHeight / 2f ),
			sideScale
		);
	}

	private void CreateGoalEnclosureWall( string name, Vector3 position, Vector3 scale )
	{
		var wallObj = new GameObject
		{
			Name = name,
			Parent = collidersContainer,
			WorldPosition = position
		};

		var collider = wallObj.Components.Create<BoxCollider>();
		collider.Scale = scale;
		goalObjects.Add( wallObj );
	}

	#endregion

	#region Vertex Generation

	private (List<Vector3>, List<(int, int)>) GenerateVertices()
	{
		var origin = WorldPosition;
		var halfWidth = PitchWidth / 2f;
		var halfLength = PitchLength / 2f;
		var halfGoalWidth = GoalWidth / 2f;
		var bevel = MathF.Min( BevelAmount, MathF.Min( halfWidth, halfLength ) );

		var vertices = new List<Vector3>();
		var goalLineIndices = new List<(int, int)>();

		// Top edge with goal opening (Y = +halfLength)
		int topGoalRight = vertices.Count;
		vertices.Add( origin + new Vector3( halfGoalWidth, halfLength, 0 ) );
		int topGoalLeft = vertices.Count;
		vertices.Add( origin + new Vector3( -halfGoalWidth, halfLength, 0 ) );
		goalLineIndices.Add( (topGoalRight, topGoalLeft) );

		// Top-left corner
		vertices.Add( origin + new Vector3( -halfWidth + bevel, halfLength, 0 ) );
		AddBevelCorner( vertices, origin + new Vector3( -halfWidth + bevel, halfLength - bevel, 0 ), bevel, MathF.PI / 2f );

		// Left edge
		vertices.Add( origin + new Vector3( -halfWidth, -halfLength + bevel, 0 ) );

		// Bottom-left corner
		AddBevelCorner( vertices, origin + new Vector3( -halfWidth + bevel, -halfLength + bevel, 0 ), bevel, MathF.PI );

		// Bottom edge with goal opening (Y = -halfLength)
		// Note: bottom-left bevel ends at (-halfWidth + bevel, -halfLength, 0)
		int bottomGoalLeft = vertices.Count;
		vertices.Add( origin + new Vector3( -halfGoalWidth, -halfLength, 0 ) );
		int bottomGoalRight = vertices.Count;
		vertices.Add( origin + new Vector3( halfGoalWidth, -halfLength, 0 ) );
		goalLineIndices.Add( (bottomGoalLeft, bottomGoalRight) );
		vertices.Add( origin + new Vector3( halfWidth - bevel, -halfLength, 0 ) );

		// Bottom-right corner
		AddBevelCorner( vertices, origin + new Vector3( halfWidth - bevel, -halfLength + bevel, 0 ), bevel, -MathF.PI / 2f );

		// Right edge
		vertices.Add( origin + new Vector3( halfWidth, halfLength - bevel, 0 ) );

		// Top-right corner (connects back to first vertex)
		AddBevelCorner( vertices, origin + new Vector3( halfWidth - bevel, halfLength - bevel, 0 ), bevel, 0f );

		return (vertices, goalLineIndices);
	}

	private void AddBevelCorner( List<Vector3> vertices, Vector3 center, float radius, float startAngle )
	{
		for ( int i = 1; i <= BevelResolution; i++ )
		{
			float angle = startAngle + (MathF.PI / 2f) * (i / (float)BevelResolution);
			vertices.Add( center + new Vector3( MathF.Cos( angle ), MathF.Sin( angle ), 0 ) * radius );
		}
	}

	#endregion

	#region Debug Drawing

	private void DrawPitch()
	{
		var (vertices, goalLineIndices) = GenerateVertices();

		DrawPitchOutline( vertices, goalLineIndices );

		if ( ShowWallsDebug )
			DrawWallsDebug( vertices, goalLineIndices );

		if ( ShowGoalsDebug )
			DrawGoalsDebug( vertices, goalLineIndices );

		if ( ShowGoalEnclosureDebug )
			DrawGoalEnclosureDebug( vertices, goalLineIndices );

		if ( ShowCeilingDebug )
			DrawCeilingDebug();

		if ( ShowGoalVolumeDebug )
			DrawGoalVolumeDebug( vertices, goalLineIndices );
	}

	private void DrawPitchOutline( List<Vector3> vertices, List<(int, int)> goalLineIndices )
	{
		Gizmo.Draw.Color = Color.White;

		// Draw pitch perimeter (skip goal openings)
		for ( int i = 0; i < vertices.Count; i++ )
		{
			var nextIndex = (i + 1) % vertices.Count;
			if ( !IsGoalLine( i, nextIndex, goalLineIndices ) )
				Gizmo.Draw.Line( vertices[i], vertices[nextIndex] );
		}

		// Draw goal areas
		var postRadius = WallThickness / 2f;

		foreach ( var (leftIdx, rightIdx) in goalLineIndices )
		{
			var leftPost = vertices[leftIdx];
			var rightPost = vertices[rightIdx];
			var yDirection = GetGoalDirection( leftPost );
			var center = (leftPost + rightPost) / 2f;

			var leftPostCenter = leftPost + new Vector3( 0, yDirection * (WallThickness / 2f), 0 );
			var rightPostCenter = rightPost + new Vector3( 0, yDirection * (WallThickness / 2f), 0 );

			// Goal post circles
			DrawCircle( leftPostCenter, postRadius );
			DrawCircle( rightPostCenter, postRadius );

			// Goal line (between post centers, inset by wall thickness)
			var postDirection = (rightPostCenter - leftPostCenter).Normal;
			var goalLineOffset = WallThickness / 2f;
			Gizmo.Draw.Line(
				leftPostCenter + postDirection * goalLineOffset,
				rightPostCenter - postDirection * goalLineOffset
			);

			// Goal volume outline
			var volumeStartY = center.y + yDirection * (WallThickness / 2f);
			var volumeEndY = center.y + yDirection * (GoalDepth + WallThickness / 2f);
			var halfVolumeWidth = GoalVolumeWidth / 2f;

			var backLeft = new Vector3( center.x - halfVolumeWidth, volumeEndY, 0 );
			var backRight = new Vector3( center.x + halfVolumeWidth, volumeEndY, 0 );
			var frontLeft = new Vector3( center.x - halfVolumeWidth, volumeStartY, 0 );
			var frontRight = new Vector3( center.x + halfVolumeWidth, volumeStartY, 0 );

			Gizmo.Draw.Line( backLeft, backRight );
			Gizmo.Draw.Line( frontLeft, backLeft );
			Gizmo.Draw.Line( frontRight, backRight );
		}
	}

	private void DrawWallsDebug( List<Vector3> vertices, List<(int, int)> goalLineIndices )
	{
		Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.3f );

		for ( int i = 0; i < vertices.Count; i++ )
		{
			var nextIndex = (i + 1) % vertices.Count;
			if ( IsGoalLine( i, nextIndex, goalLineIndices ) )
				continue;

			var start = vertices[i];
			var end = vertices[nextIndex];
			var center = (start + end) / 2f;
			var direction = (end - start);
			var length = direction.Length;
			direction = direction.Normal;

			var normal = new Vector3( direction.y, -direction.x, 0 );
			var wallPosition = center + normal * (WallThickness / 2f) + Vector3.Up * (WallHeight / 2f);
			var angle = MathF.Atan2( direction.y, direction.x ) * (180f / MathF.PI);

			DrawDebugBox( wallPosition, Rotation.FromYaw( angle ), new Vector3( length, WallThickness, WallHeight ) );
		}

		// Above-crossbar walls
		foreach ( var (leftIdx, rightIdx) in goalLineIndices )
			DrawAboveCrossbarWallDebug( vertices[leftIdx], vertices[rightIdx] );
	}

	private void DrawAboveCrossbarWallDebug( Vector3 leftPost, Vector3 rightPost )
	{
		var crossbarCenterZ = GoalHeight + WallThickness * 0.5f;
		var wallAboveHeight = WallHeight - crossbarCenterZ;

		if ( wallAboveHeight <= 0 )
			return;

		var center = (leftPost + rightPost) / 2f;
		var yDirection = GetGoalDirection( leftPost );
		var offsetCenter = center + new Vector3( 0, yDirection * (WallThickness / 2f), 0 );
		var wallCenter = offsetCenter.WithZ( crossbarCenterZ + wallAboveHeight / 2f );

		Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.3f );
		DrawDebugBox( wallCenter, Rotation.Identity, new Vector3( GoalWidth, WallThickness, wallAboveHeight ) );
	}

	private void DrawGoalsDebug( List<Vector3> vertices, List<(int, int)> goalLineIndices )
	{
		Gizmo.Draw.Color = Color.Yellow.WithAlpha( 0.5f );

		foreach ( var (leftIdx, rightIdx) in goalLineIndices )
		{
			DrawGoalPostDebug( vertices[leftIdx] );
			DrawGoalPostDebug( vertices[rightIdx] );
			DrawCrossbarDebug( vertices[leftIdx], vertices[rightIdx] );
		}
	}

	private void DrawGoalPostDebug( Vector3 position )
	{
		var yDirection = GetGoalDirection( position );
		var insetPosition = position + new Vector3( 0, yDirection * (WallThickness / 2f), 0 );
		var radius = WallThickness / 2f;
		var height = GoalHeight + WallThickness * 0.5f;

		DrawCylinder( insetPosition, radius, height );
	}

	private void DrawCrossbarDebug( Vector3 leftPost, Vector3 rightPost )
	{
		var center = (leftPost + rightPost) / 2f;
		var yDirection = GetGoalDirection( leftPost );
		var offsetCenter = center + new Vector3( 0, yDirection * (WallThickness / 2f), 0 );
		var crossbarCenter = offsetCenter.WithZ( GoalHeight + WallThickness / 2f );

		var radius = WallThickness / 2f;
		var halfLength = GoalWidth / 2f;

		// Draw horizontal cylinder along X axis
		for ( int i = 0; i < CircleSegments; i++ )
		{
			float angle1 = (i / (float)CircleSegments) * MathF.PI * 2f;
			float angle2 = ((i + 1) / (float)CircleSegments) * MathF.PI * 2f;

			var offset1 = new Vector3( 0, MathF.Cos( angle1 ) * radius, MathF.Sin( angle1 ) * radius );
			var offset2 = new Vector3( 0, MathF.Cos( angle2 ) * radius, MathF.Sin( angle2 ) * radius );

			var p1Start = crossbarCenter + new Vector3( -halfLength, 0, 0 ) + offset1;
			var p2Start = crossbarCenter + new Vector3( -halfLength, 0, 0 ) + offset2;
			var p1End = crossbarCenter + new Vector3( halfLength, 0, 0 ) + offset1;
			var p2End = crossbarCenter + new Vector3( halfLength, 0, 0 ) + offset2;

			Gizmo.Draw.Line( p1Start, p2Start );
			Gizmo.Draw.Line( p1End, p2End );
			Gizmo.Draw.Line( p1Start, p1End );
		}
	}

	private void DrawGoalEnclosureDebug( List<Vector3> vertices, List<(int, int)> goalLineIndices )
	{
		Gizmo.Draw.Color = Color.Orange.WithAlpha( 0.3f );

		foreach ( var (leftIdx, rightIdx) in goalLineIndices )
		{
			var leftPost = vertices[leftIdx];
			var rightPost = vertices[rightIdx];
			var center = (leftPost + rightPost) / 2f;
			var yDirection = GetGoalDirection( leftPost );
			var goalStartY = center.y + yDirection * (WallThickness / 2f);
			var sideWallOffset = GoalVolumeWidth / 2f + WallThickness / 2f;

			// Back wall
			DrawDebugBox(
				new Vector3( center.x, goalStartY + yDirection * (GoalDepth + WallThickness / 2f), GoalHeight / 2f ),
				Rotation.Identity,
				new Vector3( GoalVolumeWidth, WallThickness, GoalHeight )
			);

			// Ceiling
			DrawDebugBox(
				new Vector3( center.x, goalStartY + yDirection * (GoalDepth / 2f), GoalHeight + WallThickness / 2f ),
				Rotation.Identity,
				new Vector3( GoalVolumeWidth, GoalDepth, WallThickness )
			);

			// Side walls
			var sideWallY = goalStartY + yDirection * (GoalDepth / 2f);
			var sideScale = new Vector3( WallThickness, GoalDepth, GoalHeight );

			DrawDebugBox( new Vector3( center.x + sideWallOffset, sideWallY, GoalHeight / 2f ), Rotation.Identity, sideScale );
			DrawDebugBox( new Vector3( center.x - sideWallOffset, sideWallY, GoalHeight / 2f ), Rotation.Identity, sideScale );
		}
	}

	private void DrawGoalVolumeDebug( List<Vector3> vertices, List<(int, int)> goalLineIndices )
	{
		Gizmo.Draw.Color = Color.Green.WithAlpha( 0.3f );

		foreach ( var (leftIdx, rightIdx) in goalLineIndices )
		{
			var leftPost = vertices[leftIdx];
			var rightPost = vertices[rightIdx];
			var center = (leftPost + rightPost) / 2f;
			var yDirection = GetGoalDirection( leftPost );
			var volumeCenter = center + new Vector3( 0, yDirection * (GoalDepth / 2f + WallThickness / 2f), GoalHeight / 2f );

			var halfExtents = new Vector3( GoalVolumeWidth / 2f, GoalDepth / 2f, GoalHeight / 2f );
			Gizmo.Transform = new Transform( volumeCenter, Rotation.Identity );
			Gizmo.Draw.SolidBox( new BBox( -halfExtents, halfExtents ) );
			Gizmo.Transform = global::Transform.Zero;
		}
	}

	private void DrawCeilingDebug()
	{
		Gizmo.Draw.Color = Color.Magenta.WithAlpha( 0.3f );
		var ceilingPosition = WorldPosition + Vector3.Up * (WallHeight + WallThickness / 2f);
		DrawDebugBox( ceilingPosition, Rotation.Identity, new Vector3( PitchWidth, PitchLength, WallThickness ) );
	}

	#endregion

	#region Helper Methods

	/// <summary>
	/// Returns 1 if the position is on the top goal (positive Y), -1 if on the bottom goal.
	/// </summary>
	private static float GetGoalDirection( Vector3 position ) => position.y > 0 ? 1f : -1f;

	/// <summary>
	/// Checks if the edge between two vertex indices is a goal line opening.
	/// </summary>
	private static bool IsGoalLine( int currentIdx, int nextIdx, List<(int, int)> goalLineIndices )
	{
		foreach ( var (startIdx, endIdx) in goalLineIndices )
		{
			if ( (currentIdx == startIdx && nextIdx == endIdx) || (currentIdx == endIdx && nextIdx == startIdx) )
				return true;
		}
		return false;
	}

	/// <summary>
	/// Draws a circle on the XY plane at the given position.
	/// </summary>
	private void DrawCircle( Vector3 center, float radius )
	{
		for ( int i = 0; i < CircleSegments; i++ )
		{
			float angle1 = (i / (float)CircleSegments) * MathF.PI * 2f;
			float angle2 = ((i + 1) / (float)CircleSegments) * MathF.PI * 2f;

			var p1 = center + new Vector3( MathF.Cos( angle1 ) * radius, MathF.Sin( angle1 ) * radius, 0 );
			var p2 = center + new Vector3( MathF.Cos( angle2 ) * radius, MathF.Sin( angle2 ) * radius, 0 );
			Gizmo.Draw.Line( p1, p2 );
		}
	}

	/// <summary>
	/// Draws a vertical cylinder using line segments.
	/// </summary>
	private void DrawCylinder( Vector3 basePosition, float radius, float height )
	{
		for ( int i = 0; i < CircleSegments; i++ )
		{
			float angle1 = (i / (float)CircleSegments) * MathF.PI * 2f;
			float angle2 = ((i + 1) / (float)CircleSegments) * MathF.PI * 2f;

			var offset1 = new Vector3( MathF.Cos( angle1 ) * radius, MathF.Sin( angle1 ) * radius, 0 );
			var offset2 = new Vector3( MathF.Cos( angle2 ) * radius, MathF.Sin( angle2 ) * radius, 0 );

			var p1Top = basePosition + offset1 + Vector3.Up * height;
			var p2Top = basePosition + offset2 + Vector3.Up * height;
			var p1Bottom = basePosition + offset1;
			var p2Bottom = basePosition + offset2;

			Gizmo.Draw.Line( p1Top, p2Top );
			Gizmo.Draw.Line( p1Bottom, p2Bottom );
			Gizmo.Draw.Line( p1Top, p1Bottom );
		}
	}

	/// <summary>
	/// Draws a debug box outline with the given position, rotation, and scale.
	/// </summary>
	private void DrawDebugBox( Vector3 position, Rotation rotation, Vector3 scale )
	{
		var halfExtents = scale / 2f;
		Gizmo.Transform = new Transform( position, rotation );
		Gizmo.Draw.LineBBox( new BBox( -halfExtents, halfExtents ) );
		Gizmo.Transform = global::Transform.Zero;
	}

	#endregion
}

using Sandbox;
using System;
using System.Collections.Generic;

public sealed class PitchGenerator : Component
{
	private float pitchWidth = 100f;
	[Property]
	public float PitchWidth
	{
		get => pitchWidth;
		set { pitchWidth = value; OnPropertyChanged(); }
	}

	private float pitchLength = 150f;
	[Property]
	public float PitchLength
	{
		get => pitchLength;
		set { pitchLength = value; OnPropertyChanged(); }
	}

	private float bevelAmount = 20f;
	[Property]
	public float BevelAmount
	{
		get => bevelAmount;
		set { bevelAmount = value; OnPropertyChanged(); }
	}

	private int bevelResolution = 8;
	[Property]
	public int BevelResolution
	{
		get => bevelResolution;
		set { bevelResolution = value; OnPropertyChanged(); }
	}

	private float goalWidth = 40f;
	[Property]
	public float GoalWidth
	{
		get => goalWidth;
		set { goalWidth = value; OnPropertyChanged(); }
	}

	private float wallThickness = 10f;
	[Property]
	public float WallThickness
	{
		get => wallThickness;
		set { wallThickness = value; OnPropertyChanged(); }
	}

	private float wallHeight = 50f;
	[Property]
	public float WallHeight
	{
		get => wallHeight;
		set { wallHeight = value; OnPropertyChanged(); }
	}

	[Property] public bool DebugDraw { get; set; } = false;
	[Property, ShowIf( nameof( DebugDraw ), true )] public bool ShowWallsDebug { get; set; } = true;
	[Property, ShowIf( nameof( DebugDraw ), true )] public bool ShowGoalsDebug { get; set; } = true;
	[Property, ShowIf( nameof( DebugDraw ), true )] public bool ShowGoalEnclosureDebug { get; set; } = true;
	[Property, ShowIf( nameof( DebugDraw ), true )] public bool ShowCeilingDebug { get; set; } = false;
	[Property, ShowIf( nameof( DebugDraw ), true )] public bool ShowGoalVolumeDebug { get; set; } = false;

	private float goalHeight = 30f;
	[Property]
	public float GoalHeight
	{
		get => goalHeight;
		set { goalHeight = value; OnPropertyChanged(); }
	}

	private float goalDepth = 20f;
	[Property]
	public float GoalDepth
	{
		get => goalDepth;
		set { goalDepth = value; OnPropertyChanged(); }
	}

	private readonly List<Collider> wallColliders = new();
	private readonly List<GameObject> goalPosts = new();
	private GameObject collidersContainer;
	private bool isInitialized = false;

	private void OnPropertyChanged()
	{
		// Only regenerate if we're initialized and in a valid state
		if (!isInitialized || !GameObject.IsValid)
			return;

		GeneratePitchColliders();
	}

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

	[Button( "Clear Colliders" )]
	public void ClearColliders()
	{
		// Clear existing colliders
		foreach (var collider in wallColliders)
		{
			collider?.GameObject?.Destroy();
		}
		wallColliders.Clear();

		// Clear existing goal posts
		foreach (var post in goalPosts)
		{
			post?.Destroy();
		}
		goalPosts.Clear();

		// Destroy container if it exists
		if (collidersContainer != null && collidersContainer.IsValid)
		{
			collidersContainer.Destroy();
			collidersContainer = null;
		}
	}

	protected override void OnUpdate()
	{
		if ( DebugDraw )
		{
			DrawPitch();
		}
	}

	protected override void DrawGizmos()
	{
		if ( DebugDraw )
		{
			DrawPitch();
		}
	}

	private void GeneratePitchColliders()
	{
		// Clear existing colliders first
		ClearColliders();

		// Create container object
		collidersContainer = new GameObject();
		collidersContainer.Name = "Colliders";
		collidersContainer.Parent = GameObject;
		collidersContainer.WorldPosition = WorldPosition;

		var (vertices, goalLineIndices) = GenerateVertices();

		// Create colliders for each wall segment
		for (int i = 0; i < vertices.Count; i++)
		{
			var nextIndex = (i + 1) % vertices.Count;

			// Check if this is a goal line - if so, skip creating collider
			bool isGoalLine = false;
			foreach (var (startIdx, endIdx) in goalLineIndices)
			{
				if ((i == startIdx && nextIndex == endIdx) || (i == endIdx && nextIndex == startIdx))
				{
					isGoalLine = true;
					break;
				}
			}

			if (!isGoalLine)
			{
				CreateWallCollider(vertices[i], vertices[nextIndex]);
			}
		}

		// Create goal posts and crossbar at each goal opening
		int goalIndex = 0;
		foreach (var (topIdx, bottomIdx) in goalLineIndices)
		{
			CreateGoalPost(vertices[topIdx]);
			CreateGoalPost(vertices[bottomIdx]);
			CreateCrossbar(vertices[topIdx], vertices[bottomIdx]);
			CreateAboveCrossbarWall(vertices[topIdx], vertices[bottomIdx]);
			CreateGoalVolume(vertices[topIdx], vertices[bottomIdx], goalIndex);
			CreateGoalEnclosure(vertices[topIdx], vertices[bottomIdx], goalIndex);
			goalIndex++;
		}

		// Create ceiling
		CreateCeiling();
	}

	private void CreateCeiling()
	{
		var ceilingObj = new GameObject();
		ceilingObj.Name = "Ceiling";
		ceilingObj.Parent = collidersContainer;
		// Position at center of pitch, at wall height + half thickness so it sits on top of walls
		ceilingObj.WorldPosition = WorldPosition + Vector3.Up * (WallHeight + WallThickness / 2f);

		var collider = ceilingObj.Components.Create<BoxCollider>();
		collider.Scale = new Vector3( PitchLength, PitchWidth, WallThickness );

		goalPosts.Add( ceilingObj );
	}

	private void CreateGoalVolume(Vector3 topPost, Vector3 bottomPost, int goalIndex)
	{
		// Goal volume sits behind the goal line, used as a trigger to detect goals
		var center = (topPost + bottomPost) / 2f;

		// Direction to push the volume behind the goal (outward from pitch center)
		var xDirection = (topPost.x < 0) ? -1f : 1f;
		var volumeCenter = center + new Vector3(xDirection * (GoalDepth / 2f + WallThickness / 2f), 0, GoalHeight / 2f);

		var goalVolumeObj = new GameObject();
		goalVolumeObj.Name = goalIndex == 0 ? "GoalVolume_Left" : "GoalVolume_Right";
		goalVolumeObj.Parent = collidersContainer;
		goalVolumeObj.WorldPosition = volumeCenter;

		var collider = goalVolumeObj.Components.Create<BoxCollider>();
		collider.Scale = new Vector3(GoalDepth, GoalWidth, GoalHeight);
		collider.IsTrigger = true;

		goalPosts.Add(goalVolumeObj);
	}

	private void CreateGoalEnclosure(Vector3 topPost, Vector3 bottomPost, int goalIndex)
	{
		// Create the 4 walls that enclose the goal: back, top, and two sides
		var center = (topPost + bottomPost) / 2f;
		var xDirection = (topPost.x < 0) ? -1f : 1f;
		var sideName = goalIndex == 0 ? "Left" : "Right";

		// Back wall - at the far end of the goal depth
		var backWallX = center.x + xDirection * (GoalDepth + WallThickness);
		var backWallObj = new GameObject();
		backWallObj.Name = $"GoalBack_{sideName}";
		backWallObj.Parent = collidersContainer;
		backWallObj.WorldPosition = new Vector3(backWallX, center.y, GoalHeight / 2f);

		var backCollider = backWallObj.Components.Create<BoxCollider>();
		backCollider.Scale = new Vector3(WallThickness, GoalWidth, GoalHeight);
		goalPosts.Add(backWallObj);

		// Top wall - ceiling of the goal
		var topWallX = center.x + xDirection * (GoalDepth / 2f + WallThickness / 2f);
		var topWallObj = new GameObject();
		topWallObj.Name = $"GoalTop_{sideName}";
		topWallObj.Parent = collidersContainer;
		topWallObj.WorldPosition = new Vector3(topWallX, center.y, GoalHeight + WallThickness / 2f);

		var topCollider = topWallObj.Components.Create<BoxCollider>();
		topCollider.Scale = new Vector3(GoalDepth + WallThickness, GoalWidth, WallThickness);
		goalPosts.Add(topWallObj);

		// Side walls - left and right sides of the goal
		var sideWallX = center.x + xDirection * (GoalDepth / 2f + WallThickness / 2f);

		// Top side (positive Y)
		var topSideObj = new GameObject();
		topSideObj.Name = $"GoalSideTop_{sideName}";
		topSideObj.Parent = collidersContainer;
		topSideObj.WorldPosition = new Vector3(sideWallX, center.y + GoalWidth / 2f + WallThickness / 2f, GoalHeight / 2f);

		var topSideCollider = topSideObj.Components.Create<BoxCollider>();
		topSideCollider.Scale = new Vector3(GoalDepth + WallThickness, WallThickness, GoalHeight);
		goalPosts.Add(topSideObj);

		// Bottom side (negative Y)
		var bottomSideObj = new GameObject();
		bottomSideObj.Name = $"GoalSideBottom_{sideName}";
		bottomSideObj.Parent = collidersContainer;
		bottomSideObj.WorldPosition = new Vector3(sideWallX, center.y - GoalWidth / 2f - WallThickness / 2f, GoalHeight / 2f);

		var bottomSideCollider = bottomSideObj.Components.Create<BoxCollider>();
		bottomSideCollider.Scale = new Vector3(GoalDepth + WallThickness, WallThickness, GoalHeight);
		goalPosts.Add(bottomSideObj);
	}

	private void CreateGoalPost(Vector3 position)
	{
		// Position goal post flush against the inner face of the adjacent wall
		// Y offset is half wall thickness, direction flipped based on which side
		var yDirection = (position.x < 0) ? -1f : 1f;
		var insetPosition = position + Vector3.Forward * (WallThickness / 2f) * yDirection;

		// Create a GameObject for this goal post
		var postObj = new GameObject();
		postObj.Name = "GoalPost";
		postObj.Parent = collidersContainer;
		postObj.WorldPosition = insetPosition;

		// Add capsule collider - the ball bounces off this rounded post
		// Extra height to overlap with crossbar
		var collider = postObj.Components.Create<CapsuleCollider>();
		collider.Radius = WallThickness / 2f;
		collider.Start = new Vector3(0, 0, 0);
		collider.End = new Vector3(0, 0, GoalHeight + WallThickness * 0.5f);

		goalPosts.Add(postObj);
	}

	private void CreateCrossbar(Vector3 topPost, Vector3 bottomPost)
	{
		// Crossbar sits above the goal at GoalHeight + half wall thickness
		var center = (topPost + bottomPost) / 2f;
		var crossbarHeight = GoalHeight + (WallThickness / 2f);

		// Offset Y to align with goal posts (same logic as CreateGoalPost)
		var yDirection = (topPost.x < 0) ? -1f : 1f;
		var offsetCenter = center + Vector3.Forward * (WallThickness / 2f) * yDirection;

		// Create a GameObject for the crossbar
		var crossbarObj = new GameObject();
		crossbarObj.Name = "Crossbar";
		crossbarObj.Parent = collidersContainer;
		crossbarObj.WorldPosition = offsetCenter.WithZ(crossbarHeight);

		// Add capsule collider - horizontal cylinder across the goal
		var collider = crossbarObj.Components.Create<CapsuleCollider>();
		collider.Radius = WallThickness / 2f;
		// Crossbar runs along Y axis (between the two posts)
		collider.Start = new Vector3(0, -GoalWidth / 2f, 0);
		collider.End = new Vector3(0, GoalWidth / 2f, 0);

		goalPosts.Add(crossbarObj);
	}

	private void CreateAboveCrossbarWall(Vector3 topPost, Vector3 bottomPost)
	{
		// Wall above crossbar fills the gap from crossbar center to wall top
		// Regular walls now sit on the floor and extend from 0 to WallHeight
		var center = (topPost + bottomPost) / 2f;
		var crossbarCenter = GoalHeight + WallThickness * 0.5f; // Center of the crossbar
		var wallAboveHeight = WallHeight - crossbarCenter;

		// Only create if there's space above the crossbar
		if (wallAboveHeight <= 0)
			return;

		// Offset Y to align with goal posts (same logic as CreateGoalPost)
		var yDirection = (topPost.x < 0) ? -1f : 1f;
		var offsetCenter = center + Vector3.Forward * (WallThickness / 2f) * yDirection;

		// Create a GameObject for the wall above crossbar
		var wallObj = new GameObject();
		wallObj.Name = "AboveCrossbarWall";
		wallObj.Parent = collidersContainer;
		wallObj.WorldPosition = offsetCenter.WithZ(crossbarCenter + wallAboveHeight / 2f);

		// Add box collider
		var collider = wallObj.Components.Create<BoxCollider>();
		collider.Scale = new Vector3(WallThickness, GoalWidth, wallAboveHeight);

		goalPosts.Add(wallObj);
	}

	private void CreateWallCollider(Vector3 start, Vector3 end)
	{
		// Calculate wall properties
		var center = (start + end) / 2f;
		var direction = end - start;
		var length = direction.Length;
		direction = direction.Normal;

		// Create a GameObject for this wall segment
		var wallObj = new GameObject();
		wallObj.Name = "PitchWall";
		wallObj.Parent = collidersContainer;

		// Calculate normal (perpendicular to the wall, pointing inward to the pitch)
		var normal = new Vector3(direction.y, -direction.x, 0);

		// Position the wall - offset Z by half height so it sits on the floor
		wallObj.WorldPosition = center + normal * (WallThickness / 2f) + Vector3.Up * (WallHeight / 2f);

		// Calculate rotation to align the collider
		var angle = MathF.Atan2(direction.y, direction.x) * (180f / MathF.PI);
		wallObj.WorldRotation = Rotation.FromYaw(angle);

		// Add box collider
		var collider = wallObj.Components.Create<BoxCollider>();
		collider.Scale = new Vector3(length, WallThickness, WallHeight);

		wallColliders.Add(collider);
	}

	private (List<Vector3>, List<(int, int)>) GenerateVertices()
	{
		var origin = WorldPosition;

		// Calculate the dimensions
		// Length is along X axis (where the goals are), Width is along Y axis
		var halfLength = PitchLength / 2f;
		var halfWidth = PitchWidth / 2f;
		var bevel = MathF.Min(BevelAmount, MathF.Min(halfWidth, halfLength));

		// Calculate corner centers (where the rounded corners will be)
		// X axis = length (goals on left/right), Y axis = width (top/bottom edges)
		var topLeftCenter = origin + new Vector3(-halfLength + bevel, halfWidth - bevel, 0);
		var topRightCenter = origin + new Vector3(halfLength - bevel, halfWidth - bevel, 0);
		var bottomLeftCenter = origin + new Vector3(-halfLength + bevel, -halfWidth + bevel, 0);
		var bottomRightCenter = origin + new Vector3(halfLength - bevel, -halfWidth + bevel, 0);

		// Generate all vertices
		var vertices = new List<Vector3>();
		var goalLineIndices = new List<(int, int)>(); // Pairs of indices that form goal lines

		var halfGoalWidth = GoalWidth / 2f;

		// Top edge - no goal on this side
		vertices.Add(origin + new Vector3(halfLength - bevel, halfWidth, 0));
		vertices.Add(origin + new Vector3(-halfLength + bevel, halfWidth, 0));

		// Top-left corner (starting at top edge, going to left edge)
		for (int i = 1; i <= BevelResolution; i++)
		{
			float angle = MathF.PI / 2f + (MathF.PI / 2f) * (i / (float)BevelResolution);
			var cornerPoint = topLeftCenter + new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0) * bevel;
			vertices.Add(cornerPoint);
		}

		// Left edge - split into 3 parts for the goal
		int leftGoalTop = vertices.Count;
		vertices.Add(origin + new Vector3(-halfLength, halfGoalWidth, 0)); // Top goal post
		int leftGoalBottom = vertices.Count;
		vertices.Add(origin + new Vector3(-halfLength, -halfGoalWidth, 0)); // Bottom goal post
		goalLineIndices.Add((leftGoalTop, leftGoalBottom)); // Mark this as a goal line

		// Bottom-left corner (starting at left edge, going to bottom edge)
		for (int i = 1; i <= BevelResolution; i++)
		{
			float angle = MathF.PI + (MathF.PI / 2f) * (i / (float)BevelResolution);
			var cornerPoint = bottomLeftCenter + new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0) * bevel;
			vertices.Add(cornerPoint);
		}

		// Bottom edge - no goal on this side
		vertices.Add(origin + new Vector3(halfLength - bevel, -halfWidth, 0));

		// Bottom-right corner (starting at bottom edge, going to right edge)
		for (int i = 1; i <= BevelResolution; i++)
		{
			float angle = -MathF.PI / 2f + (MathF.PI / 2f) * (i / (float)BevelResolution);
			var cornerPoint = bottomRightCenter + new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0) * bevel;
			vertices.Add(cornerPoint);
		}

		// Right edge - split into 3 parts for the goal
		int rightGoalBottom = vertices.Count;
		vertices.Add(origin + new Vector3(halfLength, -halfGoalWidth, 0)); // Bottom goal post
		int rightGoalTop = vertices.Count;
		vertices.Add(origin + new Vector3(halfLength, halfGoalWidth, 0)); // Top goal post
		goalLineIndices.Add((rightGoalBottom, rightGoalTop)); // Mark this as a goal line

		// Top-right corner (starting at right edge, going to top edge)
		for (int i = 1; i < BevelResolution; i++)
		{
			float angle = (MathF.PI / 2f) * (i / (float)BevelResolution);
			var cornerPoint = topRightCenter + new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0) * bevel;
			vertices.Add(cornerPoint);
		}
		// Note: Last vertex of top-right corner connects back to first vertex (top edge start)

		return (vertices, goalLineIndices);
	}

	private void DrawPitch()
	{
		var (vertices, goalLineIndices) = GenerateVertices();

		// Draw lines between vertices (pitch walls)
		Gizmo.Draw.Color = Color.White;
		for (int i = 0; i < vertices.Count; i++)
		{
			var nextIndex = (i + 1) % vertices.Count;

			// Check if this is a goal line - if so, skip drawing it
			bool isGoalLine = false;
			foreach (var (startIdx, endIdx) in goalLineIndices)
			{
				if ((i == startIdx && nextIndex == endIdx) || (i == endIdx && nextIndex == startIdx))
				{
					isGoalLine = true;
					break;
				}
			}

			if (!isGoalLine)
			{
				Gizmo.Draw.Line(vertices[i], vertices[nextIndex]);
			}
		}

		// Draw debug rects at each vertex
		Gizmo.Draw.Color = Color.Red;
		for (int i = 0; i < vertices.Count; i++)
		{
			var vertex = vertices[i];
			// Check if this is a goal post vertex
			bool isGoalPost = false;
			foreach (var (startIdx, endIdx) in goalLineIndices)
			{
				if (startIdx == i || endIdx == i)
				{
					isGoalPost = true;
					break;
				}
			}
			float size = isGoalPost ? 4f : 2f;
			Gizmo.Draw.LineBBox(new BBox(vertex - Vector3.One * size, vertex + Vector3.One * size));
		}

		// Draw debug boxes for wall colliders
		if ( ShowWallsDebug )
		{
			DrawColliderDebugBoxes( vertices, goalLineIndices );
		}

		// Draw debug cylinders for goal posts
		if ( ShowGoalsDebug )
		{
			DrawGoalPostDebug( vertices, goalLineIndices );
		}

		// Draw goal enclosure debug if enabled
		if ( ShowGoalEnclosureDebug )
		{
			DrawGoalEnclosureDebug( vertices, goalLineIndices );
		}

		// Draw ceiling debug if enabled
		if ( ShowCeilingDebug )
		{
			DrawCeilingDebug();
		}

		// Draw goal volume debug if enabled
		if ( ShowGoalVolumeDebug )
		{
			DrawGoalVolumeDebug( vertices, goalLineIndices );
		}
	}

	private void DrawGoalEnclosureDebug( List<Vector3> vertices, List<(int, int)> goalLineIndices )
	{
		Gizmo.Draw.Color = Color.Orange.WithAlpha( 0.3f );

		foreach ( var (topIdx, bottomIdx) in goalLineIndices )
		{
			var topPost = vertices[topIdx];
			var bottomPost = vertices[bottomIdx];
			var center = ( topPost + bottomPost ) / 2f;
			var xDirection = ( topPost.x < 0 ) ? -1f : 1f;

			// Back wall
			var backWallX = center.x + xDirection * ( GoalDepth + WallThickness );
			var backHalfExtents = new Vector3( WallThickness / 2f, GoalWidth / 2f, GoalHeight / 2f );
			Gizmo.Transform = new Transform( new Vector3( backWallX, center.y, GoalHeight / 2f ), Rotation.Identity );
			Gizmo.Draw.LineBBox( new BBox( -backHalfExtents, backHalfExtents ) );

			// Top wall
			var topWallX = center.x + xDirection * ( GoalDepth / 2f + WallThickness / 2f );
			var topHalfExtents = new Vector3( ( GoalDepth + WallThickness ) / 2f, GoalWidth / 2f, WallThickness / 2f );
			Gizmo.Transform = new Transform( new Vector3( topWallX, center.y, GoalHeight + WallThickness / 2f ), Rotation.Identity );
			Gizmo.Draw.LineBBox( new BBox( -topHalfExtents, topHalfExtents ) );

			// Side walls
			var sideWallX = center.x + xDirection * ( GoalDepth / 2f + WallThickness / 2f );
			var sideHalfExtents = new Vector3( ( GoalDepth + WallThickness ) / 2f, WallThickness / 2f, GoalHeight / 2f );

			// Top side (positive Y)
			Gizmo.Transform = new Transform( new Vector3( sideWallX, center.y + GoalWidth / 2f + WallThickness / 2f, GoalHeight / 2f ), Rotation.Identity );
			Gizmo.Draw.LineBBox( new BBox( -sideHalfExtents, sideHalfExtents ) );

			// Bottom side (negative Y)
			Gizmo.Transform = new Transform( new Vector3( sideWallX, center.y - GoalWidth / 2f - WallThickness / 2f, GoalHeight / 2f ), Rotation.Identity );
			Gizmo.Draw.LineBBox( new BBox( -sideHalfExtents, sideHalfExtents ) );

			Gizmo.Transform = global::Transform.Zero;
		}
	}

	private void DrawGoalVolumeDebug( List<Vector3> vertices, List<(int, int)> goalLineIndices )
	{
		Gizmo.Draw.Color = Color.Green.WithAlpha( 0.3f );

		foreach ( var (topIdx, bottomIdx) in goalLineIndices )
		{
			var topPost = vertices[topIdx];
			var bottomPost = vertices[bottomIdx];
			var center = ( topPost + bottomPost ) / 2f;

			var xDirection = ( topPost.x < 0 ) ? -1f : 1f;
			var volumeCenter = center + new Vector3( xDirection * ( GoalDepth / 2f + WallThickness / 2f ), 0, GoalHeight / 2f );

			var halfExtents = new Vector3( GoalDepth / 2f, GoalWidth / 2f, GoalHeight / 2f );
			var bbox = new BBox( -halfExtents, halfExtents );

			Gizmo.Transform = new Transform( volumeCenter, Rotation.Identity );
			Gizmo.Draw.SolidBox( bbox );
			Gizmo.Transform = global::Transform.Zero;
		}
	}

	private void DrawCeilingDebug()
	{
		Gizmo.Draw.Color = Color.Magenta.WithAlpha( 0.3f );

		var ceilingPosition = WorldPosition + Vector3.Up * ( WallHeight + WallThickness / 2f );
		var halfExtents = new Vector3( PitchLength / 2f, PitchWidth / 2f, WallThickness / 2f );
		var bbox = new BBox( -halfExtents, halfExtents );

		Gizmo.Transform = new Transform( ceilingPosition, Rotation.Identity );
		Gizmo.Draw.LineBBox( bbox );
		Gizmo.Transform = global::Transform.Zero;
	}

	private void DrawColliderDebugBoxes(List<Vector3> vertices, List<(int, int)> goalLineIndices)
	{
		Gizmo.Draw.Color = Color.Cyan.WithAlpha(0.3f);

		for (int i = 0; i < vertices.Count; i++)
		{
			var nextIndex = (i + 1) % vertices.Count;

			// Check if this is a goal line - if so, skip drawing it
			bool isGoalLine = false;
			foreach (var (startIdx, endIdx) in goalLineIndices)
			{
				if ((i == startIdx && nextIndex == endIdx) || (i == endIdx && nextIndex == startIdx))
				{
					isGoalLine = true;
					break;
				}
			}

			if (!isGoalLine)
			{
				var start = vertices[i];
				var end = vertices[nextIndex];

				// Calculate wall properties (same as CreateWallCollider)
				var center = (start + end) / 2f;
				var direction = end - start;
				var length = direction.Length;
				direction = direction.Normal;

				// Calculate normal (perpendicular to the wall, pointing inward)
				var normal = new Vector3(direction.y, -direction.x, 0);

				// Position the wall - offset Z by half height so it sits on the floor
				var wallPosition = center + normal * (WallThickness / 2f) + Vector3.Up * (WallHeight / 2f);

				// Calculate rotation
				var angle = MathF.Atan2(direction.y, direction.x) * (180f / MathF.PI);
				var wallRotation = Rotation.FromYaw(angle);

				// Create bounding box for visualization
				var halfExtents = new Vector3(length / 2f, WallThickness / 2f, WallHeight / 2f);
				var bbox = new BBox(-halfExtents, halfExtents);

				// Draw the box with rotation
				Gizmo.Transform = new Transform(wallPosition, wallRotation);
				Gizmo.Draw.LineBBox(bbox);
				Gizmo.Transform = global::Transform.Zero;
			}
		}
	}

	private void DrawGoalPostDebug(List<Vector3> vertices, List<(int, int)> goalLineIndices)
	{
		Gizmo.Draw.Color = Color.Yellow.WithAlpha(0.5f);

		// Draw cylinders at each goal post position and crossbar
		foreach (var (topIdx, bottomIdx) in goalLineIndices)
		{
			DrawGoalPostCylinder(vertices[topIdx]);
			DrawGoalPostCylinder(vertices[bottomIdx]);
			DrawCrossbarCylinder(vertices[topIdx], vertices[bottomIdx]);
			DrawAboveCrossbarWall(vertices[topIdx], vertices[bottomIdx]);
		}
	}

	private void DrawGoalPostCylinder(Vector3 position)
	{
		// Match actual goal post position
		var yDirection = (position.x < 0) ? -1f : 1f;
		var insetPosition = position + Vector3.Forward * (WallThickness / 2f) * yDirection;

		// Draw a cylinder using line segments
		// Extra height to overlap with crossbar
		var radius = WallThickness / 2f;
		var height = GoalHeight + WallThickness * 0.5f;
		var segments = 16;

		// Draw top circle
		for (int i = 0; i < segments; i++)
		{
			float angle1 = (i / (float)segments) * MathF.PI * 2f;
			float angle2 = ((i + 1) / (float)segments) * MathF.PI * 2f;

			var p1Top = insetPosition + new Vector3(MathF.Cos(angle1) * radius, MathF.Sin(angle1) * radius, height);
			var p2Top = insetPosition + new Vector3(MathF.Cos(angle2) * radius, MathF.Sin(angle2) * radius, height);

			var p1Bottom = insetPosition + new Vector3(MathF.Cos(angle1) * radius, MathF.Sin(angle1) * radius, 0);
			var p2Bottom = insetPosition + new Vector3(MathF.Cos(angle2) * radius, MathF.Sin(angle2) * radius, 0);

			// Top circle
			Gizmo.Draw.Line(p1Top, p2Top);
			// Bottom circle
			Gizmo.Draw.Line(p1Bottom, p2Bottom);
			// Vertical line
			Gizmo.Draw.Line(p1Top, p1Bottom);
		}
	}

	private void DrawCrossbarCylinder(Vector3 topPost, Vector3 bottomPost)
	{
		var center = (topPost + bottomPost) / 2f;
		var crossbarHeight = GoalHeight + (WallThickness / 2f);

		// Offset Y to align with goal posts
		var yDirection = (topPost.x < 0) ? -1f : 1f;
		var offsetCenter = center + Vector3.Forward * (WallThickness / 2f) * yDirection;
		var crossbarCenter = offsetCenter.WithZ(crossbarHeight);

		var radius = WallThickness / 2f;
		var halfLength = GoalWidth / 2f;
		var segments = 16;

		// Draw circles at each end and connecting lines
		for (int i = 0; i < segments; i++)
		{
			float angle1 = (i / (float)segments) * MathF.PI * 2f;
			float angle2 = ((i + 1) / (float)segments) * MathF.PI * 2f;

			// Circle points in XZ plane (crossbar runs along Y)
			var p1Start = crossbarCenter + new Vector3(MathF.Cos(angle1) * radius, -halfLength, MathF.Sin(angle1) * radius);
			var p2Start = crossbarCenter + new Vector3(MathF.Cos(angle2) * radius, -halfLength, MathF.Sin(angle2) * radius);

			var p1End = crossbarCenter + new Vector3(MathF.Cos(angle1) * radius, halfLength, MathF.Sin(angle1) * radius);
			var p2End = crossbarCenter + new Vector3(MathF.Cos(angle2) * radius, halfLength, MathF.Sin(angle2) * radius);

			// Start circle
			Gizmo.Draw.Line(p1Start, p2Start);
			// End circle
			Gizmo.Draw.Line(p1End, p2End);
			// Connecting line
			Gizmo.Draw.Line(p1Start, p1End);
		}
	}

	private void DrawAboveCrossbarWall(Vector3 topPost, Vector3 bottomPost)
	{
		var center = (topPost + bottomPost) / 2f;
		var crossbarCenterZ = GoalHeight + WallThickness * 0.5f; // Center of the crossbar
		// Wall goes from crossbar center to wall top (regular walls now extend from 0 to WallHeight)
		var wallAboveHeight = WallHeight - crossbarCenterZ;

		// Only draw if there's space above the crossbar
		if (wallAboveHeight <= 0)
			return;

		// Offset Y to align with goal posts
		var yDirection = (topPost.x < 0) ? -1f : 1f;
		var offsetCenter = center + Vector3.Forward * (WallThickness / 2f) * yDirection;
		// Center the wall between crossbar center and wall top
		var wallCenterZ = crossbarCenterZ + wallAboveHeight / 2f;
		var wallCenter = offsetCenter.WithZ(wallCenterZ);

		// Create bounding box for visualization
		var halfExtents = new Vector3(WallThickness / 2f, GoalWidth / 2f, wallAboveHeight / 2f);
		var bbox = new BBox(-halfExtents, halfExtents);

		// Draw the box in cyan like other walls, then restore yellow
		Gizmo.Draw.Color = Color.Cyan.WithAlpha(0.3f);
		Gizmo.Transform = new Transform(wallCenter, Rotation.Identity);
		Gizmo.Draw.LineBBox(bbox);
		Gizmo.Transform = global::Transform.Zero;
		Gizmo.Draw.Color = Color.Yellow.WithAlpha(0.5f);
	}
}

using Sandbox;
using System;

public sealed class CameraController : Component
{
	[Property, Group( "Debug" )] public bool ShowDebug { get; set; } = false;
	[Property, Group( "Debug" )] public float DebugPointSize { get; set; } = 2f;

	[Property, Group( "Pan Settings" )] public float EdgeThreshold { get; set; } = 50f; // How close cursor must be to edge to trigger panning
	[Property, Group( "Pan Settings" )] public float PanSpeed { get; set; } = 1f; // Multiplier for pan speed
	[Property, Group( "Pan Settings" )] public bool SmoothPan { get; set; } = true; // Do we smooth the camera
	[Property, Group( "Pan Settings" )] public float Smoothing { get; set; } = 10f; // How quickly camera reaches target position

	[Property, Group( "Passive Pan" )] public bool EnablePassivePan { get; set; } = false;
	[Property, Group( "Passive Pan" )] public float PassivePanSpeed { get; set; } = 50f; // World units of max pan offset

	private Vector3 debugPieceWorldPos;
	private float debugWorldMaxFlickDistance;
	private bool debugIsInPanZone;
	private string debugPanDirection;
	private Vector2 debugCursorPosition;

	// Track where the oval intersects each edge (min/max along that edge)
	private Vector2 leftEdgeRange;   // y range where oval touches left edge
	private Vector2 rightEdgeRange;  // y range where oval touches right edge
	private Vector2 topEdgeRange;    // x range where oval touches top edge
	private Vector2 bottomEdgeRange; // x range where oval touches bottom edge

	// Camera pan state
	private Vector3 initialCameraPosition;
	private Vector3 targetCameraPosition; // Where the camera wants to be
	private bool isDraggingPiece;
	private Vector2 lastPanDirection; // Track which direction we were panning

	protected override void OnStart()
	{
		// Initialize target position to current position
		targetCameraPosition = WorldPosition;
	}

	protected override void OnUpdate()
	{
		// Passive pan offset is always applied on top of target position
		Vector3 finalTarget = targetCameraPosition + GetPassivePanOffset();

		//Do smoothing
		if ( SmoothPan ) { WorldPosition = Vector3.Lerp( WorldPosition, finalTarget, Time.Delta * Smoothing ); } else { WorldPosition = finalTarget; }
	


		if ( ShowDebug )
		{
			DrawDebug();
		}
	}

	public void UpdateCursorPosition( Vector2 cursorPosition )
	{
		debugCursorPosition = cursorPosition;
	}

	private Vector3 GetPassivePanOffset()
	{
		if ( !EnablePassivePan ) return Vector3.Zero;

		// Calculate cursor offset from screen center
		Vector2 screenCenter = new Vector2( Screen.Width / 2f, Screen.Height / 2f );
		Vector2 cursorOffset = debugCursorPosition - screenCenter;

		// Normalize by screen size to get a -1 to 1 range
		Vector2 normalizedOffset = new Vector2(
			cursorOffset.x / screenCenter.x,
			cursorOffset.y / screenCenter.y
		);

		// Convert to world offset (same direction as cursor from center)
		return new Vector3(
			normalizedOffset.x,
			-normalizedOffset.y, // Invert Y because screen Y is opposite to world Y
			0
		) * PassivePanSpeed;
	}

	private void DrawDebug()
	{
		// Draw the piece's max flick circle projected from world space to screen space
		if ( debugPieceWorldPos != Vector3.Zero && debugWorldMaxFlickDistance > 0 )
		{
			Vector2 screenSize = new Vector2( Screen.Width, Screen.Height );

			// Project world-space circle points to screen space to get accurate oval shape
			int segments = 128;
			for ( int i = 0; i < segments; i++ )
			{
				float angle = (i / (float)segments) * MathF.PI * 2f;

				// Calculate point on world-space circle (in XY plane, Z=0)
				Vector3 worldPoint = debugPieceWorldPos + new Vector3(
					MathF.Cos( angle ) * debugWorldMaxFlickDistance,
					MathF.Sin( angle ) * debugWorldMaxFlickDistance,
					0
				);

				// Project to screen space
				Vector2 screenPoint = Scene.Camera.PointToScreenPixels( worldPoint );

				// Check if this point is at the edge (within 1 pixel threshold)
				float edgeThreshold = 1f;
				bool isAtEdge = screenPoint.x <= edgeThreshold || screenPoint.x >= screenSize.x - edgeThreshold ||
								screenPoint.y <= edgeThreshold || screenPoint.y >= screenSize.y - edgeThreshold;

				// Draw a rect at each point - red if at edge, cyan if not
				float halfSize = DebugPointSize / 2f;
				Color dotColor = isAtEdge ? Color.Red : Color.Cyan;
				Gizmo.Draw.ScreenRect( new Rect( screenPoint.x - halfSize, screenPoint.y - halfSize, DebugPointSize, DebugPointSize ), dotColor );
			}
		}

		// Debug text showing pan zone status
		Vector2 debugScreenSize = new Vector2( Screen.Width, Screen.Height );

		string debugText = $"In Pan Zone: {debugIsInPanZone}";
		if ( debugIsInPanZone )
		{
			debugText += $"\nDirection: {debugPanDirection}";
		}
		debugText += $"\nCursor: ({debugCursorPosition.x:F0}, {debugCursorPosition.y:F0})";

		Gizmo.Draw.ScreenText( debugText, new Vector2( 10, 100 ), "roboto", 14f, TextFlag.Left );

		// Highlight screen edges when in pan zone
		if ( debugIsInPanZone )
		{
			float thickness = 8f; // Made thicker for visibility

			Color edgeColor = Color.Yellow.WithAlpha( 0.3f );

			if ( debugPanDirection.Contains( "left" ) )
			{
				float height = leftEdgeRange.y - leftEdgeRange.x;
				Gizmo.Draw.ScreenRect( new Rect( 0, leftEdgeRange.x, thickness, height ), edgeColor );
			}
			if ( debugPanDirection.Contains( "right" ) )
			{
				float height = rightEdgeRange.y - rightEdgeRange.x;
				Gizmo.Draw.ScreenRect( new Rect( debugScreenSize.x - thickness, rightEdgeRange.x, thickness, height ), edgeColor );
			}
			if ( debugPanDirection.Contains( "top" ) )
			{
				float width = topEdgeRange.y - topEdgeRange.x;
				Gizmo.Draw.ScreenRect( new Rect( topEdgeRange.x, 0, width, thickness ), edgeColor );
			}
			if ( debugPanDirection.Contains( "bottom" ) )
			{
				float width = bottomEdgeRange.y - bottomEdgeRange.x;
				Gizmo.Draw.ScreenRect( new Rect( bottomEdgeRange.x, debugScreenSize.y - thickness, width, thickness ), edgeColor );
			}
		}
	}

	public void UpdatePan( Vector2 cursorPosition, Vector3 piecePosition, bool isDragging, float worldMaxFlickDistance )
	{
		// Always store cursor position for debug
		debugCursorPosition = cursorPosition;

		if ( !isDragging )
		{
			// If we were dragging and now we're not, set target back to initial position (will smooth there)
			if ( isDraggingPiece )
			{
				targetCameraPosition = initialCameraPosition;
				isDraggingPiece = false;
			}

			debugPieceWorldPos = Vector3.Zero;
			debugWorldMaxFlickDistance = 0;
			debugIsInPanZone = false;
			debugPanDirection = "";
			return;
		}

		// If we just started dragging, store the initial camera position
		// Subtract the passive pan offset to get the true base position (since WorldPosition includes it)
		if ( !isDraggingPiece )
		{
			Vector3 basePosition = WorldPosition - GetPassivePanOffset();
			initialCameraPosition = basePosition;
			targetCameraPosition = basePosition;
			isDraggingPiece = true;
		}

		// Store debug data for visualization
		debugPieceWorldPos = piecePosition;
		debugWorldMaxFlickDistance = worldMaxFlickDistance;

		// Check which edges the oval overlaps
		var ovalOverlap = CheckOvalEdgeOverlap( piecePosition, worldMaxFlickDistance );
		debugIsInPanZone = ovalOverlap.overlaps;
		debugPanDirection = ovalOverlap.direction;

		// Check if cursor is near an edge that has oval overlap, and pan if so
		Vector2 panDirection = GetPanDirection( cursorPosition, ovalOverlap );

		if ( panDirection != Vector2.Zero )
		{
			ApplyPan( panDirection );
		}
		else
		{
			// If not panning, move back towards initial position
			ReturnToInitialPosition();
		}
	}

	private void ReturnToInitialPosition()
	{
		Vector3 diff = initialCameraPosition - targetCameraPosition;

		// If we're already at the initial position, nothing to do
		if ( diff.Length < 0.01f )
		{
			lastPanDirection = Vector2.Zero;
			return;
		}

		// Use mouse delta in the opposite direction of the last pan to return
		Vector2 mouseDelta = Mouse.Delta;
		float returnAmount = 0f;

		// If we were panning left (negative X), moving mouse right (positive delta) should return
		// If we were panning right (positive X), moving mouse left (negative delta) should return
		if ( lastPanDirection.x < 0 && mouseDelta.x > 0 )
			returnAmount = MathF.Max( returnAmount, mouseDelta.x );
		else if ( lastPanDirection.x > 0 && mouseDelta.x < 0 )
			returnAmount = MathF.Max( returnAmount, -mouseDelta.x );

		// If we were panning up (negative Y), moving mouse down (positive delta) should return
		// If we were panning down (positive Y), moving mouse up (negative delta) should return
		if ( lastPanDirection.y < 0 && mouseDelta.y > 0 )
			returnAmount = MathF.Max( returnAmount, mouseDelta.y );
		else if ( lastPanDirection.y > 0 && mouseDelta.y < 0 )
			returnAmount = MathF.Max( returnAmount, -mouseDelta.y );

		if ( returnAmount > 0 )
		{
			// Move target towards initial position, but don't overshoot
			Vector3 moveDir = diff.Normal;
			float moveAmount = MathF.Min( returnAmount * PanSpeed, diff.Length );
			targetCameraPosition += moveDir * moveAmount;
		}
	}

	private Vector2 GetPanDirection( Vector2 cursorPosition, (bool overlaps, string direction) ovalOverlap )
	{
		if ( !ovalOverlap.overlaps )
			return Vector2.Zero;

		Vector2 screenSize = new Vector2( Screen.Width, Screen.Height );
		Vector2 panDir = Vector2.Zero;

		// Check if cursor is near left edge AND oval overlaps left edge AND cursor is within the overlap range
		bool nearLeft = cursorPosition.x < EdgeThreshold;
		bool nearRight = cursorPosition.x > screenSize.x - EdgeThreshold;
		bool nearTop = cursorPosition.y < EdgeThreshold;
		bool nearBottom = cursorPosition.y > screenSize.y - EdgeThreshold;

		// For each edge: check if cursor is near it, oval overlaps it, and cursor Y/X is within the overlap range
		if ( nearLeft && ovalOverlap.direction.Contains( "left" ) )
		{
			// Check if cursor Y is within the oval's overlap range on the left edge
			if ( cursorPosition.y >= leftEdgeRange.x && cursorPosition.y <= leftEdgeRange.y )
			{
				panDir.x = -1f; // Pan camera left (so world moves right on screen)
			}
		}

		if ( nearRight && ovalOverlap.direction.Contains( "right" ) )
		{
			if ( cursorPosition.y >= rightEdgeRange.x && cursorPosition.y <= rightEdgeRange.y )
			{
				panDir.x = 1f; // Pan camera right
			}
		}

		if ( nearTop && ovalOverlap.direction.Contains( "top" ) )
		{
			if ( cursorPosition.x >= topEdgeRange.x && cursorPosition.x <= topEdgeRange.y )
			{
				panDir.y = -1f; // Pan camera up
			}
		}

		if ( nearBottom && ovalOverlap.direction.Contains( "bottom" ) )
		{
			if ( cursorPosition.x >= bottomEdgeRange.x && cursorPosition.x <= bottomEdgeRange.y )
			{
				panDir.y = 1f; // Pan camera down
			}
		}

		return panDir;
	}

	private void ApplyPan( Vector2 panDirection )
	{
		// Use mouse delta to determine pan amount
		Vector2 mouseDelta = Mouse.Delta;

		// Calculate pan amount based on mouse movement in the pan direction
		// We want to pan when the mouse is pushing against the edge
		float panAmountX = 0f;
		float panAmountY = 0f;

		// If panning left, use negative mouse X delta (pushing left)
		// If panning right, use positive mouse X delta (pushing right)
		if ( panDirection.x < 0 && mouseDelta.x < 0 )
			panAmountX = -mouseDelta.x; // Pushing left
		else if ( panDirection.x > 0 && mouseDelta.x > 0 )
			panAmountX = mouseDelta.x; // Pushing right

		// If panning up (negative Y), use negative mouse Y delta (pushing up)
		// If panning down (positive Y), use positive mouse Y delta (pushing down)
		if ( panDirection.y < 0 && mouseDelta.y < 0 )
			panAmountY = -mouseDelta.y; // Pushing up
		else if ( panDirection.y > 0 && mouseDelta.y > 0 )
			panAmountY = mouseDelta.y; // Pushing down

		float totalPanAmount = MathF.Max( panAmountX, panAmountY );

		if ( totalPanAmount > 0 )
		{
			// Store the pan direction so return knows which way we went
			lastPanDirection = panDirection;

			// Convert screen pan direction to world direction
			// Camera looks down at the play field, so we need to convert appropriately
			Vector3 worldPanDirection = new Vector3(
				panDirection.x,
				-panDirection.y, // Invert Y because screen Y is opposite to world Y
				0
			);

			// Apply the pan to target position (actual camera will lerp towards it)
			Vector3 panOffset = worldPanDirection * totalPanAmount * PanSpeed;
			targetCameraPosition += panOffset;
		}
	}

	private (bool overlaps, string direction) CheckOvalEdgeOverlap( Vector3 piecePosition, float worldMaxFlickDistance )
	{
		// Check each direction to see if the oval extends beyond the screen
		bool leftExtends = DoesOvalExtendBeyondEdge( piecePosition, worldMaxFlickDistance, "left" );
		bool rightExtends = DoesOvalExtendBeyondEdge( piecePosition, worldMaxFlickDistance, "right" );
		bool topExtends = DoesOvalExtendBeyondEdge( piecePosition, worldMaxFlickDistance, "top" );
		bool bottomExtends = DoesOvalExtendBeyondEdge( piecePosition, worldMaxFlickDistance, "bottom" );


		string direction = "";
		bool overlaps = false;

		if ( leftExtends )
		{
			overlaps = true;
			direction = "left";
		}
		else if ( rightExtends )
		{
			overlaps = true;
			direction = "right";
		}

		if ( topExtends )
		{
			overlaps = true;
			direction = overlaps ? direction + "+top" : "top";
		}
		else if ( bottomExtends )
		{
			overlaps = true;
			direction = overlaps ? direction + "+bottom" : "bottom";
		}

		return (overlaps, direction);
	}

	private bool DoesOvalExtendBeyondEdge( Vector3 piecePosition, float worldMaxFlickDistance, string edge )
	{
		Vector2 screenSize = new Vector2( Screen.Width, Screen.Height );

		// Track the range along each edge where the oval touches
		float edgeMin = float.MaxValue;
		float edgeMax = float.MinValue;

		bool extends = false;
		float edgeThreshold = 1f;

		// Sample points around the entire circle
		int samples = 64;
		for ( int i = 0; i < samples; i++ )
		{
			float angle = (i / (float)samples) * MathF.PI * 2f;

			Vector3 worldPoint = piecePosition + new Vector3(
				MathF.Cos( angle ) * worldMaxFlickDistance,
				MathF.Sin( angle ) * worldMaxFlickDistance,
				0
			);

			Vector2 screenPoint = Scene.Camera.PointToScreenPixels( worldPoint );

			// Check if this point is at the specified screen edge (within threshold since points get clamped)
			switch ( edge )
			{
				case "left":
					if ( screenPoint.x <= edgeThreshold )
					{
						extends = true;
						edgeMin = MathF.Min( edgeMin, screenPoint.y );
						edgeMax = MathF.Max( edgeMax, screenPoint.y );
					}
					break;
				case "right":
					if ( screenPoint.x >= screenSize.x - edgeThreshold )
					{
						extends = true;
						edgeMin = MathF.Min( edgeMin, screenPoint.y );
						edgeMax = MathF.Max( edgeMax, screenPoint.y );
					}
					break;
				case "top":
					if ( screenPoint.y <= edgeThreshold )
					{
						extends = true;
						edgeMin = MathF.Min( edgeMin, screenPoint.x );
						edgeMax = MathF.Max( edgeMax, screenPoint.x );
					}
					break;
				case "bottom":
					if ( screenPoint.y >= screenSize.y - edgeThreshold )
					{
						extends = true;
						edgeMin = MathF.Min( edgeMin, screenPoint.x );
						edgeMax = MathF.Max( edgeMax, screenPoint.x );
					}
					break;
			}
		}

		// Store the edge range for drawing
		if ( extends )
		{
			Vector2 range = new Vector2( edgeMin, edgeMax );
			switch ( edge )
			{
				case "left": leftEdgeRange = range; break;
				case "right": rightEdgeRange = range; break;
				case "top": topEdgeRange = range; break;
				case "bottom": bottomEdgeRange = range; break;
			}
		}

		return extends;
	}
}

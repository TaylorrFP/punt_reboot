using Sandbox;
using System;

/// <summary>
/// Controls camera panning behavior during piece selection.
/// Supports edge panning when flick radius extends beyond screen, and passive cursor-based panning.
/// </summary>
public sealed class CameraController : Component
{
	#region Properties

	[Property, Group( "Debug" )] public bool ShowDebug { get; set; } = false;
	[Property, Group( "Debug" )] public float DebugPointSize { get; set; } = 2f;

	[Property, Group( "Edge Pan" )] public float EdgeThreshold { get; set; } = 50f;
	[Property, Group( "Edge Pan" )] public float PanSpeed { get; set; } = 1f;

	[Property, Group( "Smoothing" )] public bool SmoothPan { get; set; } = true;
	[Property, Group( "Smoothing" )] public float Smoothing { get; set; } = 10f;

	[Property, Group( "Passive Pan" )] public bool EnablePassivePan { get; set; } = false;
	[Property, Group( "Passive Pan" )] public float PassivePanSpeed { get; set; } = 50f;

	#endregion

	#region Private State

	// Current cursor position (updated by PlayerController)
	private Vector2 cursorPosition;

	// Camera position tracking
	private Vector3 initialPosition;
	private Vector3 targetPosition;

	// Drag state
	private bool isDragging;
	private Vector2 lastPanDirection;

	// Edge overlap tracking (stores Y range for left/right edges, X range for top/bottom)
	private Vector2 leftEdgeRange;
	private Vector2 rightEdgeRange;
	private Vector2 topEdgeRange;
	private Vector2 bottomEdgeRange;

	// Debug visualization data
	private Vector3 pieceWorldPosition;
	private float worldFlickRadius;
	private bool isInPanZone;
	private string panDirections;

	#endregion

	#region Lifecycle

	protected override void OnStart()
	{
		targetPosition = WorldPosition;
	}

	protected override void OnUpdate()
	{
		Vector3 finalTarget = targetPosition + GetPassivePanOffset();

		if ( SmoothPan )
		{
			WorldPosition = Vector3.Lerp( WorldPosition, finalTarget, Time.Delta * Smoothing );
		}
		else
		{
			WorldPosition = finalTarget;
		}

		if ( ShowDebug )
		{
			DrawDebug();
		}
	}

	#endregion

	#region Public API

	/// <summary>
	/// Updates the cursor position for passive pan calculations.
	/// Called every frame by PlayerController.
	/// </summary>
	public void UpdateCursorPosition( Vector2 position )
	{
		cursorPosition = position;
	}

	/// <summary>
	/// Updates edge panning state based on piece selection.
	/// </summary>
	/// <param name="cursor">Current cursor screen position</param>
	/// <param name="piecePosition">World position of selected piece (or Zero if not dragging)</param>
	/// <param name="isDraggingPiece">Whether a piece is currently being dragged</param>
	/// <param name="flickRadius">World-space radius of the flick circle</param>
	public void UpdatePan( Vector2 cursor, Vector3 piecePosition, bool isDraggingPiece, float flickRadius )
	{
		cursorPosition = cursor;

		if ( !isDraggingPiece )
		{
			HandleDragEnd();
			return;
		}

		if ( !isDragging )
		{
			HandleDragStart();
		}

		UpdateDebugState( piecePosition, flickRadius );

		var overlap = CheckOvalEdgeOverlap( piecePosition, flickRadius );
		isInPanZone = overlap.hasOverlap;
		panDirections = overlap.directions;

		Vector2 panDirection = GetPanDirection( cursor, overlap );

		if ( panDirection != Vector2.Zero )
		{
			ApplyEdgePan( panDirection );
		}
		else
		{
			ReturnToInitialPosition();
		}
	}

	#endregion

	#region Drag State Management

	private void HandleDragStart()
	{
		// Subtract passive pan offset to get true base position
		Vector3 basePosition = WorldPosition - GetPassivePanOffset();
		initialPosition = basePosition;
		targetPosition = basePosition;
		isDragging = true;
	}

	private void HandleDragEnd()
	{
		if ( isDragging )
		{
			targetPosition = initialPosition;
			isDragging = false;
		}

		ClearDebugState();
	}

	private void UpdateDebugState( Vector3 piecePos, float radius )
	{
		pieceWorldPosition = piecePos;
		worldFlickRadius = radius;
	}

	private void ClearDebugState()
	{
		pieceWorldPosition = Vector3.Zero;
		worldFlickRadius = 0;
		isInPanZone = false;
		panDirections = "";
	}

	#endregion

	#region Passive Pan

	private Vector3 GetPassivePanOffset()
	{
		if ( !EnablePassivePan ) return Vector3.Zero;

		Vector2 screenCenter = new Vector2( Screen.Width / 2f, Screen.Height / 2f );
		Vector2 cursorOffset = cursorPosition - screenCenter;

		// Normalize to -1 to 1 range
		Vector2 normalized = new Vector2(
			cursorOffset.x / screenCenter.x,
			cursorOffset.y / screenCenter.y
		);

		// Convert to world offset (invert Y for screen-to-world conversion)
		return new Vector3( normalized.x, -normalized.y, 0 ) * PassivePanSpeed;
	}

	#endregion

	#region Edge Pan

	private Vector2 GetPanDirection( Vector2 cursor, (bool hasOverlap, string directions) overlap )
	{
		if ( !overlap.hasOverlap ) return Vector2.Zero;

		Vector2 screenSize = new Vector2( Screen.Width, Screen.Height );
		Vector2 panDir = Vector2.Zero;

		bool nearLeft = cursor.x < EdgeThreshold;
		bool nearRight = cursor.x > screenSize.x - EdgeThreshold;
		bool nearTop = cursor.y < EdgeThreshold;
		bool nearBottom = cursor.y > screenSize.y - EdgeThreshold;

		// Check each edge: cursor must be near it, oval must overlap it, and cursor must be within overlap range
		if ( nearLeft && overlap.directions.Contains( "left" ) && IsInRange( cursor.y, leftEdgeRange ) )
		{
			panDir.x = -1f;
		}
		if ( nearRight && overlap.directions.Contains( "right" ) && IsInRange( cursor.y, rightEdgeRange ) )
		{
			panDir.x = 1f;
		}
		if ( nearTop && overlap.directions.Contains( "top" ) && IsInRange( cursor.x, topEdgeRange ) )
		{
			panDir.y = -1f;
		}
		if ( nearBottom && overlap.directions.Contains( "bottom" ) && IsInRange( cursor.x, bottomEdgeRange ) )
		{
			panDir.y = 1f;
		}

		return panDir;
	}

	private bool IsInRange( float value, Vector2 range )
	{
		return value >= range.x && value <= range.y;
	}

	private void ApplyEdgePan( Vector2 panDirection )
	{
		Vector2 mouseDelta = Mouse.Delta;

		// Only pan when mouse is pushing in the pan direction
		float panAmountX = GetDirectionalPanAmount( panDirection.x, mouseDelta.x );
		float panAmountY = GetDirectionalPanAmount( panDirection.y, mouseDelta.y );
		float totalPanAmount = MathF.Max( panAmountX, panAmountY );

		if ( totalPanAmount > 0 )
		{
			lastPanDirection = panDirection;

			Vector3 worldPanDir = new Vector3( panDirection.x, -panDirection.y, 0 );
			targetPosition += worldPanDir * totalPanAmount * PanSpeed;
		}
	}

	private float GetDirectionalPanAmount( float panDir, float mouseDelta )
	{
		// Pan when mouse moves in same direction as pan
		if ( panDir < 0 && mouseDelta < 0 ) return -mouseDelta;
		if ( panDir > 0 && mouseDelta > 0 ) return mouseDelta;
		return 0f;
	}

	private void ReturnToInitialPosition()
	{
		Vector3 diff = initialPosition - targetPosition;

		if ( diff.Length < 0.01f )
		{
			lastPanDirection = Vector2.Zero;
			return;
		}

		Vector2 mouseDelta = Mouse.Delta;
		float returnAmount = GetReturnAmount( mouseDelta );

		if ( returnAmount > 0 )
		{
			Vector3 moveDir = diff.Normal;
			float moveAmount = MathF.Min( returnAmount * PanSpeed, diff.Length );
			targetPosition += moveDir * moveAmount;
		}
	}

	private float GetReturnAmount( Vector2 mouseDelta )
	{
		float returnAmount = 0f;

		// Return when mouse moves opposite to last pan direction
		if ( lastPanDirection.x < 0 && mouseDelta.x > 0 )
			returnAmount = MathF.Max( returnAmount, mouseDelta.x );
		else if ( lastPanDirection.x > 0 && mouseDelta.x < 0 )
			returnAmount = MathF.Max( returnAmount, -mouseDelta.x );

		if ( lastPanDirection.y < 0 && mouseDelta.y > 0 )
			returnAmount = MathF.Max( returnAmount, mouseDelta.y );
		else if ( lastPanDirection.y > 0 && mouseDelta.y < 0 )
			returnAmount = MathF.Max( returnAmount, -mouseDelta.y );

		return returnAmount;
	}

	#endregion

	#region Oval Edge Detection

	private (bool hasOverlap, string directions) CheckOvalEdgeOverlap( Vector3 piecePos, float radius )
	{
		bool left = CheckEdgeOverlap( piecePos, radius, ScreenEdge.Left );
		bool right = CheckEdgeOverlap( piecePos, radius, ScreenEdge.Right );
		bool top = CheckEdgeOverlap( piecePos, radius, ScreenEdge.Top );
		bool bottom = CheckEdgeOverlap( piecePos, radius, ScreenEdge.Bottom );

		string directions = "";
		bool hasOverlap = false;

		if ( left ) { hasOverlap = true; directions = "left"; }
		else if ( right ) { hasOverlap = true; directions = "right"; }

		if ( top ) { hasOverlap = true; directions = hasOverlap && directions != "" ? directions + "+top" : "top"; }
		else if ( bottom ) { hasOverlap = true; directions = hasOverlap && directions != "" ? directions + "+bottom" : "bottom"; }

		return (hasOverlap, directions);
	}

	private enum ScreenEdge { Left, Right, Top, Bottom }

	private bool CheckEdgeOverlap( Vector3 piecePos, float radius, ScreenEdge edge )
	{
		Vector2 screenSize = new Vector2( Screen.Width, Screen.Height );
		float edgeMin = float.MaxValue;
		float edgeMax = float.MinValue;
		bool extends = false;
		const float threshold = 1f;
		const int samples = 64;

		// Calculate where the camera will be (target + passive pan)
		// We need to check edges relative to target position, not current position,
		// otherwise smoothing causes overshoot as detection lags behind
		Vector3 targetCameraPos = targetPosition + GetPassivePanOffset();
		Vector3 cameraOffset = targetCameraPos - WorldPosition;

		for ( int i = 0; i < samples; i++ )
		{
			float angle = (i / (float)samples) * MathF.PI * 2f;
			Vector3 worldPoint = piecePos + new Vector3(
				MathF.Cos( angle ) * radius,
				MathF.Sin( angle ) * radius,
				0
			);

			// Offset the world point as if camera were already at target position
			Vector3 adjustedPoint = worldPoint - cameraOffset;
			Vector2 screenPoint = Scene.Camera.PointToScreenPixels( adjustedPoint );

			bool atEdge = edge switch
			{
				ScreenEdge.Left => screenPoint.x <= threshold,
				ScreenEdge.Right => screenPoint.x >= screenSize.x - threshold,
				ScreenEdge.Top => screenPoint.y <= threshold,
				ScreenEdge.Bottom => screenPoint.y >= screenSize.y - threshold,
				_ => false
			};

			if ( atEdge )
			{
				extends = true;
				// For left/right edges, track Y range; for top/bottom, track X range
				float rangeValue = (edge == ScreenEdge.Left || edge == ScreenEdge.Right) ? screenPoint.y : screenPoint.x;
				edgeMin = MathF.Min( edgeMin, rangeValue );
				edgeMax = MathF.Max( edgeMax, rangeValue );
			}
		}

		if ( extends )
		{
			Vector2 range = new Vector2( edgeMin, edgeMax );
			switch ( edge )
			{
				case ScreenEdge.Left: leftEdgeRange = range; break;
				case ScreenEdge.Right: rightEdgeRange = range; break;
				case ScreenEdge.Top: topEdgeRange = range; break;
				case ScreenEdge.Bottom: bottomEdgeRange = range; break;
			}
		}

		return extends;
	}

	#endregion

	#region Debug Visualization

	private void DrawDebug()
	{
		DrawFlickRadiusOval();
		DrawDebugText();
		DrawEdgeHighlights();
	}

	private void DrawFlickRadiusOval()
	{
		if ( pieceWorldPosition == Vector3.Zero || worldFlickRadius <= 0 ) return;

		Vector2 screenSize = new Vector2( Screen.Width, Screen.Height );
		const int segments = 128;
		const float edgeThreshold = 1f;
		float halfSize = DebugPointSize / 2f;

		for ( int i = 0; i < segments; i++ )
		{
			float angle = (i / (float)segments) * MathF.PI * 2f;
			Vector3 worldPoint = pieceWorldPosition + new Vector3(
				MathF.Cos( angle ) * worldFlickRadius,
				MathF.Sin( angle ) * worldFlickRadius,
				0
			);

			Vector2 screenPoint = Scene.Camera.PointToScreenPixels( worldPoint );

			bool isAtEdge = screenPoint.x <= edgeThreshold ||
							screenPoint.x >= screenSize.x - edgeThreshold ||
							screenPoint.y <= edgeThreshold ||
							screenPoint.y >= screenSize.y - edgeThreshold;

			Color dotColor = isAtEdge ? Color.Red : Color.Cyan;
			Gizmo.Draw.ScreenRect(
				new Rect( screenPoint.x - halfSize, screenPoint.y - halfSize, DebugPointSize, DebugPointSize ),
				dotColor
			);
		}
	}

	private void DrawDebugText()
	{
		string text = $"In Pan Zone: {isInPanZone}";
		if ( isInPanZone )
		{
			text += $"\nDirection: {panDirections}";
		}
		text += $"\nCursor: ({cursorPosition.x:F0}, {cursorPosition.y:F0})";

		Gizmo.Draw.ScreenText( text, new Vector2( 10, 100 ), "roboto", 14f, TextFlag.Left );
	}

	private void DrawEdgeHighlights()
	{
		if ( !isInPanZone ) return;

		Vector2 screenSize = new Vector2( Screen.Width, Screen.Height );
		const float thickness = 8f;
		Color edgeColor = Color.Yellow.WithAlpha( 0.3f );

		if ( panDirections.Contains( "left" ) )
		{
			float height = leftEdgeRange.y - leftEdgeRange.x;
			Gizmo.Draw.ScreenRect( new Rect( 0, leftEdgeRange.x, thickness, height ), edgeColor );
		}
		if ( panDirections.Contains( "right" ) )
		{
			float height = rightEdgeRange.y - rightEdgeRange.x;
			Gizmo.Draw.ScreenRect( new Rect( screenSize.x - thickness, rightEdgeRange.x, thickness, height ), edgeColor );
		}
		if ( panDirections.Contains( "top" ) )
		{
			float width = topEdgeRange.y - topEdgeRange.x;
			Gizmo.Draw.ScreenRect( new Rect( topEdgeRange.x, 0, width, thickness ), edgeColor );
		}
		if ( panDirections.Contains( "bottom" ) )
		{
			float width = bottomEdgeRange.y - bottomEdgeRange.x;
			Gizmo.Draw.ScreenRect( new Rect( bottomEdgeRange.x, screenSize.y - thickness, width, thickness ), edgeColor );
		}
	}

	#endregion
}

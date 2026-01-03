using Sandbox;
using System;

public sealed class CameraController : Component
{
	[Property, Group( "Debug" )] public bool ShowDebug { get; set; } = false;
	[Property, Group( "Debug" )] public float DebugPointSize { get; set; } = 2f;

	[Property, Group( "Pan Settings" )] public float EdgeThreshold { get; set; } = 100f; // How close to edge before considering panning

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

	protected override void OnUpdate()
	{
		if ( ShowDebug )
		{
			DrawDebug();
		}
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
			debugPieceWorldPos = Vector3.Zero;
			debugWorldMaxFlickDistance = 0;
			debugIsInPanZone = false;
			debugPanDirection = "";
			return;
		}

		// Store debug data for visualization
		debugPieceWorldPos = piecePosition;
		debugWorldMaxFlickDistance = worldMaxFlickDistance;

		// Simplified: just check if oval overlaps any edge
		var ovalOverlap = CheckOvalEdgeOverlap( piecePosition, worldMaxFlickDistance );
		debugIsInPanZone = ovalOverlap.overlaps;
		debugPanDirection = ovalOverlap.direction;

		// Camera panning logic will go here
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

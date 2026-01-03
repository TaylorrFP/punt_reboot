using Sandbox;
using System;

public sealed class CameraController : Component
{
	[Property, Group( "Pan Settings" )] public float EdgeThreshold { get; set; } = 150f;
	[Property, Group( "Pan Settings" )] public float MaxPanDistance { get; set; } = 300f;
	[Property, Group( "Pan Settings" )] public float PanSmoothness { get; set; } = 10f;

	[Property, Group( "Debug" )] public bool ShowDebug { get; set; } = false;
	[Property, Group( "Debug" )] public Color DebugColor { get; set; } = Color.Yellow;

	private Vector3 initialPosition;
	private Vector3 currentPanOffset;
	private Vector3 targetPanOffset;

	protected override void OnStart()
	{
		initialPosition = Transform.Position;
	}

	protected override void OnUpdate()
	{
		//Smoothly interpolate to target offset
	   currentPanOffset = Vector3.Lerp( currentPanOffset, targetPanOffset, Time.Delta * PanSmoothness );
		//currentPanOffset = targetPanOffset;
		Transform.Position = initialPosition + currentPanOffset;

		if ( ShowDebug )
		{
			DrawDebug();
		}
	}

	private Vector2 debugPieceScreenPos;
	private float debugMaxXDistance;
	private float debugMaxYDistance;
	private float debugDesiredCursorX;
	private float debugDesiredCursorY;
	private float debugNeededPanX;
	private float debugNeededPanY;

	private void DrawDebug()
	{
		Vector2 screenSize = new Vector2( Screen.Width, Screen.Height );
		float lineThickness = 2f;

		// Draw safe frame box
		// Top line
		Gizmo.Draw.ScreenRect( new Rect( EdgeThreshold, EdgeThreshold, screenSize.x - EdgeThreshold * 2, lineThickness ), DebugColor );

		// Bottom line
		Gizmo.Draw.ScreenRect( new Rect( EdgeThreshold, screenSize.y - EdgeThreshold, screenSize.x - EdgeThreshold * 2, lineThickness ), DebugColor );

		// Left line
		Gizmo.Draw.ScreenRect( new Rect( EdgeThreshold, EdgeThreshold, lineThickness, screenSize.y - EdgeThreshold * 2 ), DebugColor );

		// Right line
		Gizmo.Draw.ScreenRect( new Rect( screenSize.x - EdgeThreshold, EdgeThreshold, lineThickness, screenSize.y - EdgeThreshold * 2 ), DebugColor );

		// Draw debug info
		Gizmo.Draw.ScreenText( $"Pan Offset: {targetPanOffset}", new Vector2( 10, 100 ), "roboto", 14f, TextFlag.Left );
		Gizmo.Draw.ScreenText( $"Camera Pos: {Transform.Position}", new Vector2( 10, 120 ), "roboto", 14f, TextFlag.Left );
		Gizmo.Draw.ScreenText( $"Piece Screen: {debugPieceScreenPos}", new Vector2( 10, 140 ), "roboto", 14f, TextFlag.Left );
		Gizmo.Draw.ScreenText( $"Max X/Y Dist: {debugMaxXDistance:F0}/{debugMaxYDistance:F0}", new Vector2( 10, 160 ), "roboto", 14f, TextFlag.Left );
		Gizmo.Draw.ScreenText( $"Desired Cursor: {debugDesiredCursorX:F0},{debugDesiredCursorY:F0}", new Vector2( 10, 180 ), "roboto", 14f, TextFlag.Left );
		Gizmo.Draw.ScreenText( $"Needed Pan: {debugNeededPanX:F0},{debugNeededPanY:F0}", new Vector2( 10, 200 ), "roboto", 14f, TextFlag.Left );
	}

	public void UpdatePan( Vector2 cursorPosition, Vector3 piecePosition, bool isDragging, float maxFlickDistance )
	{
		if ( !isDragging )
		{
			// Reset to initial position when not dragging
			targetPanOffset = Vector3.Zero;
			return;
		}

		Vector2 screenSize = new Vector2( Screen.Width, Screen.Height );

		// Calculate piece screen position from INITIAL camera position to avoid feedback loop
		Vector3 savedCameraPos = Transform.Position;
		Transform.Position = initialPosition;
		Vector2 pieceScreenPos = Scene.Camera.PointToScreenPixels( piecePosition );
		Transform.Position = savedCameraPos;

		debugPieceScreenPos = pieceScreenPos;
		debugMaxXDistance = 0;
		debugMaxYDistance = 0;
		debugDesiredCursorX = 0;
		debugDesiredCursorY = 0;
		debugNeededPanX = 0;
		debugNeededPanY = 0;

		Vector3 panOffset = Vector3.Zero;

		// Calculate current distance from piece to cursor
		Vector2 cursorToPiece = cursorPosition - pieceScreenPos;
		float currentDistance = cursorToPiece.Length;

		// Simple premise: Only pan if the piece's max flick circle overlaps with the screen edge
		// Check if we're at an edge and if the piece's circle would extend beyond it

		// Left edge
		if ( cursorPosition.x < EdgeThreshold && cursorPosition.x < pieceScreenPos.x )
		{
			// Does the piece's max flick circle extend beyond the left edge threshold?
			float pieceLeftEdge = pieceScreenPos.x - maxFlickDistance;
			debugDesiredCursorX = pieceLeftEdge;

			if ( pieceLeftEdge < EdgeThreshold )
			{
				// Yes, we need to pan
				float neededPan = EdgeThreshold - pieceLeftEdge;
				debugNeededPanX = neededPan;
				float edgeDistance = EdgeThreshold - cursorPosition.x;
				float edgeIntensity = edgeDistance / EdgeThreshold;

				float panAmount = Math.Min( neededPan * edgeIntensity, MaxPanDistance );
				panOffset.x = -panAmount;
			}
		}
		// Right edge
		else if ( cursorPosition.x > screenSize.x - EdgeThreshold && cursorPosition.x > pieceScreenPos.x )
		{
			float pieceRightEdge = pieceScreenPos.x + maxFlickDistance;
			debugDesiredCursorX = pieceRightEdge;

			if ( pieceRightEdge > screenSize.x - EdgeThreshold )
			{
				float neededPan = pieceRightEdge - (screenSize.x - EdgeThreshold);
				debugNeededPanX = neededPan;
				float edgeDistance = cursorPosition.x - (screenSize.x - EdgeThreshold);
				float edgeIntensity = edgeDistance / EdgeThreshold;

				float panAmount = Math.Min( neededPan * edgeIntensity, MaxPanDistance );
				panOffset.x = panAmount;
			}
		}

		// Top edge
		if ( cursorPosition.y < EdgeThreshold && cursorPosition.y < pieceScreenPos.y )
		{
			float pieceTopEdge = pieceScreenPos.y - maxFlickDistance;
			debugDesiredCursorY = pieceTopEdge;

			if ( pieceTopEdge < EdgeThreshold )
			{
				float neededPan = EdgeThreshold - pieceTopEdge;
				debugNeededPanY = neededPan;
				float edgeDistance = EdgeThreshold - cursorPosition.y;
				float edgeIntensity = edgeDistance / EdgeThreshold;

				float panAmount = Math.Min( neededPan * edgeIntensity, MaxPanDistance );
				panOffset.y = panAmount;
			}
		}
		// Bottom edge
		else if ( cursorPosition.y > screenSize.y - EdgeThreshold && cursorPosition.y > pieceScreenPos.y )
		{
			float pieceBottomEdge = pieceScreenPos.y + maxFlickDistance;
			debugDesiredCursorY = pieceBottomEdge;

			if ( pieceBottomEdge > screenSize.y - EdgeThreshold )
			{
				float neededPan = pieceBottomEdge - (screenSize.y - EdgeThreshold);
				debugNeededPanY = neededPan;
				float edgeDistance = cursorPosition.y - (screenSize.y - EdgeThreshold);
				float edgeIntensity = edgeDistance / EdgeThreshold;

				float panAmount = Math.Min( neededPan * edgeIntensity, MaxPanDistance );
				panOffset.y = -panAmount;
			}
		}

		targetPanOffset = panOffset.WithZ( 0 );
	}
}

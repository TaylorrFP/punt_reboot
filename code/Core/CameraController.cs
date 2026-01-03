using Sandbox;

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
		// Smoothly interpolate to target offset
		//currentPanOffset = Vector3.Lerp( currentPanOffset, targetPanOffset, Time.Delta * PanSmoothness );
		currentPanOffset = targetPanOffset;
		Transform.Position = initialPosition + currentPanOffset;

		if ( ShowDebug )
		{
			DrawDebug();
		}
	}

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

		Vector3 panOffset = Vector3.Zero;

		// Calculate the maximum possible cursor distance from piece (in screen space)
		// This is where the cursor would be if not clamped by screen edges
		Vector2 cursorToPiece = cursorPosition - pieceScreenPos;
		float maxPossibleDistance = maxFlickDistance;

		// Check each edge and calculate pan amount
		// Left edge
		if ( cursorPosition.x < EdgeThreshold )
		{
			// Only pan if:
			// 1. Cursor is left of piece (dragging away)
			// 2. The cursor would be further left if not for the screen edge
			if ( cursorPosition.x < pieceScreenPos.x )
			{
				// Check if we're actually constrained by the edge
				float desiredCursorX = pieceScreenPos.x - maxPossibleDistance;
				if ( desiredCursorX < 0 )
				{
					float edgeDistance = EdgeThreshold - cursorPosition.x;
					float panAmount = (edgeDistance / EdgeThreshold) * MaxPanDistance;
					panOffset.x = -panAmount;
				}
			}
		}
		// Right edge
		else if ( cursorPosition.x > screenSize.x - EdgeThreshold )
		{
			if ( cursorPosition.x > pieceScreenPos.x )
			{
				float desiredCursorX = pieceScreenPos.x + maxPossibleDistance;
				if ( desiredCursorX > screenSize.x )
				{
					float edgeDistance = cursorPosition.x - (screenSize.x - EdgeThreshold);
					float panAmount = (edgeDistance / EdgeThreshold) * MaxPanDistance;
					panOffset.x = panAmount;
				}
			}
		}

		// Top edge
		if ( cursorPosition.y < EdgeThreshold )
		{
			if ( cursorPosition.y < pieceScreenPos.y )
			{
				float desiredCursorY = pieceScreenPos.y - maxPossibleDistance;
				if ( desiredCursorY < 0 )
				{
					float edgeDistance = EdgeThreshold - cursorPosition.y;
					float panAmount = (edgeDistance / EdgeThreshold) * MaxPanDistance;
					panOffset.y = panAmount;
				}
			}
		}
		// Bottom edge
		else if ( cursorPosition.y > screenSize.y - EdgeThreshold )
		{
			if ( cursorPosition.y > pieceScreenPos.y )
			{
				float desiredCursorY = pieceScreenPos.y + maxPossibleDistance;
				if ( desiredCursorY > screenSize.y )
				{
					float edgeDistance = cursorPosition.y - (screenSize.y - EdgeThreshold);
					float panAmount = (edgeDistance / EdgeThreshold) * MaxPanDistance;
					panOffset.y = -panAmount;
				}
			}
		}

		targetPanOffset = panOffset.WithZ( 0 );
	}
}

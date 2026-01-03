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
	}

	public void UpdatePan( Vector2 cursorPosition, Vector3 piecePosition, bool isDragging )
	{
		if ( !isDragging )
		{
			// Reset to initial position when not dragging
			targetPanOffset = Vector3.Zero;
			return;
		}

		Vector2 screenSize = new Vector2( Screen.Width, Screen.Height );
		Vector3 panOffset = Vector3.Zero;

		// Check each edge and calculate pan amount
		// Left edge
		if ( cursorPosition.x < EdgeThreshold )
		{
			float edgeDistance = EdgeThreshold - cursorPosition.x;
			float panAmount = (edgeDistance / EdgeThreshold) * MaxPanDistance;

			// Only pan if dragging away from piece (towards left edge)
			Vector2 pieceScreenPos = Scene.Camera.PointToScreenPixels( piecePosition );
			if ( cursorPosition.x < pieceScreenPos.x )
			{
				panOffset.x = -panAmount;
			}
		}
		// Right edge
		else if ( cursorPosition.x > screenSize.x - EdgeThreshold )
		{
			float edgeDistance = cursorPosition.x - (screenSize.x - EdgeThreshold);
			float panAmount = (edgeDistance / EdgeThreshold) * MaxPanDistance;

			Vector2 pieceScreenPos = Scene.Camera.PointToScreenPixels( piecePosition );
			if ( cursorPosition.x > pieceScreenPos.x )
			{
				panOffset.x = panAmount;
			}
		}

		// Top edge
		if ( cursorPosition.y < EdgeThreshold )
		{
			float edgeDistance = EdgeThreshold - cursorPosition.y;
			float panAmount = (edgeDistance / EdgeThreshold) * MaxPanDistance;

			Vector2 pieceScreenPos = Scene.Camera.PointToScreenPixels( piecePosition );
			if ( cursorPosition.y < pieceScreenPos.y )
			{
				panOffset.y = panAmount;
			}
		}
		// Bottom edge
		else if ( cursorPosition.y > screenSize.y - EdgeThreshold )
		{
			float edgeDistance = cursorPosition.y - (screenSize.y - EdgeThreshold);
			float panAmount = (edgeDistance / EdgeThreshold) * MaxPanDistance;

			Vector2 pieceScreenPos = Scene.Camera.PointToScreenPixels( piecePosition );
			if ( cursorPosition.y > pieceScreenPos.y )
			{
				panOffset.y = -panAmount;
			}
		}

		targetPanOffset = panOffset.WithZ( 0 );
	}
}

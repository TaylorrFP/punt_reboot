using Sandbox;
using System;

public sealed class CameraController : Component
{
	[Property, Group( "Edge Pan Settings" )] public float EdgeThreshold { get; set; } = 150f;
	[Property, Group( "Edge Pan Settings" )] public float MaxPanDistance { get; set; } = 300f;
	[Property, Group( "Edge Pan Settings" )] public float PanSmoothSpeed { get; set; } = 8f;

	[Property, Group( "Passive Pan Settings" )] public bool EnablePassivePan { get; set; } = true;
	[Property, Group( "Passive Pan Settings" )] public float PassivePanStrength { get; set; } = 0.3f;
	[Property, Group( "Passive Pan Settings" )] public float DraggingPanStrength { get; set; } = 1.0f;
	[Property, Group( "Passive Pan Settings" )] public float DraggingCurve { get; set; } = 2.0f;

	[Property, Group( "Debug" )] public bool ShowSafeFrame { get; set; } = false;
	[Property, Group( "Debug" )] public Color SafeFrameColor { get; set; } = Color.Cyan.WithAlpha( 0.5f );

	private Vector3 basePosition;
	private Vector3 targetOffset;
	private Vector3 currentOffset;

	protected override void OnStart()
	{
		// Store the initial camera position as our base
		basePosition = LocalPosition;
		targetOffset = Vector3.Zero;
		currentOffset = Vector3.Zero;
	}

	protected override void OnUpdate()
	{
		// Smoothly move current offset toward target
		currentOffset = Vector3.Lerp( currentOffset, targetOffset, PanSmoothSpeed * Time.Delta );

		// Apply the offset
		LocalPosition = basePosition + currentOffset;

		// Draw debug safe frame
		if ( ShowSafeFrame )
		{
			DrawSafeFrame();
		}
	}

	private void DrawSafeFrame()
	{
		Vector2 screenSize = new Vector2( Screen.Width, Screen.Height );
		float lineThickness = 2f;

		Gizmo.Draw.Color = SafeFrameColor;

		// Draw four rectangles representing the edge threshold boundary

		// Top line
		Gizmo.Draw.ScreenRect( new Rect(
			EdgeThreshold,
			EdgeThreshold,
			screenSize.x - EdgeThreshold * 2f,
			lineThickness
		), SafeFrameColor );

		// Bottom line
		Gizmo.Draw.ScreenRect( new Rect(
			EdgeThreshold,
			screenSize.y - EdgeThreshold - lineThickness,
			screenSize.x - EdgeThreshold * 2f,
			lineThickness
		), SafeFrameColor );

		// Left line
		Gizmo.Draw.ScreenRect( new Rect(
			EdgeThreshold,
			EdgeThreshold,
			lineThickness,
			screenSize.y - EdgeThreshold * 2f
		), SafeFrameColor );

		// Right line
		Gizmo.Draw.ScreenRect( new Rect(
			screenSize.x - EdgeThreshold - lineThickness,
			EdgeThreshold,
			lineThickness,
			screenSize.y - EdgeThreshold * 2f
		), SafeFrameColor );
	}

	/// <summary>
	/// Called by PlayerController when dragging to apply camera pan based on cursor position
	/// </summary>
	public void UpdateEdgePan( Vector2 cursorPos, bool isDragging, Vector2? pieceScreenPos )
	{
		if ( !EnablePassivePan )
		{
			targetOffset = Vector3.Zero;
			return;
		}

		Vector2 screenSize = new Vector2( Screen.Width, Screen.Height );
		Vector2 panVector = Vector2.Zero;

		// Calculate distance from each edge
		float leftDist = cursorPos.x;
		float rightDist = screenSize.x - cursorPos.x;
		float topDist = cursorPos.y;
		float bottomDist = screenSize.y - cursorPos.y;

		// Determine current strength based on dragging state
		float baseStrength = isDragging ? DraggingPanStrength : PassivePanStrength;

		// Calculate pan intensity for each edge (0 when far from edge, 1 when at edge)
		float leftIntensity = 0f;
		float rightIntensity = 0f;
		float topIntensity = 0f;
		float bottomIntensity = 0f;

		if ( isDragging )
		{
			// When dragging, only pan if within EdgeThreshold of an edge
			if ( leftDist < EdgeThreshold )
			{
				float normalizedDist = 1f - (leftDist / EdgeThreshold);
				leftIntensity = MathF.Pow( normalizedDist, DraggingCurve );
			}
			else if ( rightDist < EdgeThreshold )
			{
				float normalizedDist = 1f - (rightDist / EdgeThreshold);
				rightIntensity = MathF.Pow( normalizedDist, DraggingCurve );
			}

			if ( topDist < EdgeThreshold )
			{
				float normalizedDist = 1f - (topDist / EdgeThreshold);
				topIntensity = MathF.Pow( normalizedDist, DraggingCurve );
			}
			else if ( bottomDist < EdgeThreshold )
			{
				float normalizedDist = 1f - (bottomDist / EdgeThreshold);
				bottomIntensity = MathF.Pow( normalizedDist, DraggingCurve );
			}
		}
		else
		{
			// When not dragging, passive pan across entire screen
			// Calculate normalized position from screen center (0 at center, 1 at edge)
			Vector2 screenCenter = screenSize / 2f;
			Vector2 fromCenter = cursorPos - screenCenter;
			Vector2 normalizedFromCenter = fromCenter / screenCenter;

			// Split into directional components
			if ( normalizedFromCenter.x < 0 )
				leftIntensity = -normalizedFromCenter.x;
			else
				rightIntensity = normalizedFromCenter.x;

			if ( normalizedFromCenter.y < 0 )
				topIntensity = -normalizedFromCenter.y;
			else
				bottomIntensity = normalizedFromCenter.y;
		}

		// Calculate pan direction based on edge proximity
		panVector.x = (rightIntensity - leftIntensity) * baseStrength;
		panVector.y = (topIntensity - bottomIntensity) * baseStrength;

		// Scale by MaxPanDistance
		Vector2 scaledPan = panVector * MaxPanDistance;

		// Clamp the total pan distance to MaxPanDistance (prevents over-panning in corners)
		scaledPan = scaledPan.ClampLength( MaxPanDistance );

		// Convert to 3D offset
		targetOffset = new Vector3( scaledPan.x, scaledPan.y, 0 );
	}
}

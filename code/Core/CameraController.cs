using Sandbox;
using System;

public sealed class CameraController : Component
{
	[Property, Group( "Debug" )] public bool ShowDebug { get; set; } = false;
	[Property, Group( "Debug" )] public float DebugPointSize { get; set; } = 2f;

	private Vector3 debugPieceWorldPos;
	private float debugWorldMaxFlickDistance;

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

				// Draw a rect at each point
				float halfSize = DebugPointSize / 2f;
				Gizmo.Draw.ScreenRect( new Rect( screenPoint.x - halfSize, screenPoint.y - halfSize, DebugPointSize, DebugPointSize ), Color.Cyan );
			}
		}

		// Placeholder for future debug text
		Gizmo.Draw.ScreenText( $"Debug Info", new Vector2( 10, 100 ), "roboto", 14f, TextFlag.Left );
	}

	public void UpdatePan( Vector2 cursorPosition, Vector3 piecePosition, bool isDragging, float worldMaxFlickDistance )
	{
		if ( !isDragging )
		{
			debugPieceWorldPos = Vector3.Zero;
			debugWorldMaxFlickDistance = 0;
			return;
		}

		// Store debug data for visualization
		debugPieceWorldPos = piecePosition;
		debugWorldMaxFlickDistance = worldMaxFlickDistance;

		// Camera panning logic will go here
	}
}

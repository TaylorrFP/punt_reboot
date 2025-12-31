using Sandbox;
using System;

public sealed class PlayerController : Component
{
	[Property, Sync] public TeamSide Team { get; set; }

	[Property, Group( "Flick Settings" )] public float FlickStrength { get; set; } = 0.6f;
	[Property, Group( "Flick Settings" )] public float MaxFlickStrength { get; set; } = 650f;
	[Property, Group( "Flick Settings" )] public float MinFlickDistance { get; set; } = 50f;
	[Property, Group( "Flick Settings" )] public float MaxFlickDistance { get; set; } = 650f;

	[Property, Group( "Debug" )] public bool isDebug { get; set; } = false;

	[Property, Group( "Cursor" )] public float EdgeThreshold { get; set; } = 10f;
	[Property, Group( "Cursor" )] public float FakeCursorSize { get; set; } = 10f;

	// === Interaction State ===
	private ISelectable hoveredSelectable;
	private ISelectable selectedSelectable;
	private Vector3 currentWorldPosition;

	// === Flick Tracking ===
	private Vector2 mouseOffset;
	private Vector2 initialScreenPosition;
	private Vector3 flickVector;
	private float lastCursorDelta;

	// === Virtual Cursor System ===
	[Property, ReadOnly] public bool isCursorHidden { get; private set; } = false;
	private Vector2 virtualCursorPosition;

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		UpdateVirtualCursor();
		UpdateCursorPosition();

		if ( selectedSelectable != null )
		{
			UpdateSelected();
		}
		else
		{
			UpdateHovering();
		}

		UpdateCursorVisuals();
		DrawFakeCursor();
	}

	#region Virtual Cursor System

	/// <summary>
	/// Manages the virtual cursor that appears when the real cursor hits screen edges.
	/// Hides real cursor at edge, shows fake cursor, unhides when moving back inward.
	/// </summary>
	private void UpdateVirtualCursor()
	{
		Vector2 mousePos = Mouse.Position;
		Vector2 screenSize = new Vector2( Screen.Width, Screen.Height );

		if ( !isCursorHidden )
		{
			// Check if cursor touched any screen edge
			bool hitEdge = mousePos.x <= EdgeThreshold ||
						  mousePos.x >= screenSize.x - EdgeThreshold ||
						  mousePos.y <= EdgeThreshold ||
						  mousePos.y >= screenSize.y - EdgeThreshold;

			if ( hitEdge )
			{
				// Hide real cursor and initialize virtual cursor at clamped edge position
				Mouse.Visibility = MouseVisibility.Hidden;
				virtualCursorPosition = ClampToScreen( mousePos, screenSize );
				isCursorHidden = true;
			}
		}
		else
		{
			// Virtual cursor is active - track movement but keep clamped to screen
			virtualCursorPosition += Mouse.Delta;
			virtualCursorPosition = ClampToScreen( virtualCursorPosition, screenSize );

			// Check if virtual cursor has moved away from ALL edges (back into safe zone)
			bool awayFromAllEdges = virtualCursorPosition.x > EdgeThreshold &&
								   virtualCursorPosition.x < screenSize.x - EdgeThreshold &&
								   virtualCursorPosition.y > EdgeThreshold &&
								   virtualCursorPosition.y < screenSize.y - EdgeThreshold;

			if ( awayFromAllEdges )
			{
				// Unhide real cursor at the virtual cursor's position
				Mouse.Position = virtualCursorPosition;
				Mouse.Visibility = MouseVisibility.Visible;
				isCursorHidden = false;
			}
		}
	}

	/// <summary>
	/// Clamps a screen position to be within screen bounds.
	/// </summary>
	private Vector2 ClampToScreen( Vector2 position, Vector2 screenSize )
	{
		return new Vector2(
			Math.Clamp( position.x, 0, screenSize.x ),
			Math.Clamp( position.y, 0, screenSize.y )
		);
	}

	/// <summary>
	/// Draws the fake cursor when the real cursor is hidden.
	/// </summary>
	private void DrawFakeCursor()
	{
		if ( !isCursorHidden ) return;

		// Fake cursor is always drawn at the clamped virtual position (at screen edge)
		Gizmo.Draw.Color = Color.White;
		float halfSize = FakeCursorSize / 2f;
		Gizmo.Draw.ScreenRect( new Rect(
			virtualCursorPosition.x - halfSize,
			virtualCursorPosition.y - halfSize,
			FakeCursorSize,
			FakeCursorSize
		), Color.Black );
	}

	#endregion

	#region Cursor Position & World Tracking

	/// <summary>
	/// Unprojects cursor position to world space on the pitch (Z=0 plane).
	/// </summary>
	private void UpdateCursorPosition()
	{
		var camera = Scene.Camera;
		var ray = camera.ScreenPixelToRay( Mouse.Position );

		// Intersect ray with Z=0 plane: t = -Origin.z / Direction.z
		float t = -ray.Position.z / ray.Forward.z;
		currentWorldPosition = ray.Position + ray.Forward * t;
		currentWorldPosition = currentWorldPosition.WithZ( 0f );
	}

	#endregion

	#region Selection & Interaction

	private void UpdateHovering()
	{
		var nearest = FindNearestSelectable();

		// Handle hover transitions
		if ( nearest != hoveredSelectable )
		{
			hoveredSelectable?.OnHoverExit();
			hoveredSelectable = nearest;
			hoveredSelectable?.OnHoverEnter();
		}

		// Check for click
		if ( Input.Pressed( "attack1" ) && hoveredSelectable != null && hoveredSelectable.CanSelect )
		{
			SelectTarget( hoveredSelectable );
		}
	}

	private void UpdateSelected()
	{
		// Check for abort (right-click)
		if ( Input.Pressed( "attack2" ) )
		{
			AbortTarget();
			return;
		}

		// For draggable selectables (pieces), calculate flick vector from accumulated mouse offset
		if ( selectedSelectable.CapturesSelection )
		{
			// Accumulate raw mouse delta (works even when cursor is at screen edge)
			Vector2 delta = Mouse.Delta;
			lastCursorDelta = delta.Length;
			mouseOffset += delta;

			// Determine effective cursor position (use virtual cursor if real cursor is hidden)
			Vector2 effectiveCursorPosition = isCursorHidden ? virtualCursorPosition : Mouse.Position;

			// Unproject effective cursor position to world space
			var camera = Scene.Camera;
			var ray = camera.ScreenPixelToRay( effectiveCursorPosition );
			float t = -ray.Position.z / ray.Forward.z;
			Vector3 worldPosition = ray.Position + ray.Forward * t;

			// Calculate flick vector (from piece to cursor)
			flickVector = (selectedSelectable.SelectPosition - worldPosition).WithZ( 0 );
			flickVector *= FlickStrength;
			flickVector = flickVector.ClampLength( MaxFlickDistance );

			// Calculate drag info for feedback
			float intensity = flickVector.Length / MaxFlickDistance;
			bool exceedsMinimum = flickVector.Length >= MinFlickDistance;

			// Calculate clamped cursor position for aim indicator
			Vector3 pieceToCursor = worldPosition - selectedSelectable.SelectPosition;
			pieceToCursor = pieceToCursor.WithZ( 0 );
			Vector3 clampedOffset = pieceToCursor.ClampLength( MaxFlickDistance );
			Vector3 clampedCursorPosition = selectedSelectable.SelectPosition + clampedOffset;

			// Update the selectable
			selectedSelectable.OnDragUpdate( intensity, lastCursorDelta, clampedCursorPosition, exceedsMinimum );

			// Debug visualization
			if ( isDebug )
			{
				DrawFlickDebug( worldPosition );
			}
		}

		// Check for release
		if ( Input.Released( "attack1" ) )
		{
			ReleaseTarget();
		}
	}

	private void SelectTarget( ISelectable target )
	{
		selectedSelectable = target;
		selectedSelectable.OnSelect();

		// Clear hover state
		hoveredSelectable?.OnHoverExit();
		hoveredSelectable = null;

		// Initialize flick tracking
		initialScreenPosition = Mouse.Position;
		mouseOffset = Vector2.Zero;
		flickVector = Vector3.Zero;
		lastCursorDelta = 0f;

		// Non-capturing selectables (props) release immediately
		if ( !selectedSelectable.CapturesSelection )
		{
			selectedSelectable = null;
		}
	}

	private void ReleaseTarget()
	{
		// Check if flick meets minimum distance threshold
		if ( flickVector.Length < MinFlickDistance )
		{
			AbortTarget();
			return;
		}

		selectedSelectable?.OnDeselect( flickVector );
		ResetSelectionState();
	}

	private void AbortTarget()
	{
		selectedSelectable?.OnAbort();
		ResetSelectionState();
	}

	private void ResetSelectionState()
	{
		selectedSelectable = null;
		flickVector = Vector3.Zero;
		mouseOffset = Vector2.Zero;
		lastCursorDelta = 0f;
	}

	private ISelectable FindNearestSelectable()
	{
		// Gather all selectables
		var selectables = new List<ISelectable>();
		selectables.AddRange( Scene.GetAllComponents<PuntPiece>() );
		selectables.AddRange( Scene.GetAllComponents<ClickableProp>() );

		// Find best valid target
		ISelectable best = null;
		float bestScore = float.MaxValue;

		foreach ( var selectable in selectables )
		{
			if ( !IsValidTarget( selectable ) ) continue;

			float dist = (selectable.SelectPosition - currentWorldPosition).WithZ( 0 ).Length;
			if ( dist > selectable.SelectRadius ) continue;

			// Lower score = better (distance minus priority bonus)
			float score = dist - selectable.SelectPriority;
			if ( score < bestScore )
			{
				bestScore = score;
				best = selectable;
			}
		}

		return best;
	}

	private bool IsValidTarget( ISelectable selectable )
	{
		// Filter out enemy pieces
		if ( selectable is PuntPiece piece )
		{
			return piece.Team == Team;
		}

		// Props are always valid
		return true;
	}

	#endregion

	#region Cursor Visuals

	private void UpdateCursorVisuals()
	{
		if ( selectedSelectable != null )
		{
			Mouse.CursorType = "grabbing";
		}
		else if ( hoveredSelectable == null )
		{
			Mouse.CursorType = "pointer";
		}
		else if ( !hoveredSelectable.CanSelect )
		{
			Mouse.CursorType = "not-allowed";
		}
		else
		{
			Mouse.CursorType = "pointer";
		}
	}

	#endregion

	#region Debug Visualization

	private void DrawFlickDebug( Vector3 worldPosition )
	{
		// Cyan sphere: unprojected world position
		Gizmo.Draw.Color = Color.Cyan;
		Gizmo.Draw.SolidSphere( worldPosition, 5f, 16, 16 );

		// White line: flick vector direction
		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.Line( selectedSelectable.SelectPosition, selectedSelectable.SelectPosition + (flickVector * -1f) );

		// Red circle: minimum threshold
		Gizmo.Draw.Color = Color.Red;
		Gizmo.Draw.LineCircle( selectedSelectable.SelectPosition, Vector3.Up, MinFlickDistance, 0, 360, 64 );

		// Green circle: maximum threshold
		Gizmo.Draw.Color = Color.Green;
		Gizmo.Draw.LineCircle( selectedSelectable.SelectPosition, Vector3.Up, MaxFlickDistance / FlickStrength, 0, 360, 512 );

		// Black text: flick distance
		Gizmo.Draw.Color = Color.Black;
		Gizmo.Draw.ScreenText( flickVector.Length.ToString( "F1" ), initialScreenPosition + Vector2.Up * 20, "roboto", 16f );
	}

	#endregion
}

using Sandbox;
using System;
using System.Diagnostics;
public sealed class PlayerController : Component
{
	[Property, Sync] public TeamSide Team { get; set; }

	[Property, Group( "Flick Settings" )] public float FlickStrength { get; set; } = 0.6f;
	[Property, Group( "Flick Settings" )] public float MaxFlickStrength { get; set; } = 650f;
	[Property, Group( "Flick Settings" )] public float MinFlickDistance { get; set; } = 50f;
	[Property, Group( "Flick Settings" )] public float MaxFlickDistance { get; set; } = 650f;

	[Property, Group( "Debug" )] public bool isDebug { get; set; } = false;

	[Property, Group( "Cursor" )] public float EdgeThreshold { get; set; } = 5f; // Pixels from edge before hiding cursor
	[Property, Group( "Cursor" )] public float FakeCursorSize { get; set; } = 12f; // Size of fake cursor square
	[Property, Group( "Cursor" )] public Color FakeCursorColor { get; set; } = Color.White;

	// === Local State ===
	private ISelectable hoveredSelectable;
	private ISelectable selectedSelectable;
	private Vector2 mouseOffset; // Accumulated mouse movement since selection
	private Vector2 initialScreenPosition; // Screen position when piece was selected
	private Vector3 flickVector;
	private float lastCursorDelta;
	private Vector3 currentWorldPosition; // Current cursor world position (updated every frame)

	// === Cursor Hiding (for off-screen tracking) ===
	private bool isCursorHidden;
	private Vector2 clampedScreenPosition; // Where to draw fake cursor when hidden

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		UpdateCursorPosition();

		if ( selectedSelectable != null )
		{
			UpdateSelected();
		}
		else
		{
			UpdateHovering();
		}

		UpdateCursor();
		DrawFakeCursor();
	}

	private void UpdateCursorPosition()
	{
		// Unproject current mouse position to world space at Z=0 (the pitch plane)
		var camera = Scene.Camera;
		var ray = camera.ScreenPixelToRay( Mouse.Position );

		// Intersect ray with Z=0 plane to get world position
		// Ray equation: P = Origin + t * Direction
		// For Z=0 plane: Origin.z + t * Direction.z = 0
		// Solve for t: t = -Origin.z / Direction.z
		float t = -ray.Position.z / ray.Forward.z;
		currentWorldPosition = ray.Position + ray.Forward * t;
		currentWorldPosition = currentWorldPosition.WithZ( 0f );
	}

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
			// Track cursor movement this frame
			lastCursorDelta = Mouse.Delta.Length;

			// === SMART CURSOR RESET FOR OFF-SCREEN MOVEMENT ===
			// Calculate where we would be with normal accumulation
			Vector2 tentativeOffset = mouseOffset + Mouse.Delta;
			Vector2 tentativePosition = initialScreenPosition + tentativeOffset;

			// If cursor is currently hidden (off-screen), check movement direction
			if ( isCursorHidden )
			{
				// Check if the movement brings us closer to the screen
				float currentDist = GetDistanceFromScreen( initialScreenPosition + mouseOffset );
				float newDist = GetDistanceFromScreen( tentativePosition );

				if ( newDist < currentDist ) // Moving TOWARDS screen
				{
					// "Teleport" cursor to edge so it immediately responds
					// This prevents having to drag all the way back from off-screen
					mouseOffset = clampedScreenPosition - initialScreenPosition;
					Mouse.Position = clampedScreenPosition;

					// Note: currentScreenPosition will be recalculated below
				}
				else // Moving AWAY from screen
				{
					// Normal accumulation - continue building flick distance
					mouseOffset = tentativeOffset;
				}
			}
			else
			{
				// When cursor is visible, always accumulate normally
				mouseOffset = tentativeOffset;
			}

			// Calculate the new screen position (initial position + accumulated offset)
			Vector2 currentScreenPosition = initialScreenPosition + mouseOffset;

			// === EDGE DETECTION & CURSOR HIDING ===
			Vector2 screenSize = new Vector2( Screen.Width, Screen.Height );

			// Check if we're at or beyond the screen edge (with threshold)
			bool atEdge = currentScreenPosition.x <= EdgeThreshold ||
						  currentScreenPosition.x >= screenSize.x - EdgeThreshold ||
						  currentScreenPosition.y <= EdgeThreshold ||
						  currentScreenPosition.y >= screenSize.y - EdgeThreshold;

			// Hide cursor when at edge so Mouse.Delta continues working off-screen
			if ( atEdge && !isCursorHidden )
			{
				Mouse.Visibility = MouseVisibility.Hidden;
				isCursorHidden = true;
			}
			// Restore cursor when back on screen
			else if ( !atEdge && isCursorHidden )
			{
				Mouse.Visibility = MouseVisibility.Visible;
				isCursorHidden = false;
			}

			// Calculate fake cursor position as intersection of piece-to-cursor line with screen edge
			// This keeps the fake cursor aligned with the aim indicator direction
			var camera = Scene.Camera;
			Vector2 pieceScreenPosition = camera.PointToScreenPixels( selectedSelectable.SelectPosition );
			clampedScreenPosition = GetScreenEdgeIntersection( pieceScreenPosition, currentScreenPosition );

			var ray = camera.ScreenPixelToRay( currentScreenPosition );

			// Intersect ray with Z=0 plane to get world position
			// Ray equation: P = Origin + t * Direction
			// For Z=0 plane: Origin.z + t * Direction.z = 0
			// Solve for t: t = -Origin.z / Direction.z
			float t = -ray.Position.z / ray.Forward.z;
			Vector3 worldPosition = ray.Position + ray.Forward * t;

			// Calculate flick vector from piece to this world position
			flickVector = (selectedSelectable.SelectPosition - worldPosition).WithZ( 0 );

			// Apply strength multiplier for tuning
			flickVector *= FlickStrength;

			// Clamp to max distance
			flickVector = flickVector.ClampLength( MaxFlickDistance );

			// Calculate intensity (0-1) based on flick strength
			float intensity = flickVector.Length / MaxFlickDistance;

			// Check if we've exceeded the minimum threshold
			bool exceedsMinimum = flickVector.Length >= MinFlickDistance;

			// Calculate clamped cursor position for aim indicator
			// Use worldPosition (unprojected) for perfect consistency with flick vector
			Vector3 pieceToCursor = worldPosition - selectedSelectable.SelectPosition;
			pieceToCursor = pieceToCursor.WithZ( 0 ); // Keep on 2D plane
			Vector3 clampedOffset = pieceToCursor.ClampLength( MaxFlickDistance );
			Vector3 clampedCursorPosition = selectedSelectable.SelectPosition + clampedOffset;

			// Tell the selectable about the ongoing drag (with clamped cursor position)
			// Pass exceedsMinimum so the aim indicator can show/hide based on threshold
			selectedSelectable.OnDragUpdate( intensity, lastCursorDelta, clampedCursorPosition, exceedsMinimum );

			if ( isDebug )
			{
				// Cyan sphere: worldPosition (unprojected from accumulated mouse offset)
				// Continues tracking even off-screen - this is what the flick uses!
				Gizmo.Draw.Color = Color.Cyan;
				Gizmo.Draw.SolidSphere( worldPosition, 5f, 16, 16 );

				// White line: Actual flick vector direction
				Gizmo.Draw.Color = Color.White;
				Gizmo.Draw.Line( selectedSelectable.SelectPosition, selectedSelectable.SelectPosition + (flickVector * -1f) );

				// Red circle: Min threshold
				Gizmo.Draw.Color = Color.Red;
				Gizmo.Draw.LineCircle( selectedSelectable.SelectPosition, Vector3.Up, MinFlickDistance, 0, 360, 64 );

				// Green circle: Max threshold (in drag distance, not flick power)
				Gizmo.Draw.Color = Color.Green;
				Gizmo.Draw.LineCircle( selectedSelectable.SelectPosition, Vector3.Up, MaxFlickDistance / FlickStrength, 0, 360, 512 );

				//Black text: Flick distance
				Gizmo.Draw.Color = Color.Black;
				Gizmo.Draw.ScreenText( flickVector.Length.ToString(), initialScreenPosition + Vector2.Up * 20, "roboto", 16f );
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

		// Clear hover
		hoveredSelectable?.OnHoverExit();
		hoveredSelectable = null;

		// CRITICAL FIX: Store the cursor's ACTUAL screen position when clicking
		// Not the piece's position! The cursor could be offset from piece center.
		initialScreenPosition = Mouse.Position;

		// Reset flick tracking
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
		// Restore cursor visibility if it was hidden
		RestoreCursor();

		// Check if flick meets minimum distance threshold
		float flickDistance = flickVector.Length;
		if ( flickDistance < MinFlickDistance )
		{
			// Below threshold - abort instead of applying flick
			AbortTarget();
			return;
		}

		selectedSelectable?.OnDeselect( flickVector );
		selectedSelectable = null;
		flickVector = Vector3.Zero;
		mouseOffset = Vector2.Zero;
		lastCursorDelta = 0f;
	}

	private void AbortTarget()
	{
		// Restore cursor visibility if it was hidden
		RestoreCursor();

		selectedSelectable?.OnAbort();
		selectedSelectable = null;
		flickVector = Vector3.Zero;
		mouseOffset = Vector2.Zero;
		lastCursorDelta = 0f;
	}

	private void RestoreCursor()
	{
		if ( isCursorHidden )
		{
			Mouse.Visibility = MouseVisibility.Visible;
			isCursorHidden = false;
		}
	}

	private void DrawFakeCursor()
	{
		// Only draw fake cursor when it's hidden and we're dragging
		if ( !isCursorHidden || selectedSelectable == null ) return;

		// Draw a simple square cursor at the clamped screen position
		float halfSize = FakeCursorSize / 2f;
		Rect cursorRect = new Rect(
			clampedScreenPosition.x - halfSize,
			clampedScreenPosition.y - halfSize,
			FakeCursorSize,
			FakeCursorSize
		);

		Gizmo.Draw.Color = FakeCursorColor;
		Gizmo.Draw.ScreenRect( cursorRect, Color.White );
	}

	private ISelectable FindNearestSelectable()
	{
		// Build a list of all selectables from known types
		var selectables = new List<ISelectable>();

		// Add all pieces
		selectables.AddRange( Scene.GetAllComponents<PuntPiece>() );

		// Add all props (includes inherited types like ClickableDuck)
		selectables.AddRange( Scene.GetAllComponents<ClickableProp>() );

		// Filter to valid targets
		var validSelectables = selectables.Where( s => IsValidTarget( s ) );

		ISelectable best = null;
		float bestScore = float.MaxValue;

		foreach ( var selectable in validSelectables )
		{
			var dist = (selectable.SelectPosition - currentWorldPosition).WithZ( 0 ).Length;

			// Must be within select radius
			if ( dist > selectable.SelectRadius ) continue;

			// Score: lower is better (distance minus priority)
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

	/// <summary>
	/// Calculate how far off-screen a position is.
	/// Returns 0 if on-screen, otherwise the maximum distance past any edge.
	/// </summary>
	private float GetDistanceFromScreen( Vector2 position )
	{
		Vector2 screenSize = new Vector2( Screen.Width, Screen.Height );

		// Distance past each edge (0 if not past that edge)
		float leftDist = Math.Max( 0, -position.x );
		float rightDist = Math.Max( 0, position.x - screenSize.x );
		float topDist = Math.Max( 0, -position.y );
		float bottomDist = Math.Max( 0, position.y - screenSize.y );

		// Return the maximum off-screen distance
		return Math.Max( Math.Max( leftDist, rightDist ), Math.Max( topDist, bottomDist ) );
	}

	/// <summary>
	/// Find where a line from start to end intersects the screen rectangle.
	/// Returns the intersection point closest to start.
	/// </summary>
	private Vector2 GetScreenEdgeIntersection( Vector2 start, Vector2 end )
	{
		Vector2 screenSize = new Vector2( Screen.Width, Screen.Height );
		Vector2 direction = end - start;

		float closestT = float.MaxValue;
		Vector2 closestPoint = end; // Default to end if no intersection found

		// Check intersection with each screen edge
		// Top edge (y = 0)
		if ( direction.y != 0 )
		{
			float t = (0 - start.y) / direction.y;
			if ( t >= 0 && t <= 1 )
			{
				float x = start.x + t * direction.x;
				if ( x >= 0 && x <= screenSize.x && t < closestT )
				{
					closestT = t;
					closestPoint = new Vector2( x, 0 );
				}
			}
		}

		// Bottom edge (y = height)
		if ( direction.y != 0 )
		{
			float t = (screenSize.y - start.y) / direction.y;
			if ( t >= 0 && t <= 1 )
			{
				float x = start.x + t * direction.x;
				if ( x >= 0 && x <= screenSize.x && t < closestT )
				{
					closestT = t;
					closestPoint = new Vector2( x, screenSize.y );
				}
			}
		}

		// Left edge (x = 0)
		if ( direction.x != 0 )
		{
			float t = (0 - start.x) / direction.x;
			if ( t >= 0 && t <= 1 )
			{
				float y = start.y + t * direction.y;
				if ( y >= 0 && y <= screenSize.y && t < closestT )
				{
					closestT = t;
					closestPoint = new Vector2( 0, y );
				}
			}
		}

		// Right edge (x = width)
		if ( direction.x != 0 )
		{
			float t = (screenSize.x - start.x) / direction.x;
			if ( t >= 0 && t <= 1 )
			{
				float y = start.y + t * direction.y;
				if ( y >= 0 && y <= screenSize.y && t < closestT )
				{
					closestT = t;
					closestPoint = new Vector2( screenSize.x, y );
				}
			}
		}

		return closestPoint;
	}

	private void UpdateCursor()
	{
		// Don't change cursor type when it's hidden (we're off-screen)
		if ( isCursorHidden ) return;

		// Grabbing takes priority
		if ( selectedSelectable != null )
		{
			Mouse.CursorType = "grabbing";
			return;
		}

		// Nothing hovered
		if ( hoveredSelectable == null )
		{
			Mouse.CursorType = "pointer";
			return;
		}

		// Hovering something we can't select
		if ( !hoveredSelectable.CanSelect )
		{
			Mouse.CursorType = "not-allowed";
			return;
		}

		// Hovering something selectable
		Mouse.CursorType = "pointer";
	}
}

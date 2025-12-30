using Sandbox;
using System.Diagnostics;
public sealed class PlayerController : Component
{
	[Property, Sync] public TeamSide Team { get; set; }

	[Property, Group( "Flick Settings" )] public float FlickStrength { get; set; } = 0.6f;
	[Property, Group( "Flick Settings" )] public float MaxFlickStrength { get; set; } = 650f;
	[Property, Group( "Flick Settings" )] public float MinFlickDistance { get; set; } = 50f;
	[Property, Group( "Flick Settings" )] public float MaxFlickDistance { get; set; } = 650f;

	[Property, Group( "Debug" )] public bool isDebug { get; set; } = false;

	// === Local State ===
	private ISelectable hoveredSelectable;
	private ISelectable selectedSelectable;
	private Vector2 mouseOffset; // Accumulated mouse movement since selection
	private Vector2 initialScreenPosition; // Screen position when piece was selected
	private Vector3 flickVector;
	private float lastCursorDelta;
	private Vector3 currentWorldPosition; // Current cursor world position (updated every frame)

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

			// Accumulate mouse offset in screen space (works even if cursor leaves screen)
			mouseOffset += Mouse.Delta;

			// Calculate the new screen position (initial position + accumulated offset)
			Vector2 currentScreenPosition = initialScreenPosition + mouseOffset;

			// Unproject this screen position back to world space at Z=0 (the pitch plane)
			var camera = Scene.Camera;
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

				// Green circle: Max threshold
				Gizmo.Draw.Color = Color.Green;
				Gizmo.Draw.LineCircle( selectedSelectable.SelectPosition, Vector3.Up, MaxFlickDistance, 0, 360, 512 );

				//White text: Flick distance
				Gizmo.Draw.Color = Color.Black;
				Gizmo.Draw.ScreenText( flickVector.Length.ToString(), initialScreenPosition + Vector2.Up*20,"roboto",16f);
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
		selectedSelectable?.OnAbort();
		selectedSelectable = null;
		flickVector = Vector3.Zero;
		mouseOffset = Vector2.Zero;
		lastCursorDelta = 0f;
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

	private void UpdateCursor()
	{
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

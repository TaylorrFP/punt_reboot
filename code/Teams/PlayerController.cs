using Sandbox;
public sealed class PlayerController : Component
{
	[Property, Sync] public TeamSide Team { get; set; }
	[Property, Sync] public Vector3 CursorWorldPosition { get; private set; }

	[Property, Group( "Flick Settings" )] public float FlickStrength { get; set; } = 0.6f;
	[Property, Group( "Flick Settings" )] public float MaxFlickStrength { get; set; } = 650f;

	// === Local State ===
	private ISelectable hoveredSelectable;
	private ISelectable selectedSelectable;
	private Vector2 currentMouseOffset;
	private Vector3 flickVector;
	private float lastCursorDelta;

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
		var camera = Scene.Camera;
		var ray = camera.ScreenPixelToRay( Mouse.Position );
		var tr = Scene.Trace.Ray( ray, 10000f )
			.WithAllTags( "pitch" )
			.Run();

		if ( tr.Hit )
		{
			CursorWorldPosition = tr.HitPosition.WithZ( 0f );
		}
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
		// For draggable selectables (pieces), calculate flick vector
		if ( selectedSelectable.CapturesSelection )
		{
			// Track cursor movement this frame
			lastCursorDelta = Mouse.Delta.Length;

			currentMouseOffset += Mouse.Delta;
			flickVector = new Vector3( -currentMouseOffset.x, currentMouseOffset.y, 0f ) * FlickStrength;
			flickVector = flickVector.ClampLength( MaxFlickStrength );

			// Calculate intensity (0-1) based on flick strength
			float intensity = flickVector.Length / MaxFlickStrength;

			// Tell the selectable about the ongoing drag
			selectedSelectable.OnDragUpdate( intensity, lastCursorDelta );
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

		// Reset flick tracking
		currentMouseOffset = Vector2.Zero;
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
		selectedSelectable?.OnDeselect( flickVector );
		selectedSelectable = null;
		flickVector = Vector3.Zero;
		currentMouseOffset = Vector2.Zero;
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
			var dist = (selectable.SelectPosition - CursorWorldPosition).WithZ( 0 ).Length;

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

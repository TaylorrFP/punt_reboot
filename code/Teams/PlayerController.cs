using Sandbox;
public sealed class PlayerController : Component
{
	[Property, Sync] public TeamSide Team { get; set; }
	[Property, Sync] public Vector3 CursorWorldPosition { get; private set; }

	[Property] public float FlickStrength { get; set; } = 0.6f;
	[Property] public float MaxFlickStrength { get; set; } = 650f;

	// === Local State ===
	private ISelectable hoveredSelectable;
	private ISelectable selectedSelectable;
	private Vector2 currentMouseOffset;
	private Vector3 flickVector;

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
			Gizmo.Draw.SolidSphere(tr.HitPosition, 5f,32);
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

		// Update cursor
		UpdateCursor( hoveredSelectable );

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
			currentMouseOffset += Mouse.Delta;
			flickVector = new Vector3( -currentMouseOffset.x, currentMouseOffset.y, 0f ) * FlickStrength;
			flickVector = flickVector.ClampLength( MaxFlickStrength );
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
	}

	private ISelectable FindNearestSelectable()
	{
		// Find all selectables in scene
		var allSelectables = Scene.GetAllComponents<Component>()
			.OfType<ISelectable>()
			.Where( s => IsValidTarget( s ) );

		ISelectable best = null;
		float bestScore = float.MaxValue;


		Log.Info(allSelectables.Count() + " selectables in scene");

		foreach ( var selectable in allSelectables )
		{
			var dist = (selectable.SelectPosition - CursorWorldPosition).WithZ( 0 ).Length;

			// Must be within select radius
			if ( dist > selectable.SelectRadius ) continue;

			// Score: lower is better (distance minus priority)
			// Higher priority = lower score = selected first
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

	private void UpdateCursor( ISelectable target )
	{
		if ( target == null )
		{
			Mouse.CursorType = "default";
		}
		else if ( !target.CanSelect )
		{
			Mouse.CursorType = "not-allowed";
		}
		else if ( target is PuntPiece )
		{
			Mouse.CursorType = "pointer";
		}
		else
		{
			Mouse.CursorType = "pointer";
		}
	}
}

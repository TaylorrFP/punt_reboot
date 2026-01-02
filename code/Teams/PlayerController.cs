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

	[Property, Group( "Virtual Cursor" )] public bool ShowRealCursor { get; set; } = false;
	[Property, Group( "Virtual Cursor" )] public bool ShowCursor { get; set; } = true;
	[Property, Group( "Virtual Cursor" )] public float Sensitivity { get; set; } = 1.0f;

	// === Interaction State ===
	private ISelectable hoveredSelectable;
	private ISelectable selectedSelectable;
	private Vector3 currentWorldPosition;

	// === Flick Tracking ===
	private Vector3 flickVector;
	private float lastCursorDelta;

	// === Virtual Cursor ===
	private Vector2 cursorPosition;
	private bool initialized = false;


	

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		UpdateCursor();
		UpdateWorldPosition();

		if ( selectedSelectable != null )
		{
			UpdateSelected();
		}
		else
		{
			UpdateHovering();
		}

		UpdateCursorVisuals();
		UpdateCursorPanel();
		DrawDebug();


		if ( Input.EscapePressed )
		{
			Mouse.Position = cursorPosition;
			Mouse.Visibility = MouseVisibility.Visible;

		}
		//This actually works - can we just do this every time we need to 

	}

	private void DrawDebug()
	{
		// Draw cursor position rect
		float cursorDebugSize = 8f;
		Gizmo.Draw.ScreenRect( new Rect(
			cursorPosition.x - cursorDebugSize / 2f,
			cursorPosition.y - cursorDebugSize / 2f,
			cursorDebugSize,
			cursorDebugSize
		), Color.Magenta );

	}

	#region Virtual Cursor

	private void UpdateCursor()
	{

		
		// Initialize cursor position on first frame
		if ( !initialized )
		{
			cursorPosition = Mouse.Position;
			initialized = true;
		}

		// Always update via delta
		cursorPosition += Mouse.Delta;

		// Clamp to screen bounds (allows sliding along edges)
		Vector2 screenSize = new Vector2( Screen.Width, Screen.Height );
		cursorPosition = new Vector2(
			Math.Clamp( cursorPosition.x, 0, screenSize.x ),
			Math.Clamp( cursorPosition.y, 0, screenSize.y )
		);

		// Control real cursor visibility
		Mouse.Visibility = ShowRealCursor ? MouseVisibility.Visible : MouseVisibility.Hidden;
	}

	private void UpdateCursorPanel()
	{

		Hud.Instance?.UpdateCursorPosition( cursorPosition );
	}

	#endregion

	#region World Position

	private void UpdateWorldPosition()
	{
		var camera = Scene.Camera;
		var ray = camera.ScreenPixelToRay( cursorPosition );

		// Intersect with Z=0 plane
		float t = -ray.Position.z / ray.Forward.z;
		currentWorldPosition = ray.Position + ray.Forward * t;
		currentWorldPosition = currentWorldPosition.WithZ( 0f );
	}

	#endregion

	#region Selection & Interaction

	private void UpdateHovering()
	{
		var nearest = FindNearestSelectable();

		if ( nearest != hoveredSelectable )
		{
			hoveredSelectable?.OnHoverExit();
			hoveredSelectable = nearest;
			hoveredSelectable?.OnHoverEnter();
		}

		if ( Input.Pressed( "attack1" ) && hoveredSelectable != null && hoveredSelectable.CanSelect )
		{
			SelectTarget( hoveredSelectable );
		}
	}

	private void UpdateSelected()
	{
		if ( Input.Pressed( "attack2" ) )
		{
			AbortTarget();
			return;
		}

		if ( selectedSelectable.CapturesSelection )
		{
			lastCursorDelta = Mouse.Delta.Length;

			// Calculate flick vector
			flickVector = (selectedSelectable.SelectPosition - currentWorldPosition).WithZ( 0 );
			flickVector *= FlickStrength;
			flickVector = flickVector.ClampLength( MaxFlickDistance );

			// Calculate feedback data
			float intensity = flickVector.Length / MaxFlickDistance;
			bool exceedsMinimum = flickVector.Length >= MinFlickDistance;

			// Calculate clamped cursor position for aim indicator
			Vector3 pieceToCursor = currentWorldPosition - selectedSelectable.SelectPosition;
			pieceToCursor = pieceToCursor.WithZ( 0 );
			Vector3 clampedOffset = pieceToCursor.ClampLength( MaxFlickDistance );
			Vector3 clampedCursorPosition = selectedSelectable.SelectPosition + clampedOffset;

			selectedSelectable.OnDragUpdate( intensity, lastCursorDelta, clampedCursorPosition, exceedsMinimum );

			if ( isDebug )
			{
				DrawFlickDebug();
			}
		}

		if ( Input.Released( "attack1" ) )
		{
			ReleaseTarget();
		}
	}

	private void SelectTarget( ISelectable target )
	{
		selectedSelectable = target;
		selectedSelectable.OnSelect();

		hoveredSelectable?.OnHoverExit();
		hoveredSelectable = null;

		flickVector = Vector3.Zero;
		lastCursorDelta = 0f;

		if ( !selectedSelectable.CapturesSelection )
		{
			selectedSelectable = null;
		}
	}

	private void ReleaseTarget()
	{
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
		lastCursorDelta = 0f;
	}

	private ISelectable FindNearestSelectable()
	{
		var selectables = new List<ISelectable>();
		selectables.AddRange( Scene.GetAllComponents<PuntPiece>() );
		selectables.AddRange( Scene.GetAllComponents<ClickableProp>() );

		ISelectable best = null;
		float bestScore = float.MaxValue;

		foreach ( var selectable in selectables )
		{
			if ( !IsValidTarget( selectable ) ) continue;

			float dist = (selectable.SelectPosition - currentWorldPosition).WithZ( 0 ).Length;
			if ( dist > selectable.SelectRadius ) continue;

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
		if ( selectable is PuntPiece piece )
		{
			return piece.Team == Team;
		}

		return true;
	}

	#endregion

	#region Cursor Visuals

	private void UpdateCursorVisuals()
	{
		if ( Hud.Instance == null ) return;

		if ( selectedSelectable != null )
		{
			Mouse.CursorType = "grabbing";
			Hud.Instance.SetCursorState( CursorState.Grabbing );
		}
		else if ( hoveredSelectable == null )
		{
			Mouse.CursorType = "pointer";
			Hud.Instance.SetCursorState( CursorState.Default );
		}
		else if ( !hoveredSelectable.CanSelect )
		{
			Mouse.CursorType = "not-allowed";
			Hud.Instance.SetCursorState( CursorState.Disabled );
		}
		else
		{
			Mouse.CursorType = "pointer";
			Hud.Instance.SetCursorState( CursorState.Hover );
		}
	}

	#endregion

	#region Debug

	private void DrawFlickDebug()
	{
		Gizmo.Draw.Color = Color.Cyan;
		Gizmo.Draw.SolidSphere( currentWorldPosition, 16f, 16, 16 );

		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.Line( selectedSelectable.SelectPosition, selectedSelectable.SelectPosition + (flickVector * -1f) );

		Gizmo.Draw.Color = Color.Red;
		Gizmo.Draw.LineCircle( selectedSelectable.SelectPosition, Vector3.Up, MinFlickDistance, 0, 360, 64 );

		Gizmo.Draw.Color = Color.Green;
		Gizmo.Draw.LineCircle( selectedSelectable.SelectPosition, Vector3.Up, MaxFlickDistance / FlickStrength, 0, 360, 512 );

		Gizmo.Draw.Color = Color.Black;
		Gizmo.Draw.ScreenText( flickVector.Length.ToString( "F1" ), cursorPosition + Vector2.Down * 32, "roboto", 16f );
	}

	#endregion
}

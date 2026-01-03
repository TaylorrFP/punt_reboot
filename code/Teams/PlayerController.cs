using Sandbox;
using System;

/// <summary>
/// Handles player input for selecting and flicking pieces.
/// Manages virtual cursor, hover/selection states, and camera control.
/// </summary>
public sealed class PlayerController : Component
{
	#region Properties

	[Property, Sync] public TeamSide Team { get; set; }

	[Property, Group( "Flick Settings" )] public float FlickStrength { get; set; } = 0.6f;
	[Property, Group( "Flick Settings" )] public float MaxFlickStrength { get; set; } = 650f;
	[Property, Group( "Flick Settings" )] public float MinFlickDistance { get; set; } = 50f;
	[Property, Group( "Flick Settings" )] public float MaxFlickDistance { get; set; } = 650f;

	[Property, Group( "Cursor" )] public bool ShowRealCursor { get; set; } = false;
	[Property, Group( "Cursor" )] public bool ShowCursor { get; set; } = true;
	[Property, Group( "Cursor" )] public float Sensitivity { get; set; } = 1.0f;

	[Property, Group( "Debug" )] public bool ShowDebug { get; set; } = false;

	[Property] public CameraController CameraController { get; set; }

	#endregion

	#region Private State

	// Virtual cursor
	private Vector2 cursorPosition;
	private bool cursorInitialized;

	// World position of cursor
	private Vector3 worldCursorPosition;

	// Selection state
	private ISelectable hoveredSelectable;
	private ISelectable selectedSelectable;

	// Flick tracking
	private Vector3 flickVector;
	private float lastCursorDelta;

	#endregion

	#region Lifecycle

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		UpdateCursor();
		UpdateWorldPosition();

		if ( selectedSelectable != null )
		{
			UpdateSelection();
		}
		else
		{
			UpdateHover();
		}

		UpdateCamera();
		UpdateCursorVisuals();
		UpdateHud();

		HandleEscapeKey();

		if ( ShowDebug )
		{
			DrawDebug();
		}
	}

	#endregion

	#region Virtual Cursor

	private void UpdateCursor()
	{
		if ( !cursorInitialized )
		{
			cursorPosition = Mouse.Position;
			cursorInitialized = true;
		}

		cursorPosition += Mouse.Delta;

		// Clamp to screen bounds
		cursorPosition = new Vector2(
			Math.Clamp( cursorPosition.x, 0, Screen.Width ),
			Math.Clamp( cursorPosition.y, 0, Screen.Height )
		);

		Mouse.Visibility = ShowRealCursor ? MouseVisibility.Visible : MouseVisibility.Hidden;
	}

	private void UpdateWorldPosition()
	{
		var ray = Scene.Camera.ScreenPixelToRay( cursorPosition );

		// Intersect with Z=0 plane
		float t = -ray.Position.z / ray.Forward.z;
		worldCursorPosition = ray.Position + ray.Forward * t;
		worldCursorPosition = worldCursorPosition.WithZ( 0f );
	}

	private void HandleEscapeKey()
	{
		if ( Input.EscapePressed )
		{
			Mouse.Position = cursorPosition;
			Mouse.Visibility = MouseVisibility.Visible;
		}
	}

	#endregion

	#region Hover & Selection

	private void UpdateHover()
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

	private void UpdateSelection()
	{
		if ( Input.Pressed( "attack2" ) )
		{
			AbortSelection();
			return;
		}

		if ( selectedSelectable.CapturesSelection )
		{
			UpdateFlickVector();
		}

		if ( Input.Released( "attack1" ) )
		{
			ReleaseSelection();
		}
	}

	private void UpdateFlickVector()
	{
		lastCursorDelta = Mouse.Delta.Length;

		// Calculate flick vector from piece to cursor, scaled and clamped
		flickVector = (selectedSelectable.SelectPosition - worldCursorPosition).WithZ( 0 );
		flickVector *= FlickStrength;
		flickVector = flickVector.ClampLength( MaxFlickDistance );

		// Calculate feedback data for the selectable
		float intensity = flickVector.Length / MaxFlickDistance;
		bool exceedsMinimum = flickVector.Length >= MinFlickDistance;

		// Calculate clamped cursor position for aim indicator
		Vector3 pieceToCursor = worldCursorPosition - selectedSelectable.SelectPosition;
		pieceToCursor = pieceToCursor.WithZ( 0 );
		Vector3 clampedOffset = pieceToCursor.ClampLength( MaxFlickDistance );
		Vector3 clampedCursorPos = selectedSelectable.SelectPosition + clampedOffset;

		selectedSelectable.OnDragUpdate( intensity, lastCursorDelta, clampedCursorPos, exceedsMinimum );
	}

	private void SelectTarget( ISelectable target )
	{
		selectedSelectable = target;
		selectedSelectable.OnSelect();

		hoveredSelectable?.OnHoverExit();
		hoveredSelectable = null;

		flickVector = Vector3.Zero;
		lastCursorDelta = 0f;

		// Some selectables don't capture (e.g. instant-click buttons)
		if ( !selectedSelectable.CapturesSelection )
		{
			selectedSelectable = null;
		}
	}

	private void ReleaseSelection()
	{
		if ( flickVector.Length < MinFlickDistance )
		{
			AbortSelection();
			return;
		}

		selectedSelectable?.OnDeselect( flickVector );
		ClearSelectionState();
	}

	private void AbortSelection()
	{
		selectedSelectable?.OnAbort();
		ClearSelectionState();
	}

	private void ClearSelectionState()
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

			float dist = (selectable.SelectPosition - worldCursorPosition).WithZ( 0 ).Length;
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

	#region Camera

	private void UpdateCamera()
	{
		if ( CameraController == null ) return;

		bool isDragging = selectedSelectable != null && selectedSelectable.CapturesSelection;
		Vector3 piecePosition = isDragging ? selectedSelectable.SelectPosition : Vector3.Zero;
		float worldFlickRadius = isDragging ? MaxFlickDistance / FlickStrength : 0f;

		CameraController.UpdatePan( cursorPosition, piecePosition, isDragging, worldFlickRadius );
	}

	#endregion

	#region Cursor Visuals

	private void UpdateCursorVisuals()
	{
		if ( Hud.Instance == null ) return;

		CursorState state;

		if ( selectedSelectable != null )
		{
			Mouse.CursorType = "grabbing";
			state = CursorState.Grabbing;
		}
		else if ( hoveredSelectable == null )
		{
			Mouse.CursorType = "pointer";
			state = CursorState.Default;
		}
		else if ( !hoveredSelectable.CanSelect )
		{
			Mouse.CursorType = "not-allowed";
			state = CursorState.Disabled;
		}
		else
		{
			Mouse.CursorType = "pointer";
			state = CursorState.Hover;
		}

		Hud.Instance.SetCursorState( state );
	}

	private void UpdateHud()
	{
		Hud.Instance?.UpdateCursorPosition( cursorPosition );
	}

	#endregion

	#region Debug

	private void DrawDebug()
	{
		DrawCursorDebug();

		if ( selectedSelectable != null && selectedSelectable.CapturesSelection )
		{
			DrawFlickDebug();
		}
	}

	private void DrawCursorDebug()
	{
		const float size = 8f;
		Gizmo.Draw.ScreenRect(
			new Rect( cursorPosition.x - size / 2f, cursorPosition.y - size / 2f, size, size ),
			Color.Magenta
		);
	}

	private void DrawFlickDebug()
	{
		// Draw cursor world position
		Gizmo.Draw.Color = Color.Cyan;
		Gizmo.Draw.SolidSphere( worldCursorPosition, 16f, 16, 16 );

		// Draw flick direction line
		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.Line( selectedSelectable.SelectPosition, selectedSelectable.SelectPosition - flickVector );

		// Draw min flick distance circle
		Gizmo.Draw.Color = Color.Red;
		Gizmo.Draw.LineCircle( selectedSelectable.SelectPosition, Vector3.Up, MinFlickDistance, 0, 360, 64 );

		// Draw max flick distance circle
		Gizmo.Draw.Color = Color.Green;
		Gizmo.Draw.LineCircle( selectedSelectable.SelectPosition, Vector3.Up, MaxFlickDistance / FlickStrength, 0, 360, 512 );

		// Draw flick strength text
		Gizmo.Draw.Color = Color.Black;
		Gizmo.Draw.ScreenText( flickVector.Length.ToString( "F1" ), cursorPosition + Vector2.Down * 32, "roboto", 16f );
	}

	#endregion
}

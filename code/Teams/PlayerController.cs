using Punt;
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

	[Property, Group( "Controller" )] public float ControllerDistanceWeight { get; set; } = 0.1f;

	[Property, Group( "Debug" )] public bool ShowDebug { get; set; } = false;

	[Property] public CameraController CameraController { get; set; }
	[Property] public InputManager InputManager { get; set; }

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

	// Controller mode - active piece
	private ISelectable activeSelectable;
	private ISelectable controllerHoverTarget; // Piece being previewed while stick is held

	// Flick tracking
	private Vector3 flickVector;
	private float lastCursorDelta;
	private Vector2 lastValidStickDirection; // Tracks last valid stick direction for smooth rotation

	#endregion

	#region Lifecycle

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		// Determine input mode
		bool isControllerMode = InputManager != null && InputManager.CurrentMode == InputMode.Controller;

		if ( isControllerMode )
		{
			UpdateControllerMode();
		}
		else
		{
			UpdateMouseMode();
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

	private void UpdateMouseMode()
	{
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
	}

	private void UpdateControllerMode()
	{
		// Ensure we have an active piece
		if ( activeSelectable == null || !IsValidTarget( activeSelectable ) || !activeSelectable.CanSelect )
		{
			activeSelectable = FindNearestSelectableToCamera();
			if ( activeSelectable != null )
			{
				activeSelectable.OnHoverEnter();
			}
		}

		// Update world cursor position from right stick
		UpdateControllerCursor();

		// Handle right stick flick
		if ( selectedSelectable != null )
		{
			// Check for release first
			if ( InputManager.RightStick.WasReleased )
			{
				// Release flick
				ReleaseControllerFlick();
			}
			else
			{
				// Already flicking - update flick with cursor position
				UpdateSelection();
			}
		}
		else if ( InputManager.RightStick.IsHeld && activeSelectable != null && activeSelectable.CanSelect )
		{
			// Start flicking the active piece
			SelectTarget( activeSelectable );
		}
		// Handle left stick piece selection (only when not flicking)
		else if ( InputManager.LeftStick.IsHeld )
		{
			// Update search continuously as stick direction changes
			UpdateControllerPieceSelection();
		}
		else if ( InputManager.LeftStick.WasReleased )
		{
			// Quick release detected - confirm the selection change
			ConfirmControllerPieceSelection();
		}
		else
		{
			// Stick returned to neutral (either slow drift or after confirmation)
			// Clear hover target and stay on current active piece
			if ( controllerHoverTarget != null )
			{
				controllerHoverTarget.OnHoverExit();
				controllerHoverTarget = null;
			}
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

		// Debug logging
		if ( ShowDebug )
		{
			Log.Info( $"UpdateFlickVector - worldCursor: {worldCursorPosition}, flickVector: {flickVector}, length: {flickVector.Length:F1}" );
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
		lastValidStickDirection = InputManager.RightStick.CurrentInput.Normal;

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

	#region Controller Cursor

	private void UpdateControllerCursor()
	{
		// Use the selected piece if we're flicking, otherwise use the active piece
		ISelectable centerPiece = selectedSelectable ?? activeSelectable;
		if ( centerPiece == null ) return;

		// Only update cursor from stick input while gesture is actively held
		// This prevents springback from affecting the flick vector
		if ( !InputManager.RightStick.IsHeld && selectedSelectable != null )
		{
			// Stick released during flick - freeze cursor position
			return;
		}

		// Get right stick input
		Vector2 stickInput = InputManager.RightStick.CurrentInput;

		// When flicking, detect sudden direction reversal (springback while still held)
		// Compare against last valid direction to allow smooth rotation but block sudden reversals
		if ( selectedSelectable != null && InputManager.RightStick.IsHeld && stickInput.Length > 0.1f )
		{
			Vector2 currentDirection = stickInput.Normal;
			float dot = Vector2.Dot( currentDirection, lastValidStickDirection );

			// If dot product is very negative, this is a sudden reversal (springback)
			// Allow gradual rotation (dot > -0.5 means less than ~120 degree change)
			if ( dot < -0.5f )
			{
				if ( ShowDebug )
				{
					Log.Info( $"UpdateCursor - BLOCKED reversal: StickInput: {stickInput}, LastValid: {lastValidStickDirection}, dot: {dot:F2}" );
				}
				return;
			}

			// Update last valid direction for smooth tracking
			lastValidStickDirection = currentDirection;
		}

		// Convert stick direction to world space with pull-back behavior
		// Stick: X (right), Y (up) -> World: -Y (forward), -X (right)
		// Negate both to create pull-back: pull down = aim up, pull right = aim left
		Vector3 worldOffset = new Vector3( -stickInput.y, -stickInput.x, 0 );

		// Scale by max flick distance
		worldOffset *= MaxFlickDistance;

		// Set world cursor position relative to the center piece
		worldCursorPosition = centerPiece.SelectPosition + worldOffset;

		// Also update screen cursor position for camera panning
		cursorPosition = Scene.Camera.PointToScreenPixels( worldCursorPosition );

		// Debug logging when flicking
		if ( selectedSelectable != null && ShowDebug )
		{
			Log.Info( $"UpdateCursor - StickInput: {stickInput}, WorldOffset: {worldOffset}, IsHeld: {InputManager.RightStick.IsHeld}" );
		}
	}

	#endregion

	#region Controller Selection

	private void UpdateControllerPieceSelection()
	{
		// Find the nearest piece in the direction of the left stick
		Vector2 stickDirection = InputManager.LeftStick.Direction;

		// Convert 2D stick direction to 3D world direction
		// Stick: X (right), Y (up) -> World: -Y (forward), -X (right)
		Vector3 worldDirection = new Vector3( -stickDirection.y, -stickDirection.x, 0 ).Normal;

		var targetPiece = FindNearestSelectableInDirection( activeSelectable.SelectPosition, worldDirection );

		if ( targetPiece != null && targetPiece != activeSelectable )
		{
			// Clear hover from current active
			if ( activeSelectable != null && activeSelectable != selectedSelectable )
			{
				activeSelectable.OnHoverExit();
			}

			// Set hover target and hover it
			controllerHoverTarget = targetPiece;
			if ( selectedSelectable == null )
			{
				controllerHoverTarget.OnHoverEnter();
			}
		}
	}

	private void ConfirmControllerPieceSelection()
	{
		// Commit the hover target as the new active piece
		if ( controllerHoverTarget != null )
		{
			activeSelectable = controllerHoverTarget;
			controllerHoverTarget = null;

			// Play confirmation sound
			Sound.Play( "sounds/kenny/debugrelease.sound" );
		}
	}

	private ISelectable FindNearestSelectableInDirection( Vector3 fromPosition, Vector3 direction )
	{
		var selectables = new List<ISelectable>();
		selectables.AddRange( Scene.GetAllComponents<PuntPiece>() );
		selectables.AddRange( Scene.GetAllComponents<ClickableProp>() );

		ISelectable best = null;
		float bestScore = float.MaxValue;

		foreach ( var selectable in selectables )
		{
			if ( !IsValidTarget( selectable ) ) continue;
			if ( selectable == activeSelectable ) continue; // Skip current active

			Vector3 toSelectable = (selectable.SelectPosition - fromPosition).WithZ( 0 );
			float distance = toSelectable.Length;

			// Skip pieces that are too close (avoid self-selection issues)
			if ( distance < 10f ) continue;

			// Calculate the angle between stick direction and direction to piece
			Vector3 toPieceDirection = toSelectable.Normal;
			float dot = Vector3.Dot( direction, toPieceDirection );

			// Convert dot product to angle (in degrees)
			float angle = MathF.Acos( Math.Clamp( dot, -1f, 1f ) ) * (180f / MathF.PI);

			// Combine angle and distance with tunable weight
			// Lower score = better match
			float score = angle + (distance * ControllerDistanceWeight);

			if ( score < bestScore )
			{
				bestScore = score;
				best = selectable;
			}
		}

		return best;
	}

	private ISelectable FindNearestSelectableToCamera()
	{
		var selectables = new List<ISelectable>();
		selectables.AddRange( Scene.GetAllComponents<PuntPiece>() );
		selectables.AddRange( Scene.GetAllComponents<ClickableProp>() );

		ISelectable best = null;
		float bestDistance = float.MaxValue;

		Vector3 cameraPosition = Scene.Camera.WorldPosition.WithZ( 0 );

		foreach ( var selectable in selectables )
		{
			if ( !IsValidTarget( selectable ) ) continue;
			if ( !selectable.CanSelect ) continue;

			float distance = (selectable.SelectPosition - cameraPosition).WithZ( 0 ).Length;

			if ( distance < bestDistance )
			{
				bestDistance = distance;
				best = selectable;
			}
		}

		return best;
	}

	#endregion

	#region Controller Flick

	private void ReleaseControllerFlick()
	{
		if ( flickVector.Length < MinFlickDistance )
		{
			Log.Info( $"Flick aborted - too weak: {flickVector.Length:F1} < {MinFlickDistance:F1}" );
			AbortSelection();
			return;
		}

		Log.Info( $"Flick executed - strength: {flickVector.Length:F1}, direction: {flickVector.Normal}" );
		selectedSelectable?.OnDeselect( flickVector );
		ClearSelectionState();
	}

	#endregion

	#region Camera

	private void UpdateCamera()
	{
		if ( CameraController == null ) return;

		bool isControllerMode = InputManager != null && InputManager.CurrentMode == InputMode.Controller;
		bool isDragging = selectedSelectable != null && selectedSelectable.CapturesSelection;
		Vector3 piecePosition = isDragging ? selectedSelectable.SelectPosition : Vector3.Zero;

		// In controller mode, flick vector is already in world units
		// In mouse mode, we need to convert cursor distance to world units
		float worldFlickRadius = isDragging ? (isControllerMode ? MaxFlickDistance : MaxFlickDistance / FlickStrength) : 0f;

		CameraController.UpdatePan( cursorPosition, piecePosition, isDragging, worldFlickRadius, isControllerMode );
	}

	#endregion

	#region Cursor Visuals

	private void UpdateCursorVisuals()
	{
		if ( Hud.Instance == null ) return;

		bool isControllerMode = InputManager != null && InputManager.CurrentMode == InputMode.Controller;

		// Hide virtual cursor in controller mode
		if ( isControllerMode )
		{
			Hud.Instance.SetCursorState( CursorState.Hidden );
			return;
		}

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
		bool isControllerMode = InputManager != null && InputManager.CurrentMode == InputMode.Controller;

		if ( isControllerMode )
		{
			DrawControllerDebug();
		}
		else
		{
			DrawCursorDebug();
		}

		if ( selectedSelectable != null && selectedSelectable.CapturesSelection )
		{
			DrawFlickDebug();
		}
	}

	private void DrawControllerDebug()
	{
		// Draw active piece indicator (current selected piece)
		if ( activeSelectable != null && selectedSelectable == null )
		{
			Gizmo.Draw.Color = Color.Green;
			Gizmo.Draw.LineCircle( activeSelectable.SelectPosition, Vector3.Up, 80f, 0, 360, 32 );
		}

		// Draw hover target indicator (piece being previewed)
		if ( controllerHoverTarget != null )
		{
			Gizmo.Draw.Color = Color.Cyan;
			Gizmo.Draw.LineCircle( controllerHoverTarget.SelectPosition, Vector3.Up, 100f, 0, 360, 32 );
		}

		// Draw left stick direction if held
		if ( InputManager.LeftStick.IsHeld && activeSelectable != null && selectedSelectable == null )
		{
			Vector2 stickDir = InputManager.LeftStick.Direction;
			// Stick: X (right), Y (up) -> World: -Y (forward), -X (right)
			Vector3 worldDir = new Vector3( -stickDir.y, -stickDir.x, 0 ).Normal * 200f;

			Gizmo.Draw.Color = Color.Yellow;
			Gizmo.Draw.Arrow( activeSelectable.SelectPosition, activeSelectable.SelectPosition + worldDir, 8f, 4f );
		}

		// Draw right stick flick vector if held
		if ( InputManager.RightStick.IsHeld && selectedSelectable != null )
		{
			Vector2 stickInput = InputManager.RightStick.CurrentInput;
			float magnitude = stickInput.Length;

			// Draw flick direction arrow
			if ( magnitude > 0.01f )
			{
				Vector3 worldDir = new Vector3( -stickInput.y, -stickInput.x, 0 ).Normal;
				Gizmo.Draw.Color = Color.Magenta;
				Gizmo.Draw.Arrow( selectedSelectable.SelectPosition, selectedSelectable.SelectPosition + worldDir * 200f, 8f, 4f );
			}

			// Draw flick vector magnitude text at screen position
			Vector2 textScreenPos = Scene.Camera.PointToScreenPixels( selectedSelectable.SelectPosition + Vector3.Up * 100f );
			Gizmo.Draw.Color = Color.Black;
			Gizmo.Draw.ScreenText(
				$"Magnitude: {magnitude:F2}\nFlick: {flickVector.Length:F1}",
				textScreenPos,
				"roboto",
				16f
			);
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

using Punt;
using Sandbox;
using System;

/// <summary>
/// Handles player input for selecting and flicking pieces.
/// Manages virtual cursor, hover/selection states, and camera control.
/// </summary>
public sealed class PlayerController : Component
{
	#region Events

	/// <summary>
	/// Fired when a piece is flicked. Parameters: the piece that was flicked, and the flick velocity vector.
	/// </summary>
	public event Action<PuntPiece, Vector3> OnPieceFlicked;

	#endregion

	#region Properties

	[Property, Sync] public TeamSide Team { get; set; }

	[Property, Group( "Flick Settings" )] public float MinFlickDistance { get; set; } = 50f;
	[Property, Group( "Flick Settings" )] public float MaxFlickDistance { get; set; } = 650f;
	[Property, Group( "Flick Settings" )] public float MinFlickForce { get; set; } = 100f;
	[Property, Group( "Flick Settings" )] public float MaxFlickForce { get; set; } = 650f;
	[Property, Group( "Flick Settings" )] public bool InvertAimIndicator { get; set; } = true;
	[Property, Group( "Flick Settings" ), Description( "Minimum mouse movement required to snap cursor back from off-screen (prevents jitter)" )]
	public float SnapBackThreshold { get; set; } = 3f;

	[Property, Group( "Cursor" )] public bool ShowRealCursor { get; set; } = false;
	[Property, Group( "Cursor" )] public bool ShowCursor { get; set; } = true;
	[Property, Group( "Cursor" )] public float Sensitivity { get; set; } = 1.0f;

	[Property, Group( "Controller" )] public float ControllerDistanceWeight { get; set; } = 0.1f;
	[Property, Group( "Controller" )] public float ControllerAimSmoothing { get; set; } = 10f;

	[Property, Group( "Debug" )] public bool ShowDebug { get; set; } = false;

	[Property] public CameraController CameraController { get; set; }
	[Property] public InputManager InputManager { get; set; }

	#endregion

	#region Private State

	// Virtual cursor
	private Vector2 cursorPosition;
	private bool cursorInitialized;

	// Unclamped cursor position for snap-back threshold behavior
	private Vector2 unclampedCursorPosition;

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
	private float dragDistance; // Tracks the actual drag distance for minimum flick validation
	private float lastCursorDelta;
	private Vector2 lastValidStickDirection; // Tracks last valid stick direction for smooth rotation
	private Vector3 smoothedFlickDirection; // Smoothed direction for controller aiming (magnitude preserved)

	// Controller flick magnitude buffer - stores recent magnitudes to capture peak flick strength
	private const int FlickBufferSize = 8;
	private float[] flickMagnitudeBuffer = new float[FlickBufferSize];
	private int flickBufferIndex = 0;

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
		// Ensure we have an active piece (only when not currently flicking)
		if ( selectedSelectable == null )
		{
			if ( activeSelectable == null || !IsValidTarget( activeSelectable ) || !activeSelectable.CanSelect )
			{
				var previousActive = activeSelectable;
				activeSelectable = FindNearestSelectableToCamera();

				// Update hover states when active piece changes
				if ( activeSelectable != previousActive )
				{
					previousActive?.OnHoverExit();
					activeSelectable?.OnHoverEnter();
				}
			}

			// Ensure active piece is always highlighted (in case hover was cleared elsewhere)
			if ( activeSelectable != null && controllerHoverTarget == null )
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
			unclampedCursorPosition = cursorPosition;
			cursorInitialized = true;
		}

		Vector2 delta = Mouse.Delta;

		if ( selectedSelectable != null )
		{
			// Snap-back threshold behavior while dragging:
			// Check if we're currently off-screen on each axis
			bool offLeft = unclampedCursorPosition.x < 0;
			bool offRight = unclampedCursorPosition.x > Screen.Width;
			bool offTop = unclampedCursorPosition.y < 0;
			bool offBottom = unclampedCursorPosition.y > Screen.Height;

			// If moving back toward screen on an axis where we're off-screen,
			// snap that axis to the edge immediately (with threshold to prevent jitter)
			if ( offLeft && delta.x > SnapBackThreshold )
			{
				// Moving right while off left edge - snap X to left edge
				unclampedCursorPosition.x = 0;
			}
			else if ( offRight && delta.x < -SnapBackThreshold )
			{
				// Moving left while off right edge - snap X to right edge
				unclampedCursorPosition.x = Screen.Width;
			}

			if ( offTop && delta.y > SnapBackThreshold )
			{
				// Moving down while off top edge - snap Y to top edge
				unclampedCursorPosition.y = 0;
			}
			else if ( offBottom && delta.y < -SnapBackThreshold )
			{
				// Moving up while off bottom edge - snap Y to bottom edge
				unclampedCursorPosition.y = Screen.Height;
			}

			// Now apply the delta
			unclampedCursorPosition += delta;

			// Check if unclamped cursor is within screen bounds
			bool isOnScreen = unclampedCursorPosition.x >= 0 && unclampedCursorPosition.x <= Screen.Width &&
							  unclampedCursorPosition.y >= 0 && unclampedCursorPosition.y <= Screen.Height;

			if ( isOnScreen )
			{
				// Cursor is on screen - sync visual cursor
				cursorPosition = unclampedCursorPosition;
			}
			else
			{
				// Cursor is off-screen - clamp visual cursor to edge
				cursorPosition = new Vector2(
					Math.Clamp( unclampedCursorPosition.x, 0, Screen.Width ),
					Math.Clamp( unclampedCursorPosition.y, 0, Screen.Height )
				);
			}
		}
		else
		{
			// Normal mode: cursor follows mouse and clamps to screen
			cursorPosition += delta;
			cursorPosition = new Vector2(
				Math.Clamp( cursorPosition.x, 0, Screen.Width ),
				Math.Clamp( cursorPosition.y, 0, Screen.Height )
			);
			// Keep unclamped in sync when not in special mode
			unclampedCursorPosition = cursorPosition;
		}

		Mouse.Visibility = ShowRealCursor ? MouseVisibility.Visible : MouseVisibility.Hidden;
	}

	private void UpdateWorldPosition()
	{
		// Always use clamped cursor for world position (direction)
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
		bool isControllerMode = InputManager != null && InputManager.CurrentMode == InputMode.Controller;

		// Calculate direction from clamped cursor (visual cursor position)
		Vector3 pieceToCursor = (worldCursorPosition - selectedSelectable.SelectPosition).WithZ( 0 );
		Vector3 direction = pieceToCursor.Normal;

		// Calculate magnitude - use unclamped position for magnitude calculation
		float magnitude;
		if ( !isControllerMode )
		{
			// Convert unclamped screen position to world for magnitude calculation
			var unclampedRay = Scene.Camera.ScreenPixelToRay( unclampedCursorPosition );
			float t = -unclampedRay.Position.z / unclampedRay.Forward.z;
			Vector3 unclampedWorldPos = (unclampedRay.Position + unclampedRay.Forward * t).WithZ( 0f );
			magnitude = (unclampedWorldPos - selectedSelectable.SelectPosition).WithZ( 0 ).Length;
		}
		else
		{
			magnitude = pieceToCursor.Length;
		}

		// Store unclamped drag distance for minimum flick validation
		dragDistance = magnitude;

		// Clamp drag distance to valid physical range for force calculation
		float clampedMagnitude = Math.Clamp( magnitude, 0, MaxFlickDistance );

		// Calculate flick force based on how far we've dragged
		float dragRatio = MaxFlickDistance > MinFlickDistance
			? (clampedMagnitude - MinFlickDistance) / (MaxFlickDistance - MinFlickDistance)
			: 0f;
		dragRatio = Math.Clamp( dragRatio, 0, 1 );
		float force = MathX.Lerp( MinFlickForce, MaxFlickForce, dragRatio );

		// Build flick vector - don't clamp magnitude, let it scale naturally with drag distance
		flickVector = -direction * force;

		// Buffer the magnitude for controller mode (captures peak strength before stick returns to center)
		if ( isControllerMode )
		{
			flickMagnitudeBuffer[flickBufferIndex] = flickVector.Length;
			flickBufferIndex = (flickBufferIndex + 1) % FlickBufferSize;
		}

		// Calculate feedback data for the selectable
		float intensity = Math.Clamp( clampedMagnitude / MaxFlickDistance, 0, 1 );
		bool exceedsMinimum = magnitude >= MinFlickDistance;

		// Calculate aim indicator position using clamped offset (matches visual cursor direction)
		Vector3 clampedOffset = direction * clampedMagnitude;
		Vector3 aimIndicatorPos;
		if ( InvertAimIndicator )
		{
			// Show trajectory: position on opposite side of piece from cursor
			aimIndicatorPos = selectedSelectable.SelectPosition - clampedOffset;
		}
		else
		{
			// Show cursor position (clamped)
			aimIndicatorPos = selectedSelectable.SelectPosition + clampedOffset;
		}

		selectedSelectable.OnDragUpdate( intensity, lastCursorDelta, aimIndicatorPos, exceedsMinimum, InvertAimIndicator );
	}

	private void SelectTarget( ISelectable target )
	{
		selectedSelectable = target;
		selectedSelectable.OnSelect();

		hoveredSelectable?.OnHoverExit();
		hoveredSelectable = null;

		flickVector = Vector3.Zero;
		dragDistance = 0f;
		lastCursorDelta = 0f;
		lastValidStickDirection = InputManager.RightStick.CurrentInput.Normal;
		smoothedFlickDirection = Vector3.Zero; // Will be initialized on first cursor update

		// Clear magnitude buffer for fresh flick tracking
		Array.Clear( flickMagnitudeBuffer, 0, FlickBufferSize );
		flickBufferIndex = 0;

		// Some selectables don't capture (e.g. instant-click buttons)
		if ( !selectedSelectable.CapturesSelection )
		{
			selectedSelectable = null;
		}
	}

	private void ReleaseSelection()
	{
		if ( dragDistance < MinFlickDistance )
		{
			AbortSelection();
			return;
		}

		// Fire flick event if it's a PuntPiece
		if ( selectedSelectable is PuntPiece piece )
		{
			OnPieceFlicked?.Invoke( piece, flickVector );
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
		dragDistance = 0f;
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
		float magnitude = worldOffset.Length * MaxFlickDistance;
		Vector3 direction = worldOffset.Normal;

		// Apply smoothing to direction only (not magnitude) when flicking
		if ( selectedSelectable != null && ControllerAimSmoothing > 0f && direction.Length > 0.01f )
		{
			float smoothFactor = 1f - MathF.Exp( -ControllerAimSmoothing * Time.Delta );
			smoothedFlickDirection = Vector3.Lerp( smoothedFlickDirection, direction, smoothFactor ).Normal;

			// Recombine smoothed direction with unsmoothed magnitude
			worldCursorPosition = centerPiece.SelectPosition + smoothedFlickDirection * magnitude;
		}
		else
		{
			worldCursorPosition = centerPiece.SelectPosition + direction * magnitude;
			smoothedFlickDirection = direction;
		}

		// Also update screen cursor position for camera panning
		cursorPosition = Scene.Camera.PointToScreenPixels( worldCursorPosition );

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
		// Controller only interacts with PuntPieces (other selectables are mouse-only)
		var selectables = new List<ISelectable>();
		selectables.AddRange( Scene.GetAllComponents<PuntPiece>() );

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
		// Controller only interacts with PuntPieces (other selectables are mouse-only)
		var selectables = new List<ISelectable>();
		selectables.AddRange( Scene.GetAllComponents<PuntPiece>() );

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
		// Find peak magnitude from buffer (captures true flick strength before stick returned to center)
		float peakMagnitude = 0f;
		for ( int i = 0; i < FlickBufferSize; i++ )
		{
			if ( flickMagnitudeBuffer[i] > peakMagnitude )
				peakMagnitude = flickMagnitudeBuffer[i];
		}

		// Use peak magnitude if stronger than current
		if ( peakMagnitude > flickVector.Length )
		{
			flickVector = flickVector.Normal * peakMagnitude;
		}

		if ( dragDistance < MinFlickDistance )
		{
			AbortSelection();
			return;
		}

		// Fire flick event if it's a PuntPiece
		if ( selectedSelectable is PuntPiece piece )
		{
			OnPieceFlicked?.Invoke( piece, flickVector );
		}

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

		// Disable camera pan during drag (no edge panning behavior)
		if ( isDragging && !isControllerMode )
		{
			// Still update cursor position for passive pan
			CameraController.UpdatePan( cursorPosition, Vector3.Zero, false, 0f, false );
			return;
		}

		Vector3 piecePosition = isDragging ? selectedSelectable.SelectPosition : Vector3.Zero;

		// Physical drag radius for camera pan
		float worldFlickRadius = isDragging ? MaxFlickDistance : 0f;

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

		// Draw drag line to actual cursor position
		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.Line( selectedSelectable.SelectPosition, worldCursorPosition );

		// Draw min flick distance circle
		Gizmo.Draw.Color = Color.Red;
		Gizmo.Draw.LineCircle( selectedSelectable.SelectPosition, Vector3.Up, MinFlickDistance, 0, 360, 64 );

		// Draw max flick distance circle
		Gizmo.Draw.Color = Color.Green;
		Gizmo.Draw.LineCircle( selectedSelectable.SelectPosition, Vector3.Up, MaxFlickDistance, 0, 360, 512 );

		// Draw drag distance and flick force text
		Gizmo.Draw.Color = Color.Black;
		Gizmo.Draw.ScreenText( $"Distance: {dragDistance:F1}\nForce: {flickVector.Length:F1}", cursorPosition + Vector2.Down * 32, "roboto", 16f );
	}

	#endregion
}

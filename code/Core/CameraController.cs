using Sandbox;
using System;

/// <summary>
/// Controls camera panning behavior during piece selection.
/// Supports edge panning when flick radius extends beyond screen, and passive cursor-based panning.
/// </summary>
public sealed class CameraController : Component
{
	#region Properties

	[Property, Group( "Smoothing" )] public bool SmoothPan { get; set; } = true;
	[Property, Group( "Smoothing" )] public float Smoothing { get; set; } = 10f;

	[Property, Group( "Passive Pan" )] public bool EnablePassivePan { get; set; } = false;
	[Property, Group( "Passive Pan" )] public float PassivePanSpeed { get; set; } = 50f;
	[Property, Group( "Passive Pan" )] public float PassivePanHorizontalMult { get; set; } = 2f;
	[Property, Group( "Passive Pan" )] public float PassivePanDepthMultUp { get; set; } = 1f;
	[Property, Group( "Passive Pan" )] public float PassivePanDepthMultDown { get; set; } = 1f;

	#endregion

	#region Private State

	// Current cursor position (updated by PlayerController)
	private Vector2 cursorPosition;

	// Camera position tracking
	private Vector3 initialPosition;
	private Vector3 targetPosition;

	// Drag state
	private bool isDragging;

	#endregion

	#region Lifecycle

	protected override void OnStart()
	{
		targetPosition = WorldPosition;
	}

	protected override void OnUpdate()
	{
		Vector3 finalTarget = targetPosition + GetPassivePanOffset();

		if ( SmoothPan )
		{
			WorldPosition = Vector3.Lerp( WorldPosition, finalTarget, Time.Delta * Smoothing );
		}
		else
		{
			WorldPosition = finalTarget;
		}
	}

	#endregion

	#region Public API

	/// <summary>
	/// Updates the cursor position for passive pan calculations.
	/// Called every frame by PlayerController.
	/// </summary>
	public void UpdateCursorPosition( Vector2 position )
	{
		cursorPosition = position;
	}

	/// <summary>
	/// Updates edge panning state based on piece selection.
	/// </summary>
	/// <param name="cursor">Current cursor screen position</param>
	/// <param name="piecePosition">World position of selected piece (or Zero if not dragging)</param>
	/// <param name="isDraggingPiece">Whether a piece is currently being dragged</param>
	/// <param name="flickRadius">World-space radius of the flick circle</param>
	/// <param name="isControllerMode">Whether controller input is active</param>
	public void UpdatePan( Vector2 cursor, Vector3 piecePosition, bool isDraggingPiece, float flickRadius, bool isControllerMode = false )
	{
		cursorPosition = cursor;

		if ( !isDraggingPiece )
		{
			HandleDragEnd();
		}
		else if ( !isDragging )
		{
			HandleDragStart();
		}
	}

	#endregion

	#region Drag State Management

	private void HandleDragStart()
	{
		// targetPosition already represents the base position (without passive pan)
		// so we just need to capture it as our initial position to return to
		initialPosition = targetPosition;
		isDragging = true;
	}

	private void HandleDragEnd()
	{
		if ( isDragging )
		{
			targetPosition = initialPosition;
			isDragging = false;
		}
	}

	#endregion

	#region Passive Pan

	private Vector3 GetPassivePanOffset()
	{
		if ( !EnablePassivePan ) return Vector3.Zero;

		Vector2 screenCenter = new Vector2( Screen.Width / 2f, Screen.Height / 2f );
		Vector2 cursorOffset = cursorPosition - screenCenter;

		// Normalize to -1 to 1 range
		Vector2 normalized = new Vector2(
			cursorOffset.x / screenCenter.x,
			cursorOffset.y / screenCenter.y
		);

		// Apply different multipliers for up vs down movement
		// When normalized.y < 0, mouse is above center (panning away), when > 0 it's below center (panning towards)
		float depthMult = normalized.y < 0 ? PassivePanDepthMultUp : PassivePanDepthMultDown;

		// Convert to world offset (screen X -> world -Y, screen Y -> world -X)
		return new Vector3( -normalized.y * depthMult, -normalized.x * PassivePanHorizontalMult, 0 ) * PassivePanSpeed;
	}

	#endregion
}

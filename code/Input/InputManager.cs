using Sandbox;
using System;

namespace Punt;

/// <summary>
/// Manages input from both mouse and controller, automatically switching between input modes.
/// Provides a unified interface for PlayerController to consume.
/// </summary>
public sealed class InputManager : Component
{
	#region Properties

	[Property, Group( "Debug" )] public bool ShowDebug { get; set; } = false;

	[Property, Group( "Controller" )] public float StickDeadzone { get; set; } = 0.2f;
	[Property, Group( "Controller" )] public float StickActivationThreshold { get; set; } = 0.3f;
	[Property, Group( "Controller" )] public float StickReleaseThreshold { get; set; } = 0.25f;

	#endregion

	#region Public State

	/// <summary>
	/// Current active input mode.
	/// </summary>
	public InputMode CurrentMode { get; private set; } = InputMode.Mouse;

	/// <summary>
	/// Left analog stick state (controller mode).
	/// </summary>
	public AnalogStickState LeftStick { get; private set; } = new AnalogStickState();

	/// <summary>
	/// Right analog stick state (controller mode).
	/// </summary>
	public AnalogStickState RightStick { get; private set; } = new AnalogStickState();

	#endregion

	#region Lifecycle

	protected override void OnStart()
	{
		LeftStick.Deadzone = StickDeadzone;
		LeftStick.ActivationThreshold = StickActivationThreshold;
		LeftStick.ReleaseThreshold = StickReleaseThreshold;
		RightStick.Deadzone = StickDeadzone;
		RightStick.ActivationThreshold = StickActivationThreshold;
		RightStick.ReleaseThreshold = StickReleaseThreshold;
	}

	protected override void OnUpdate()
	{
		DetectInputMode();
		UpdateControllerInput();

		if ( ShowDebug )
		{
			DrawDebug();
		}
	}

	#endregion

	#region Input Mode Detection

	private void DetectInputMode()
	{
		// Read controller inputs using raw analog values
		Vector2 leftStick = new Vector2(
			Input.GetAnalog( InputAnalog.LeftStickX ),
			Input.GetAnalog( InputAnalog.LeftStickY )
		);
		Vector2 rightStick = new Vector2(
			Input.GetAnalog( InputAnalog.RightStickX ),
			Input.GetAnalog( InputAnalog.RightStickY )
		);

		bool hasControllerInput = leftStick.Length > StickDeadzone || rightStick.Length > StickDeadzone;

		// Check for mouse input
		if ( Mouse.Delta.Length > 0.1f )
		{
			if ( CurrentMode != InputMode.Mouse )
			{
				CurrentMode = InputMode.Mouse;
				Log.Info( "Switched to Mouse input" );
			}
		}
		// Check for controller stick input
		else if ( hasControllerInput )
		{
			if ( CurrentMode != InputMode.Controller )
			{
				CurrentMode = InputMode.Controller;
				Log.Info( $"Switched to Controller input - L: {leftStick.Length:F2}, R: {rightStick.Length:F2}" );
			}
		}
	}

	#endregion

	#region Controller Input

	private void UpdateControllerInput()
	{
		// Read raw stick inputs using GetAnalog for proper normalized values
		Vector2 leftStickInput = new Vector2(
			Input.GetAnalog( InputAnalog.LeftStickX ),
			Input.GetAnalog( InputAnalog.LeftStickY )
		);

		Vector2 rightStickInput = new Vector2(
			Input.GetAnalog( InputAnalog.RightStickX ),
			Input.GetAnalog( InputAnalog.RightStickY )
		);

		// Clamp magnitude to 1.0 (diagonal input can exceed 1.0 otherwise)
		leftStickInput = leftStickInput.ClampLength( 1f );
		rightStickInput = rightStickInput.ClampLength( 1f );

		// Update stick states
		LeftStick.Update( leftStickInput );
		RightStick.Update( rightStickInput );
	}

	#endregion

	#region Debug Visualization

	private void DrawDebug()
	{
		DrawInputModeIndicator();
		DrawStickVisualization();
	}

	private void DrawInputModeIndicator()
	{
		string modeText = $"Input Mode: {CurrentMode}";
		Color modeColor = CurrentMode == InputMode.Mouse ? Color.Cyan : Color.Green;

		Gizmo.Draw.Color = Color.Black;
		Gizmo.Draw.ScreenText( modeText, new Vector2( 10, 10 ), "roboto", 20f, TextFlag.Left );

		// Draw colored indicator box
		Rect indicatorBox = new Rect( 10, 40, 20, 20 );
		Gizmo.Draw.ScreenRect( indicatorBox, modeColor );
	}

	private void DrawStickVisualization()
	{
		if ( CurrentMode != InputMode.Controller ) return;

		const float leftStickY = 220f;
		const float rightStickY = 80f;
		const float stickX = 200f;
		const float radius = 40f;
		const float stickRadius = 8f;

		// Left stick (on the left)
		DrawStickCircle( new Vector2( leftStickY, stickX ), radius, LeftStick, "Left Stick" );

		// Right stick (on the right)
		DrawStickCircle( new Vector2( rightStickY, stickX ), radius, RightStick, "Right Stick" );
	}

	private void DrawStickCircle( Vector2 center, float radius, AnalogStickState stick, string label )
	{
		Vector3 worldCenter = ToWorldPos( center );

		// Draw outer circle
		Gizmo.Draw.Color = Color.White.WithAlpha( 0.3f );
		Gizmo.Draw.LineCircle( worldCenter, Vector3.Up, radius, 0, 360, 32 );

		// Draw deadzone circle
		Gizmo.Draw.Color = Color.Red.WithAlpha( 0.2f );
		Gizmo.Draw.LineCircle( worldCenter, Vector3.Up, radius * stick.Deadzone, 0, 360, 16 );

		// Draw activation threshold circle
		Gizmo.Draw.Color = Color.Yellow.WithAlpha( 0.3f );
		Gizmo.Draw.LineCircle( worldCenter, Vector3.Up, radius * stick.ActivationThreshold, 0, 360, 16 );

		// Draw current stick position
		Vector2 stickPos = center + stick.CurrentInput * radius;
		Color stickColor = stick.IsHeld ? Color.Green : Color.White;
		Gizmo.Draw.Color = stickColor;
		Gizmo.Draw.ScreenRect(
			new Rect( stickPos.x - 4f, stickPos.y - 4f, 8f, 8f ),
			stickColor
		);

		// Draw direction arrow if held
		if ( stick.IsHeld )
		{
			Vector2 directionEnd = center + stick.Direction * radius;
			Gizmo.Draw.Color = Color.Cyan;
			Gizmo.Draw.Arrow( ToWorldPos( center ), ToWorldPos( directionEnd ), 4f, 2f );
		}

		// Draw label and state info
		Gizmo.Draw.Color = Color.Black;
		string stateText = $"{label}\n";
		stateText += $"Mag: {stick.CurrentInput.Length:F2}\n";
		stateText += $"Held: {stick.IsHeld}\n";
		stateText += $"Released: {stick.WasReleased}";

		Gizmo.Draw.ScreenText( stateText, center + new Vector2( radius + 10f, -30f ), "roboto", 12f, TextFlag.Left );
	}

	/// <summary>
	/// Converts a 2D position to world space for debug drawing.
	/// Maps: X (right) -> -Y (world), Y (up) -> -X (world)
	/// </summary>
	private Vector3 ToWorldPos( Vector2 pos )
	{
		return new Vector3( -pos.y, -pos.x, 0 );
	}

	#endregion
}

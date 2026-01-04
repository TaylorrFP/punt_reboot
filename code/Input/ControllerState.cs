using Sandbox;
using System;

namespace Punt;

/// <summary>
/// Tracks the state of a single analog stick for "flick" gesture detection.
/// A flick is: stick movement → hold → release.
/// </summary>
public class AnalogStickState
{
	/// <summary>
	/// Current analog stick input (magnitude 0-1).
	/// </summary>
	public Vector2 CurrentInput { get; private set; }

	/// <summary>
	/// Direction of the stick when it was first moved (normalized).
	/// </summary>
	public Vector2 Direction { get; private set; }

	/// <summary>
	/// Is the stick currently being held?
	/// </summary>
	public bool IsHeld { get; private set; }

	/// <summary>
	/// Was the stick just released this frame?
	/// </summary>
	public bool WasReleased { get; private set; }

	/// <summary>
	/// Deadzone threshold for stick input.
	/// </summary>
	public float Deadzone { get; set; } = 0.2f;

	/// <summary>
	/// Minimum magnitude required to register as input.
	/// </summary>
	public float ActivationThreshold { get; set; } = 0.3f;

	/// <summary>
	/// Maximum magnitude to consider the stick "released" (returned to neutral).
	/// Should be close to deadzone for quick release detection.
	/// </summary>
	public float ReleaseThreshold { get; set; } = 0.25f;

	/// <summary>
	/// Minimum magnitude required to update the direction during a gesture.
	/// Prevents direction jitter as stick returns to neutral.
	/// </summary>
	public float DirectionUpdateThreshold { get; set; } = 0.5f;

	/// <summary>
	/// Updates the stick state with new input.
	/// </summary>
	/// <param name="input">Raw stick input from controller</param>
	public void Update( Vector2 input )
	{
		WasReleased = false;
		CurrentInput = input;

		float magnitude = input.Length;

		// Apply deadzone
		if ( magnitude < Deadzone )
		{
			magnitude = 0f;
			input = Vector2.Zero;
		}

		// Check if we're starting a new gesture
		if ( !IsHeld && magnitude >= ActivationThreshold )
		{
			// Start tracking
			IsHeld = true;
			Direction = input.Normal;
		}
		// Update existing gesture
		else if ( IsHeld && magnitude >= Deadzone )
		{
			// Only update direction if magnitude is strong enough
			// This prevents direction jitter as the stick returns to neutral
			if ( magnitude >= DirectionUpdateThreshold )
			{
				Direction = input.Normal;
			}
		}
		// Release detected - stick returned to neutral
		else if ( IsHeld && magnitude < ReleaseThreshold )
		{
			IsHeld = false;
			WasReleased = true;
		}
	}

	/// <summary>
	/// Resets the stick state.
	/// </summary>
	public void Reset()
	{
		CurrentInput = Vector2.Zero;
		Direction = Vector2.Zero;
		IsHeld = false;
		WasReleased = false;
	}
}

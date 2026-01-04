using Sandbox;

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
	/// Maximum magnitude reached during this gesture.
	/// </summary>
	public float PeakMagnitude { get; private set; }

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
			PeakMagnitude = magnitude;
		}
		// Update existing gesture
		else if ( IsHeld && magnitude >= Deadzone )
		{
			// Update peak magnitude
			if ( magnitude > PeakMagnitude )
			{
				PeakMagnitude = magnitude;
			}

			// Update direction (allow slight adjustments)
			Direction = input.Normal;
		}
		// Release detected
		else if ( IsHeld && magnitude < Deadzone )
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
		PeakMagnitude = 0f;
		IsHeld = false;
		WasReleased = false;
	}
}

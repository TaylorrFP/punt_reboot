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
	/// Deadzone threshold for stick input. Also used as the activation threshold for gestures.
	/// </summary>
	public float Deadzone { get; set; } = 0.2f;

	/// <summary>
	/// Maximum magnitude to consider the stick "released" (returned to neutral).
	/// Should be below deadzone for hysteresis.
	/// </summary>
	public float ReleaseThreshold { get; set; } = 0.15f;

	/// <summary>
	/// Tracks if the stick was in deadzone. Used to prevent double-flicks from springback.
	/// </summary>
	private bool wasInDeadzone = true;

	/// <summary>
	/// Time when the last gesture was released. Used to prevent springback from starting a new gesture.
	/// </summary>
	private float lastReleaseTime = float.NegativeInfinity;

	/// <summary>
	/// Direction of the previous gesture. Used to detect springback (opposite direction activation).
	/// </summary>
	private Vector2 previousGestureDirection = Vector2.Zero;

	/// <summary>
	/// Minimum time (in seconds) that must pass after a release before a new gesture can start.
	/// </summary>
	public float GestureCooldown { get; set; } = 0.15f;

	/// <summary>
	/// Minimum dot product between previous and new direction to allow gesture (prevents opposite springback).
	/// -1 = opposite directions allowed, 0 = perpendicular+, 1 = same direction only.
	/// </summary>
	public float MinDirectionDot { get; set; } = -0.5f;

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
			wasInDeadzone = true;
		}

		// Check if we're starting a new gesture
		// Only allow new gesture if:
		// 1. Stick was previously in deadzone (prevents springback)
		// 2. Enough time has passed since last release (prevents rapid springback double-flicks)
		// 3. OR the new direction is not opposite to the previous gesture (springback detection)
		float timeSinceRelease = Time.Now - lastReleaseTime;
		bool cooldownExpired = timeSinceRelease >= GestureCooldown;

		if ( !IsHeld && magnitude >= Deadzone && wasInDeadzone )
		{
			Vector2 newDirection = input.Normal;

			// Check if this might be springback by comparing direction to previous gesture
			bool isLikelySpringback = false;
			if ( !cooldownExpired && previousGestureDirection != Vector2.Zero )
			{
				float directionDot = Vector2.Dot( newDirection, previousGestureDirection );
				isLikelySpringback = directionDot < MinDirectionDot;

				if ( isLikelySpringback )
				{
		
				}
			}

			// Start gesture if cooldown expired OR not springback
			if ( cooldownExpired || !isLikelySpringback )
			{
				IsHeld = true;
				Direction = newDirection;
				wasInDeadzone = false;
			}
		}
		// During existing gesture, direction is locked to initial direction
		// (no updates to prevent direction reversal during release)
		// Release detected - stick returned to neutral
		else if ( IsHeld && magnitude < ReleaseThreshold )
		{
			IsHeld = false;
			WasReleased = true;
			lastReleaseTime = Time.Now;
			previousGestureDirection = Direction; // Remember direction for springback detection
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
		wasInDeadzone = true;
		lastReleaseTime = float.NegativeInfinity;
		previousGestureDirection = Vector2.Zero;
	}
}

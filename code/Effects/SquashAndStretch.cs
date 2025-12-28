using Sandbox;
using System;

/// <summary>
/// Applies a squash and stretch animation to a GameObject's scale.
/// Useful for juice/feedback when selecting pieces, bouncing balls, etc.
/// </summary>
public sealed class SquashAndStretch : Component
{
	/// <summary>
	/// The curve defining the squash shape over time.
	/// Y value of 0.5 = normal scale, 0 = fully squashed, 1 = fully stretched.
	/// </summary>
	[Property] public Curve SquashCurve { get; set; }

	/// <summary>
	/// Maximum scale multiplier at full stretch (curve Y = 1).
	/// </summary>
	[Property] public float MaxStretch { get; set; } = 1.3f;

	/// <summary>
	/// Minimum scale multiplier at full squash (curve Y = 0).
	/// </summary>
	[Property] public float MinSquash { get; set; } = 0.7f;

	/// <summary>
	/// Which axis to squash/stretch along. Perpendicular axes scale inversely to preserve volume.
	/// </summary>
	[Property] public Axis StretchAxis { get; set; } = Axis.Z;

	/// <summary>
	/// If true, ignores timescale (useful for UI or slow-motion unaffected feedback).
	/// </summary>
	[Property] public bool UseRealTime { get; set; } = true;

	// Runtime state
	private float duration;
	private TimeSince timeSinceStarted;
	private RealTimeSince realTimeSinceStarted;
	private bool isAnimating;
	private Vector3 originalScale;

	public bool IsAnimating => isAnimating;

	protected override void OnStart()
	{
		originalScale = GameObject.LocalScale;
	}

	/// <summary>
	/// Start the squash and stretch animation.
	/// </summary>
	/// <param name="animDuration">How long the animation should take.</param>
	public void Play( float animDuration = 0.4f )
	{
		duration = animDuration;
		timeSinceStarted = 0;
		realTimeSinceStarted = 0;
		isAnimating = true;
	}

	/// <summary>
	/// Stop the animation and reset to original scale.
	/// </summary>
	public void Stop()
	{
		isAnimating = false;
		GameObject.LocalScale = originalScale;
	}

	protected override void OnUpdate()
	{
		if ( !isAnimating ) return;

		float elapsed = UseRealTime ? realTimeSinceStarted : timeSinceStarted;
		float progress = duration > 0 ? elapsed / duration : 1f;

		if ( progress >= 1f )
		{
			// Animation complete
			isAnimating = false;
			GameObject.LocalScale = originalScale;
			return;
		}

		// Evaluate curve (expected range 0-1, where 0.5 is neutral)
		float curveValue = SquashCurve.Evaluate( progress );

		// Map curve value to scale
		// 0 = MinSquash, 0.5 = 1.0 (neutral), 1 = MaxStretch
		float axisScale;
		if ( curveValue < 0.5f )
		{
			// Squashing: lerp from MinSquash to 1.0
			axisScale = MathX.Lerp( MinSquash, 1f, curveValue * 2f );
		}
		else
		{
			// Stretching: lerp from 1.0 to MaxStretch
			axisScale = MathX.Lerp( 1f, MaxStretch, (curveValue - 0.5f) * 2f );
		}

		// Preserve volume: if one axis scales by S, others scale by 1/sqrt(S)
		float perpScale = 1f / MathF.Sqrt( axisScale );

		// Apply based on chosen axis
		Vector3 newScale = StretchAxis switch
		{
			Axis.X => new Vector3( axisScale, perpScale, perpScale ),
			Axis.Y => new Vector3( perpScale, axisScale, perpScale ),
			Axis.Z => new Vector3( perpScale, perpScale, axisScale ),
			_ => Vector3.One
		};

		GameObject.LocalScale = originalScale * newScale;
	}

	public enum Axis
	{
		X,
		Y,
		Z
	}
}

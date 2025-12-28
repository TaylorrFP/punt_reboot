using Sandbox;

using Sandbox.Utility;
using System;

/// <summary>
/// Applies a smooth procedural shake to a GameObject using Perlin noise.
/// Useful for grabbed pieces, camera shake, impacts, etc.
/// </summary>
public sealed class ShakeEffect : Component
{
	// === Position Shake ===
	[Property, Group( "Position" )] public bool ShakePosition { get; set; } = true;
	[Property, Group( "Position" )] public float PositionAmount { get; set; } = 5f;
	[Property, Group( "Position" )] public float PositionFrequency { get; set; } = 10f;

	// === Rotation Shake ===
	[Property, Group( "Rotation" )] public bool ShakeRotation { get; set; } = true;
	[Property, Group( "Rotation" )] public float RotationAmount { get; set; } = 5f;
	[Property, Group( "Rotation" )] public float RotationFrequency { get; set; } = 10f;

	// === Control ===

	/// <summary>
	/// Master strength multiplier (0 = no shake, 1 = full shake).
	/// Animate this for fade in/out effects.
	/// </summary>
	[Property, Group( "Control" ), Range( 0f, 2f )]
	public float Strength { get; set; } = 1f;

	/// <summary>
	/// If true, shake timing ignores game timescale (useful during slow-motion).
	/// </summary>
	[Property, Group( "Control" )]
	public bool UseRealTime { get; set; } = true;

	/// <summary>
	/// If true, the shake is currently active.
	/// </summary>
	[Property, Group( "Control" )]
	public bool IsActive { get; set; } = true;

	// === Internal ===
	private Vector3 baseLocalPosition;
	private Rotation baseLocalRotation;
	private float positionTime;
	private float rotationTime;
	private bool hasStoredBase;

	// Unique seed so multiple shake effects don't sync up
	private float noiseSeed;

	protected override void OnStart()
	{
		StoreBaseTransform();
		noiseSeed = Random.Shared.Float( 0f, 1000f );
	}

	protected override void OnEnabled()
	{
		StoreBaseTransform();
	}

	protected override void OnDisabled()
	{
		ResetToBase();
	}

	private void StoreBaseTransform()
	{
		if ( hasStoredBase ) return;

		baseLocalPosition = GameObject.LocalPosition;
		baseLocalRotation = GameObject.LocalRotation;
		hasStoredBase = true;
	}

	private void ResetToBase()
	{
		if ( !hasStoredBase ) return;

		GameObject.LocalPosition = baseLocalPosition;
		GameObject.LocalRotation = baseLocalRotation;
	}

	protected override void OnUpdate()
	{
		if ( !IsActive || Strength <= 0f )
		{
			ResetToBase();
			return;
		}

		float deltaTime = UseRealTime ? RealTime.Delta : Time.Delta;

		// Advance noise time
		positionTime += deltaTime * PositionFrequency;
		rotationTime += deltaTime * RotationFrequency;

		// Calculate position offset
		Vector3 positionOffset = Vector3.Zero;
		if ( ShakePosition && PositionAmount > 0f )
		{
			positionOffset = new Vector3(
				SampleNoise( positionTime, 0f ) * PositionAmount,
				SampleNoise( positionTime, 100f ) * PositionAmount,
				SampleNoise( positionTime, 200f ) * PositionAmount
			) * Strength;
		}

		// Calculate rotation offset
		Rotation rotationOffset = Rotation.Identity;
		if ( ShakeRotation && RotationAmount > 0f )
		{
			float pitch = SampleNoise( rotationTime, 300f ) * RotationAmount * Strength;
			float yaw = SampleNoise( rotationTime, 400f ) * RotationAmount * Strength;
			float roll = SampleNoise( rotationTime, 500f ) * RotationAmount * Strength;
			rotationOffset = Rotation.From( pitch, yaw, roll );
		}

		// Apply
		GameObject.LocalPosition = baseLocalPosition + positionOffset;
		GameObject.LocalRotation = baseLocalRotation * rotationOffset;
	}

	/// <summary>
	/// Sample Perlin noise centered around 0 (range -1 to 1).
	/// </summary>
	private float SampleNoise( float time, float offset )
	{
		// Perlin returns 0-1, we want -1 to 1
		return (Noise.Perlin( time + noiseSeed, offset + noiseSeed ) - 0.5f) * 2f;
	}

	// === Public API ===

	/// <summary>
	/// Start shaking with the current settings.
	/// </summary>
	public void Play()
	{
		IsActive = true;
		Strength = 1f;
	}

	/// <summary>
	/// Start shaking with a specific strength.
	/// </summary>
	public void Play( float strength )
	{
		IsActive = true;
		Strength = strength;
	}

	/// <summary>
	/// Stop shaking and reset to base transform.
	/// </summary>
	public void Stop()
	{
		IsActive = false;
		Strength = 0f;
		ResetToBase();
	}
}

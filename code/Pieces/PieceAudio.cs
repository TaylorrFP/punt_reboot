namespace Punt;

using System;

/// <summary>
/// Handles the "rubber band" audio feedback when a piece is being grabbed and charged.
/// 
/// The stretch sound behaves like pulling a rubber band:
/// - Pitch increases with drag distance (more tension = higher pitch)
/// - Volume spikes when actively stretching (cursor moving)
/// - A minimum "tension hum" volume increases as you approach max charge
/// </summary>
public sealed class PieceAudio : Component
{
	// === Sound ===

	[Property, Group( "Sound" )]
	public SoundEvent StretchSound { get; set; }

	/// <summary>
	/// If true, the sound starts at a random position each time.
	/// Makes repeated grabs sound more varied.
	/// </summary>
	[Property, Group( "Sound" )]
	public bool RandomizeStartPosition { get; set; } = true;

	// === Pitch ===

	/// <summary>
	/// Maps drag intensity (0-1) to pitch multiplier.
	/// Typically starts around 0.8 and rises to 1.5 at full charge.
	/// </summary>
	[Property, Group( "Pitch" )]
	public Curve PitchCurve { get; set; } = new Curve( new Curve.Frame( 0f, 0.8f ), new Curve.Frame( 1f, 1.5f ) );

	// === Volume ===

	/// <summary>
	/// Volume when actively stretching (cursor moving).
	/// </summary>
	[Property, Group( "Volume" ), Range( 0f, 1f )]
	public float StretchVolume { get; set; } = 0.6f;

	/// <summary>
	/// Maps drag intensity (0-1) to minimum volume as a percentage of StretchVolume.
	/// At low intensity, silence when not moving. At high intensity, 
	/// there's always a baseline hum representing the held tension.
	/// Value of 1.0 = 100% of StretchVolume.
	/// </summary>
	[Property, Group( "Volume" )]
	public Curve MinVolumeCurve { get; set; } = new Curve( new Curve.Frame( 0f, 0f ), new Curve.Frame( 0.5f, 0f ), new Curve.Frame( 1f, 0.7f ) );

	// === Smoothing ===

	/// <summary>
	/// How quickly volume ramps up when stretching.
	/// </summary>
	[Property, Group( "Smoothing" )]
	public float VolumeAttackSpeed { get; set; } = 15f;

	/// <summary>
	/// How quickly volume fades when cursor stops moving.
	/// </summary>
	[Property, Group( "Smoothing" )]
	public float VolumeDecaySpeed { get; set; } = 5f;

	/// <summary>
	/// How quickly pitch changes to match target.
	/// </summary>
	[Property, Group( "Smoothing" )]
	public float PitchSmoothSpeed { get; set; } = 10f;

	/// <summary>
	/// Minimum cursor movement per frame to be considered "stretching".
	/// </summary>
	[Property, Group( "Smoothing" )]
	public float MovementThreshold { get; set; } = 0.5f;

	// === Internal ===

	private SoundHandle soundHandle;
	private float currentVolume;
	private float targetVolume;
	private float currentPitch;
	private float targetPitch;
	private bool isPlaying;

	/// <summary>
	/// Call each frame while the piece is grabbed.
	/// </summary>
	/// <param name="intensity">Normalized drag distance (0 = no pull, 1 = max pull).</param>
	/// <param name="cursorDelta">How much the cursor moved this frame.</param>
	public void UpdateStretch( float intensity, float cursorDelta )
	{
		// Start sound if needed
		if ( !isPlaying )
		{
			StartSound();
		}

		// Calculate target pitch from curve
		targetPitch = PitchCurve.Evaluate( intensity );

		// Calculate minimum volume (curve outputs 0-1 as percentage of StretchVolume)
		float minVolumePercent = MinVolumeCurve.Evaluate( intensity );
		float minVolume = minVolumePercent * StretchVolume;

		bool isStretching = cursorDelta > MovementThreshold;

		if ( isStretching )
		{
			// Active stretching - use full stretch volume
			targetVolume = StretchVolume;
		}
		else
		{
			// Not moving - decay to tension minimum
			targetVolume = minVolume;
		}
	}

	/// <summary>
	/// Call when the piece is released.
	/// </summary>
	public void StopStretch()
	{
		targetVolume = 0f;
		// Let OnUpdate handle the fade out and cleanup
	}

	private void StartSound()
	{
		if ( StretchSound == null ) return;

		isPlaying = true;
		currentVolume = 0f;
		currentPitch = PitchCurve.Evaluate( 0f );

		soundHandle = Sound.Play( StretchSound, WorldPosition );
		if ( soundHandle.IsValid )
		{
			soundHandle.Volume = 0f;
			soundHandle.Pitch = currentPitch;

			// Start at random position so it doesn't sound identical each time
			if ( RandomizeStartPosition )
			{
				soundHandle.Position = Random.Shared.Float( 0f, 3f );
			}
		}
	}

	protected override void OnUpdate()
	{
		if ( !isPlaying ) return;

		float dt = Time.Delta;

		// Smooth volume (fast attack, slower decay)
		float volumeSpeed = targetVolume > currentVolume ? VolumeAttackSpeed : VolumeDecaySpeed;
		currentVolume = MathX.Lerp( currentVolume, targetVolume, dt * volumeSpeed );

		// Smooth pitch
		currentPitch = MathX.Lerp( currentPitch, targetPitch, dt * PitchSmoothSpeed );

		// Apply to sound
		if ( soundHandle.IsValid )
		{
			soundHandle.Volume = currentVolume;
			soundHandle.Pitch = currentPitch;
			soundHandle.Position = WorldPosition;
		}

		// Stop when fully faded out
		if ( currentVolume < 0.01f && targetVolume <= 0f )
		{
			soundHandle.Stop();
			isPlaying = false;
		}
	}

	protected override void OnDestroy()
	{
		//soundHandle.Stop();
	}
}













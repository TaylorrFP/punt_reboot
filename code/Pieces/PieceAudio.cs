using Sandbox;

/// <summary>
/// Handles audio feedback for a piece being grabbed and charged.
/// Plays a stretching/charging sound that increases in pitch as drag intensity increases.
/// </summary>
public sealed class PieceAudio : Component
{
	// === Sound Configuration ===

	[Property, Group( "Stretch Sound" )]
	public SoundEvent StretchSound { get; set; }

	/// <summary>
	/// Minimum pitch when barely dragging.
	/// </summary>
	[Property, Group( "Stretch Sound" )]
	public float MinPitch { get; set; } = 0.8f;

	/// <summary>
	/// Maximum pitch at full charge.
	/// </summary>
	[Property, Group( "Stretch Sound" )]
	public float MaxPitch { get; set; } = 1.5f;

	/// <summary>
	/// Volume of the stretch sound.
	/// </summary>
	[Property, Group( "Stretch Sound" ), Range( 0f, 1f )]
	public float Volume { get; set; } = 0.5f;

	// === Tension Sound (at max charge) ===

	[Property, Group( "Tension Sound" )]
	public SoundEvent TensionSound { get; set; }

	/// <summary>
	/// Intensity threshold (0-1) at which tension sound starts playing.
	/// </summary>
	[Property, Group( "Tension Sound" ), Range( 0f, 1f )]
	public float TensionThreshold { get; set; } = 0.85f;

	// === Behaviour ===

	/// <summary>
	/// How fast the sound fades out when cursor stops moving.
	/// </summary>
	[Property, Group( "Behaviour" )]
	public float FadeOutSpeed { get; set; } = 5f;

	/// <summary>
	/// Minimum cursor delta to be considered "moving".
	/// </summary>
	[Property, Group( "Behaviour" )]
	public float MovementThreshold { get; set; } = 0.5f;

	/// <summary>
	/// How quickly pitch changes (smoothing).
	/// </summary>
	[Property, Group( "Behaviour" )]
	public float PitchSmoothSpeed { get; set; } = 10f;

	// === Internal State ===

	private SoundHandle stretchHandle;
	private SoundHandle tensionHandle;

	private float currentVolume;
	private float targetVolume;
	private float currentPitch;
	private float targetPitch;
	private float currentIntensity;
	private bool isActive;

	/// <summary>
	/// Call this each frame while the piece is being dragged.
	/// </summary>
	/// <param name="intensity">0-1 drag intensity (distance / max distance).</param>
	/// <param name="cursorDelta">How much the cursor moved this frame.</param>
	public void UpdateStretch( float intensity, float cursorDelta )
	{
		currentIntensity = intensity;
		bool isMoving = cursorDelta > MovementThreshold;

		// Start sound if not playing
		if ( !isActive )
		{
			StartStretchSound();
		}

		// Target pitch based on intensity
		targetPitch = MathX.Lerp( MinPitch, MaxPitch, intensity );

		// Target volume based on movement (or high intensity)
		if ( isMoving || intensity >= TensionThreshold )
		{
			targetVolume = Volume;
		}
		else
		{
			targetVolume = 0f;
		}

		// Handle tension sound at high intensity
		UpdateTensionSound( intensity );
	}

	/// <summary>
	/// Call this when the piece is released or deselected.
	/// </summary>
	public void StopStretch()
	{
		isActive = false;
		targetVolume = 0f;

		// Stop tension immediately
		tensionHandle.Stop();
	}

	private void StartStretchSound()
	{
		if ( StretchSound == null ) return;

		isActive = true;
		currentVolume = 0f;
		currentPitch = MinPitch;

		// Start looping sound
		stretchHandle = Sound.Play( StretchSound, WorldPosition );
		if ( stretchHandle.IsValid )
		{
			stretchHandle.Volume = 0f;
			stretchHandle.Pitch = MinPitch;
		}
	}

	private void UpdateTensionSound( float intensity )
	{
		if ( TensionSound == null ) return;

		bool shouldPlayTension = intensity >= TensionThreshold;

		if ( shouldPlayTension && !tensionHandle.IsValid )
		{
			// Start tension sound
			tensionHandle = Sound.Play( TensionSound, WorldPosition );
		}
		else if ( !shouldPlayTension && tensionHandle.IsValid )
		{
			// Stop tension sound
			tensionHandle.Stop();
		}
	}

	protected override void OnUpdate()
	{
		if ( !isActive && currentVolume <= 0.01f )
		{
			// Fully stopped
			if ( stretchHandle.IsValid )
			{
				stretchHandle.Stop();
			}
			return;
		}

		float dt = Time.Delta;

		// Smooth volume
		if ( targetVolume > currentVolume )
		{
			currentVolume = MathX.Lerp( currentVolume, targetVolume, dt * PitchSmoothSpeed );
		}
		else
		{
			currentVolume = MathX.Lerp( currentVolume, targetVolume, dt * FadeOutSpeed );
		}

		// Smooth pitch
		currentPitch = MathX.Lerp( currentPitch, targetPitch, dt * PitchSmoothSpeed );

		// Apply to sound
		if ( stretchHandle.IsValid )
		{
			stretchHandle.Volume = currentVolume;
			stretchHandle.Pitch = currentPitch;
			stretchHandle.Position = WorldPosition;
		}

		// Update tension position
		if ( tensionHandle.IsValid )
		{
			tensionHandle.Position = WorldPosition;
		}
	}

	protected override void OnDestroy()
	{
		stretchHandle.Stop();
		tensionHandle.Stop();
	}
}

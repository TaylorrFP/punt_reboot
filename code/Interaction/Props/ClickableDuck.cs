using Sandbox;
using System;


/// <summary>
/// A duck that quacks when clicked. Very important gameplay feature.
/// </summary>
public sealed class ClickableDuck : ClickableProp
{
	[Property] public SoundEvent QuackSound { get; set; }
	[Property] public ModelRenderer DuckModel { get; set; }

	// Optional: wobble animation settings
	[Property] public float WobbleAmount { get; set; } = 15f;
	[Property] public float WobbleDuration { get; set; } = 0.3f;

	private TimeSince timeSinceClicked;
	private bool isWobbling;
	private Rotation originalRotation;

	protected override void OnStart()
	{
		base.OnStart();

		// Store the original rotation set in the editor
		if ( DuckModel != null )
		{
			originalRotation = DuckModel.LocalRotation;
		}
	}

	protected override void OnClicked()
	{
		// Quack!
		if ( QuackSound != null )
		{
			Sound.Play( QuackSound );
		}
		else
		{
			// Fallback if no sound assigned
			Log.Info( "Quack!" );
		}

		// Start wobble animation
		isWobbling = true;
		timeSinceClicked = 0;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( isWobbling && DuckModel != null )
		{
			// Simple wobble: rotate back and forth, then settle
			float progress = timeSinceClicked / WobbleDuration;

			if ( progress >= 1f )
			{
				// Animation finished
				isWobbling = false;
				DuckModel.LocalRotation = originalRotation;
			}
			else
			{
				// Wobble with decreasing intensity, relative to original rotation
				float wobble = MathF.Sin( progress * MathF.PI * 4 ) * WobbleAmount * (1f - progress);
				DuckModel.LocalRotation = originalRotation * Rotation.FromYaw( wobble );
			}
		}
	}
}













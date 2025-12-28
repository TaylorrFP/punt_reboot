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
				DuckModel.LocalRotation = Rotation.Identity;
			}
			else
			{
				// Wobble with decreasing intensity
				float wobble = MathF.Sin( progress * MathF.PI * 4 ) * WobbleAmount * (1f - progress);
				DuckModel.LocalRotation = Rotation.FromYaw( wobble );
			}
		}
	}
}

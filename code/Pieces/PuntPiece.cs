using Punt;
using Punt.UI;
using Sandbox;
using System;

public sealed class PuntPiece : Component, ISelectable
{
	// === Networked State ===
	[Property, Sync] public TeamSide Team { get; set; }
	[Property, Sync] public PieceState State { get; private set; } = PieceState.Ready;
	[Property, Sync] public Guid GrabbedByPlayerId { get; private set; }
	[Property, Sync] public Vector3 AimTargetPosition { get; private set; }

	// === Settings ===
	[Property, Group( "Gameplay" )] public float CooldownDuration { get; set; } = 2f;

	// === Components ===
	[Property, Group( "Components" )] public Rigidbody Rigidbody { get; set; }
	[Property, Group( "Components" )] public SelectableHighlight Highlight { get; set; }
	[Property, Group( "Components" )] public ShakeEffect ShakeEffect { get; set; }
	[Property, Group( "Components" )] public SquashAndStretch SquashStretch { get; set; }
	[Property, Group( "Components" )] public AimIndicator AimIndicator { get; set; }

	// === ISelectable Implementation ===
	public bool CanSelect => State == PieceState.Ready || State == PieceState.Hovered;

	[Property, Group( "Selection" )] public float selectPriority = 100f;
	public float SelectPriority => selectPriority;
	[Property, Group( "Selection" )] public float selectRadius = 100f;
	public float SelectRadius => selectRadius;
	public Vector3 SelectPosition => WorldPosition;
	public bool CapturesSelection => true;

	// === Cooldown ===
	private TimeSince timeSinceCooldownStarted;

	public void OnHoverEnter()
	{
		if ( State == PieceState.Ready )
		{
			State = PieceState.Hovered;
			Highlight?.SetState( SelectableHighlightState.Hovered );
			Sound.Play( "sounds/kenny/puntpiecehover.sound" );
		}
	}

	public void OnHoverExit()
	{
		if ( State == PieceState.Hovered )
		{
			State = PieceState.Ready;
			Highlight?.SetState( SelectableHighlightState.None );
		}
	}

	public void OnSelect()
	{
		if ( !CanSelect ) return;

		State = PieceState.Grabbed;
		Highlight?.SetState( SelectableHighlightState.Selected );
		SquashStretch?.Play( 0.3f );
		ShakeEffect?.Play( 1f );
		Sound.Play( "sounds/kenny/pieceselect.sound" );

		// Start with indicator hidden - it will become visible once MinFlickDistance is exceeded
		if ( AimIndicator != null )
		{
			AimIndicator.IsVisible = false;
			AimIndicator.StartPosition = WorldPosition;
		}
	}

	public void OnDragUpdate( float intensity, float cursorDelta, Vector3 cursorPosition, bool exceedsMinimum, bool invertIndicator = false )
	{
		// Update the aim target position for the indicator
		AimTargetPosition = cursorPosition;

		// Update the aim indicator
		if ( AimIndicator != null && State == PieceState.Grabbed )
		{
			AimIndicator.StartPosition = WorldPosition;
			AimIndicator.EndPosition = cursorPosition;
			AimIndicator.IsInverted = invertIndicator;

			// Only show indicator once we exceed the minimum threshold
			AimIndicator.IsVisible = exceedsMinimum;
		}

		// Scale shake effect with drag intensity
		if ( ShakeEffect != null )
		{
			//ShakeEffect.Strength = intensity;
		}
	}

	public void OnDeselect( Vector3 flickVelocity )
	{
		if ( State != PieceState.Grabbed ) return;

		// Hide the aim indicator
		if ( AimIndicator != null )
		{
			AimIndicator.IsVisible = false;
		}

		// Stop effects
		ShakeEffect?.Stop();

		// Apply flick
		Rigidbody.Velocity = flickVelocity*2.5f; //random value for now

		Sound.Play( "sounds/custom/elastic/boing.sound" );

		// Start cooldown
		State = PieceState.Ready;
		GrabbedByPlayerId = Guid.Empty;
		timeSinceCooldownStarted = 0;
		Highlight?.SetState( SelectableHighlightState.None );
	}

	public void OnAbort()
	{
		if ( State != PieceState.Grabbed ) return;

		// Hide the aim indicator
		if ( AimIndicator != null )
		{
			AimIndicator.IsVisible = false;
		}

		// Stop effects
		ShakeEffect?.Stop();
		SquashStretch?.Stop();

		// Return to ready state WITHOUT applying cooldown or flick
		State = PieceState.Ready;
		GrabbedByPlayerId = Guid.Empty;
		Highlight?.SetState( SelectableHighlightState.None );

		// Play cancel sound (quieter)
		Sound.Play( "sounds/kenny/pieceabort.sound");
	}

	protected override void OnUpdate()
	{
		// Handle cooldown timer
		if ( State == PieceState.Cooldown )
		{
			if ( timeSinceCooldownStarted >= CooldownDuration )
			{
				State = PieceState.Ready;
			}
		}
	}

	/// <summary>
	/// Cooldown progress from 0 (just started) to 1 (ready).
	/// </summary>
	public float CooldownProgress => State == PieceState.Cooldown
		? Math.Min( 1f, timeSinceCooldownStarted / CooldownDuration )
		: 1f;
}

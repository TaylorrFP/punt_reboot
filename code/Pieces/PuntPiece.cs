using Punt;
using Sandbox;
using System;

public sealed class PuntPiece : Component, ISelectable
{
	// === Networked State ===
	[Property, Sync] public TeamSide Team { get; set; }
	[Property, Sync] public PieceState State { get; private set; } = PieceState.Ready;
	[Property, Sync] public Guid GrabbedByPlayerId { get; private set; }

	// === Settings ===
	[Property, Group( "Gameplay" )] public float CooldownDuration { get; set; } = 2f;

	// === Components ===
	[Property, Group( "Components" )] public Rigidbody Rigidbody { get; set; }
	[Property, Group( "Components" )] public SelectableHighlight Highlight { get; set; }
	[Property, Group( "Components" )] public ShakeEffect ShakeEffect { get; set; }
	[Property, Group( "Components" )] public SquashAndStretch SquashStretch { get; set; }

	// === ISelectable Implementation ===
	public bool CanSelect => State == PieceState.Ready || State == PieceState.Hovered;
	public float SelectPriority => 100f;
	public float SelectRadius => 100f;
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
	}

	public void OnDragUpdate( float intensity, float cursorDelta )
	{
		// Scale shake effect with drag intensity
		if ( ShakeEffect != null )
		{
			//ShakeEffect.Strength = intensity;
		}


	}

	public void OnDeselect( Vector3 flickVelocity )
	{
		if ( State != PieceState.Grabbed ) return;

		// Stop effects
		ShakeEffect?.Stop();

		// Apply flick
		Rigidbody.Velocity = flickVelocity;

		// Start cooldown
		State = PieceState.Ready;
		GrabbedByPlayerId = Guid.Empty;
		timeSinceCooldownStarted = 0;
		Highlight?.SetState( SelectableHighlightState.None );
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

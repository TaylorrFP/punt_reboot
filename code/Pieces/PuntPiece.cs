using Sandbox;
using System;

public sealed class PuntPiece : Component, ISelectable
{
	// === Networked State ===
	[Property, Sync] public TeamSide Team { get; set; }
	[Property, Sync] public PieceState State { get; private set; } = PieceState.Ready;
	[Property, Sync] public Guid GrabbedByPlayerId { get; private set; }

	// === Settings ===
	[Property] public float CooldownDuration { get; set; } = 2f;

	// === Components ===
	[Property] public Rigidbody Rigidbody { get; set; }
	[Property] public SelectableHighlight Highlight { get; set; }

	// === ISelectable Implementation ===
	public bool CanSelect => State == PieceState.Ready || State == PieceState.Hovered;
	public float SelectPriority => 100f; // High priority - pieces are important
	public float SelectRadius => 100f;   // Large radius
	public Vector3 SelectPosition => WorldPosition;
	public bool CapturesSelection => true; // Draggable

	public void OnHoverEnter()
	{
		if ( State == PieceState.Ready )
		{
			State = PieceState.Hovered;
			Highlight?.SetState( SelectableHighlightState.Hovered );
			Sound.Play( "sounds/piecehover.sound" );
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
		Sound.Play( "sounds/pieceselect.sound" );
	}

	public void OnDeselect( Vector3 flickVelocity )
	{
		if ( State != PieceState.Grabbed ) return;

		// Apply flick
		Rigidbody.Velocity = flickVelocity;

		// Start cooldown
		State = PieceState.Cooldown;
		GrabbedByPlayerId = Guid.Empty;
		Highlight?.SetState( SelectableHighlightState.None );
	}

	// ... rest of Piece logic (cooldown timer, etc.)
}

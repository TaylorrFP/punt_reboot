using Sandbox;
/// <summary>
/// Base class for clickable interactive props on the pitch.
/// </summary>
public class ClickableProp : Component, ISelectable
{
	[Property] public SelectableHighlight Highlight { get; set; }
	[Property] public float Radius { get; set; } = 30f;

	// ISelectable
	public bool CanSelect => true;
	public float SelectPriority => 10f;
	public float SelectRadius => Radius;
	public Vector3 SelectPosition => WorldPosition;
	public bool CapturesSelection => false; // Props don't capture, they just trigger clicks
	public void OnHoverExit() => Highlight?.SetState( SelectableHighlightState.None );
	public void OnSelect() => OnClicked();
	public void OnDeselect( Vector3 releaseData ) { }

	// Props don't capture selection, so this won't be called
	public void OnDragUpdate( float intensity, float cursorDelta, Vector3 cursorPosition ) { }

	protected virtual void OnClicked()
	{
		Log.Info( $"Clicked: {GameObject.Name}" );
	}

	public void OnHoverEnter()
	{
		Highlight?.SetState( SelectableHighlightState.Hovered );
		Sound.Play( "sounds/kenny/proppiecehover.sound" );
	}
}

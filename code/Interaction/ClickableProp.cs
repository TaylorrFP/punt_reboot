// Interaction/ClickableProp.cs
public class ClickableProp : Component, ISelectable
{
	[Property] public SelectableHighlight Highlight { get; set; }
	[Property] public float Radius { get; set; } = 30f;
	[Property] public SoundEvent HoverSound { get; set; }

	// ISelectable
	public bool CanSelect => true;
	public float SelectPriority => 10f;
	public float SelectRadius => Radius;
	public Vector3 SelectPosition => WorldPosition;
	public bool CapturesSelection => true;
	public void OnHoverExit() => Highlight?.SetState( SelectableHighlightState.None );
	public void OnSelect() => OnClicked();
	public void OnDeselect( Vector3 releaseData ) { }


	protected virtual void OnClicked()
	{
		Log.Info( $"Clicked: {GameObject.Name}" );
	}

	public void OnHoverEnter()
	{
		Highlight?.SetState( SelectableHighlightState.Hovered );
		Sound.Play( HoverSound );
	}


}

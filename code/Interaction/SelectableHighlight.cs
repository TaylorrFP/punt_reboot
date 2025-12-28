using Sandbox;

/// <summary>
/// Attach to any GameObject with an ISelectable to get automatic outline/highlight.
/// </summary>
public sealed class SelectableHighlight : Component
{
	[Property] public HighlightOutline Outline { get; set; }

	[Property] public Color HoverColor { get; set; } = Color.Yellow;
	[Property] public Color SelectedColor { get; set; } = Color.Green;
	[Property] public Color DisabledColor { get; set; } = Color.Gray;

	[Property] public float HoverWidth { get; set; } = 0.25f;
	[Property] public float SelectedWidth { get; set; } = 0.4f;

	public void SetState( SelectableHighlightState state )
	{
		switch ( state )
		{
			case SelectableHighlightState.None:
				Outline.Enabled = false;
				break;

			case SelectableHighlightState.Hovered:
				Outline.Enabled = true;
				Outline.Color = HoverColor;
				Outline.Width = HoverWidth;
				break;

			case SelectableHighlightState.Selected:
				Outline.Enabled = true;
				Outline.Color = SelectedColor;
				Outline.Width = SelectedWidth;
				break;

			case SelectableHighlightState.Disabled:
				Outline.Enabled = true;
				Outline.Color = DisabledColor;
				Outline.Width = HoverWidth;
				break;
		}
	}
}

public enum SelectableHighlightState
{
	None,
	Hovered,
	Selected,
	Disabled
}

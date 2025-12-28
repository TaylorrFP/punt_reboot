/// <summary>
/// Anything the player can hover over and interact with.
/// </summary>
public interface ISelectable
{
	/// <summary>
	/// Can this be selected right now?
	/// </summary>
	bool CanSelect { get; }

	/// <summary>
	/// Higher priority = selected first when overlapping.
	/// Pieces should be high (100), props should be low (10).
	/// </summary>
	float SelectPriority { get; }

	/// <summary>
	/// Maximum distance from cursor to be considered for selection.
	/// </summary>
	float SelectRadius { get; }

	/// <summary>
	/// World position for distance checks.
	/// </summary>
	Vector3 SelectPosition { get; }

	/// <summary>
	/// Called when cursor enters hover range.
	/// </summary>
	void OnHoverEnter();

	/// <summary>
	/// Called when cursor exits hover range.
	/// </summary>
	void OnHoverExit();

	/// <summary>
	/// Called when clicked/selected.
	/// </summary>
	void OnSelect();

	/// <summary>
	/// Called when selection ends (for draggable things like pieces).
	/// </summary>
	void OnDeselect( Vector3 releaseData );

	/// <summary>
	/// Does this selectable capture the cursor while selected?
	/// True for pieces (drag to flick), false for props (just click).
	/// </summary>
	bool CapturesSelection { get; }

	/// <summary>
	/// Called each frame while this selectable is being dragged.
	/// Only called if CapturesSelection is true.
	/// </summary>
	/// <param name="intensity">0-1 value representing drag distance / max distance.</param>
	/// <param name="cursorDelta">How much the cursor moved this frame.</param>
	void OnDragUpdate( float intensity, float cursorDelta );
}

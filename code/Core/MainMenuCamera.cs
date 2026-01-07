using Sandbox;

namespace Punt;

/// <summary>
/// A camera component for the main menu that subtly pans based on mouse screen position.
/// Pans along the camera's local X axis (horizontal) and Z axis (vertical).
/// </summary>
public sealed class MainMenuCamera : Component
{
	/// <summary>
	/// Maximum pan distance on the horizontal (local X) axis.
	/// </summary>
	[Property, Group( "Pan Settings" )]
	public float HorizontalPanRange { get; set; } = 50f;

	/// <summary>
	/// Maximum pan distance on the vertical (local Z) axis.
	/// </summary>
	[Property, Group( "Pan Settings" )]
	public float VerticalPanRange { get; set; } = 30f;

	/// <summary>
	/// How smoothly the camera follows the target position. Higher = snappier.
	/// </summary>
	[Property, Group( "Pan Settings" )]
	public float Smoothing { get; set; } = 5f;

	/// <summary>
	/// If true, the camera pans in the opposite direction of the mouse (parallax effect).
	/// </summary>
	[Property, Group( "Pan Settings" )]
	public bool InvertPan { get; set; } = false;

	/// <summary>
	/// The base/origin position of the camera (set on start).
	/// </summary>
	private Vector3 _basePosition;

	/// <summary>
	/// Current offset from the base position.
	/// </summary>
	private Vector3 _currentOffset;

	protected override void OnStart()
	{
		_basePosition = WorldPosition;
		_currentOffset = Vector3.Zero;
	}

	protected override void OnUpdate()
	{
		UpdateCameraPan();
	}

	private void UpdateCameraPan()
	{
		// Get normalized mouse position (-1 to 1, with 0 being center of screen)
		var normalizedX = (Mouse.Position.x / Screen.Width - 0.5f) * 2f;
		var normalizedY = (Mouse.Position.y / Screen.Height - 0.5f) * 2f;

		// Clamp to ensure we stay within bounds even if mouse goes off-screen
		normalizedX = normalizedX.Clamp( -1f, 1f );
		normalizedY = normalizedY.Clamp( -1f, 1f );

		// Invert if desired (creates a parallax-like effect)
		var multiplier = InvertPan ? -1f : 1f;

		// Calculate target offset in local space
		// X = horizontal, Z = vertical (Y is typically depth/forward in S&box)
		var targetOffsetLocal = new Vector3(
			0f,
			-normalizedX * HorizontalPanRange * multiplier,
			-normalizedY * VerticalPanRange * multiplier // Negative because screen Y is inverted
		);

		// Convert local offset to world offset using the camera's rotation
		var targetOffsetWorld = WorldRotation * targetOffsetLocal;

		// Smoothly interpolate to target
		_currentOffset = _currentOffset.LerpTo( targetOffsetWorld, Time.Delta * Smoothing );

		// Apply the offset to the base position
		WorldPosition = _basePosition + _currentOffset;
	}

	/// <summary>
	/// Resets the camera to its base position.
	/// </summary>
	public void ResetToBase()
	{
		_currentOffset = Vector3.Zero;
		WorldPosition = _basePosition;
	}

	/// <summary>
	/// Updates the base position to the current world position.
	/// Useful if you need to reposition the camera origin at runtime.
	/// </summary>
	public void SetNewBasePosition()
	{
		_basePosition = WorldPosition - _currentOffset;
	}

	/// <summary>
	/// Sets a specific base position for the camera.
	/// </summary>
	public void SetBasePosition( Vector3 position )
	{
		_basePosition = position;
	}
}

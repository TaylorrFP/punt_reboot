using System;
using Sandbox;

public sealed class PuntBall : Component
{
	[Property] public WorldPanel ballGuide;

	[Property] public Rigidbody ballRB;

	[Property] public GameObject ballModel;

	[Property] public ModelRenderer ballRenderer;

	[Property, Group( "Squash & Stretch" )] public float StretchSpeedThreshold { get; set; } = 200f;
	[Property, Group( "Squash & Stretch" )] public float MaxStretch { get; set; } = 1.4f;
	[Property, Group( "Squash & Stretch" )] public float MaxSquash { get; set; } = 0.6f;
	[Property, Group( "Squash & Stretch" )] public float ImpactThreshold { get; set; } = 50f;
	[Property, Group( "Squash & Stretch" )] public float ImpactSquashMultiplier { get; set; } = 0.002f;
	[Property, Group( "Squash & Stretch" )] public float RecoverySpeed { get; set; } = 5f;
	[Property, Group( "Squash & Stretch" )] public float AxisSmoothSpeed { get; set; } = 10f;

	private Vector3 lastVelocity;
	private float currentStretchAmount = 1f;
	private Vector3 currentAxis = Vector3.Up;
	private float impactStretch = 1f;

	protected override void OnUpdate()
	{
		CalculateBallGuide();
		CalculateSquashStretch();
	}

	private void CalculateSquashStretch()
	{
		var velocity = ballRB.Velocity;
		var deltaTime = Time.Delta;

		// Detect sharp velocity changes (impacts)
		var velocityChange = velocity - lastVelocity;
		var impactMagnitude = velocityChange.Length;

		// On impact, squash in the direction of velocity change
		if ( impactMagnitude > ImpactThreshold )
		{
			float squashAmount = 1f - (impactMagnitude * ImpactSquashMultiplier);
			impactStretch = MathF.Max( squashAmount, MaxSquash );

			// Set axis to the impact direction
			if ( velocityChange.Length > 0.1f )
			{
				currentAxis = velocityChange.Normal;
			}
		}

		// Calculate velocity-based stretch
		float speed = velocity.Length;
		float velocityStretch = 1f;
		if ( speed > StretchSpeedThreshold )
		{
			float stretchFactor = (speed - StretchSpeedThreshold) / StretchSpeedThreshold;
			velocityStretch = 1f + stretchFactor * (MaxStretch - 1f);
			velocityStretch = MathF.Min( velocityStretch, MaxStretch );
		}

		// Smoothly recover impact squash back to 1
		impactStretch = impactStretch.LerpTo( 1f, deltaTime * RecoverySpeed );

		// Combine: use the more extreme value (squash wins over stretch during impact)
		float targetStretch = impactStretch < 1f ? impactStretch : velocityStretch;

		// Smoothly interpolate to target for spongy feel
		currentStretchAmount = currentStretchAmount.LerpTo( targetStretch, deltaTime * RecoverySpeed );

		// Smoothly interpolate axis direction
		if ( speed > 10f )
		{
			currentAxis = currentAxis.LerpTo( velocity.Normal, deltaTime * AxisSmoothSpeed ).Normal;
		}

		// Apply to shader
		if ( ballRenderer != null && ballRenderer.SceneObject != null )
		{
			ballRenderer.SceneObject.Attributes.Set( "ObjectPivot", GameObject.WorldPosition );
			ballRenderer.SceneObject.Attributes.Set( "Axis", currentAxis );
			ballRenderer.SceneObject.Attributes.Set( "StretchAmount", currentStretchAmount );
		}

		lastVelocity = velocity;

		Vector2 textScreenPos = Scene.Camera.PointToScreenPixels( WorldPosition );
		Gizmo.Draw.Color = Color.Black;
		Gizmo.Draw.ScreenText(
			$"Velocity: {velocity.Length:F0}\nStretch: {currentStretchAmount:F2}",
			textScreenPos,
			"roboto",
			16f
		);
	}

	private void CalculateBallGuide()
	{
		if ( ballGuide == null ) return;
		ballGuide.WorldPosition = new Vector3( WorldPosition.x, WorldPosition.y, 10f );
		ballGuide.WorldRotation = Rotation.From( new Angles( -90f, 0, 0 ) );
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		// Update shader in editor
		if ( ballRenderer != null && ballRenderer.SceneObject != null )
		{
			ballRenderer.SceneObject.Attributes.Set( "ObjectPivot", GameObject.WorldPosition );
			ballRenderer.SceneObject.Attributes.Set( "Axis", Vector3.Up );
			ballRenderer.SceneObject.Attributes.Set( "StretchAmount", currentStretchAmount );
		}
	}

}

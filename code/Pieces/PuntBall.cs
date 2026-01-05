using System;
using Sandbox;

public sealed class PuntBall : Component
{
	[Property] public WorldPanel ballGuide;

	[Property] public Rigidbody ballRB;

	[Property] public GameObject ballModel;

	[Property] public ModelRenderer ballRenderer;

	[Property, Group( "Squash & Stretch" )] public float ImpactThreshold { get; set; } = 50f;
	[Property, Group( "Squash & Stretch" )] public float ImpactStrength { get; set; } = 0.003f;
	[Property, Group( "Squash & Stretch" )] public float SpringFrequency { get; set; } = 8f;
	[Property, Group( "Squash & Stretch" )] public float SpringDamping { get; set; } = 0.5f;
	[Property, Group( "Squash & Stretch" )] public float MaxSquash { get; set; } = 0.5f;
	[Property, Group( "Squash & Stretch" )] public float MaxStretch { get; set; } = 1.5f;
	[Property, Group( "Squash & Stretch" )] public float AxisBlendSpeed { get; set; } = 15f;

	private Vector3 lastVelocity;
	private float currentStretchAmount = 1f;
	private float stretchVelocity = 0f;
	private Vector3 currentAxis = Vector3.Up;
	private Vector3 targetAxis = Vector3.Up;

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

		// On impact, apply an impulse to the spring (pushes toward squash)
		if ( impactMagnitude > ImpactThreshold )
		{
			// Calculate squash impulse based on impact strength
			float impulse = impactMagnitude * ImpactStrength;
			stretchVelocity -= impulse;

			// Set target axis to the impact direction
			if ( velocityChange.Length > 0.1f )
			{
				targetAxis = velocityChange.Normal;
			}
		}

		// Smoothly blend axis toward target
		currentAxis = currentAxis.LerpTo( targetAxis, deltaTime * AxisBlendSpeed ).Normal;

		// Damped spring simulation toward rest (1.0)
		// Spring force pulls toward 1.0
		float displacement = currentStretchAmount - 1f;
		float springForce = -displacement * SpringFrequency * SpringFrequency;
		float dampingForce = -stretchVelocity * 2f * SpringDamping * SpringFrequency;

		// Apply forces to velocity
		stretchVelocity += (springForce + dampingForce) * deltaTime;

		// Update position
		currentStretchAmount += stretchVelocity * deltaTime;

		// Clamp to limits
		currentStretchAmount = MathF.Max( MaxSquash, MathF.Min( MaxStretch, currentStretchAmount ) );

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

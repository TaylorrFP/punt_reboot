using System;
using Sandbox;

public sealed class PhysicsSquashStretch : Component
{
	[Property] public Rigidbody Rigidbody { get; set; }
	[Property] public ModelRenderer Renderer { get; set; }

	[Property, Group( "Spring" )] public float ImpactThreshold { get; set; } = 50f;
	[Property, Group( "Spring" )] public float ImpactStrength { get; set; } = 0.003f;
	[Property, Group( "Spring" )] public float SpringFrequency { get; set; } = 8f;
	[Property, Group( "Spring" )] public float SpringDamping { get; set; } = 0.5f;
	[Property, Group( "Spring" )] public float MaxSquash { get; set; } = 0.5f;
	[Property, Group( "Spring" )] public float MaxStretch { get; set; } = 1.5f;
	[Property, Group( "Spring" )] public float AxisBlendSpeed { get; set; } = 15f;

	private Vector3 lastVelocity;
	private float currentStretchAmount = 1f;
	private float stretchVelocity = 0f;
	private Vector3 currentAxis = Vector3.Up;
	private Vector3 targetAxis = Vector3.Up;

	protected override void OnUpdate()
	{
		if ( Rigidbody == null ) return;

		var velocity = Rigidbody.Velocity;
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
		ApplyToShader();

		lastVelocity = velocity;
	}

	private void ApplyToShader()
	{
		if ( Renderer == null || Renderer.SceneObject == null ) return;

		Renderer.SceneObject.Attributes.Set( "ObjectPivot", GameObject.WorldPosition );
		Renderer.SceneObject.Attributes.Set( "Axis", currentAxis );
		Renderer.SceneObject.Attributes.Set( "StretchAmount", currentStretchAmount );
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();
		ApplyToShader();
	}
}

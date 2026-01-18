using Sandbox;
using System;

public sealed class PuntBall : Component, Component.ICollisionListener
{
	[Property] public Rigidbody Rigidbody { get; set; }

	#region Wall Slide Settings

	/// <summary>
	/// Maps angle of incidence (0-90°) to bounce factor (0-1).
	/// X axis: 0 = parallel to wall, 1 = perpendicular to wall.
	/// Y axis: 0 = full slide, 1 = full bounce.
	/// </summary>
	[Property, Group( "Wall Slide" )]
	public Curve WallSlideCurve { get; set; } = new Curve(
		new Curve.Frame( 0f, 0f ),
		new Curve.Frame( 0.4f, 0f ),
		new Curve.Frame( 0.6f, 1f ),
		new Curve.Frame( 1f, 1f )
	);

	/// <summary>
	/// How much velocity is retained when sliding along a wall (0-1).
	/// </summary>
	[Property, Group( "Wall Slide" ), Range( 0f, 1f ), Step( 0.05f )]
	public float SlideVelocityRetention { get; set; } = 0.9f;

	/// <summary>
	/// Enable debug logging for wall collisions.
	/// </summary>
	[Property, Group( "Wall Slide" )]
	public bool DebugWallSlide { get; set; } = false;

	#endregion

	public void OnCollisionStart( Collision collision )
	{
		// Only apply wall slide behavior to objects tagged as "wall"
		if ( !collision.Other.GameObject.Tags.Has( "wall" ) )
			return;

		ApplyWallSlide( collision );
	}

	public void OnCollisionUpdate( Collision collision )
	{
		// Not needed for this behavior
	}

	public void OnCollisionStop( CollisionStop collision )
	{
		// Not needed for this behavior
	}

	private void ApplyWallSlide( Collision collision )
	{
		if ( Rigidbody == null || !Rigidbody.IsValid )
			return;

		var velocity = Rigidbody.Velocity;
		if ( velocity.LengthSquared < 0.01f )
			return;

		// Get the collision normal (points away from the wall, toward the ball)
		var contact = collision.Contact;
		var wallNormal = contact.Normal;

		// We only care about horizontal wall collisions (XY plane)
		// Project to 2D by zeroing out Z component
		var velocity2D = velocity.WithZ( 0 );
		var wallNormal2D = wallNormal.WithZ( 0 ).Normal;

		if ( velocity2D.LengthSquared < 0.01f || wallNormal2D.LengthSquared < 0.01f )
			return;

		var velocityDir2D = velocity2D.Normal;

		// Calculate angle of incidence (angle between velocity and wall surface)
		// Dot product with normal gives cos(angle from perpendicular)
		// We want angle from the wall surface, so: surfaceAngle = 90 - normalAngle
		var dotWithNormal = MathF.Abs( Vector3.Dot( velocityDir2D, wallNormal2D ) );
		var angleFromPerpendicular = MathF.Acos( Math.Clamp( dotWithNormal, 0f, 1f ) ) * (180f / MathF.PI);
		var angleFromWall = 90f - angleFromPerpendicular;

		// Normalize angle to 0-1 range for curve lookup (0 = parallel, 1 = perpendicular)
		var normalizedAngle = angleFromWall / 90f;

		// Sample the curve to get blend factor (0 = slide, 1 = bounce)
		var blendFactor = WallSlideCurve.Evaluate( normalizedAngle );

		if ( DebugWallSlide )
			Log.Info( $"Wall collision - Angle: {angleFromWall:F1}° ({normalizedAngle:F2}) -> Blend: {blendFactor:F2}" );

		// If mostly bouncing, let physics handle it
		if ( blendFactor > 0.95f )
			return;

		// Calculate the slide direction (velocity projected onto wall surface)
		// Wall tangent is perpendicular to normal in the XY plane
		var wallTangent = new Vector3( -wallNormal2D.y, wallNormal2D.x, 0 );

		// Make sure tangent points in the same general direction as velocity
		if ( Vector3.Dot( wallTangent, velocity2D ) < 0 )
			wallTangent = -wallTangent;

		// Slide velocity: project velocity onto wall tangent
		var slideSpeed = velocity2D.Length * SlideVelocityRetention;
		var slideVelocity2D = wallTangent * slideSpeed;

		// Calculate the bounced velocity (what physics would normally do)
		// Reflect velocity around normal: v' = v - 2(v·n)n
		var bouncedVelocity2D = velocity2D - 2f * Vector3.Dot( velocity2D, wallNormal2D ) * wallNormal2D;

		// Blend between slide and bounce based on angle
		var finalVelocity2D = Vector3.Lerp( slideVelocity2D, bouncedVelocity2D, blendFactor );

		// Apply the new velocity, preserving Z component
		Rigidbody.Velocity = finalVelocity2D.WithZ( velocity.z );

		if ( DebugWallSlide )
			Log.Info( $"Velocity changed: {velocity2D.Length:F1} -> {finalVelocity2D.Length:F1}" );
	}
}

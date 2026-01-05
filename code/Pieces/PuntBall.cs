using System;
using Sandbox;

public sealed class PuntBall : Component
{
	[Property] public WorldPanel ballGuide;

	[Property] public Rigidbody ballRB;

	[Property] public GameObject ballModel;

	[Property] public ModelRenderer ballRenderer;

	[Property] public float SquashAmount;

	protected override void OnUpdate()
	{
		CalculateBallGuide();
		CalculateSquashStretch();
	}

	private void CalculateSquashStretch()
	{
		var velocity = ballRB.Velocity;

		// Set the object pivot for squash/stretch shader
		if ( ballModel != null )
		{
			var renderer = ballRenderer;
			if ( renderer != null && renderer.SceneObject != null )
			{

				
				renderer.SceneObject.Attributes.Set( "ObjectPivot", GameObject.WorldPosition );
				renderer.SceneObject.Attributes.Set( "Axis", velocity.Normal );
				renderer.SceneObject.Attributes.Set( "StretchAmount", SquashAmount );

			}
		}

		Vector2 textScreenPos = Scene.Camera.PointToScreenPixels( WorldPosition );
		Gizmo.Draw.Color = Color.Black;
		Gizmo.Draw.ScreenText(
			$"Velocity: {velocity.Length:F0}",
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
			ballRenderer.SceneObject.Attributes.Set( "StretchAmount", SquashAmount );
		}
	}

}

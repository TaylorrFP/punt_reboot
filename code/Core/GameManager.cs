using Sandbox;

public sealed class GameManager : Component
{

	[Property] public float TimeScale { get; set; } = 1f;

	protected override void OnUpdate()
	{
		Scene.TimeScale = TimeScale;

	}

	protected override void OnStart()
	{

		
	}
}

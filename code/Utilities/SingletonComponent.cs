using Sandbox;

// Utilities/SingletonComponent.cs
public abstract class SingletonComponent<T> : Component where T : SingletonComponent<T>
{
	[SkipHotload]
	public static T Instance { get; private set; }

	protected override void OnAwake()
	{
		if ( Instance != null && Instance != this )
		{
			Log.Warning( $"Multiple {typeof( T ).Name} instances detected!" );

			Destroy();
			return;
		}

		Instance = (T)this;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
		{
			Instance = null;
		}
	}
}

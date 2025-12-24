using Sandbox;
// Match/MatchClock.cs
public sealed class MatchClock : SingletonComponent<MatchClock>
{
	[Property] public float RoundDuration { get; set; } = 180f;
	[Property, Sync] public float TimeRemaining { get; private set; }
	[Property, Sync] public bool IsRunning { get; private set; }

	public bool IsFinished => TimeRemaining <= 0;

	public string FormattedTime
	{
		get
		{
			var minutes = (int)TimeRemaining / 60;
			var seconds = (int)TimeRemaining % 60;
			return $"{minutes:00}:{seconds:00}";
		}
	}

	public void Start() { }
	public void Pause() { }
}

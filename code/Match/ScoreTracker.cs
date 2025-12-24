using Sandbox;
using System;

// Match/ScoreTracker.cs
public sealed class ScoreTracker : SingletonComponent<ScoreTracker>
{
	[Property, Sync] public int BlueScore { get; private set; }
	[Property, Sync] public int RedScore { get; private set; }

	public bool IsDraw => BlueScore == RedScore;

	public event Action<TeamSide> OnGoalScored;

	public void RegisterGoal( TeamSide scoringTeam )
	{
		if ( scoringTeam == TeamSide.Blue )
			BlueScore++;
		else if ( scoringTeam == TeamSide.Red )
			RedScore++;

		OnGoalScored?.Invoke( scoringTeam );
	}
}

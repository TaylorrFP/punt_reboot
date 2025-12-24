using Sandbox;

public sealed class MatchController : Component
{
	// Current state
	[Property, Sync] public MatchState State { get; private set; }

	// Settings (could be a separate MatchSettings asset)
	[Property] public float CountdownDuration { get; set; } = 3f;
	[Property] public float GoalReplayDuration { get; set; } = 5f;
	[Property] public bool IsSinglePlayer { get; set; }

	// Internal timers
	private TimeUntil stateTimer;

	protected override void OnStart()
	{
		ChangeState( MatchState.Intro );
	}

	protected override void OnUpdate()
	{
		// Each state has its own update logic
		switch ( State )
		{
			case MatchState.Intro:
				UpdateIntro();
				break;
			case MatchState.WaitingForPlayers:
				UpdateWaitingForPlayers();
				break;
			case MatchState.Countdown:
				UpdateCountdown();
				break;
			case MatchState.Playing:
				UpdatePlaying();
				break;
			case MatchState.GoalScored:
				UpdateGoalScored();
				break;
			case MatchState.Overtime:
				UpdateOvertime();
				break;
			case MatchState.Results:
				UpdateResults();
				break;
		}
	}

	// Clean state transitions
	private void ChangeState( MatchState newState )
	{
		var oldState = State;
		State = newState;

		OnExitState( oldState );
		OnEnterState( newState );
	}

	private void OnEnterState( MatchState state )
	{
		switch ( state )
		{
			case MatchState.Intro:
				//Camera.PlayIntro();
				stateTimer = 4f; // Intro duration
				break;

			case MatchState.WaitingForPlayers:
				// UI will show "Waiting for players..."
				break;

			case MatchState.Countdown:
				stateTimer = CountdownDuration;
				//Pitch.ResetPieces();
				//Pitch.ResetBall();
				break;

			case MatchState.Playing:
				MatchClock.Instance.Start();
				//EnablePlayerInput( true );
				break;

			case MatchState.GoalScored:
				MatchClock.Instance.Pause();
				//EnablePlayerInput( false );
				//Replay.PlayGoalReplay();
				stateTimer = GoalReplayDuration;
				break;

			case MatchState.Overtime:
				// Same as playing but no timer
				//EnablePlayerInput( true );
				break;

			case MatchState.Results:
				//EnablePlayerInput( false );
				// UI shows results
				break;
		}
	}

	private void OnExitState( MatchState state )
	{
		// Cleanup when leaving a state if needed
	}



	private void UpdateIntro()
	{
		if ( stateTimer <= 0 )
		{
			if ( IsSinglePlayer )
				ChangeState( MatchState.Countdown );
			else
				ChangeState( MatchState.WaitingForPlayers );
		}
	}

	private void UpdateWaitingForPlayers()
	{
		//if ( AllPlayersConnected() )
		//{
		//	ChangeState( MatchState.Countdown );
		//}
	}

	private void UpdateCountdown()
	{
		if ( stateTimer <= 0 )
		{
			ChangeState( MatchState.Playing );
		}
	}

	private void UpdatePlaying()
	{
		if ( MatchClock.Instance.IsFinished )
		{
			//if ( Score.IsDraw )
			//	ChangeState( MatchState.Overtime );
			//else
			//	ChangeState( MatchState.Results );
		}
	}

	private void UpdateGoalScored()
	{
		// Wait for replay to finish (or player skips)
		//if ( stateTimer <= 0 || ReplaySkipped() )
		//{
		//	ChangeState( MatchState.Countdown );
		//}
	}

	private void UpdateOvertime()
	{
		// Overtime ends when a goal is scored (handled by OnGoalScored)
	}

	private void UpdateResults()
	{
		// Wait for player input to continue
	}
}

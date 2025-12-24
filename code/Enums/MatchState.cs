using Sandbox;
public enum MatchState
{
	None,
	Intro,              // Cinematic fly-in
	WaitingForPlayers,  // Lobby/connecting
	Countdown,          // 3, 2, 1...
	Playing,            // Main gameplay, clock running
	GoalScored,         // Freeze, show replay, reset
	Overtime,           // Sudden death
	Results             // Final screen
}

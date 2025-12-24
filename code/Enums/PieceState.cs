using Sandbox;

public enum PieceState
{
	Ready,      // Can be hovered/grabbed
	Hovered,    // Being hovered (but not grabbed)
	Grabbed,    // Currently grabbed by a player
	Cooldown,   // Recently flicked, can't be used
	Frozen      // Locked (e.g., during countdown)
}

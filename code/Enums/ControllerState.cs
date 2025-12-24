using Sandbox;

public enum ControllerState
{
	Idle,           // Not interacting with anything
	Hovering,       // Hovering over a valid piece
	Grabbing,       // Currently grabbing a piece
	InvalidTarget   // Hovering something we can't interact with
}

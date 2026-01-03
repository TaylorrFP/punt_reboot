using Sandbox;
using System;

public sealed class PitchGenerator : Component
{
	[Property] public float PitchLength { get; set; } = 100f;
	[Property] public float PitchWidth { get; set; } = 150f;
	[Property] public float BevelAmount { get; set; } = 20f;
	[Property] public int BevelResolution { get; set; } = 8;
	[Property] public bool DebugDraw { get; set; } = false;

	protected override void OnUpdate()
	{
		if ( DebugDraw )
		{
			DrawPitch();
		}
	}

	protected override void DrawGizmos()
	{
		DrawPitch();
	}

	private void DrawPitch()
	{
		var origin = WorldPosition;

		// Calculate the dimensions
		var halfWidth = PitchWidth / 2f;
		var halfLength = PitchLength / 2f;
		var bevel = MathF.Min(BevelAmount, MathF.Min(halfWidth, halfLength));

		// Calculate corner centers (where the rounded corners will be)
		var topLeftCenter = origin + new Vector3(-halfWidth + bevel, halfLength - bevel, 0);
		var topRightCenter = origin + new Vector3(halfWidth - bevel, halfLength - bevel, 0);
		var bottomLeftCenter = origin + new Vector3(-halfWidth + bevel, -halfLength + bevel, 0);
		var bottomRightCenter = origin + new Vector3(halfWidth - bevel, -halfLength + bevel, 0);

		// Generate all vertices
		var vertices = new System.Collections.Generic.List<Vector3>();

		// Top edge (right to left to go clockwise)
		vertices.Add(origin + new Vector3(halfWidth - bevel, halfLength, 0));
		vertices.Add(origin + new Vector3(-halfWidth + bevel, halfLength, 0));

		// Top-left corner
		for (int i = 0; i <= BevelResolution; i++)
		{
			float angle = MathF.PI / 2f + (MathF.PI / 2f) * (i / (float)BevelResolution);
			var cornerPoint = topLeftCenter + new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0) * bevel;
			vertices.Add(cornerPoint);
		}

		// Left edge (top to bottom)
		vertices.Add(origin + new Vector3(-halfWidth, -halfLength + bevel, 0));

		// Bottom-left corner
		for (int i = 0; i <= BevelResolution; i++)
		{
			float angle = MathF.PI + (MathF.PI / 2f) * (i / (float)BevelResolution);
			var cornerPoint = bottomLeftCenter + new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0) * bevel;
			vertices.Add(cornerPoint);
		}

		// Bottom edge (left to right)
		vertices.Add(origin + new Vector3(halfWidth - bevel, -halfLength, 0));

		// Bottom-right corner
		for (int i = 0; i <= BevelResolution; i++)
		{
			float angle = -MathF.PI / 2f + (MathF.PI / 2f) * (i / (float)BevelResolution);
			var cornerPoint = bottomRightCenter + new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0) * bevel;
			vertices.Add(cornerPoint);
		}

		// Right edge (bottom to top)
		vertices.Add(origin + new Vector3(halfWidth, halfLength - bevel, 0));

		// Top-right corner
		for (int i = 0; i <= BevelResolution; i++)
		{
			float angle = (MathF.PI / 2f) * (i / (float)BevelResolution);
			var cornerPoint = topRightCenter + new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0) * bevel;
			vertices.Add(cornerPoint);
		}

		// Draw lines between vertices
		Gizmo.Draw.Color = Color.White;
		for (int i = 0; i < vertices.Count; i++)
		{
			var nextIndex = (i + 1) % vertices.Count;
			Gizmo.Draw.Line(vertices[i], vertices[nextIndex]);
		}

		// Draw debug rects at each vertex
		Gizmo.Draw.Color = Color.Red;
		foreach (var vertex in vertices)
		{
			Gizmo.Draw.LineBBox(new BBox(vertex - Vector3.One * 2f, vertex + Vector3.One * 2f));
		}
	}
}

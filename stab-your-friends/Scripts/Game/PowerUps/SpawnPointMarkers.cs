using Godot;

namespace StabYourFriends.Game.PowerUps;

public partial class SpawnPointMarkers : Node2D
{
	private const float BaseMarkerRadius = 28f;

	private Vector2[] _points = [];
	private float _scaleFactor = 1f;

	public override void _Ready()
	{
		ZIndex = -2;
	}

	public void Update(Vector2[] points, float scaleFactor)
	{
		_points = points;
		_scaleFactor = scaleFactor;
		QueueRedraw();
	}

	public override void _Draw()
	{
		float radius = BaseMarkerRadius * _scaleFactor;
		var color = new Color(1f, 1f, 1f, 0.08f);

		foreach (var point in _points)
		{
			DrawCircle(point, radius, color);
		}
	}
}

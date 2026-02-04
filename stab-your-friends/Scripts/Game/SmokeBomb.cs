#nullable enable

using Godot;

namespace StabYourFriends.Game;

public partial class SmokeBomb : Node2D
{
	private enum Phase { Expanding, Lingering, Dissipating }

	private const float ExpandDuration = 3f;
	private const float LingerDuration = 0.25f;
	private const float FadeDuration = 1.5f;
	private static readonly Color SmokeColor = new Color(0.4f, 0.4f, 0.4f, 1f);

	// Particle constants
	private const int MaxParticles = 300;
	private const int InitialBurst = 50;
	private const float SpawnRate = 120f; // particles per second
	private const float ParticleMinSpeed = 0.8f;  // Multiplier of maxRadius/sec
	private const float ParticleMaxSpeed = 1.2f;
	private const float ParticleMinSize = 8f;
	private const float ParticleMaxSize = 30f;

	private float _maxRadius;
	private float _currentRadius;
	private float _opacity = 1f;
	private float _phaseTimer;
	private Phase _phase = Phase.Expanding;

	// Particle state
	private readonly Vector2[] _particlePos = new Vector2[MaxParticles];
	private readonly Vector2[] _particleVel = new Vector2[MaxParticles];
	private readonly float[] _particleSize = new float[MaxParticles];
	private readonly Color[] _particleColor = new Color[MaxParticles];
	private readonly bool[] _particleAlive = new bool[MaxParticles];
	private int _particlePoolSize; // how many slots have been activated
	private float _spawnAccumulator;
	private readonly RandomNumberGenerator _rng = new();

	public void Initialize(Vector2 spawnPosition, float maxRadius)
	{
		Position = spawnPosition;
		_maxRadius = maxRadius;
		_currentRadius = 0f;
		_phaseTimer = 0f;
		_spawnAccumulator = 0f;
		ZIndex = 50;

		_rng.Randomize();

		// Initial burst of 80 particles
		_particlePoolSize = InitialBurst;
		for (int i = 0; i < InitialBurst; i++)
		{
			SpawnParticle(i);
		}
	}

	private void SpawnParticle(int i)
	{
		float angle = _rng.RandfRange(0f, Mathf.Tau);
		float speed = _rng.RandfRange(ParticleMinSpeed, ParticleMaxSpeed) * _maxRadius / ExpandDuration;
		_particleVel[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
		_particlePos[i] = Vector2.Zero;
		_particleSize[i] = _rng.RandfRange(ParticleMinSize, ParticleMaxSize);
		_particleAlive[i] = true;

		float grey = _rng.RandfRange(0.25f, 0.7f);
		_particleColor[i] = new Color(grey, grey, grey, _rng.RandfRange(0.5f, 0.9f));
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_phaseTimer += dt;

		switch (_phase)
		{
			case Phase.Expanding:
				ProcessExpanding();
				break;
			case Phase.Lingering:
				ProcessLingering();
				break;
			case Phase.Dissipating:
				ProcessDissipating();
				break;
		}

		UpdateParticles(dt);
		QueueRedraw();
	}

	private void UpdateParticles(float dt)
	{
		float radiusSq = _currentRadius * _currentRadius;

		// Move existing particles, kill those past the edge
		for (int i = 0; i < _particlePoolSize; i++)
		{
			if (!_particleAlive[i]) continue;

			_particlePos[i] += _particleVel[i] * dt;

			if (_particlePos[i].LengthSquared() >= radiusSq)
			{
				_particleAlive[i] = false;
			}
		}

		// Spawn new particles at ~80/sec by recycling dead slots or expanding the pool
		_spawnAccumulator += dt * (SpawnRate /** (1 - (_maxRadius - _currentRadius) / _maxRadius)*/);
		while (_spawnAccumulator >= 1f && _phase == Phase.Expanding)
		{
			_spawnAccumulator -= 1f;

			// Try to recycle a dead slot first
			bool recycled = false;
			for (int i = 0; i < _particlePoolSize; i++)
			{
				if (!_particleAlive[i])
				{
					SpawnParticle(i);
					recycled = true;
					break;
				}
			}

			// Otherwise expand the pool
			if (!recycled && _particlePoolSize < MaxParticles)
			{
				SpawnParticle(_particlePoolSize);
				_particlePoolSize++;
				GD.Print($"[SmokeBomb] _particlePoolSize={_particlePoolSize}");
			}
		}
	}

	private void ProcessExpanding()
	{
		float t = Mathf.Min(_phaseTimer / ExpandDuration, 1f);
		_currentRadius = Mathf.Lerp(0f, _maxRadius, t);

		if (_phaseTimer >= ExpandDuration)
		{
			_phase = Phase.Lingering;
			_phaseTimer = 0f;
			_currentRadius = _maxRadius;
		}
	}

	private void ProcessLingering()
	{
		_currentRadius = _maxRadius;

		if (_phaseTimer >= LingerDuration)
		{
			_phase = Phase.Dissipating;
			_phaseTimer = 0f;
		}
	}

	private void ProcessDissipating()
	{
		float t = Mathf.Min(_phaseTimer / FadeDuration, 1f);
		_opacity = 1f - t;

		if (_phaseTimer >= FadeDuration)
		{
			QueueFree();
		}
	}

	public override void _Draw()
	{
		var color = new Color(SmokeColor.R, SmokeColor.G, SmokeColor.B, SmokeColor.A * _opacity);
		DrawCircle(Vector2.Zero, _currentRadius, color);

		for (int i = 0; i < _particlePoolSize; i++)
		{
			if (!_particleAlive[i]) continue;

			var c = _particleColor[i];
			var drawColor = new Color(c.R, c.G, c.B, c.A * _opacity);
			DrawCircle(_particlePos[i], _particleSize[i], drawColor);
		}
	}
}

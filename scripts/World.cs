using Godot;
using System;

[GlobalClass]
public partial class World : Node2D
{
    [Export]
    public NodePath PlayerPath { get; set; } = new NodePath("Player");

    [Signal]
    public delegate void PlayerDiedEventHandler();

    [Signal]
    public delegate void PlayerHealthChangedEventHandler(int health, int maxHealth);

    private Player _player;
    private bool _isGameOver;

    public override void _Ready()
    {
        _player = GetNodeOrNull<Player>(PlayerPath);
        if (_player != null)
        {
            _player.Connect(Player.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));
            _player.Connect(Player.SignalName.HealthChanged, new Callable(this, nameof(OnPlayerHealthChanged)));
            EmitSignal(SignalName.PlayerHealthChanged, _player.CurrentHealth, _player.MaxHealth);
        }
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_player) &&
            _player.IsConnected(Player.SignalName.HealthChanged, new Callable(this, nameof(OnPlayerHealthChanged))))
        {
            _player.Disconnect(Player.SignalName.HealthChanged, new Callable(this, nameof(OnPlayerHealthChanged)));
        }

        if (GodotObject.IsInstanceValid(_player) &&
            _player.IsConnected(Player.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied))))
        {
            _player.Disconnect(Player.SignalName.PlayerDied, new Callable(this, nameof(OnPlayerDied)));
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isGameOver)
            return;
    }

    public override void _Input(InputEvent @event)
    {
        // Input handling for restart is now handled by Main.
    }

    private void OnPlayerDied()
    {
        if (_isGameOver)
            return;

        _isGameOver = true;
        EmitSignal(SignalName.PlayerDied);
    }

    private void OnPlayerHealthChanged(int health, int maxHealth)
    {
        EmitSignal(SignalName.PlayerHealthChanged, health, maxHealth);
    }
}

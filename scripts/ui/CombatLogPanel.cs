using System;
using System.Collections.Generic;

using Godot;

[GlobalClass]
public partial class CombatLogPanel : Control
{
    private const int MaxLines = 100;
    private const int DefaultLineFontSize = 13;
    private const float DefaultMarginRight = 16.0f;
    private const float DefaultMarginBottom = 96.0f;
    private const float MinVisible = 60.0f;

    private static readonly Color InfoColor = new Color(0.85f, 0.85f, 0.85f, 1.0f);
    private static readonly Color DamageColor = new Color(1.0f, 0.45f, 0.45f, 1.0f);
    private static readonly Color HealColor = new Color(0.45f, 1.0f, 0.55f, 1.0f);
    private static readonly Color AbsorbColor = new Color(1.0f, 0.95f, 0.45f, 1.0f);
    private static readonly Color DebugColor = new Color(0.6f, 0.75f, 1.0f, 1.0f);

    [Export]
    public NodePath PanelPath { get; set; } = new("Panel");

    [Export]
    public NodePath RowsPath { get; set; } = new("Panel/Margin/Rows");

    [Export]
    public Vector2I PanelSize { get; set; } = new Vector2I(360, 180);

    private readonly Queue<Label> _rows = new();
    private PanelContainer _panel;
    private VBoxContainer _rowsContainer;
    private Action<CombatLogEntry> _entryHandler;
    private Action<bool> _showChangedHandler;
    private Action<bool> _lockChangedHandler;
    private Action<Vector2> _positionChangedHandler;
    private bool _dragging;
    private Vector2 _dragOffset;
    private bool _appliedInitialPosition;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AnchorRight = 1.0f;
        AnchorBottom = 1.0f;
        OffsetLeft = 0.0f;
        OffsetTop = 0.0f;
        OffsetRight = 0.0f;
        OffsetBottom = 0.0f;
        ProcessMode = ProcessModeEnum.Always;

        _panel = GetNodeOrNull<PanelContainer>(PanelPath);
        _rowsContainer = GetNodeOrNull<VBoxContainer>(RowsPath);

        if (_panel != null)
        {
            _panel.CustomMinimumSize = PanelSize;
            _panel.GuiInput += OnPanelGuiInput;
            _panel.Resized += OnPanelResized;
            ApplyMouseFilterForLock(GameSettings.LockCombatLogPosition);
        }

        Visible = GameSettings.ShowCombatLog;

        _entryHandler = OnCombatLogEntry;
        _showChangedHandler = OnShowCombatLogChanged;
        _lockChangedHandler = OnLockCombatLogPositionChanged;
        _positionChangedHandler = OnCombatLogPositionChanged;

        CombatLog.Emitted += _entryHandler;
        GameSettings.ShowCombatLogChanged += _showChangedHandler;
        GameSettings.LockCombatLogPositionChanged += _lockChangedHandler;
        GameSettings.CombatLogPositionChanged += _positionChangedHandler;

        GetTree().Root.SizeChanged += OnViewportSizeChanged;

        CallDeferred(nameof(ApplyInitialPanelPosition));
    }

    public override void _ExitTree()
    {
        if (_entryHandler != null)
            CombatLog.Emitted -= _entryHandler;

        if (_showChangedHandler != null)
            GameSettings.ShowCombatLogChanged -= _showChangedHandler;

        if (_lockChangedHandler != null)
            GameSettings.LockCombatLogPositionChanged -= _lockChangedHandler;

        if (_positionChangedHandler != null)
            GameSettings.CombatLogPositionChanged -= _positionChangedHandler;

        if (_panel != null && GodotObject.IsInstanceValid(_panel))
        {
            _panel.GuiInput -= OnPanelGuiInput;
            _panel.Resized -= OnPanelResized;
        }

        var sceneTree = GetTreeOrNull();
        if (sceneTree != null && sceneTree.Root != null && GodotObject.IsInstanceValid(sceneTree.Root))
            sceneTree.Root.SizeChanged -= OnViewportSizeChanged;
    }

    private SceneTree GetTreeOrNull()
    {
        return IsInsideTree() ? GetTree() : null;
    }

    private void ApplyInitialPanelPosition()
    {
        _appliedInitialPosition = true;
        if (_panel == null || !GodotObject.IsInstanceValid(_panel))
            return;

        if (GameSettings.CombatLogPositionCustomized)
            _panel.Position = ClampPanelPosition(GameSettings.CombatLogPosition);
        else
            _panel.Position = ResolveDefaultPanelPosition();
    }

    private Vector2 ResolveDefaultPanelPosition()
    {
        var viewport = GetViewportRect().Size;
        var panelSize = ResolvePanelSize();
        var x = Math.Max(0.0f, viewport.X - panelSize.X - DefaultMarginRight);
        var y = Math.Max(0.0f, viewport.Y - panelSize.Y - DefaultMarginBottom);
        return new Vector2(x, y);
    }

    private Vector2 ResolvePanelSize()
    {
        if (_panel == null)
            return new Vector2(PanelSize.X, PanelSize.Y);

        var size = _panel.Size;
        if (size == Vector2.Zero)
            size = _panel.GetCombinedMinimumSize();

        if (size == Vector2.Zero)
            size = new Vector2(PanelSize.X, PanelSize.Y);

        return size;
    }

    private Vector2 ClampPanelPosition(Vector2 candidate)
    {
        var viewport = GetViewportRect().Size;
        var panelSize = ResolvePanelSize();
        var minX = -panelSize.X + MinVisible;
        var maxX = viewport.X - MinVisible;
        var minY = 0.0f;
        var maxY = viewport.Y - MinVisible;
        return new Vector2(
            Mathf.Clamp(candidate.X, minX, maxX),
            Mathf.Clamp(candidate.Y, minY, maxY));
    }

    private void OnCombatLogEntry(CombatLogEntry entry)
    {
        AppendLine(entry);
    }

    private void AppendLine(CombatLogEntry entry)
    {
        if (_rowsContainer == null || !GodotObject.IsInstanceValid(_rowsContainer))
            return;

        while (_rows.Count >= MaxLines)
        {
            var oldest = _rows.Dequeue();
            if (oldest != null && GodotObject.IsInstanceValid(oldest))
                oldest.QueueFree();
        }

        var label = new Label
        {
            Text = entry.Text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontSizeOverride("font_size", DefaultLineFontSize);
        label.AddThemeColorOverride("font_color", ResolveColorFor(entry.Kind));
        _rowsContainer.AddChild(label);
        _rows.Enqueue(label);
    }

    private static Color ResolveColorFor(CombatLogEntryKind kind)
    {
        return kind switch
        {
            CombatLogEntryKind.Damage => DamageColor,
            CombatLogEntryKind.Heal => HealColor,
            CombatLogEntryKind.Absorb => AbsorbColor,
            CombatLogEntryKind.Debug => DebugColor,
            _ => InfoColor,
        };
    }

    private void OnShowCombatLogChanged(bool show)
    {
        Visible = show;
    }

    private void OnLockCombatLogPositionChanged(bool locked)
    {
        ApplyMouseFilterForLock(locked);
        if (_dragging && locked)
            _dragging = false;
    }

    private void OnCombatLogPositionChanged(Vector2 position)
    {
        if (_panel == null || !GodotObject.IsInstanceValid(_panel))
            return;

        if (_dragging)
            return;

        _panel.Position = ClampPanelPosition(position);
    }

    private void ApplyMouseFilterForLock(bool locked)
    {
        if (_panel == null)
            return;

        _panel.MouseFilter = locked ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
    }

    private void OnPanelGuiInput(InputEvent @event)
    {
        if (GameSettings.LockCombatLogPosition)
            return;

        if (_panel == null || !GodotObject.IsInstanceValid(_panel))
            return;

        switch (@event)
        {
            case InputEventMouseButton mouseButton when mouseButton.ButtonIndex == MouseButton.Left:
                if (mouseButton.Pressed)
                {
                    _dragging = true;
                    _dragOffset = _panel.GetGlobalMousePosition() - _panel.GlobalPosition;
                    _panel.AcceptEvent();
                }
                else if (_dragging)
                {
                    _dragging = false;
                    PersistCurrentPosition();
                    _panel.AcceptEvent();
                }
                break;

            case InputEventMouseMotion when _dragging:
                _panel.Position = ClampPanelPosition(_panel.GetGlobalMousePosition() - _dragOffset);
                _panel.AcceptEvent();
                break;
        }
    }

    private void PersistCurrentPosition()
    {
        if (_panel == null || !GodotObject.IsInstanceValid(_panel))
            return;

        GameSettings.SetCombatLogPosition(_panel.Position, customized: true);

        var store = new GameConfigStore();
        if (!store.TrySaveGameSettings(out var message))
            GD.PushWarning(message);
    }

    private void OnViewportSizeChanged()
    {
        if (!_appliedInitialPosition || _panel == null || !GodotObject.IsInstanceValid(_panel))
            return;

        if (!GameSettings.CombatLogPositionCustomized)
            _panel.Position = ResolveDefaultPanelPosition();
        else
            _panel.Position = ClampPanelPosition(_panel.Position);
    }

    private void OnPanelResized()
    {
        if (!_appliedInitialPosition || _panel == null || !GodotObject.IsInstanceValid(_panel))
            return;

        if (!GameSettings.CombatLogPositionCustomized)
            _panel.Position = ResolveDefaultPanelPosition();
    }
}

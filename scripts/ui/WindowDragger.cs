using Godot;

using System;

// Lets the user drag a window panel around its parent viewport. Clicks on child controls
// with MouseFilter.Stop (slot frames etc.) consume input before reaching the panel, so the
// drag only fires on panel background regions.
public sealed class WindowDragger
{
    private readonly Control _window;
    private readonly Control _panel;
    private readonly Control.GuiInputEventHandler _handler;
    private bool _dragging;
    private Vector2 _dragOffset;

    private const float MinVisible = 60.0f;

    // Invoked on left mouse press in the panel background, before drag setup.
    public Action BringToFront { get; set; }

    public WindowDragger(Control window, Control panel)
    {
        _window = window;
        _panel = panel;
        _handler = OnPanelGuiInput;
        _panel.GuiInput += _handler;
    }

    public void Detach()
    {
        if (_panel != null && GodotObject.IsInstanceValid(_panel))
            _panel.GuiInput -= _handler;
    }

    public void ClampToViewport()
    {
        if (_panel == null || !GodotObject.IsInstanceValid(_panel))
            return;

        _panel.GlobalPosition = ClampPosition(_panel.GlobalPosition);
    }

    private void OnPanelGuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton mouseButton when mouseButton.ButtonIndex == MouseButton.Left:
                if (mouseButton.Pressed)
                {
                    BringToFront?.Invoke();
                    _dragging = true;
                    _dragOffset = _panel.GetGlobalMousePosition() - _panel.GlobalPosition;
                    _panel.AcceptEvent();
                }
                else if (_dragging)
                {
                    _dragging = false;
                    _panel.AcceptEvent();
                }
                break;

            case InputEventMouseMotion when _dragging:
                _panel.GlobalPosition = ClampPosition(_panel.GetGlobalMousePosition() - _dragOffset);
                _panel.AcceptEvent();
                break;
        }
    }

    private Vector2 ClampPosition(Vector2 candidate)
    {
        var viewportSize = _window.GetViewportRect().Size;
        var panelSize = _panel.Size;
        if (panelSize == Vector2.Zero)
            panelSize = _panel.GetCombinedMinimumSize();

        var minX = -panelSize.X + MinVisible;
        var maxX = viewportSize.X - MinVisible;
        var minY = 0.0f;
        var maxY = viewportSize.Y - MinVisible;

        return new Vector2(
            Mathf.Clamp(candidate.X, minX, maxX),
            Mathf.Clamp(candidate.Y, minY, maxY));
    }
}

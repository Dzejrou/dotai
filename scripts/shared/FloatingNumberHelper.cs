using Godot;

public static class FloatingNumberHelper
{
    public static void ShowFloatingNumber(Node2D owner, string text, Color color, float riseDistance = 18.0f, float duration = 0.6f, int fontSize = 20)
    {
        FloatingText.Show(text, owner, color, riseDistance: riseDistance, duration: duration, fontSize: fontSize);
    }
}

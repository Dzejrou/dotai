using Godot;

[GlobalClass]
public partial class RingOfFireArea : AreaOfEffect
{
    public RingOfFireArea()
    {
        EffectLifetime = 5.0f;
        TickInterval = 1.0f;
        ApplyOnEnter = false;
        ApplyOnTick = true;
        FillColor = new Color(1.0f, 0.45f, 0.08f, 0.32f);
        OutlineColor = new Color(1.0f, 0.62f, 0.14f, 0.9f);
        PreviewFillColor = new Color(1.0f, 0.45f, 0.08f, 0.14f);
        PreviewOutlineColor = new Color(1.0f, 0.62f, 0.14f, 0.45f);
    }
}

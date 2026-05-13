using Godot;

[GlobalClass]
public partial class DamageSchoolTag : Node
{
    [Export]
    public DamageSchool School { get; set; } = DamageSchool.Physical;

    public static DamageSchool? Resolve(Node owner)
    {
        if (owner == null || !GodotObject.IsInstanceValid(owner))
            return null;

        DamageSchoolTag found = null;
        foreach (var child in owner.GetChildren())
        {
            if (child is not DamageSchoolTag tag)
                continue;

            if (found == null)
            {
                found = tag;
                continue;
            }

            GD.PushWarning($"{owner.GetPath()}: multiple DamageSchoolTag children found; using the first ({found.School}).");
            break;
        }

        return found?.School;
    }

    public static void EnsureOnChild(Node child, Node parentContext)
    {
        if (child == null || !GodotObject.IsInstanceValid(child))
            return;

        if (Resolve(child).HasValue)
            return;

        var inherited = Resolve(parentContext);
        if (!inherited.HasValue)
            return;

        var tag = new DamageSchoolTag { School = inherited.Value };
        child.AddChild(tag);
    }
}

using System;

// Runtime gear identity. A GearInstance is the per-pickup unique object that will eventually
// carry random rolls/upgrade levels; GearDefinition is the shared template. Do not treat two
// instances with the same Definition as interchangeable — they will diverge once rolls land.
public sealed class GearInstance
{
    public GearInstance(GearDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public GearDefinition Definition { get; }
}

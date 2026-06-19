// Room kinds a generated dungeon run plan can contain. Distinct from the Dungeon node's
// current private runtime enum (Combat/Special/Timed): this is the plan-model kind set and
// adds Boss for the guaranteed terminal room. The two converge in a later slice when the
// runtime starts consuming the plan.
public enum DungeonRoomKind
{
    Combat,
    Timed,
    Special,
    Boss,
}

namespace RorType.Gameplay.Rts
{
    public enum RtsTeam
    {
        Neutral,
        Player,
        Enemy
    }

    public enum RtsUnitType
    {
        Infantry,
        Tank,
        Helicopter,
        Target
    }

    public enum RtsCommandMode
    {
        Idle,
        ForcedMove,
        AssaultMove,
        AttackTarget
    }

    public enum RtsFacilityType
    {
        Base,
        TankFactory,
        AirFactory,
        Barracks
    }
}

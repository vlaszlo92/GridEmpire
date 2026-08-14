using Unity.Netcode;

public struct EffectiveUnitStats : INetworkSerializable
{
    public int MaxHp;
    public float StaminaPerTurn;
    public float MaxStamina;
    public float ConquerSpeed;
    public float ExploreSpeed;
    public int BaseDamage;
    public int BonusDamage;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref MaxHp);
        serializer.SerializeValue(ref StaminaPerTurn);
        serializer.SerializeValue(ref MaxStamina);
        serializer.SerializeValue(ref ConquerSpeed);
        serializer.SerializeValue(ref ExploreSpeed);
        serializer.SerializeValue(ref BaseDamage);
        serializer.SerializeValue(ref BonusDamage);
    }
}
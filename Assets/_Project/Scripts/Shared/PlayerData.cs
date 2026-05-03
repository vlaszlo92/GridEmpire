using Unity.Netcode;
using UnityEngine;
using Unity.Collections;

public struct PlayerData : INetworkSerializable, System.IEquatable<PlayerData>
{
    public int Id;
    public Color32 Color;
    public bool IsAi;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref Color);
        serializer.SerializeValue(ref IsAi);
    }

    public bool Equals(PlayerData other) => Id == other.Id;
}
public struct PlayerClientMapping : INetworkSerializable, System.IEquatable<PlayerClientMapping>
{
    public ulong ClientId;
    public int PlayerId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerId);
    }

    public bool Equals(PlayerClientMapping other) => ClientId == other.ClientId;
}
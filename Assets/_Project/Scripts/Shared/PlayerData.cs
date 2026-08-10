using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

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

    public bool Equals(PlayerData other) =>
        Id == other.Id &&
        Color.Equals(other.Color) &&
        IsAi == other.IsAi;

    public override bool Equals(object obj) => obj is PlayerData other && Equals(other);

    public override int GetHashCode() => System.HashCode.Combine(Id, Color, IsAi);
}

public struct PlayerClientMapping : INetworkSerializable, System.IEquatable<PlayerClientMapping>
{
    public ulong ClientId;
    public int PlayerId;
    public FixedString64Bytes AuthId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerId);
        serializer.SerializeValue(ref AuthId);
    }

    public bool Equals(PlayerClientMapping other) =>
        ClientId == other.ClientId &&
        PlayerId == other.PlayerId &&
        AuthId.Equals(other.AuthId);

    public override bool Equals(object obj) => obj is PlayerClientMapping other && Equals(other);

    public override int GetHashCode() => System.HashCode.Combine(ClientId, PlayerId, AuthId);
}

public struct PlayerLobbyInfo : INetworkSerializable, System.IEquatable<PlayerLobbyInfo>
{
    public int PlayerId;
    public FixedString32Bytes Name;
    public int ColorIndex; // -1 = not selected, 0-7 = selected color index

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerId);
        serializer.SerializeValue(ref Name);
        serializer.SerializeValue(ref ColorIndex);
    }

    public bool Equals(PlayerLobbyInfo other) =>
        PlayerId == other.PlayerId &&
        ColorIndex == other.ColorIndex &&
        Name.Equals(other.Name);

    public override bool Equals(object obj) => obj is PlayerLobbyInfo other && Equals(other);

    public override int GetHashCode() => System.HashCode.Combine(PlayerId, Name, ColorIndex);
}
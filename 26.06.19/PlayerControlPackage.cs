using UnityEngine;
using Unity.Collections;
using Unity.Netcode;

public struct PlayerControlPackage : INetworkSerializable
{
    public int RequestId;
    public ulong ClientId;

    public float s_horizontal;
    public float s_vertical;

    public int s_hpDelta;

    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref RequestId);
        serializer.SerializeValue(ref ClientId);

        serializer.SerializeValue(ref s_horizontal);
        serializer.SerializeValue(ref s_vertical);

        serializer.SerializeValue(ref s_hpDelta);
    }
}

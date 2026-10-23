using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public struct CardControllPackage : INetworkSerializable
{
    public const int MaxTargets = 4;
    public ulong CardNetworkObjectId;
    public List<ulong> TargetNetworkObjectId;

    /*
    public ulong Target0NetworkObjectId;
    public ulong Target1NetworkObjectId;
    public ulong Target2NetworkObjectId;
    public ulong Target3NetworkObjectId;
    */
    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref CardNetworkObjectId);
        /*
        serializer.SerializeValue(ref Target0NetworkObjectId);
        serializer.SerializeValue(ref Target1NetworkObjectId);
        serializer.SerializeValue(ref Target2NetworkObjectId);
        serializer.SerializeValue(ref Target3NetworkObjectId);
        */
        int _count = TargetNetworkObjectId != null ? TargetNetworkObjectId.Count : 0;
        serializer.SerializeValue(ref _count);

        if (serializer.IsReader)
        {
            if (_count < 0 || _count > MaxTargets)
            {
                TargetNetworkObjectId = new List<ulong>();
                return;
            }
            TargetNetworkObjectId = new List<ulong>(_count);
            for (int i = 0; i < _count; i++)
            {
                ulong __value = 0;
                serializer.SerializeValue(ref __value);
                TargetNetworkObjectId.Add(__value);
            }
        }
        else
        {
            for (int i = 0; i < _count; i++)
            {
                ulong __value = TargetNetworkObjectId[i];
                serializer.SerializeValue(ref __value);
            }
        }
    }

    public ulong GetTarget(int index)
    {
        return TargetNetworkObjectId[index];
    }

    public void SetTarget(int index, ulong networkObjectId)
    {
      TargetNetworkObjectId[index] = networkObjectId;
    }

    public static CardControllPackage CreateEmpty(ulong cardNetworkObjectId)
    {
        var _targets = new List<ulong>(MaxTargets);
        for (int i = 0; i < MaxTargets; i++)
            _targets.Add(0);
        return new CardControllPackage
        {
            CardNetworkObjectId = cardNetworkObjectId,

            TargetNetworkObjectId = _targets
        };
    }
}

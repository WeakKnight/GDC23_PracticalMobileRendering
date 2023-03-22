using System;
using UnityEngine;

[Serializable]
public struct UInt2
{
    public UInt2(uint px, uint py)
    {
        x = px;
        y = py;
    }

    [SerializeField]
    public uint x;
    [SerializeField]
    public uint y;
}
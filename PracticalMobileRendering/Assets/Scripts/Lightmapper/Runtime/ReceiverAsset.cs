using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PMRP
{
    [PreferBinarySerialization]
    public class ReceiverAsset : ScriptableObject
    {
        [SerializeField]
        public List<Vector2Int> offsets = new();
        [SerializeField]
        public List<Receiver> receivers = new();
    }
}

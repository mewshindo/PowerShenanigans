using SDG.Unturned;
using System.Collections.Generic;
using UnityEngine;

namespace Wired.Nodes
{
    public abstract class Node : MonoBehaviour, IElectricNode
    {
        public ICollection<IElectricNode> Connections { get; set; }
        public void unInit()
        {
            DebugLogger.Log($"Destroyed node {instanceID}");
            StopAllCoroutines();
            Destroy(this);
        }
        public uint Voltage { get; protected set; }
        public uint instanceID { get; set; }

        protected virtual void Awake()
        {
            Connections = new List<IElectricNode>();
            instanceID = BarricadeManager.FindBarricadeByRootTransform(gameObject.transform).instanceID;
        }

        public void AddConnection(IElectricNode other)
        {
            if (!Connections.Contains(other))
                Connections.Add(other);
            if (!other.Connections.Contains(this))
                other.Connections.Add(this);

            Plugin.Instance.UpdateAllNetworks();
        }

        public void RemoveConnection(IElectricNode other)
        {
            Connections.Remove(other);
            other.Connections.Remove(this);

            Plugin.Instance.UpdateAllNetworks();
        }

        public abstract void IncreaseVoltage(uint amount);
        public abstract void DecreaseVoltage(uint amount);
    }

}
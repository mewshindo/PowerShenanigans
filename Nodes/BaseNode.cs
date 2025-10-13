using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PowerShenanigans.Nodes
{
    public abstract class BaseNode : MonoBehaviour, IElectricNode
    {
        public ICollection<IElectricNode> Connections { get; set; }
        public uint _voltage { get; protected set; }
        public uint instanceID => BarricadeManager.FindBarricadeByRootTransform(gameObject.transform).instanceID;

        protected virtual void Awake()
        {
            Connections = new List<IElectricNode>();
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

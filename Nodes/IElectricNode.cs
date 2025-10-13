using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PowerShenanigans.Nodes
{

    public interface IElectricNode
    {
        uint _voltage { get; }
        uint instanceID { get; }
        ICollection<IElectricNode> Connections { get; set; }
        void AddConnection(IElectricNode node);
        void RemoveConnection(IElectricNode node);

        void IncreaseVoltage(uint amount);
        void DecreaseVoltage(uint amount);
    }
}
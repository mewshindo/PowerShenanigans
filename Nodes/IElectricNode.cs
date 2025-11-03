using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Wired.Nodes
{

    public interface IElectricNode
    {
        uint Voltage { get; }
        uint instanceID { get; set; }
        ICollection<IElectricNode> Connections { get; set; }
        void AddConnection(IElectricNode node);
        void RemoveConnection(IElectricNode node);
        void unInit();

        void IncreaseVoltage(uint amount);
        void DecreaseVoltage(uint amount);
    }
}
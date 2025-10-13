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
        ICollection<IElectricNode> Children { get; set; }
        IElectricNode Parent { get; set; }
        void IncreaseVoltage(uint amount);
        void DecreaseVoltage(uint amount);
    }
}
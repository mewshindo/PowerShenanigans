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
        uint CurrentVoltage { get; }
        void IncreaseVoltage(uint amount);
        void DecreaseVoltage(uint amount);
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PowerShenanigans.Nodes
{
    public class SwitchNode : BaseNode
    {
        public bool IsOn { get; private set; } = true;

        public void Toggle(bool state)
        {
            IsOn = state;
            Plugin.Instance.UpdateAllNetworks();
        }

        public override void IncreaseVoltage(uint amount)
        {
            if (!IsOn) return;

            _voltage = amount;
            foreach (var conn in Connections)
                conn.IncreaseVoltage(amount);
        }

        public override void DecreaseVoltage(uint amount)
        {
            if (!IsOn) return;

            if (_voltage < amount) _voltage = 0;
            else _voltage -= amount;

            foreach (var conn in Connections)
                conn.DecreaseVoltage(amount);
        }
    }

}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Wired.Nodes
{
    public class SwitchNode : Node
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

            Voltage = amount;
            foreach (var conn in Connections)
                conn.IncreaseVoltage(amount);
        }

        public override void DecreaseVoltage(uint amount)
        {
            if (!IsOn) return;

            if (Voltage < amount) Voltage = 0;
            else Voltage -= amount;

            foreach (var conn in Connections)
                conn.DecreaseVoltage(amount);
        }
    }

}
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Wired.Nodes
{
    /// <summary>
    /// Switches allow current to pass when enabled.
    /// </summary>
    public class SwitchNode : Node
    {
        public bool IsOn { get; private set; } = true;

        public void Toggle(bool state)
        {
            // If we're turning the switch off, proactively clear voltage on connected nodes
            // so downstream components (timers, consumers, etc.) get DecreaseVoltage called
            // and can reset their internal state.
            if (!state)
            {
                // clear this switch's voltage
                _voltage = 0;

                // make a copy to avoid modification during iteration
                var conns = Connections.ToList();
                foreach (var conn in conns)
                {
                    try
                    {
                        // ask the connected node to drop its current to zero
                        conn.DecreaseVoltage(conn._voltage);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[SwitchNode] Error while decreasing connected node voltage: {ex}");
                    }
                }
            }

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
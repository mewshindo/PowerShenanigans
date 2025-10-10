using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PowerShenanigans.Nodes
{
    public class SwitchNode : MonoBehaviour, IElectricNode
    {
        private readonly IElectricNode _decoratee;
        private bool _isToggled;

        public uint CurrentVoltage => _isToggled ? _decoratee.CurrentVoltage : 0;

        public SwitchNode(IElectricNode decoratee)
        {
            _decoratee = decoratee;
        }

        public void Toggle()
        {
            _isToggled = !_isToggled;
        }

        public void IncreaseVoltage(uint amount)
        {
            if (_isToggled)
                _decoratee.IncreaseVoltage(amount);
        }

        public void DecreaseVoltage(uint amount)
        {
            if (_isToggled)
                _decoratee.DecreaseVoltage(amount);
        }
    }
}

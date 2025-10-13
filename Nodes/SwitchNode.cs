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
        public IElectricNode Parent { get; set; }
        public ICollection<IElectricNode> Children { get; set; }

        private bool _isToggled;
        public uint _voltage { get; private set; }

        public void Awake()
        {
            Children = new List<IElectricNode>();
        }

        public void Toggle()
        {
            _isToggled = !_isToggled;
        }

        public void IncreaseVoltage(uint amount)
        {

        }

        public void DecreaseVoltage(uint amount)
        {

        }
    }
}

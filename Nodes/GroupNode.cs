using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;



namespace PowerShenanigans.Nodes
{
    public class GroupNode : MonoBehaviour, IElectricNode
    {
        private readonly IReadOnlyCollection<IElectricNode> _children;
        public uint CurrentVoltage { get; private set; }
        public GroupNode(IReadOnlyCollection<IElectricNode> children)
        {
            _children = children;
        }
        public void IncreaseVoltage(uint amount)
        {
            CurrentVoltage += amount;
            foreach (var child in _children)
                child.IncreaseVoltage(amount);
        }
        public void DecreaseVoltage(uint amount)
        {
            if (CurrentVoltage < amount)
                CurrentVoltage = 0;
            else
                CurrentVoltage -= amount;

            foreach (var child in _children)
                child.DecreaseVoltage(amount);
        }
    }
}

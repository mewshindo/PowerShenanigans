using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PowerShenanigans.Nodes
{
    public class Supplier
    {
        public Transform generator;
        public uint maxSupply;
        private InteractableGenerator _generator;
        public Supplier(Transform generator, uint maxSupply)
        {
            this.generator = generator;
            this.maxSupply = maxSupply;
            _generator = generator.GetComponent<InteractableGenerator>();
        }
    }
    public class SupplierNode : MonoBehaviour, IElectricNode
    {
        private readonly IReadOnlyCollection<IElectricNode> _children;
        public uint CurrentVoltage { get; private set; }
        public uint MaxSupply { get; }
        public SupplierNode(uint maxSupply, IReadOnlyCollection<IElectricNode> children)
        {
            MaxSupply = maxSupply;
            _children = children;
        }
        public void IncreaseVoltage(uint amount)
        {
            CurrentVoltage = Math.Min(CurrentVoltage + amount, MaxSupply);

            foreach (var child in _children)
            {
                child.IncreaseVoltage(amount);
            }
        }
        public void DecreaseVoltage(uint amount)
        {
            if (CurrentVoltage < amount)
                CurrentVoltage = 0;
            else
                CurrentVoltage -= amount;

            foreach (var child in _children)
            {
                child.DecreaseVoltage(amount);
            }
        }
    }
}
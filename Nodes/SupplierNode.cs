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
        public ICollection<IElectricNode> Children { get; set; }
        public IElectricNode Parent { get; set; }
        public uint _voltage { get; private set; }
        public uint MaxSupply { get; }
        public void Awake()
        {
            Children = new List<IElectricNode>();
        }
        public void IncreaseVoltage(uint amount)
        {

        }
        public void DecreaseVoltage(uint amount)
        {

        }
    }
}
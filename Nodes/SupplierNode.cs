using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PowerShenanigans.Nodes
{
    public class SupplierNode : BaseNode
    {
        public uint MaxSupply { get; set; }

        private InteractableGenerator _generator;

        protected override void Awake()
        {
            base.Awake();
            _generator = GetComponent<InteractableGenerator>();
        }
        public uint GetAvailablePower() => MaxSupply;
        public override void IncreaseVoltage(uint amount) { }
        public override void DecreaseVoltage(uint amount) { }
    }
}
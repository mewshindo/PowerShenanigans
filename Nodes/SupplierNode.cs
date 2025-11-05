using SDG.Unturned;

namespace Wired.Nodes
{
    public class SupplierNode : Node
    {
        public uint Supply { get; set; }

        private InteractableGenerator _generator;

        protected override void Awake()
        {
            base.Awake();
            _generator = GetComponent<InteractableGenerator>();
        }
        public override void IncreaseVoltage(uint amount) { }
        public override void DecreaseVoltage(uint amount) { }
    }
}
using SDG.Unturned;
using Steamworks;

namespace Wired.Nodes
{
    public class ConsumerNode : Node
    {
        public uint Consumption { get; set; }
        private bool isPowered;

        private InteractableSpot _spot;
        private InteractableOven _oven;
        private InteractableOxygenator _oxygenator;
        private InteractableSafezone _safezone;
        private InteractableCharge _charge;
        private CoolConsumer _coolConsumer;

        protected override void Awake()
        {
            base.Awake();
            _spot = GetComponent<InteractableSpot>();
            _oven = GetComponent<InteractableOven>();
            _oxygenator = GetComponent<InteractableOxygenator>();
            _safezone = GetComponent<InteractableSafezone>();
            _charge = GetComponent<InteractableCharge>();
            _coolConsumer = GetComponent<CoolConsumer>();
        }
        public override void IncreaseVoltage(uint amount)
        {
            Voltage = amount;
            CheckPowerStatus();
        }

        public override void DecreaseVoltage(uint amount)
        {
            Voltage = (Voltage < amount) ? 0 : Voltage - amount;
            CheckPowerStatus();
        }

        private void CheckPowerStatus()
        {
            bool newPowered = Voltage >= Consumption;
            if (isPowered != newPowered)
            {
                isPowered = newPowered;
                SetPowered(isPowered);
            }
        }

        public void SetPowered(bool powered)
        {
            if (_spot != null)
                BarricadeManager.ServerSetSpotPowered(_spot, powered);
            if (_coolConsumer != null)
                _coolConsumer.SetActive(powered);
            if (_oven != null)
                BarricadeManager.ServerSetOvenLit(_oven, powered);
            if (_oxygenator != null)
                BarricadeManager.ServerSetOxygenatorPowered(_oxygenator, powered);
            if (_safezone != null)
                BarricadeManager.ServerSetSafezonePowered(_safezone, powered);
            if (_charge != null && powered)
                _charge.detonate((CSteamID)BarricadeManager.FindBarricadeByRootTransform(_charge.transform).GetServersideData().owner);
        }
    }
}
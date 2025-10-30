using PowerShenanigans.Consumers;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PowerShenanigans.Nodes
{
    public class ConsumerNode : Node
    {
        public uint consumption { get; set; }
        private bool isPowered;

        private InteractableSpot _spot;
        private InteractableOven _oven;
        private InteractableOxygenator _oxygenator;
        private InteractableSafezone _safezone;
        private CoolConsumer _coolConsumer;

        protected override void Awake()
        {
            base.Awake();
            _spot = GetComponent<InteractableSpot>();
            _oven = GetComponent<InteractableOven>();
            _oxygenator = GetComponent<InteractableOxygenator>();
            _safezone = GetComponent<InteractableSafezone>();
            _coolConsumer = GetComponent<CoolConsumer>();
        }
        public override void IncreaseVoltage(uint amount)
        {
            _voltage = amount;
            CheckPowerStatus();
        }

        public override void DecreaseVoltage(uint amount)
        {
            _voltage = (_voltage < amount) ? 0 : _voltage - amount;
            CheckPowerStatus();
        }

        private void CheckPowerStatus()
        {
            bool newPowered = _voltage >= consumption;
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
            else if(_coolConsumer != null)
                _coolConsumer.SetActive(powered);
            else if (_oven != null)
                BarricadeManager.ServerSetOvenLit(_oven, powered);
            else if (_oxygenator != null)
                BarricadeManager.ServerSetOxygenatorPowered(_oxygenator, powered);
            else if (_safezone != null)
                BarricadeManager.ServerSetSafezonePowered(_safezone, powered);
        }
    }
}
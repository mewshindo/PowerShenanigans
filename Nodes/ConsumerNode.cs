using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PowerShenanigans.Nodes
{
    public class Consumer
    {
        private Transform _barricade;

        private InteractableSpot _spot;
        private InteractableOven _oven;
        private InteractableBeacon _beacon;
        private InteractableOxygenator _oxygenator;
        private InteractableSafezone _safezone;

        public uint consumption;

        public Consumer(Transform barricade, uint consumption)
        {
            this._barricade = barricade;
            _spot = barricade.GetComponent<InteractableSpot>();
            _oven = barricade.GetComponent<InteractableOven>();
            _beacon = barricade.GetComponent<InteractableBeacon>();
            _oxygenator = barricade.GetComponent<InteractableOxygenator>();
            _safezone = barricade.GetComponent<InteractableSafezone>();
            this.consumption = consumption;
        }
        public void SetPowered(bool isPowered)
        {
            if (_spot != null)
                BarricadeManager.ServerSetSpotPowered(_spot, isPowered);
            else if (_oven != null)
                BarricadeManager.ServerSetOvenLit(_oven, isPowered);
            else if (_oxygenator != null)
                BarricadeManager.ServerSetOxygenatorPowered(_oxygenator, isPowered);
            else if (_safezone != null)
                BarricadeManager.ServerSetSafezonePowered(_safezone, isPowered);
        }
    }
    public class ConsumerNode : MonoBehaviour, IElectricNode
    {
        public IElectricNode Parent { get; set; }
        public ICollection<IElectricNode> Children { get; set; }

        public Consumer _consumer;
        public uint _voltage { get; private set; }

        public void Awake()
        {
            Children = new List<IElectricNode>();
        }
        public void IncreaseVoltage(uint amount)
        {
            _voltage += amount;
            CheckPowerStatus();
        }

        public void DecreaseVoltage(uint amount)
        {
            if (_voltage < amount)
                _voltage = 0;
            else
                _voltage -= amount;

            CheckPowerStatus();
        }

        private void CheckPowerStatus()
        {
            bool isPowered = _voltage >= _consumer.consumption;
            _consumer.SetPowered(isPowered);

        }
    }
}

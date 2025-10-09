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
    }
    public class ConsumerNode : IElectricNode
    {
        private readonly Consumer _consumer;
        public uint CurrentVoltage { get; private set; }

        public ConsumerNode(Consumer consumer)
        {
            _consumer = consumer;
        }

        public void IncreaseVoltage(uint amount)
        {
            CurrentVoltage += amount;
            CheckPowerStatus();
        }

        public void DecreaseVoltage(uint amount)
        {
            if (CurrentVoltage < amount)
                CurrentVoltage = 0;
            else
                CurrentVoltage -= amount;

            CheckPowerStatus();
        }

        private void CheckPowerStatus()
        {
            bool isPowered = CurrentVoltage >= _consumer.consumption;

        }
    }
}

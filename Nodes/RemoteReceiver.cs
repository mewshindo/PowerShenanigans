
using SDG.Unturned;
using System;
using UnityEngine;

namespace Wired.Nodes
{
    /// <summary>
    /// A remote receiver acts as a switch
    /// </summary>
    public class RemoteReceiver : Node
    {
        public bool IsOn { get; private set; } = true;
        public string Frequency { get; private set; }
        protected override void Awake()
        {
            base.Awake();
            Frequency = (Mathf.Round((3f + UnityEngine.Random.Range(0.1f, 0.8f)) * 1000f) / 1000f).ToString();
            DebugLogger.Log($"Assigned frequency {Frequency} to transmitter");
            
            NPCEventManager.onEvent += OnSignalBroadcasted;
        }
        private void OnDestroy()
        {

        }
        private void OnSignalBroadcasted(Player instigatingPlayer, string signal)
        {
            if (!signal.StartsWith(Frequency))
                return;
            signal = signal.Split(':')[1];

        }

        public void Toggle(bool state)
        {
            IsOn = state;
            Plugin.Instance.UpdateAllNetworks();
        }

        public override void IncreaseVoltage(uint amount)
        {
            if (!IsOn) return;
        }

        public override void DecreaseVoltage(uint amount)
        {
            if (!IsOn) return;
        }
    }
}
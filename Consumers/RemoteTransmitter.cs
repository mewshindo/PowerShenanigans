
using SDG.Unturned;
using System;
using UnityEngine;

namespace Wired.Nodes
{
    /// <summary>
    /// A remote receiver acts as a switch
    /// </summary>
    public class RemoteTransmitter : CoolConsumer
    {
        public string Frequency { get; private set; }
        private void Awake()
        {
            Frequency = (Mathf.Round((3f + UnityEngine.Random.Range(0.1f, 0.8f)) * 1000f) / 1000f).ToString();
            DebugLogger.Log($"Assigned frequency {Frequency} to transmitter");
        }
        public override void SetActive(bool active)
        {
            base.SetActive(active);
            TransmitSignal();
        }
        private void TransmitSignal()
        {
            NPCEventManager.broadcastEvent(null, $"{Frequency}:{isActive.ToString()}", false);
        }
    }
}

using SDG.Unturned;
using System;
using UnityEngine;

namespace Wired.Nodes
{
    /// <summary>
    /// A remote receiver acts as a switch
    /// </summary>
    public class ReceiverNode : Node
    {
        public bool IsOn { get; private set; } = true;
        public string Frequency { get; private set; }
        private InteractableSign _displaySign;
        protected override void Awake()
        {
            base.Awake();
            Frequency = (Mathf.Round((3f + UnityEngine.Random.Range(0.2f, 0.8f)) * 1000f) / 1000f).ToString();
            DebugLogger.Log($"Assigned frequency {Frequency} to transmitter");
            
            NPCEventManager.onEvent += OnSignalBroadcasted;
            _displaySign = GetComponent<InteractableSign>();
            if (_displaySign != null)
            {
                BarricadeManager.ServerSetSignText(_displaySign, $"FREQ {Frequency}");
            }
        }
        private void OnDestroy()
        {
            NPCEventManager.onEvent -= OnSignalBroadcasted;
        }
        private void OnSignalBroadcasted(Player instigatingPlayer, string signal)
        {
            if (!signal.StartsWith(this.Frequency))
                return;
            var value = signal.Split(':')[1];
            Console.WriteLine($"Received a signal: {signal}, {value=="True"}");
            Toggle(value == "True");
        }
        public bool TrySetFrequency(string signinput, Player instigator)
        {
            try
            {
                if (!signinput.StartsWith("FREQ "))
                    throw new Exception();
                var freq = signinput.Split(' ')[1];
                if (freq.Length != 5)
                    throw new Exception();
                if (!float.TryParse(freq, out var f))
                    throw new Exception();
                if (f < 3f || f > 4f)
                    throw new Exception();
                Frequency = freq;
                if (_displaySign != null)
                {
                    BarricadeManager.ServerSetSignText(_displaySign, $"FREQ {Frequency}");
                }
                DebugLogger.Log($"Set frequency to {Frequency}");
            }
            catch (Exception)
            {
                instigator.ServerShowHint("Invalid frequency! Please use format: FREQ X.XXX (between 3.000 and 4.000)", 5f);
                return false;
            }
            return true;
        }
        public void Toggle(bool state)
        {
            IsOn = state;
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
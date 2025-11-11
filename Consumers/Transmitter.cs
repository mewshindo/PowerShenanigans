
using SDG.Unturned;
using System;
using System.Linq.Expressions;
using UnityEngine;

namespace Wired.Nodes
{
    /// <summary>
    /// A remote receiver acts as a switch
    /// </summary>
    public class Transmitter : CoolConsumer
    {
        public string Frequency { get; private set; }
        public float Range { get; private set; } = 50f;
        private InteractableSign _displaySign;
        private void Awake()
        {
            Frequency = (Mathf.Round((3f + UnityEngine.Random.Range(0.1f, 0.8f)) * 1000f) / 1000f).ToString();
            var parser = new AssetParser(BarricadeManager.FindBarricadeByRootTransform(transform).asset.getFilePath());
            if (parser.TryGetFloat("Transmitter_Range_Meters", out var val))
            {
                Range = val;
            }
            _displaySign = GetComponent<InteractableSign>();
            if (_displaySign != null)
            {
                BarricadeManager.ServerSetSignText(_displaySign, $"FREQ {Frequency}");
            }
            DebugLogger.Log($"Assigned frequency {Frequency} to transmitter");
        }
        public bool TrySetFrequency(string signinput, Player instigator)
        {
            try
            {
                if (!signinput.StartsWith("FREQ "))
                    throw new Exception();
                var freq = signinput.Split(' ')[1];
                if(freq.Length != 5)
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
                instigator.ServerShowHint("Invalid frequency! Please use format: FREQ X.XXX (between 3.000 and 4.000)", 2f);
                return false;
            }
            return true;
        }
        public override void SetActive(bool active)
        {
            TransmitSignal(active);
            base.SetActive(active);
        }
        private void TransmitSignal(bool state)
        {
            NPCEventManager.broadcastEvent(null, $"{Frequency}:{state.ToString()}", false);
        }
    }
}
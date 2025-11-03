using SDG.Unturned;
using System.Collections;
using UnityEngine;

namespace Wired.Nodes
{
    /// <summary>
    /// Timers are signs, they wait for a specified delay before allowing current to pass through, while displaying a countdown via BarricadeManager.ServeretSignText().
    /// </summary>
    public class TimerNode : Node
    {
        public bool AllowCurrent = false;
        public bool IsCountingDown = false;
        public float DelaySeconds = 5f;

        private float _remainingTime = 0f;
        public bool Activated = false;
        private InteractableSign _displaySign;
        private Coroutine _coroutine;

        protected override void Awake()
        {
            base.Awake();
            _displaySign = GetComponent<InteractableSign>();
        }

        public override void IncreaseVoltage(uint amount)
        {
            if (Activated || IsCountingDown)
                return;

            Voltage = amount;
            StartTimer();
        }


        public override void DecreaseVoltage(uint amount)
        {
            if (Voltage < amount)
                Voltage = 0;
            else
                Voltage -= amount;

            if (Voltage == 0 && (IsCountingDown || Activated || AllowCurrent))
            {
                StopIfRunning();
                if (_displaySign != null)
                    BarricadeManager.ServerSetSignText(_displaySign, "OFF");
            }
        }

        public void StartTimer()
        {
            if (Activated || IsCountingDown)
                return;
            DebugLogger.Log($"[TimerNode {instanceID}] Starting countdown for {DelaySeconds} seconds at {Voltage}V.");
            _remainingTime = DelaySeconds;
            IsCountingDown = true;
            Activated = true;

            _coroutine = StartCoroutine(TimerCoroutine());
        }

        private IEnumerator TimerCoroutine()
        {
            while (_remainingTime > 0f)
            {
                yield return new WaitForSeconds(1f);
                _remainingTime--;

                if (_displaySign != null)
                {
                    int totalSeconds = (int)_remainingTime;
                    int minutes = totalSeconds / 60;
                    int seconds = totalSeconds % 60;

                    int msTenths = (int)((_remainingTime - totalSeconds) * 10f);
                    if (msTenths < 0) msTenths = 0;

                    string formattedTime = $"{minutes:D2}:{seconds:D2}";
                    BarricadeManager.ServerSetSignText(_displaySign, $"{formattedTime}");
                }
            }
            IsCountingDown = false;

            DebugLogger.Log($"[TimerNode {instanceID}] Countdown finished — passing {Voltage}V forward.");

            AllowCurrent = true;
            Plugin.Instance.UpdateAllNetworks();
            _coroutine = null;
        }

        public void StopIfRunning()
        {
            DebugLogger.Log($"[TimerNode {instanceID}] Stopping countdown.");
            if (_coroutine != null)
            {
                StopCoroutine(_coroutine);
            }
            Activated = false;
            AllowCurrent = false;
            IsCountingDown = false;
        }
    }
}
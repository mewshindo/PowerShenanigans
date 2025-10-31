﻿using SDG.Unturned;
using System.Collections;
using UnityEngine;

namespace Wired.Nodes
{
    public class TimerNode : Node
    {
        public bool allowCurrent = false;
        public bool isCountingDown = false;
        public float DelaySeconds = 5f;

        private float _remainingTime = 0f;
        public bool _activated = false;
        private InteractableSign _displaySign;
        private Coroutine _coroutine;

        protected override void Awake()
        {
            base.Awake();
            _displaySign = GetComponent<InteractableSign>();
        }

        public override void IncreaseVoltage(uint amount)
        {
            if (_activated || isCountingDown)
                return;

            _voltage = amount;
            StartTimer();
        }


        public override void DecreaseVoltage(uint amount)
        {
            if (_voltage < amount)
                _voltage = 0;
            else
                _voltage -= amount;

            if (_voltage == 0)
            {
                StopIfRunning();

                if (_displaySign != null)
                    BarricadeManager.ServerSetSignText(_displaySign, "OFF");
            }
            _activated = false;
            allowCurrent = false;
            isCountingDown = false;
        }

        public void StartTimer()
        {
            if(_activated || isCountingDown)
                return;
            DebugLogger.Log($"[TimerNode {instanceID}] Starting countdown for {DelaySeconds} seconds at {_voltage}V.");
            _remainingTime = DelaySeconds;
            isCountingDown = true;
            _activated = true;

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
                    int bars = Mathf.RoundToInt((_remainingTime / DelaySeconds) * 10f);
                    string progressBar = new string('|', bars);
                    BarricadeManager.ServerSetSignText(_displaySign, $"{progressBar}");
                }
            }
            isCountingDown = false;

            DebugLogger.Log($"[TimerNode {instanceID}] Countdown finished — passing {_voltage}V forward.");

            allowCurrent = true;
            Plugin.Instance.UpdateAllNetworks();
            _coroutine = null;
        }

        public void StopIfRunning()
        {
            if (_coroutine != null)
            {
                StopCoroutine(_coroutine);
            }
        }
    }



}

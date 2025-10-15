using SDG.Unturned;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace PowerShenanigans.Nodes
{
    public class TimerNode : BaseNode
    {
        public bool isCountingDown = false;
        public float DelaySeconds = 5f;
        private float _remainingTime = 0f;
        private bool _activated = false;

        public Coroutine Coroutine;
        private InteractableSign _displaySign;
        private string _signtext = "";
        private IEnumerator TimerCoroutine(float time)
        {
            while (_remainingTime > 0)
            {
                yield return new WaitForSeconds(1f);
                _signtext = "";
                _remainingTime -= 1f;
                float percentagePassed = DelaySeconds / _remainingTime * 100;
                for (int i = 0; i < percentagePassed; i++)
                {
                    _signtext += "|";
                }
                BarricadeManager.ServerSetSignText(_displaySign, _signtext);
            }

            foreach (var conn in Connections)
                conn.IncreaseVoltage(_voltage);
            Plugin.Instance.UpdateAllNetworks();

            isCountingDown = false;
        }
        protected override void Awake()
        {
            base.Awake();
            _displaySign = GetComponent<InteractableSign>();
        }

        public void StartTimer()
        {
            isCountingDown = true;
            Coroutine = StartCoroutine(TimerCoroutine(DelaySeconds));
        }

        public override void IncreaseVoltage(uint amount)
        {
            _voltage = amount;
            if (!_activated)
            {
                _activated = true;
                StartTimer();
            }
        }

        public override void DecreaseVoltage(uint amount)
        {
            if (_voltage < amount) _voltage = 0;
            else _voltage -= amount;

            if(_voltage == 0)
            {
                _activated = false;
            }

            foreach (var conn in Connections)
                conn.DecreaseVoltage(amount);
        }
    }
}

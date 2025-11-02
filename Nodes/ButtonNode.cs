using SDG.Unturned;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Wired.Nodes
{
    /// <summary>
    /// Buttons work like in minecraft, they allow current for a set amount of time when pressed.
    /// </summary>
    public class ButtonNode : Node
    {
        public bool allowCurrent = false;
        public float Delay = 1.25f;

        private Coroutine _coroutine;

        protected override void Awake()
        {
            base.Awake();
        }

        public override void IncreaseVoltage(uint amount)
        {
            if (!allowCurrent)
                return;

            _voltage = amount;
            ButtonOn();
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
            }
            allowCurrent = false;
        }

        public void ButtonOn()
        {
            if (allowCurrent)
                return;

            allowCurrent = true;
            Plugin.Instance.UpdateAllNetworks();

            _coroutine = StartCoroutine(ButtonCoroutine());
        }

        private IEnumerator ButtonCoroutine()
        {

            yield return new WaitForSeconds(Delay);

            allowCurrent = false;
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
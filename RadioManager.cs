using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Wired.Nodes;

namespace Wired
{
    public class RadioManager : MonoBehaviour
    {
        private static RadioManager Instance;
        private void Awake()
        {
            DebugLogger.Log("Initialized Radiomanager");
        }
        public void Transmit(string frequency, RadioSignalType signal)
        {
            List<ReceiverNode> receivers = Plugin.Instance.Nodes.OfType<ReceiverNode>().Where(r => r.Frequency == frequency).ToList();

            ushort touchedreceivers = 0;
            foreach (ReceiverNode receiver in receivers)
            {
                if (receiver.IsOn && signal == RadioSignalType.False || !receiver.IsOn && signal == RadioSignalType.True || signal == RadioSignalType.Toggle)
                {
                    receiver.SetState(signal);
                    touchedreceivers++;
                }
            }
            if(touchedreceivers > 0)
            {
                StopAllCoroutines();
                StartCoroutine(DelayedUpdateNetworks());
            }
        }
        IEnumerator DelayedUpdateNetworks()
        {
            yield return new WaitUntil(() => Plugin.Instance.UpdateFinished);
            Plugin.Instance.UpdateAllNetworks();

        }
    }
    public enum RadioSignalType
    {
        False,
        True,
        Toggle
    }
}

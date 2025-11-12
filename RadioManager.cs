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

            foreach (ReceiverNode receiver in receivers)
            {
                receiver.SetState(signal);
            }
            StartCoroutine(DelayedUpdateNetworks());
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

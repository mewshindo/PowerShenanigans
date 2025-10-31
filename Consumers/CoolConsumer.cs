using UnityEngine;

namespace Wired
{
    public abstract class CoolConsumer : MonoBehaviour
    {
        public bool isActive {  get; protected set; }
        public abstract void SetActive(bool active);
        public void unInit()
        {
            Destroy(this);
        }
    }
}

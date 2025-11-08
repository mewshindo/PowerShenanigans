using UnityEngine;

namespace Wired
{
    public abstract class CoolConsumer : MonoBehaviour
    {
        public bool isActive {  get; protected set; }
        public virtual void SetActive(bool active)
        {
            isActive = active;
        }
        public void unInit()
        {
            Destroy(this);
        }
    }
}

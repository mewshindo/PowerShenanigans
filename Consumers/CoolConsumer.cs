using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PowerShenanigans
{
    public abstract class CoolConsumer : MonoBehaviour
    {
        public bool isActive {  get; private set; }
        public abstract void SetActive(bool active);
    }
}

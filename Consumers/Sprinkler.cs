using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PowerShenanigans.Consumers
{
    public class Sprinkler : CoolConsumer
    {
        public float effectiveRadius {  get; private set; }
        public Sprinkler(float radius)
        {
            effectiveRadius = radius;
        }

        public override void SetActive(bool active)
        {
            if (active)
            {
                foreach(var crop in getCropsInRadius(effectiveRadius))
                {
                    crop.model.GetComponent<InteractableFarm>().updatePlanted(1u);
                }
            }
        }

        private List<BarricadeDrop> getCropsInRadius(float radius)
        {
            List<BarricadeDrop> result = new List<BarricadeDrop>();
            BarricadeRegion[,] regions = BarricadeManager.regions;
            foreach (var reg in regions)
            {
                foreach (var drop in reg.drops)
                {
                    if (drop.model.GetComponent<InteractableFarm>() == null)
                        continue;
                    float dist = Vector3.Distance(transform.position, drop.model.position);
                    if (dist < radius)
                    {
                        result.Add(drop);
                    }
                }
            }
            return result;
        }
    }
}

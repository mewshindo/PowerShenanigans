using SDG.Unturned;
using System.Collections.Generic;
using UnityEngine;

namespace Wired.Consumers
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
            isActive = active;
        }

        public List<Transform> GetCropsInRadius()
        {
            if (!isActive)
                return null;

            var crops = new List<Transform>();

            var bfinder = new BarricadeFinder(this.gameObject.transform.position, effectiveRadius);
            List<BarricadeDrop> drops = bfinder.GetBarricadesInRadius();
            foreach (var drop in drops)
            {
                if(drop.model.GetComponent <InteractableFarm>() == null) 
                    continue;
                if (!((ItemFarmAsset)drop.asset).shouldRainAffectGrowth)
                    continue;

                crops.Add(drop.model);
            }
            return crops;
        }
    }
}

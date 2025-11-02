using SDG.Unturned;
using System.Collections.Generic;
using UnityEngine;

namespace Wired
{
    public class BarricadeFinder
    {
        private readonly float _radius;
        private readonly Vector3 _position;

        public BarricadeFinder(Vector3 position = new Vector3(), float radius = 0f)
        {
            _radius = radius;
            _position = position;
        }

        public List<BarricadeDrop> GetBarricadesInRadius()
        {
            List<BarricadeDrop> result = new List<BarricadeDrop>();
            BarricadeRegion[,] regions = BarricadeManager.regions;
            if (_radius == 0)
            {
                foreach (var region in regions)
                {
                    foreach (var drop in region.drops)
                    {
                        result.Add(drop);
                    }
                }
            }
            foreach (var reg in regions)
            {
                foreach (var drop in reg.drops)
                {
                    float dist = Vector3.Distance(_position, drop.model.position);
                    if (dist < _radius)
                    {
                        result.Add(drop);
                    }
                }
            }
            return result;
        }
        public List<BarricadeDrop> GetBarricadesOfType<T>() where T: Component
        {
            List<BarricadeDrop> result = new List<BarricadeDrop>();

            foreach(BarricadeRegion reg in BarricadeManager.regions)
            {
                foreach(BarricadeDrop drop in reg.drops)
                {
                    if (drop.model.GetComponent<T>() != null)
                        result.Add(drop);
                }
            }
            return result;
        }
    }
}
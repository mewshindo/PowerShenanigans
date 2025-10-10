using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PowerShenanigans
{
    public static class Raycast
    {
        public static InteractableVehicle GetVehicle(Player player, out string collider)
        {
            Transform aim = player.look.aim;
            collider = "";
            if (!Physics.Raycast(aim.position, aim.forward, out var hitInfo, 3f, RayMasks.BLOCK_COLLISION))
            {
                return null;
            }
            if (!Physics.Raycast(aim.position, aim.forward, out var hit, 3f, 67108864) || hitInfo.transform != hit.transform)
            {
                return null;
            }
            InteractableVehicle interactableVehicle = VehicleManager.vehicles.FirstOrDefault((InteractableVehicle v) => v.transform == hit.transform);
            if (!(interactableVehicle != null))
            {
                return null;
            }
            collider = hit.collider.name;
            if (collider == "0")
            {
                RaycastHit validHit;
                RaycastHit[] hits = Physics.RaycastAll(aim.position, aim.forward, 3f);


                // Sort hits by distance so the closest ones come first
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (RaycastHit hit2 in hits)
                {
                    if (hit2.collider.gameObject.name == "0" || hit2.collider.gameObject.tag != "Vehicle")
                        continue; // ignore objects named "0"

                    validHit = hit; // found a valid hit
                    collider = hit2.collider.name;
                    return interactableVehicle;
                }
                return null;
            }
            return interactableVehicle;
        }

        public static BarricadeDrop GetBarricade(Player player, out string collider)
        {
            Transform aim = player.look.aim;
            collider = "";
            if (!Physics.Raycast(aim.position, aim.forward, out var hitInfo, float.PositiveInfinity, RayMasks.BLOCK_COLLISION))
            {
                return null;
            }
            if (!Physics.Raycast(aim.position, aim.forward, out var hit, float.PositiveInfinity, RayMasks.BARRICADE_INTERACT) || hitInfo.transform != hit.transform)
            {
                return null;
            }
            RaycastHit raycastInfo;
            Physics.Raycast(new Ray(player.look.aim.position, player.look.aim.forward), out raycastInfo, float.PositiveInfinity, RayMasks.BARRICADE_INTERACT);
            if ((raycastInfo.transform.name == "Hinge" || raycastInfo.transform.name == "Left_Hinge" || raycastInfo.transform.name == "Right_Hinge") && raycastInfo.transform.parent.name == "Skeleton")
            {
                BarricadeDrop barricadeDrop = BarricadeManager.FindBarricadeByRootTransform(raycastInfo.transform.parent.parent);
                collider = raycastInfo.collider.name;
                return barricadeDrop;
            }
            BarricadeDrop bardrop = BarricadeManager.FindBarricadeByRootTransform(raycastInfo.transform);
            collider = raycastInfo.collider.name;
            return bardrop;
        }
    }
}

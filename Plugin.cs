using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using PowerShenanigans;
using Rocket.Core.Plugins;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace PowerShenanigans
{
    public class Plugin : RocketPlugin<Config>
    {
        public static Plugin Instance;
        public Resources _resources;

        private const float traceinterval = 0.25f;
        protected override void Load()
        {
            Instance = this;
            Level.onLevelLoaded = (LevelLoaded)Delegate.Combine(Level.onLevelLoaded, (LevelLoaded)delegate
            {
                Level.info.configData.Has_Global_Electricity = true;
                _resources = new Resources();
            });
            BarricadeManager.onBarricadeSpawned = (BarricadeSpawnedHandler)Delegate.Combine(BarricadeManager.onBarricadeSpawned, new BarricadeSpawnedHandler(onBarricadeSpawned));
            Harmony harmony = new Harmony("com.mew.powerShenanigans");
            harmony.PatchAll();
            foreach (MethodBase method in harmony.GetPatchedMethods())
            {
                Console.WriteLine("Patched method: " + method.DeclaringType.FullName + "." + method.Name);
            }
        }

        protected override void Unload()
        {
            Harmony harmony = new Harmony("com.mew.powerShenanigans");
            harmony.UnpatchAll("com.mew.powerShenanigans");
            Instance = null;
        }

        private void onBarricadeSpawned(BarricadeRegion region, BarricadeDrop drop)
        {
            if (!(drop.model.GetComponent<InteractableSpot>() != null) && !(drop.model.GetComponent<InteractableFire>() != null))
            {
                return;
            }
            Console.WriteLine($"[PowerShenanigans] Spot spawned: {drop.model.name} at {drop.model.position}");
            UnturnedPlayer player = UnturnedPlayer.FromCSteamID((CSteamID)drop.GetServersideData().owner);
            if (player != null)
            {
                Console.WriteLine("[PowerShenanigans] Owner: " + player.DisplayName);
            }
            sendEffectCool(player, drop.model.position, _resources.node_consumer);
            foreach (Transform t in getBarricadesInRadius(drop.model.position, 100f, EElectricalComponentType.SWITCH))
            {
                Console.WriteLine("[PowerShenanigans] Found switch " + t.name + " in radius");
                sendEffectCool(player, t.position, _resources.node_power);
                TracePath(player, drop.model.position, t.position, _resources.path_power);
            }
        }

        private static List<Transform> getBarricadesInRadius(Vector3 center, float radius)
        {
            List<Transform> result = new List<Transform>();
            BarricadeRegion[,] regions = BarricadeManager.regions;
            foreach (BarricadeRegion reg in regions)
            {
                foreach (BarricadeDrop drop in reg.drops)
                {
                    float dist = Vector3.Distance(center, drop.model.position);
                    if (dist < radius)
                    {
                        result.Add(drop.model);
                    }
                }
            }
            return result;
        }
        private List<Transform> getBarricadesInRadius(Vector3 center, float radius, EElectricalComponentType type)
        {
            List<Transform> result = new List<Transform>();
            BarricadeRegion[,] regions = BarricadeManager.regions;
            foreach (BarricadeRegion reg in regions)
            {
                foreach (BarricadeDrop drop in reg.drops)
                {
                    float dist = Vector3.Distance(center, drop.model.position);
                    if (dist < radius)
                    {
                        switch (type)
                        {
                            case EElectricalComponentType.SWITCH:
                                if (drop.model.GetComponent<InteractableFire>() != null)
                                {
                                    result.Add(drop.model);
                                }
                                break;
                            case EElectricalComponentType.GENERATOR:
                                if (drop.model.GetComponent<InteractableGenerator>() != null)
                                {
                                    result.Add(drop.model);
                                }
                                break;
                            case EElectricalComponentType.SPOT:
                                if (drop.model.GetComponent<InteractableSpot>() != null)
                                {
                                    result.Add(drop.model);
                                }
                                break;
                            case EElectricalComponentType.OVEN:
                                if (drop.model.GetComponent<InteractableFire>() != null)
                                {
                                    result.Add(drop.model);
                                }
                                break;
                        }
                    }
                }
            }
            return result;
        }

        private async void sendEffectCool(UnturnedPlayer player, Vector3 dropPosition, EffectAsset asset)
        {
            TriggerEffectParameters effect = new TriggerEffectParameters
            {
                asset = asset,
                position = dropPosition,
                relevantDistance = 64f,
                shouldReplicate = true,
                reliable = true
            };
            effect.SetDirection(Vector3.down);
            effect.SetRelevantPlayer(player.SteamPlayer());
            EffectManager.triggerEffect(effect);
            await Task.Delay(10000);
            EffectManager.ClearEffectByGuid(asset.GUID, Provider.findTransportConnection(player.CSteamID));
        }

        private void TracePath(UnturnedPlayer player, Vector3 point1, Vector3 point2, EffectAsset patheffect)
        {
            float distance = Vector3.Distance(point1, point2);
            int count = Mathf.FloorToInt(distance / 0.25f);
            Vector3 direction = (point2 - point1).normalized;
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = point1 + direction * ((float)i * 0.25f);
                sendEffectCool(player, pos, patheffect);
            }
        }
        [HarmonyPatch(typeof(InteractableSpot), "ReceiveToggleRequest")]
        private static class Patch_InteractableSpot_ReceiveToggleRequest
        {
            private static bool Prefix(InteractableSpot __instance, ServerInvocationContext context, bool desiredPowered)
            {
                Player player = context.GetPlayer();
                Console.WriteLine(string.Format("[PowerShenanigans] ReceiveToggleRequest from player {0} desiredPowered={1}, __instance.name: {2}", player?.ToString() ?? "null", desiredPowered, __instance.name));
                if (player == null)
                {
                    return true;
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(InteractableFire), "ReceiveToggleRequest")]
        private static class Patch_InteractableFire_ReceiveToggleRequest
        {
            private static bool Prefix(InteractableFire __instance, ServerInvocationContext context, bool desiredLit)
            {
                Console.WriteLine(string.Format("[PowerShenanigans] ReceiveToggleRequest from player {0} desiredLit={1}, __instance.name: {2}", context.GetPlayer()?.ToString() ?? "null", desiredLit, __instance.name));
                if (__instance.name == "360")
                {
                    List<Transform> barricadesinradius = getBarricadesInRadius(__instance.transform.position, 100f);
                    Console.WriteLine($"Found {barricadesinradius.Count} barricades in radius");
                    foreach (Transform t in barricadesinradius)
                    {
                        if (t.GetComponent<InteractableSpot>() != null)
                        {
                            Console.WriteLine($"[PowerShenanigans] Setting spot {t.name} to {desiredLit}");
                            t.GetComponent<InteractableSpot>().updateWired(desiredLit);
                            BarricadeManager.ServerSetSpotPowered(t.GetComponent<InteractableSpot>(), desiredLit);
                        }
                    }
                }
                return true;
            }
        }
    }
    public enum EElectricalComponentType
    {
        GENERATOR,
        SPOT,
        OVEN,
        SWITCH
    }
}

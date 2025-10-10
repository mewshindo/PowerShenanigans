using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using PowerShenanigans;
using PowerShenanigans.Nodes;
using Rocket.Core.Plugins;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;
using static SDG.Unturned.GunAttachmentEventHook;

namespace PowerShenanigans
{
    public class Plugin : RocketPlugin<Config>
    {
        public static Plugin Instance;
        public Resources _resources;

        private const float traceinterval = 0.25f;
        protected override void Unload()
        {
            Level.onLevelLoaded -= onLevelLoaded;
            BarricadeManager.onBarricadeSpawned -= onBarricadeSpawned;

            Harmony harmony = new Harmony("com.mew.powerShenanigans");
            harmony.UnpatchAll("com.mew.powerShenanigans");
            Instance = null;
        }
        protected override void Load()
        {
            Instance = this;
            Level.onLevelLoaded += onLevelLoaded;
            BarricadeManager.onBarricadeSpawned += onBarricadeSpawned;
            UseableGun.onBulletHit += UseableGun_onBulletHit;

            Harmony harmony = new Harmony("com.mew.powerShenanigans");
            harmony.PatchAll();
            foreach (MethodBase method in harmony.GetPatchedMethods())
            {
                Console.WriteLine("Patched method: " + method.DeclaringType.FullName + "." + method.Name);
            }
        }

        private void UseableGun_onBulletHit(UseableGun gun, BulletInfo bullet, InputInfo hit, ref bool shouldAllow)
        {
            if(hasFlag(gun.equippedGunAsset, "ElectricalInspector"))
            {
                shouldAllow = false;
            }
        }

        private void onLevelLoaded(int level)
        {
            Level.info.configData.Has_Global_Electricity = true;
            _resources = new Resources();
        }
        private void onBarricadeSpawned(BarricadeRegion region, BarricadeDrop drop)
        {
            if (isElectricalComponent(drop.model))
            {
                if(drop.model.GetComponent<InteractableGenerator>() != null)
                {
                    if (drop.model.GetComponent<SupplierNode>() == null)
                        drop.model.gameObject.AddComponent<SupplierNode>();
                }
                else if (isSwitch(drop))
                {
                    if(drop.model.GetComponent<SwitchNode>() == null)
                        drop.model.gameObject.AddComponent<SwitchNode>();
                }
                else if (isConsumer(drop.model))
                {
                    if (drop.model.GetComponent<ConsumerNode>() == null)
                        drop.model.gameObject.AddComponent<ConsumerNode>();
                }
            }

            //if (!(drop.model.GetComponent<InteractableSpot>() != null) && !(drop.model.GetComponent<InteractableFire>() != null))
            //{
            //    return;
            //}
            //Console.WriteLine($"[PowerShenanigans] Spot spawned: {drop.model.name} at {drop.model.position}");
            //UnturnedPlayer player = UnturnedPlayer.FromCSteamID((CSteamID)drop.GetServersideData().owner);
            //if (player != null)
            //{
            //    Console.WriteLine("[PowerShenanigans] Owner: " + player.DisplayName);
            //}
            //sendEffectCool(player, drop.model.position, _resources.node_consumer);
            //foreach (Transform t in getBarricadesInRadius(drop.model.position, 100f, EElectricalComponentType.SWITCH))
            //{
            //    Console.WriteLine("[PowerShenanigans] Found switch " + t.name + " in radius");
            //    sendEffectCool(player, t.position, _resources.node_power);
            //    TracePath(player, drop.model.position, t.position, _resources.path_power);
            //}
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
                            case EElectricalComponentType.CONSUMER:
                                if (isConsumer(drop.model))
                                {
                                    result.Add(drop.model);
                                }
                                break;
                            case EElectricalComponentType.OVEN:
                                if (drop.model.GetComponent<InteractableOven>() != null)
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
            int count = Mathf.FloorToInt(distance / traceinterval);
            Vector3 direction = (point2 - point1).normalized;
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = point1 + direction * ((float)i * traceinterval);
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
        private bool isConsumer(Transform barricade)
        {
            if(barricade == null) return false;

            if(barricade.GetComponent<InteractableSpot>() != null)
                return true;
            if(barricade.GetComponent<InteractableOven>() != null)
                return true;
            if(barricade.GetComponent <InteractableOxygenator>() != null)
                return true;
            if (barricade.GetComponent <InteractableSafezone>() != null)
                return true;
            if(barricade.GetComponent<InteractableBeacon>() != null)
                return true;

            return false;
        }
        private bool isElectricalComponent(Transform barricade)
        {
            if (barricade != null) return false;

            if(barricade.GetComponent<InteractableGenerator>() != null)
                return true;
            if(isConsumer(barricade)) return true;
            return false;
        }
        private bool isSwitch(BarricadeDrop drop)
        {
            if(drop == null) return false;
            if (hasFlag(drop.asset, "Switch"))
                return true;
            return false;
        }
        private bool hasFlag(Asset asset, string flag)
        {
            StreamReader reader = File.OpenText(asset.getFilePath());
            string line;
            while((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith(flag))
                    return true;
            }
            return false;
        }
    }
    public enum EElectricalComponentType
    {
        GENERATOR,
        CONSUMER,
        OVEN,
        SWITCH
    }
}

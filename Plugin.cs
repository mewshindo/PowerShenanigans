using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using PowerShenanigans;
using PowerShenanigans.Nodes;
using Rocket.Core.Assets;
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

        private List<Guid> _ElectricalInspectors = new List<Guid>();
        private Dictionary<CSteamID, Transform> _SelectedNode = new Dictionary<CSteamID, Transform>();
        protected override void Unload()
        {
            Level.onLevelLoaded -= onLevelLoaded;
            BarricadeManager.onBarricadeSpawned -= onBarricadeSpawned;
            UseableGun.onBulletHit -= UseableGun_onBulletHit;
            PlayerEquipment.OnUseableChanged_Global -= PlayerEquipment_OnUseableChanged_Global;

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
            PlayerEquipment.OnUseableChanged_Global += PlayerEquipment_OnUseableChanged_Global;

            Harmony harmony = new Harmony("com.mew.powerShenanigans");
            harmony.PatchAll();
            foreach (MethodBase method in harmony.GetPatchedMethods())
            {
                Console.WriteLine("Patched method: " + method.DeclaringType.FullName + "." + method.Name);
            }
        }
        private void PlayerEquipment_OnUseableChanged_Global(PlayerEquipment equipment)
        {
            throw new NotImplementedException();
        }

        private void UseableGun_onBulletHit(UseableGun gun, BulletInfo bullet, InputInfo hit, ref bool shouldAllow)
        {
            var asset = gun.equippedGunAsset;

            // Only handle Electrical Inspectors or special gun id
            if (!_ElectricalInspectors.Contains(asset.GUID) && asset.id != 1165)
                return;

            shouldAllow = false;

            var steamid = gun.player.channel.owner.playerID.steamID;
            var player = UnturnedPlayer.FromCSteamID(steamid);

            BarricadeDrop drop = Raycast.GetBarricade(gun.player, out _);
            if (drop == null)
            {
                ClearSelection(player);
                return;
            }

            var model = drop.model;
            if (!isElectricalComponent(model))
                return;

            // Case 1: Selecting first node
            if (!_SelectedNode.ContainsKey(steamid))
            {
                _SelectedNode[steamid] = model;
                player.Player.ServerShowHint($"Selected {drop.asset.name} ({drop.instanceID})\nShoot another component to link.\nShoot ground to clear selection.", 10f);
                return;
            }

            // Case 2: Linking with a second node
            var node1 = _SelectedNode[steamid];
            var node2 = model;

            // Deselect if same node
            if (node1 == node2)
            {
                _SelectedNode.Remove(steamid);
                player.Player.ServerShowHint("Cleared selection.", 3f);
                return;
            }

            // Ensure both are valid components
            if (!isElectricalComponent(node1) || !isElectricalComponent(node2))
            {
                ClearSelection(player);
                return;
            }

            if (!TryEnsureNodeScripts(node1, node2))
            {
                player.Player.ServerShowHint("One of the nodes is missing a component script! Cannot link.", 5f);
                ClearSelection(player);
                return;
            }

            var electricNode1 = node1.GetComponent<IElectricNode>();
            var electricNode2 = node2.GetComponent<IElectricNode>();

            if (electricNode1?.Children == null || electricNode2?.Children == null)
            {
                player.Player.ServerShowHint("Invalid node structure.", 3f);
                return;
            }

            if (electricNode1.Children.Contains(electricNode2) || electricNode2.Children.Contains(electricNode1))
            {
                player.Player.ServerShowHint("Unlinked nodes!", 3f);

                if (electricNode1.Children.Remove(electricNode2))
                    electricNode2.Parent = null;
                else if (electricNode2.Children.Remove(electricNode1))
                    electricNode1.Parent = null;

                ClearSelection(player);
                DisplayNodes(steamid);
                return;
            }


            if (!TryLinkNodes(electricNode1, electricNode2))
            {
                player.Player.ServerShowHint("Invalid node combination!", 3f);
                ClearSelection(player);
                return;
            }

            player.Player.ServerShowHint($"Linked {node1.name} → {node2.name}", 5f);
            _SelectedNode.Remove(steamid);

            DisplayNodes(steamid);

            //// Handle flag adding
            //if (HasFlag(asset, "ElectricalInspector"))
            //{
            //    _ElectricalInspectors.Add(asset.GUID);
            //}
        }

        private void ClearSelection(UnturnedPlayer player)
        {
            var steamid = player.CSteamID;
            _SelectedNode.Remove(steamid);
            player.Player.ServerShowHint("Cleared selection.", 3f);
        }

        private bool TryEnsureNodeScripts(Transform node1, Transform node2)
        {
            EnsureNodeScript(node1);
            EnsureNodeScript(node2);

            return node1.GetComponent<IElectricNode>() != null && node2.GetComponent<IElectricNode>() != null;
        }

        private void EnsureNodeScript(Transform node)
        {
            if (node.GetComponent<InteractableGenerator>() != null && node.GetComponent<SupplierNode>() == null)
                node.gameObject.AddComponent<SupplierNode>();

            if (isSwitch(node.GetComponent<BarricadeDrop>()) && node.GetComponent<SwitchNode>() == null)
                node.gameObject.AddComponent<SwitchNode>();

            if (isConsumer(node) && node.GetComponent<ConsumerNode>() == null)
                node.gameObject.AddComponent<ConsumerNode>();
        }

        private bool TryLinkNodes(IElectricNode a, IElectricNode b)
        {
            // Prevent invalid pairings
            if (a is SupplierNode && b is SupplierNode)
                return false;
            if (a is ConsumerNode && b is ConsumerNode)
                return false;

            // Pick direction based on types
            if (a is SupplierNode && b.GetType() != typeof(SupplierNode))
                return Link(a, b);
            if (b is SupplierNode && a.GetType() != typeof(SupplierNode))
                return Link(b, a);
            if (a is SwitchNode && b.GetType() != typeof(SwitchNode))
                return Link(a, b);
            if (b is SwitchNode && a.GetType() != typeof(SwitchNode))
                return Link(b, a);

            // Default: just link a → b
            return Link(a, b);
        }

        private bool Link(IElectricNode parent, IElectricNode child)
        {
            if(!parent.Children.Contains(child))
                parent.Children.Add(child);
            child.Parent = parent;
            return true;
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
                if (drop.model.GetComponent<InteractableGenerator>() != null)
                {
                    if (drop.model.GetComponent<SupplierNode>() == null)
                        drop.model.gameObject.AddComponent<SupplierNode>();
                }
                else if (isSwitch(drop))
                {
                    if (drop.model.GetComponent<SwitchNode>() == null)
                        drop.model.gameObject.AddComponent<SwitchNode>();
                }
                else if (isConsumer(drop.model))
                {
                    if (drop.model.GetComponent<ConsumerNode>() == null)
                        drop.model.gameObject.AddComponent<ConsumerNode>();
                }
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
        private void sendEffectCool(UnturnedPlayer player, Vector3 dropPosition, EffectAsset asset)
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
        }
        private void TracePath(UnturnedPlayer player, Vector3 point1, Vector3 point2, EffectAsset pathEffect)
        {
            float distance = Vector3.Distance(point1, point2);

            float spacing = Mathf.Clamp(distance / 20f, 0.5f, 2.0f);
            int count = Mathf.FloorToInt(distance / spacing);

            Vector3 direction = (point2 - point1).normalized;

            for (int i = 0; i <= count; i++)
            {
                Vector3 pos = point1 + direction * (i * spacing);
                sendEffectCool(player, pos, pathEffect);
            }
        }

        private async void DisplayNodes(CSteamID steamid)
        {
            UnturnedPlayer player = UnturnedPlayer.FromCSteamID(steamid);
            if (player != null)
            {
                Console.WriteLine("Displayed nodes to " + player.DisplayName);
            }
            List<Guid> usedEffects = new List<Guid>();
            foreach (Transform t in getBarricadesInRadius(player.Position, 100f))
            {
                if (isConsumer(t))
                {
                    sendEffectCool(player, t.position, _resources.node_consumer);
                    usedEffects.Add(_resources.node_consumer.GUID);
                    if (t.TryGetComponent<IElectricNode>(out IElectricNode node))
                    {
                        if (node.Parent != null)
                        {
                            TracePath(player, t.position, ((MonoBehaviour)node.Parent).transform.position, _resources.path_consumer);
                            usedEffects.Add(_resources.path_consumer.GUID);
                        }
                    }
                }
                else if (t.GetComponent<InteractableFire>() != null)
                {
                    sendEffectCool(player, t.position, _resources.node_switch);
                    usedEffects.Add(_resources.node_switch.GUID);
                    if (t.TryGetComponent<IElectricNode>(out IElectricNode node2))
                    {
                        if (node2.Parent != null)
                        {
                            TracePath(player, t.position, ((MonoBehaviour)node2.Parent).transform.position, _resources.path_switch);
                            usedEffects.Add(_resources.path_switch.GUID);
                        }
                    }
                }
                else if (t.GetComponent<InteractableGenerator>() != null)
                {
                    sendEffectCool(player, t.position, _resources.node_power);
                    usedEffects.Add(_resources.node_power.GUID);
                    if (t.TryGetComponent<IElectricNode>(out IElectricNode node3))
                    {
                        if (node3.Parent != null)
                        {
                            TracePath(player, t.position, ((MonoBehaviour)node3.Parent).transform.position, _resources.path_power);
                            usedEffects.Add(_resources.path_power.GUID);
                        }
                    }
                }
            }
            await Task.Delay(5000);
            foreach (Guid guid in usedEffects)
                EffectManager.ClearEffectByGuid(guid, Provider.findTransportConnection(player.CSteamID));
        }

        //[HarmonyPatch(typeof(UseableMelee), "startPrimary")]
        //private static class Patch_UseableMelee_startPrimary
        //{
        //    private static void Postfix(UseableMelee __instance)
        //    {
        //        if(!__instance.player.equipment.isBusy) 
        //            return;

        //        Console.WriteLine($"Lil swing from {__instance.player.name} ! isBusy: {__instance.player.equipment.isBusy}");
        //        return;
        //    }
        //}
        //[HarmonyPatch(typeof(UseableMelee), "startSecondary")]
        //private static class Patch_UseableMelee_startSecondary
        //{
        //    private static void Postfix(UseableMelee __instance)
        //    {
        //        if (!__instance.player.equipment.isBusy)
        //            return;

        //        Console.WriteLine($"Big swing from {__instance.player.name} ! isBusy: {__instance.player.equipment.isBusy}");
        //        return;
        //    }
        //}

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
            if (barricade == null) return false;

            if (barricade.GetComponent<InteractableSpot>() != null)
                return true;
            if (barricade.GetComponent<InteractableOven>() != null)
                return true;
            if (barricade.GetComponent<InteractableOxygenator>() != null)
                return true;
            if (barricade.GetComponent<InteractableSafezone>() != null)
                return true;
            if (barricade.GetComponent<InteractableBeacon>() != null)
                return true;

            return false;
        }
        private bool isElectricalComponent(Transform barricade)
        {
            if (barricade == null) return false;

            if (barricade.GetComponent<InteractableFire>() != null) return true;

            if (barricade.GetComponent<InteractableGenerator>() != null) return true;

            if (isConsumer(barricade)) return true;
            return false;
        }
        private bool isSwitch(BarricadeDrop drop)
        {
            if (drop == null) return false;
            if (HasFlag(drop.asset, "Switch"))
                return true;
            return false;
        }
        private bool HasFlag(Asset asset, string flag)
        {
            StreamReader reader = File.OpenText(asset.getFilePath());
            string line;
            while ((line = reader.ReadLine()) != null)
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

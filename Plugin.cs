using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
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
using Rocket.Unturned;
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

        private readonly Dictionary<uint, IElectricNode> nodes = new Dictionary<uint, IElectricNode>();

        private List<Guid> _WiringTools = new List<Guid>();
        private Dictionary<CSteamID, Transform> _SelectedNode = new Dictionary<CSteamID, Transform>();
        protected override void Unload()
        {
            Level.onLevelLoaded -= onLevelLoaded;
            BarricadeManager.onBarricadeSpawned -= onBarricadeSpawned;
            UseableGun.onBulletHit -= UseableGun_onBulletHit;
            UseableGun.onBulletSpawned -= onBulletSpawned;
            U.Events.OnPlayerConnected -= (player) =>
            {
                player.Player.gameObject.AddComponent<CoolEvents>();
            };
            CoolEvents.OnDequipRequested -= onDequipRequested;
            CoolEvents.OnEquipRequested -= onEquipRequested;
            BarricadeDrop.OnSalvageRequested_Global -= onSalvageRequested_Global;

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
            UseableGun.onBulletSpawned += onBulletSpawned;
            U.Events.OnPlayerConnected += (player) =>
            {
                player.Player.gameObject.AddComponent<CoolEvents>();
            };
            CoolEvents.OnDequipRequested += onDequipRequested;
            CoolEvents.OnEquipRequested += onEquipRequested;
            BarricadeDrop.OnSalvageRequested_Global += onSalvageRequested_Global;

            Harmony harmony = new Harmony("com.mew.powerShenanigans");
            harmony.PatchAll();
            foreach (MethodBase method in harmony.GetPatchedMethods())
            {
                Console.WriteLine("Patched method: " + method.DeclaringType.FullName + "." + method.Name);
            }

        }

        private void UseableGun_onBulletHit(UseableGun gun, BulletInfo bullet, InputInfo hit, ref bool shouldAllow)
        {
            if (_WiringTools.Contains(gun.equippedGunAsset.GUID))
                shouldAllow = false;
        }
        private bool doesOwnDrop(BarricadeDrop drop, CSteamID steamid)
        {
            var dropdata = drop.GetServersideData();
            if(dropdata.owner != 0 && dropdata.owner == (ulong)steamid)
                return true;
            if(dropdata.group != 0 && dropdata.group == (ulong)UnturnedPlayer.FromCSteamID(steamid).SteamGroupID)
                return true;
            if (dropdata.group != 0 && dropdata.group == (ulong)UnturnedPlayer.FromCSteamID(steamid).Player.quests.groupID)
                return true;
            return false;
        }

        private void onBulletSpawned(UseableGun gun, BulletInfo bullet)
        {
            var asset = gun.equippedGunAsset;

            // Only handle Electrical Inspectors or special gun id
            if (!_WiringTools.Contains(asset.GUID))
                return;

            var steamid = gun.player.channel.owner.playerID.steamID;
            var player = UnturnedPlayer.FromCSteamID(steamid);

            BarricadeDrop drop = Raycast.GetBarricade(gun.player, out _);
            if (drop == null)
            {
                ClearSelection(player);
                return;
            }

            if(!doesOwnDrop(drop, steamid))
            {
                ClearSelection(player);
                player.Player.ServerShowHint("You do not own this barricade!", 3f);
                return;
            }

            var model = drop.model;
            if (!isElectricalComponent(model))
                return;

            // 🟢 Case 1: Selecting the first node
            if (!_SelectedNode.ContainsKey(steamid))
            {
                _SelectedNode[steamid] = model;
                player.Player.ServerShowHint(
                    $"Selected {drop.asset.name} ({drop.instanceID})\nShoot another component to link.\nShoot ground to clear selection.",
                    10f);
                return;
            }

            // 🟠 Case 2: Attempting to link with a second node
            var node1 = _SelectedNode[steamid];
            var node2 = model;

            // Same node clicked again → deselect
            if (node1 == node2)
            {
                _SelectedNode.Remove(steamid);
                player.Player.ServerShowHint("Cleared selection.", 3f);
                return;
            }

            // Validate components
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

            if (electricNode1 == null || electricNode2 == null)
            {
                player.Player.ServerShowHint("Invalid node structure.", 3f);
                return;
            }

            // 🧩 Unlink if already connected
            if (electricNode1.Connections.Contains(electricNode2) || electricNode2.Connections.Contains(electricNode1))
            {
                player.Player.ServerShowHint("Unlinked nodes!", 3f);

                electricNode1.Connections.Remove(electricNode2);
                electricNode2.Connections.Remove(electricNode1);

                updateNodesDisplay(steamid);

                // If a node loses all connections, reset its voltage
                if (electricNode1.Connections.Count == 0)
                    electricNode1.DecreaseVoltage(electricNode1._voltage);
                if (electricNode2.Connections.Count == 0)
                    electricNode2.DecreaseVoltage(electricNode2._voltage);

                UpdateAllNetworks();
                ClearSelection(player);
                return;
            }

            if (!Link(electricNode1, electricNode2))
            {
                player.Player.ServerShowHint("Invalid node combination!", 3f);
                ClearSelection(player);
                return;
            }

            player.Player.ServerShowHint($"Linked {node1.name} ↔ {node2.name}", 5f);

            UpdateAllNetworks();
            updateNodesDisplay(steamid);
            _SelectedNode.Remove(steamid);
        }

        private void onSalvageRequested_Global(BarricadeDrop drop, SteamPlayer instigatorClient, ref bool shouldAllow)
        {
            if (drop == null)
            {
                Console.WriteLine("Drop null");
                return;
            }

            drop.model.TryGetComponent<IElectricNode>(out var nodeComp);
            if (nodeComp != null)
            {
                nodeComp.unInit();
                Console.WriteLine($"Removed node component from salvaged drop {drop.instanceID}");
            }

            uint id = drop.instanceID;
            if (!nodes.TryGetValue(id, out var node))
            {
                Console.WriteLine($"No id in _nodes: {drop.instanceID}");
                return;
            }

            // Unlink from all connected nodes
            foreach (var connected in node.Connections.ToList())
            {
                connected.Connections.Remove(node);
            }

            node.Connections.Clear();
            nodes.Remove(id);
            updateNodesDisplay(instigatorClient.playerID.steamID);
            UpdateAllNetworks(); // Recalculate power

            Console.WriteLine($"Removed node {id} and unlinked from network.");
        }
        private void onLevelLoaded(int level)
        {
            Level.info.configData.Has_Global_Electricity = true;
            _resources = new Resources();

            foreach (BarricadeRegion reg in BarricadeManager.regions)
            {
                foreach (BarricadeDrop drop in reg.drops)
                {
                    onBarricadeSpawned(reg, drop);
                }
            }
            var stopwatch = Stopwatch.StartNew();

            List<ItemGunAsset> wiringtools = new List<ItemGunAsset>();
            Assets.find(wiringtools);

            foreach (ItemGunAsset asset in wiringtools)
            {
                if (HasFlag(asset, "WiringTool"))
                {
                    _WiringTools.Add(asset.GUID);
                }
                else if (asset.GUID == new Guid("ce60ac5b55bf4d70937e83a69c76dae5") || asset.id == 1165)
                {
                    _WiringTools.Add(asset.GUID);
                }
            }


            float milliseconds = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"[Wired] Found {_WiringTools.Count} wiring tools, parsed {wiringtools.Count} item asset files, took {milliseconds} ms.");
        }
        private void onEquipRequested(PlayerEquipment equipment, ItemJar jar, ItemAsset asset, ref bool shouldAllow)
        {
            if(_WiringTools.Contains(asset.GUID))
            {
                DisplayNodes(equipment.player.channel.owner.playerID.steamID);
            }
        }

        private void onDequipRequested(Player player, PlayerEquipment equipment, ref bool shouldAllow)
        {
            if(_WiringTools.Contains(equipment.asset.GUID))
            {
                foreach (Guid guid in _resources.nodeeffects)
                    EffectManager.ClearEffectByGuid(guid, Provider.findTransportConnection(UnturnedPlayer.FromPlayer(player).CSteamID));
            }
        }
        private void onBarricadeSpawned(BarricadeRegion region, BarricadeDrop drop)
        {
            if (isElectricalComponent(drop.model))
            {
                if (drop.model.GetComponent<InteractableGenerator>() != null)
                {
                    if (drop.model.GetComponent<SupplierNode>() == null)
                        drop.model.gameObject.AddComponent<SupplierNode>();
                    var node = drop.model.GetComponent<SupplierNode>();
                    nodes[node.instanceID] = node;
                    if (drop.asset.id == 458) // Portable generator
                        node.MaxSupply = 500;
                    if(drop.asset.id == 1230) // Industrial generator
                        node.MaxSupply = 2500;
                }
                else if (isSwitch(drop))
                {
                    if (drop.model.GetComponent<SwitchNode>() == null)
                        drop.model.gameObject.AddComponent<SwitchNode>();
                    var node = drop.model.GetComponent<SwitchNode>();
                    nodes[node.instanceID] = node;
                }
                else if (isConsumer(drop.model))
                {
                    if (drop.model.GetComponent<ConsumerNode>() == null)
                        drop.model.gameObject.AddComponent<ConsumerNode>();
                    var node = drop.model.GetComponent<ConsumerNode>();
                    nodes[node.instanceID] = node;
                    node.SetPowered(false);
                    if (drop.asset.id == 459) // Spotlight
                        node.consumption = 250;
                    if (drop.asset.id == 1222) // Cagelight
                        node.consumption = 25;

                }
            }
        }
        private void ClearSelection(UnturnedPlayer player)
        {
            var steamid = player.CSteamID;
            _SelectedNode.Remove(steamid);
        }
        /// <summary>
        /// Resets and then displays nodes to the player
        /// </summary>
        /// <param name="steamid"></param>
        private void updateNodesDisplay(CSteamID steamid)
        {
            foreach (Guid guid in _resources.nodeeffects)
                EffectManager.ClearEffectByGuid(guid, Provider.findTransportConnection(steamid));
            DisplayNodes(steamid);
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

        private bool Link(IElectricNode a, IElectricNode b)
        {
            if (a == null || b == null)
                return false;

            if (!a.Connections.Contains(b))
                a.Connections.Add(b);
            if (!b.Connections.Contains(a))
                b.Connections.Add(a);

            UpdateAllNetworks();
            return true;
        }
        private static List<BarricadeDrop> getBarricadesInRadius(Vector3 center, float radius)
        {
            List<BarricadeDrop> result = new List<BarricadeDrop>();
            BarricadeRegion[,] regions = BarricadeManager.regions;
            foreach (BarricadeRegion reg in regions)
            {
                foreach (BarricadeDrop drop in reg.drops)
                {
                    float dist = Vector3.Distance(center, drop.model.position);
                    if (dist < radius)
                    {
                        result.Add(drop);
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
                reliable = true,
            };
            effect.SetDirection(Vector3.down);
            effect.SetRelevantPlayer(player.SteamPlayer());
            EffectManager.triggerEffect(effect);
        }
        private void TracePath(UnturnedPlayer player, Vector3 point1, Vector3 point2, EffectAsset pathEffect)
        {
            float distance = Vector3.Distance(point1, point2);

            Vector3 direction = (point2 - point1).normalized;

            TriggerEffectParameters effect = new TriggerEffectParameters
            {
                asset = pathEffect,
                position = point1,
                relevantDistance = 64f,
                shouldReplicate = true,
                reliable = true,
                scale = new Vector3(1f, 1f, distance)
            };
            effect.SetDirection(direction);
            effect.SetRelevantPlayer(player.SteamPlayer());
            EffectManager.triggerEffect(effect);
        }

        private void DisplayNodes(CSteamID steamid)
        {
            UnturnedPlayer player = UnturnedPlayer.FromCSteamID(steamid);
            if (player == null) return;

            Console.WriteLine($"Displayed nodes to {player.DisplayName}");

            // Keep track of which connections we’ve already drawn
            HashSet<(IElectricNode, IElectricNode)> drawnConnections = new HashSet<(IElectricNode, IElectricNode)>();

            foreach (BarricadeDrop drop in getBarricadesInRadius(player.Position, 100f))
            {
                Transform t = drop.model;
                if (!t.TryGetComponent<IElectricNode>(out IElectricNode node))
                    continue;
                if(!doesOwnDrop(drop, steamid))
                    continue;

                // Choose node type effect
                if (node is ConsumerNode)
                    sendEffectCool(player, t.position, _resources.node_consumer);
                else if (node is SupplierNode)
                    sendEffectCool(player, t.position, _resources.node_power);
                else if (node is SwitchNode)
                    sendEffectCool(player, t.position, _resources.node_switch);
                else
                    continue;

                // Draw connections (paths)
                foreach (IElectricNode connected in node.Connections)
                {
                    if (connected == null)
                        continue;

                    // Skip reversed duplicates (A↔B drawn only once)
                    var pair = (node, connected);
                    var reversed = (connected, node);
                    if (drawnConnections.Contains(pair) || drawnConnections.Contains(reversed))
                        continue;

                    drawnConnections.Add(pair);

                    Vector3 start = ((MonoBehaviour)node).transform.position;
                    Vector3 end = ((MonoBehaviour)connected).transform.position;

                    EffectAsset pathEffect = _resources.path_power;

                    if (node is SupplierNode || connected is SupplierNode)
                        pathEffect = _resources.path_power;
                    else if (node is SwitchNode || connected is SwitchNode)
                        pathEffect = _resources.path_switch;
                    else
                        pathEffect = _resources.path_consumer;

                    TracePath(player, start, end, pathEffect);
                }
            }
        }

        public void UpdateAllNetworks()
        {
            var stopwatch = Stopwatch.StartNew();
            var visited = new HashSet<IElectricNode>();

            foreach (var node in nodes.Values)
            {
                if (visited.Contains(node))
                    continue;

                var connected = GetConnectedNetwork(node, visited);

                // Gather suppliers and consumers
                var suppliers = connected.OfType<SupplierNode>().ToList();
                var consumers = connected.OfType<ConsumerNode>().ToList();

                uint totalSupply = (uint)suppliers.Sum(s => s.MaxSupply);
                uint totalConsumption = (uint)consumers.Sum(c => c.consumption);

                // If any switch in the path is off, that branch won’t appear in connected list
                if (totalConsumption > totalSupply)
                {
                    // Too much load — no one gets power
                    foreach (var c in consumers)
                        c.DecreaseVoltage(c._voltage);
                    continue;
                }

                // Distribute total supply evenly or proportionally
                uint perConsumer = consumers.Count > 0 ? totalSupply / (uint)consumers.Count : 0;

                foreach (var c in consumers)
                    c.IncreaseVoltage(perConsumer);
            }
            stopwatch.Stop();
            Console.WriteLine($"[PowerShenanigans] Updated networks in {stopwatch.ElapsedMilliseconds} ms");
        }

        private List<IElectricNode> GetConnectedNetwork(IElectricNode root, HashSet<IElectricNode> visited)
        {
            List<IElectricNode> connected = new List<IElectricNode>();
            Queue<IElectricNode> queue = new Queue<IElectricNode>();

            queue.Enqueue(root);
            visited.Add(root);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                connected.Add(node);

                foreach (var neighbor in node.Connections)
                {
                    if (neighbor is SwitchNode sw && !sw.IsOn)
                        continue; // block current flow

                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return connected;
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
                if (__instance.name == "1272")
                {
                    __instance.gameObject.GetComponent<SwitchNode>()?.Toggle(desiredLit);
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
            if (drop.asset.id == 1272) return true;
            if (HasFlag(drop.asset, "Switch"))
                return true;
            return false;
        }
        private bool HasFlag(Asset asset, string flag)
        {
            string path = asset.getFilePath();
            if (!File.Exists(path))
                return false;

            using (var reader = File.OpenText(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.StartsWith(flag, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Wired.Nodes;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;
using Wired.Consumers;

namespace Wired
{
    public class Plugin : RocketPlugin<Config>
    {
        public static Plugin Instance;
        private Resources _resources;

        public bool DevMode = true;

        private readonly Dictionary<uint, IElectricNode> _nodes = new Dictionary<uint, IElectricNode>();

        private List<Guid> _WiringTools = new List<Guid>();
        private Dictionary<CSteamID, Transform> _SelectedNode = new Dictionary<CSteamID, Transform>();


        private Dictionary<Transform, bool> _farmTransformsAffectedBySprinklers = new Dictionary<Transform, bool>();
        private List<Sprinkler> _sprinklers = new List<Sprinkler>();
        protected override void Load()
        {
            Instance = this;
            _resources = new Resources();

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
            UseableGun.OnAimingChanged_Global += UseableGun_OnAimingChanged_Global;

            LightingManager.onTimeOfDayChanged += onTimeOfDayChanged;

            Harmony harmony = new Harmony("com.mew.powerShenanigans");
            harmony.PatchAll();
            foreach (MethodBase method in harmony.GetPatchedMethods())
            {
                Console.WriteLine("Patched method: " + method.DeclaringType.FullName + "." + method.Name);
            }
        }
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
            UseableGun.OnAimingChanged_Global -= UseableGun_OnAimingChanged_Global;

            LightingManager.onTimeOfDayChanged -= onTimeOfDayChanged;

            Harmony harmony = new Harmony("com.mew.powerShenanigans");
            harmony.UnpatchAll("com.mew.powerShenanigans");
            Instance = null;
        }

        private void onTimeOfDayChanged()
        {
            var sw = Stopwatch.StartNew();

            foreach (var f in _farmTransformsAffectedBySprinklers)
            {
                if (f.Value == false)
                    continue;

                BarricadeManager.updateFarm(f.Key, f.Key.GetComponent<InteractableFarm>().planted + 1, true);
            }

            sw.Stop();
            DebugLogger.Log($"Updated time of day, took {sw.ElapsedMilliseconds} ms");
        }
        private void onLevelLoaded(int level)
        {
            _resources.Init();

            foreach (BarricadeRegion reg in BarricadeManager.regions) // Initialize electric components
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
                AssetParser parser = new AssetParser(asset.getFilePath());
                if (parser.HasEntry("WiringTool"))
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
        private void UseableGun_OnAimingChanged_Global(UseableGun obj)
        {
            if (!_WiringTools.Contains(obj.equippedGunAsset.GUID))
                return;

            if (obj.isAiming)
            {
                BarricadeDrop barricadeDrop = Raycast.GetBarricade(obj.player, out _);
                if (barricadeDrop != null && isElectricalComponent(barricadeDrop.model))
                {
                    IElectricNode node = barricadeDrop.model.GetComponent<IElectricNode>();
                    obj.player.ServerShowHint($"Voltage: {node._voltage}", 2);
                }
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
            if (dropdata.owner != 0 && dropdata.owner == (ulong)steamid)
                return true;
            if (dropdata.group != 0 && dropdata.group == (ulong)UnturnedPlayer.FromCSteamID(steamid).SteamGroupID)
                return true;
            if (dropdata.group != 0 && dropdata.group == (ulong)UnturnedPlayer.FromCSteamID(steamid).Player.quests.groupID)
                return true;
            return false;
        }

        private void onBulletSpawned(UseableGun gun, BulletInfo bullet)
        {
            var asset = gun.equippedGunAsset;

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

            if (!doesOwnDrop(drop, steamid))
            {
                ClearSelection(player);
                player.Player.ServerShowHint("You do not own this barricade!", 3f);
                return;
            }

            var model = drop.model;
            if (!isElectricalComponent(model))
                return;

            if (!_SelectedNode.ContainsKey(steamid))
            {
                _SelectedNode[steamid] = model;
                player.Player.ServerShowHint(
                    $"Selected {drop.asset.name} ({drop.instanceID})\nShoot another component to link.\nShoot ground to clear selection.",
                    10f);
                return;
            }

            var node1 = _SelectedNode[steamid];
            var node2 = model;

            if (node1 == node2)
            {
                _SelectedNode.Remove(steamid);
                player.Player.ServerShowHint("Cleared selection.", 3f);
                return;
            }

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

            if (electricNode1.Connections.Contains(electricNode2) || electricNode2.Connections.Contains(electricNode1))
            {
                player.Player.ServerShowHint("Unlinked nodes!", 3f);

                electricNode1.Connections.Remove(electricNode2);
                electricNode2.Connections.Remove(electricNode1);

                UpdateNodesDisplay(steamid);

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
            UpdateNodesDisplay(steamid);
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
            
            
            drop.model.TryGetComponent<CoolConsumer>(out var cc);
            if (cc != null)
            {
                if(cc.transform.GetComponent<Sprinkler>() != null)
                    _sprinklers.Remove((Sprinkler)cc);

                cc.unInit();
            }

            uint id = drop.instanceID;
            if (!_nodes.TryGetValue(id, out var node))
            {
                Console.WriteLine($"No id in _nodes: {drop.instanceID}");
                return;
            }

            foreach (var connected in node.Connections.ToList())
            {
                connected.Connections.Remove(node);
            }

            node.Connections.Clear();
            _nodes.Remove(id);
            UpdateNodesDisplay(instigatorClient.playerID.steamID);
            UpdateAllNetworks();
            UpdateFarmsAffected();

            Console.WriteLine($"Removed node {id} and unlinked from network.");
        }
        private void onEquipRequested(PlayerEquipment equipment, ItemJar jar, ItemAsset asset, ref bool shouldAllow)
        {
            if (_WiringTools.Contains(asset.GUID))
            {
                DisplayNodes(equipment.player.channel.owner.playerID.steamID);
            }
        }

        private void onDequipRequested(Player player, PlayerEquipment equipment, ref bool shouldAllow)
        {
            if (_WiringTools.Contains(equipment.asset.GUID))
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
                    _nodes[node.instanceID] = node;
                    if (drop.asset.id == 458) // Portable generator
                        node.MaxSupply = 500;
                    if (drop.asset.id == 1230) // Industrial generator
                        node.MaxSupply = 2500;
                }
                else if (drop.model.GetComponent<InteractableSign>() != null)
                {
                    if (drop.model.GetComponent<TimerNode>() == null)
                        drop.model.gameObject.AddComponent<TimerNode>();
                    var node = drop.model.GetComponent<TimerNode>();
                    _nodes[node.instanceID] = node;
                }
                else if (isSwitch(drop))
                {
                    if (drop.model.GetComponent<SwitchNode>() == null)
                        drop.model.gameObject.AddComponent<SwitchNode>();
                    var node = drop.model.GetComponent<SwitchNode>();
                    _nodes[node.instanceID] = node;
                }
                else if (isConsumer(drop.model))
                {
                    if (drop.model.GetComponent<ConsumerNode>() == null)
                        drop.model.gameObject.AddComponent<ConsumerNode>();
                    var node = drop.model.GetComponent<ConsumerNode>();

                    _nodes[node.instanceID] = node;
                    node.SetPowered(false);

                    if (drop.asset.id == 459) // Spotlight
                        node.consumption = 250;
                    if (drop.asset.id == 1222) // Cagelight
                        node.consumption = 25;

                    if (drop.model.GetComponent<Sprinkler>() != null)
                    {
                        _sprinklers.Add(drop.model.GetComponent<Sprinkler>());
                        UpdateFarmsAffected();
                    }
                }
            }

            if (drop.model.GetComponent<InteractableFarm>() != null)
                _farmTransformsAffectedBySprinklers.Add(drop.model, false);
        }
        private void ClearSelection(UnturnedPlayer player)
        {
            var steamid = player.CSteamID;
            _SelectedNode.Remove(steamid);
        }
        private void UpdateNodesDisplay(CSteamID steamid)
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

            HashSet<(IElectricNode, IElectricNode)> drawnConnections = new HashSet<(IElectricNode, IElectricNode)>();

            var bfinder = new BarricadeFinder(player.Position, 100f);
            foreach (BarricadeDrop drop in bfinder.GetBarricadesInRadius())
            {
                Transform t = drop.model;
                if (!t.TryGetComponent<IElectricNode>(out IElectricNode node))
                    continue;
                if (!doesOwnDrop(drop, steamid))
                    continue;

                if (node is ConsumerNode)
                    sendEffectCool(player, t.position, _resources.node_consumer);
                else if (node is SupplierNode)
                    sendEffectCool(player, t.position, _resources.node_power);
                else if (node is SwitchNode)
                    sendEffectCool(player, t.position, _resources.node_switch);
                else if (node is TimerNode)
                    sendEffectCool(player, t.position, _resources.node_timer);
                else
                    continue;

                foreach (IElectricNode connected in node.Connections)
                {
                    if (connected == null)
                        continue;

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
                    else if (node is TimerNode || connected is TimerNode)
                        pathEffect = _resources.path_timer;
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

            foreach (var node in _nodes.Values)
            {
                if (visited.Contains(node))
                    continue;

                var connected = GetConnectedNetwork(node, visited);

                var suppliers = connected.OfType<SupplierNode>().ToList();
                var consumers = connected.OfType<ConsumerNode>().ToList();
                var timers = connected.OfType<TimerNode>().ToList();

                uint totalSupply = (uint)suppliers.Sum(s => s.MaxSupply);
                uint totalConsumption = (uint)consumers.Sum(c => c.consumption);

                if (totalConsumption > totalSupply)
                {
                    foreach (var c in consumers)
                        c.DecreaseVoltage(c._voltage);
                    continue;
                }

                var usedtimers = new List<uint>();

                foreach (var t in timers)
                {
                    if (usedtimers.Contains(t.instanceID))
                        continue;
                    usedtimers.Add(t.instanceID);
                    if (totalSupply > 0 && !t._activated)
                        t.StartTimer();
                    else if (totalSupply == 0)
                    {
                        t.DecreaseVoltage(t._voltage);
                        t.StopIfRunning();
                    }
                    DebugLogger.Log($"[PowerShenanigans] TimerNode {t.instanceID}, activated={t._activated}, allowCurrent={t.allowCurrent}, isCountingDown={t.isCountingDown}");
                }

                foreach (var c in consumers)
                    c.IncreaseVoltage(c.consumption);
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
                    if (neighbor is TimerNode tn && !tn.allowCurrent)
                    {
                        connected.Add(neighbor);
                        visited.Add(neighbor);
                        continue; // block current flow
                    }
                    else if (neighbor is TimerNode)
                    {
                        connected.Add(neighbor);
                    }

                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return connected;
        }
        private void UpdateFarmsAffected()
        {
            // TODO: gotta invoke this method if the barricade is moved by F6 transform tools

            foreach (Transform t in _farmTransformsAffectedBySprinklers.Keys)
            {
                foreach (var spr in _sprinklers)
                {
                    if (!spr.isActive)
                        continue;

                    if (Vector3.Distance(t.position, spr.transform.position) <= spr.effectiveRadius)
                        _farmTransformsAffectedBySprinklers[t] = true;

                    break;
                }
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
            if (barricade.GetComponent<CoolConsumer>() != null)
                return true;

            return false;
        }
        private bool isElectricalComponent(Transform barricade)
        {
            if (barricade == null) return false;

            if (barricade.GetComponent<InteractableFire>() != null) return true;

            if (barricade.GetComponent<InteractableGenerator>() != null) return true;

            if (barricade.GetComponent<InteractableSign>() != null) return true;

            if (barricade.GetComponent<CoolConsumer>() != null) return true;

            if (isConsumer(barricade)) return true;
            return false;
        }
        private bool isSwitch(BarricadeDrop drop)
        {
            if (drop == null)
                return false;

            if (drop.asset.id == 1272)
                return true;

            AssetParser parser = new AssetParser(drop.asset.getFilePath());

            if (parser.HasEntry("Switch"))
                return true;

            return false;
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
                if (__instance.name == "1272")
                {
                    __instance.gameObject.GetComponent<SwitchNode>()?.Toggle(desiredLit);
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(InteractableFarm), "updatePlanted")]
        private static class Patch_InteractableFarm_updatePlanted
        {
            private static void Postfix(InteractableFarm __instance, uint newPlanted)
            {
                Console.WriteLine($"newPlanted: {newPlanted}\n ProviderTime: {Provider.time}");
            }
        }
    }
}

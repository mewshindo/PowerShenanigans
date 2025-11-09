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

        private readonly Dictionary<CSteamID, Transform> _selectedNode = new Dictionary<CSteamID, Transform>();

        private readonly List<CSteamID> _remoteToolBindingMode = new List<CSteamID>();

        private readonly Dictionary<Transform, bool> _farmTransformsAffectedBySprinklers = new Dictionary<Transform, bool>();
        private readonly List<Sprinkler> _sprinklers = new List<Sprinkler>();
        protected override void Load()
        {
            Instance = this;
            _resources = new Resources();

            Level.onLevelLoaded += onLevelLoaded;
            BarricadeManager.onBarricadeSpawned += onBarricadeSpawned;
            UseableGun.onBulletHit += UseableGun_onBulletHit;
            UseableGun.onBulletSpawned += OnBulletSpawned;
            U.Events.OnPlayerConnected += (player) =>
            {
                player.Player.gameObject.AddComponent<CoolEvents>();
            };
            CoolEvents.OnDequipRequested += onDequipRequested;
            CoolEvents.OnEquipRequested += onEquipRequested;
            BarricadeDrop.OnSalvageRequested_Global += onSalvageRequested_Global;
            UseableGun.OnAimingChanged_Global += UseableGun_OnAimingChanged_Global;
            NPCEventManager.onEvent += NPCEventManager_onEvent;
            PlayerEquipment.OnInspectingUseable_Global += PlayerEquipment_OnInspectingUseable_Global;
            BarricadeManager.onModifySignRequested += onModifySign;

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
            UseableGun.onBulletSpawned -= OnBulletSpawned;
            U.Events.OnPlayerConnected -= (player) =>
            {
                player.Player.gameObject.AddComponent<CoolEvents>();
            };
            CoolEvents.OnDequipRequested -= onDequipRequested;
            CoolEvents.OnEquipRequested -= onEquipRequested;
            BarricadeDrop.OnSalvageRequested_Global -= onSalvageRequested_Global;
            UseableGun.OnAimingChanged_Global -= UseableGun_OnAimingChanged_Global;
            NPCEventManager.onEvent -= NPCEventManager_onEvent;
            PlayerEquipment.OnInspectingUseable_Global -= PlayerEquipment_OnInspectingUseable_Global;

            Harmony harmony = new Harmony("com.mew.powerShenanigans");
            harmony.UnpatchAll("com.mew.powerShenanigans");
            Instance = null;
        }

        uint lasttime = 0;
        private void Update()
        {
            if (LightingManager.time == lasttime)
                return;

            lasttime = LightingManager.time;
            onTimeOfDayChanged();
        }

        private void onTimeOfDayChanged()
        {
            var sw = Stopwatch.StartNew();

            foreach (var f in _farmTransformsAffectedBySprinklers)
            {
                if (f.Value == false)
                    continue;

                BarricadeManager.updateFarm(f.Key, f.Key.GetComponent<InteractableFarm>().planted + 60, true);
            }

            sw.Stop();
        }
        private void onLevelLoaded(int level)
        {
            _resources.Init();

            var stopwatch = Stopwatch.StartNew();

            List<ItemAsset> items = new List<ItemAsset>();
            Assets.find(items);

            foreach (ItemAsset asset in items)
            {
                AssetParser parser = new AssetParser(asset.getFilePath());
                if (parser.HasEntry("WiringTool"))
                    _resources.WiredAssets.Add(asset.GUID, WiredAssetType.WiringTool);

                else if (parser.HasEntry("RemoteTool"))
                    _resources.WiredAssets.Add(asset.GUID, WiredAssetType.RemoteTool);

                else if (parser.HasEntry("Switch"))
                    _resources.WiredAssets.Add(asset.GUID, WiredAssetType.Switch);

                else if (parser.HasEntry("Timer"))
                    _resources.WiredAssets.Add(asset.GUID, WiredAssetType.Timer);

                else if (parser.HasEntry("RemoteReceiver"))
                    _resources.WiredAssets.Add(asset.GUID, WiredAssetType.RemoteReceiver);

                else if (parser.HasEntry("RemoteTransmitter"))
                    _resources.WiredAssets.Add(asset.GUID, WiredAssetType.RemoteTransmitter);
            }

            foreach (BarricadeRegion reg in BarricadeManager.regions)
            {
                foreach (BarricadeDrop drop in reg.drops)
                {
                    onBarricadeSpawned(reg, drop);
                }
            }

            float milliseconds = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"[Wired] Found: \n{_resources.WiredAssets.Where(x => x.Value == WiredAssetType.WiringTool).Count()}" +
                $" wiring tools \n{_resources.WiredAssets.Where(x => x.Value == WiredAssetType.Switch).Count()}" +
                $" switches, \n{_resources.WiredAssets.Where(x => x.Value == WiredAssetType.Timer).Count()} timers \n" +
                $"parsed {items.Count} item asset files, took {milliseconds} ms.");
        }
        private void UseableGun_OnAimingChanged_Global(UseableGun obj)
        {
            if (!_resources.WiredAssets.ContainsKey(obj.equippedGunAsset.GUID) && _resources.WiredAssets[obj.equippedGunAsset.GUID] != WiredAssetType.WiringTool)
                return;

            if (obj.isAiming)
            {
                BarricadeDrop barricadeDrop = Raycast.GetBarricade(obj.player, out _);
                if (barricadeDrop != null && IsElectricalComponent(barricadeDrop.model))
                {
                    IElectricNode node = barricadeDrop.model.GetComponent<IElectricNode>();
                    obj.player.ServerShowHint($"Voltage: {node.Voltage}", 2);
                }
            }
        }

        private void UseableGun_onBulletHit(UseableGun gun, BulletInfo bullet, InputInfo hit, ref bool shouldAllow)
        {
            if (_resources.WiredAssets.ContainsKey(gun.equippedGunAsset.GUID) && _resources.WiredAssets[gun.equippedGunAsset.GUID] == WiredAssetType.WiringTool)
                shouldAllow = false;
        }
        private bool DoesOwnDrop(BarricadeDrop drop, CSteamID steamid)
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

        private void OnBulletSpawned(UseableGun gun, BulletInfo bullet)
        {
            if (!_resources.WiredAssets.ContainsKey(gun.equippedGunAsset.GUID) && _resources.WiredAssets[gun.equippedGunAsset.GUID] != WiredAssetType.WiringTool)
                return;

            var steamid = gun.player.channel.owner.playerID.steamID;
            var player = UnturnedPlayer.FromCSteamID(steamid);

            BarricadeDrop drop = Raycast.GetBarricade(gun.player, out _);
            if (drop == null)
            {
                ClearSelection(player);
                return;
            }

            if (!DoesOwnDrop(drop, steamid))
            {
                ClearSelection(player);
                player.Player.ServerShowHint("You do not own this barricade!", 3f);
                return;
            }

            var model = drop.model;

            if (!_selectedNode.ContainsKey(steamid))
            {
                _selectedNode[steamid] = model;
                player.Player.ServerShowHint(
                    $"Selected {drop.asset.name} ({drop.instanceID})\nShoot another component to link.\nShoot ground to clear selection.",
                    10f);
                return;
            }

            var node1 = _selectedNode[steamid];
            var node2 = model;

            if (node1 == node2)
            {
                _selectedNode.Remove(steamid);
                player.Player.ServerShowHint("Cleared selection.", 3f);
                return;
            }


            var electricNode1 = node1.GetComponent<IElectricNode>();
            var electricNode2 = node2.GetComponent<IElectricNode>();

            if (electricNode1 == null || electricNode2 == null)
            {
                return;
            }

            if (electricNode1.Connections.Contains(electricNode2) || electricNode2.Connections.Contains(electricNode1))
            {
                player.Player.ServerShowHint("Unlinked nodes!", 3f);

                electricNode1.Connections.Remove(electricNode2);
                electricNode2.Connections.Remove(electricNode1);

                UpdateNodesDisplay(steamid);

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
            _selectedNode.Remove(steamid);
        }
        private void onEquipRequested(PlayerEquipment equipment, ItemJar jar, ItemAsset asset, ref bool shouldAllow)
        {
            if (_resources.WiredAssets.TryGetValue(asset.GUID, out WiredAssetType type))
            {
                if (type == WiredAssetType.WiringTool)
                    DisplayNodes(equipment.player.channel.owner.playerID.steamID);
            }
        }

        private void onDequipRequested(Player player, PlayerEquipment equipment, ref bool shouldAllow)
        {
            if (_resources.WiredAssets.TryGetValue(equipment.asset.GUID, out WiredAssetType type))
            {
                if (type == WiredAssetType.WiringTool)
                    foreach (Guid guid in _resources.nodeeffects)
                        EffectManager.ClearEffectByGuid(guid, Provider.findTransportConnection(UnturnedPlayer.FromPlayer(player).CSteamID));
            }
        }

        private void NPCEventManager_onEvent(Player instigatingPlayer, string eventId)
        {
            DebugLogger.Log($"NPCEvent broadcasted: {eventId}");
            if (instigatingPlayer != null)
            {
                var equippeditem = instigatingPlayer.equipment;

                if (equippeditem == null)
                    return;

                if (!_resources.WiredAssets.ContainsKey(equippeditem.asset.GUID) || _resources.WiredAssets[equippeditem.asset.GUID] != WiredAssetType.RemoteTool)
                    return;
                if (eventId == "Wired:RemoteLeftClick")
                {
                    Console.WriteLine("Wired:RemoteLeftClick");
                }
                else if (eventId == "Wired:RemoteRightClick")
                {
                    Console.WriteLine("Wired:RemoteRightClick");
                }
            }
        }
        private void PlayerEquipment_OnInspectingUseable_Global(PlayerEquipment obj)
        {
            if (obj == null)
                return;
            if (obj.player == null)
                return;

            if (!_resources.WiredAssets.TryGetValue(obj.asset.GUID, out WiredAssetType value))
                return;
            if (value != WiredAssetType.RemoteTool)
                return;

            if (_remoteToolBindingMode.Contains(obj.player.channel.owner.playerID.steamID))
            {
                _remoteToolBindingMode.Remove(obj.player.channel.owner.playerID.steamID);
                obj.player.ServerShowHint($"Didn't bind to anything!", 3f);
                return;
            }
            BarricadeDrop drop = Raycast.GetBarricade(obj.player, out _);
            if (drop == null)
            {
                obj.player.ServerShowHint($"Aim at a receiver to bind!", 3f);
                return;
            }
            if (!DoesOwnDrop(drop, obj.player.channel.owner.playerID.steamID))
                return;
            IElectricNode node = drop.model.GetComponent<IElectricNode>();
            if (node is ReceiverNode rr)
            {
                obj.player.ServerShowHint($"Click a mouse button to bind this receiver to that button!", 3f);
            }
            else
            {
                obj.player.ServerShowHint($"Aim at a receiver to bind!", 3f);
                return;
            }
        }

        private void onBarricadeSpawned(BarricadeRegion region, BarricadeDrop drop)
        {
            if (drop.model.GetComponent<InteractableGenerator>() != null)
            {
                if (drop.model.GetComponent<SupplierNode>() == null)
                    drop.model.gameObject.AddComponent<SupplierNode>();
                var node = drop.model.GetComponent<SupplierNode>();
                _nodes[node.instanceID] = node;
                if (drop.asset.id == 458) // Portable generator
                    node.Supply = 500;
                if (drop.asset.id == 1230) // Industrial generator
                    node.Supply = 2500;
            }
            if (_resources.WiredAssets.TryGetValue(drop.asset.GUID, out WiredAssetType type))
            {
                if (type == WiredAssetType.Timer)
                {
                    if (drop.model.GetComponent<TimerNode>() == null)
                        drop.model.gameObject.AddComponent<TimerNode>();
                    var node = drop.model.GetComponent<TimerNode>();
                    _nodes[node.instanceID] = node;
                    AssetParser parser = new AssetParser(drop.asset.getFilePath());
                    if (parser.TryGetFloat("Timer_Delay_Seconds", out var val))
                    {
                        node.DelaySeconds = val;
                    }
                    Console.WriteLine($"Created a timer with delay {val}");
                    return;
                }
                if (type == WiredAssetType.Switch)
                {
                    if (drop.model.GetComponent<SwitchNode>() == null)
                        drop.model.gameObject.AddComponent<SwitchNode>();
                    var node = drop.model.GetComponent<SwitchNode>();
                    _nodes[node.instanceID] = node;
                    return;
                }
                if (type == WiredAssetType.RemoteReceiver)
                {
                    if (drop.model.GetComponent<ReceiverNode>() == null)
                        drop.model.gameObject.AddComponent<ReceiverNode>();
                    var node = drop.model.GetComponent<ReceiverNode>();
                    _nodes[node.instanceID] = node;
                    return;
                }
                if (type == WiredAssetType.RemoteTransmitter)
                {
                    if (drop.model.GetComponent<Transmitter>() == null)
                        drop.model.gameObject.AddComponent<Transmitter>();
                }
            }
            if (IsConsumer(drop.model))
            {
                if (drop.model.GetComponent<ConsumerNode>() == null)
                    drop.model.gameObject.AddComponent<ConsumerNode>();
                var node = drop.model.GetComponent<ConsumerNode>();

                _nodes[node.instanceID] = node;
                node.SetPowered(false);

                if (drop.asset.id == 459) // Spotlight
                    node.Consumption = 250;
                if (drop.asset.id == 1222) // Cagelight
                    node.Consumption = 25;

                if (drop.model.GetComponent<CoolConsumer>() != null)
                {
                    var cc = drop.model.GetComponent<CoolConsumer>();
                    if (cc is Transmitter t)
                    {
                        node.Consumption = 100;
                    }
                    else if (cc is Sprinkler s)
                    {
                        node.Consumption = 200;
                        _sprinklers.Add(s);
                    }
                }
            }

            if (drop.model.GetComponent<InteractableSpot>() != null)
            {
                if (!drop.model.GetComponent<InteractableSpot>().isWired || !drop.model.GetComponent<InteractableSpot>().isPowered)
                {
                    Barricade bar = new Barricade(_resources.generator_technical);
                    Transform gen = BarricadeManager.dropNonPlantedBarricade(bar, drop.model.position, drop.model.rotation, 0, 0);
                    if (gen != null)
                    {
                        BarricadeManager.sendFuel(gen, 512);
                        BarricadeManager.ServerSetGeneratorPowered(gen.GetComponent<InteractableGenerator>(), true);
                    }
                }
            }

            if (drop.model.GetComponent<InteractableFarm>() != null)
                _farmTransformsAffectedBySprinklers.Add(drop.model, false);
        }

        private void onModifySign(CSteamID instigator, InteractableSign sign, ref string text, ref bool shouldAllow)
        {
            BarricadeDrop drop = BarricadeManager.FindBarricadeByRootTransform(sign.transform);
            if (drop == null)
                return;
            var node = drop.model.GetComponent<Transmitter>();
            if (node != null)
            {
                if (!node.TrySetFrequency(text, UnturnedPlayer.FromCSteamID(instigator).Player))
                {
                    shouldAllow = false;
                    return;
                }
                shouldAllow = true;
            }
            var node2 = drop.model.GetComponent<ReceiverNode>();
            if (node2 != null)
            {
                if (!node2.TrySetFrequency(text, UnturnedPlayer.FromCSteamID(instigator).Player))
                {
                    shouldAllow = false;
                    return;
                }
                shouldAllow = true;
            }
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
                if (cc.transform.GetComponent<Sprinkler>() != null)
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
        private void ClearSelection(UnturnedPlayer player)
        {
            var steamid = player.CSteamID;
            _selectedNode.Remove(steamid);
        }
        private void UpdateNodesDisplay(CSteamID steamid)
        {
            foreach (Guid guid in _resources.nodeeffects)
                EffectManager.ClearEffectByGuid(guid, Provider.findTransportConnection(steamid));
            DisplayNodes(steamid);
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
                if (!DoesOwnDrop(drop, steamid))
                    continue;

                if (node is ConsumerNode)
                    sendEffectCool(player, t.position, _resources.node_consumer);
                else if (node is SupplierNode)
                    sendEffectCool(player, t.position, _resources.node_power);
                else if (node is SwitchNode)
                    sendEffectCool(player, t.position, _resources.node_switch);
                else if (node is TimerNode)
                    sendEffectCool(player, t.position, _resources.node_timer);
                else if (node is ReceiverNode)
                    sendEffectCool(player, t.position, _resources.node_switch);
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
                    else if(node is ReceiverNode || connected is ReceiverNode)
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

            foreach (var node in _nodes.Values)
            {
                if (visited.Contains(node))
                    continue;

                var connected = GetConnectedNetwork(node, visited);

                var suppliers = connected.OfType<SupplierNode>().ToList();
                var consumers = connected.OfType<ConsumerNode>().ToList();
                var timers = connected.OfType<TimerNode>().ToList();

                uint totalSupply = (uint)suppliers.Sum(s => s.Supply);
                uint totalConsumption = (uint)consumers.Sum(c => c.Consumption);

                if (totalConsumption > totalSupply || suppliers.Count == 0)
                {
                    foreach (var c in consumers)
                        c.DecreaseVoltage(c.Voltage);
                    foreach (var t in timers)
                    {
                        if (t.AllowCurrent == false)
                            continue;
                        t.DecreaseVoltage(t.Voltage);
                    }
                    continue;
                }

                var usedtimers = new List<uint>();

                foreach (var t in timers)
                {
                    if (usedtimers.Contains(t.instanceID))
                        continue;
                    usedtimers.Add(t.instanceID);


                    if (totalSupply > 0 && !t.Activated)
                        t.IncreaseVoltage(1);

                    else if (totalSupply == 0)
                    {
                        if (t.AllowCurrent == false)
                            continue;
                        t.DecreaseVoltage(t.Voltage);
                    }
                }

                foreach (var c in consumers)
                    c.IncreaseVoltage(c.Consumption);
            }
            UpdateFarmsAffected();

            stopwatch.Stop();
            DebugLogger.Log($"[PowerShenanigans] Updated networks in {stopwatch.ElapsedMilliseconds} ms");
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

                if (node is TimerNode t && t.AllowCurrent == false)
                {
                    continue; // block current flow
                }

                foreach (var neighbor in node.Connections)
                {
                    if (neighbor is SwitchNode sw && !sw.IsOn)
                        continue; // block current flow
                    if (neighbor is ReceiverNode rr && !rr.IsOn)
                        continue; // block current flow
                    if (neighbor is TimerNode)
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
                    _farmTransformsAffectedBySprinklers[t] = false;
                    if (!spr.isActive)
                        continue;

                    if (Vector3.Distance(t.position, spr.transform.position) <= spr.effectiveRadius)
                        _farmTransformsAffectedBySprinklers[t] = true;

                    break;
                }
            }
        }
        private bool IsConsumer(Transform barricade)
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
        private bool IsElectricalComponent(Transform barricade)
        {
            if (barricade == null) return false;

            if (barricade.GetComponent<InteractableGenerator>() != null) return true;

            if (IsConsumer(barricade)) return true;
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
                if (__instance.gameObject.GetComponent<ReceiverNode>() != null)
                {
                    if (player.equipment.asset == null || !Instance._resources.WiredAssets.ContainsKey(player.equipment.asset.GUID) || Instance._resources.WiredAssets[player.equipment.asset.GUID] != WiredAssetType.RemoteTool)
                        return false;

                }
                if (__instance.gameObject.GetComponent<SwitchNode>() != null)
                {
                    __instance.gameObject.GetComponent<SwitchNode>()?.Toggle(desiredPowered);
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
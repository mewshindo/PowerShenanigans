using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PowerShenanigans
{
    public class CoolEvents : MonoBehaviour
    {
        public delegate void onDequipRequested(Player player, PlayerEquipment equipment, ref bool shouldAllow);

        public static event onDequipRequested OnDequipRequested;

        public delegate void onEquipRequested(PlayerEquipment equipment, ItemJar jar, ItemAsset asset, ref bool shouldAllow);
        public static event onEquipRequested OnEquipRequested;

        public void Awake()
        {
            Player player = this.gameObject.transform.GetComponent<Player>();
            Console.WriteLine($"added coolevents to {player.name}");
            PlayerEquipment equipment2 = player.equipment;
            equipment2.onDequipRequested = (PlayerDequipRequestHandler)Delegate.Combine(equipment2.onDequipRequested, (PlayerDequipRequestHandler)delegate (PlayerEquipment equipment, ref bool shouldAllow)
            {
                OnDequipRequested?.Invoke(player, equipment, ref shouldAllow);
            });
            equipment2.onEquipRequested = (PlayerEquipRequestHandler)Delegate.Combine(equipment2.onEquipRequested, (PlayerEquipRequestHandler)delegate (PlayerEquipment equipment, ItemJar jar, ItemAsset asset, ref bool shouldAllow)
            {
                OnEquipRequested?.Invoke(equipment, jar, asset, ref shouldAllow);
            });
        }
    }
}

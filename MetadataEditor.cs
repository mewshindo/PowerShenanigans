using SDG.Unturned;
using System;
using System.Linq;

namespace Wired
{
    public class MetadataEditor
    {
        private Item _item;
        private PlayerEquipment _playerEquipment;
        public MetadataEditor(PlayerEquipment equipment)
        {
            _playerEquipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            var item = EquipmentToItemJar(_playerEquipment);
            if (item != null)
            {
                _item = item;
            }
        }
        public bool GetMetadata(out byte[] metadata, byte offset = 0)
        {
            metadata = null;
            if (_item == null)
            {
                Console.WriteLine($"_item null");
                return false;
            }
            var md = _item.metadata.Skip(offset).Take(2).ToArray();
            if (md == null || md.Length == 0)
            {
                Console.WriteLine($"md null: {md == null}");
                return false;
            }
            metadata = md;
            return true;
        }
        private void SetMetadata(byte[] data, byte offset = 0)
        {
            if (_item == null)
            {
                Console.WriteLine("_item null");
                return;
            }

            for (int i = 0; i < data.Length; i++)
            {
                _item.metadata[i + offset] = data[i];
            }
        }
        public void SetMetadata(uint value, byte offset = 0)
        {
            var data = BitConverter.GetBytes(value);
            data.ToList();
            for(int i = 0; i < offset; i++)
            {
                data.Prepend((byte)0);
            }
            data.ToArray();
            _item.metadata = _item.metadata.Concat(data).ToArray();
            SetMetadata(data, offset);
        }

        private Item EquipmentToItemJar(PlayerEquipment equipment)
        {
            var eqitem = equipment;

            if (eqitem == null || eqitem.player.inventory == null)
                return null;

            var page = eqitem.equippedPage;
            var x = eqitem.equipped_x;
            var y = eqitem.equipped_y;

            var index = eqitem.player.inventory.getIndex(page, x, y);
            return equipment.player.inventory.getItem(page, index).item;
        }
    }
}

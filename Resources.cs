using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PowerShenanigans
{
    public class Resources
    {
        public EffectAsset node_consumer;

        public EffectAsset node_power;

        public EffectAsset path_consumer;

        public EffectAsset path_power;

        public Resources()
        {
            node_consumer = (EffectAsset)Assets.find(new Guid("ad1529d6692f473ead2ac79e70e273fb"));
            node_power = (EffectAsset)Assets.find(new Guid("f9f8409f96fe4624a280181523e5966d"));
            path_consumer = (EffectAsset)Assets.find(new Guid("0c3f255bcdb94ae0867de0c7de4d0f3e"));
            path_power = (EffectAsset)Assets.find(new Guid("aa4bf9e416b248f8b6ae4e48b30382a7"));
        }
    }
}

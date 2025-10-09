using Rocket.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PowerShenanigans
{
    public class Config : IRocketPluginConfiguration, IDefaultable
    {
        public int zalupa;

        public void LoadDefaults()
        {
            zalupa = 5;
        }
    }
}

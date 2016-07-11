using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TitanOneMultiplayerApi.Configuration
{
    internal static class AppSettings
    {
        public static bool AllowPassthrough;
        public static bool NormalizeControls;
        public static bool Ds4ControllerMode;
        public static List<bool> AllowRumble;

        static AppSettings()
        {
            AllowRumble = new List<bool>();
            //Turn them all on
            for (var count = 0; count < 4; count++)
            {
                AllowRumble.Add(true);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader.Config;

namespace CorePatcher.Configs
{
    public class ExamplePatchConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ServerSide;

        [Header("Patch")]
        [DefaultValue(true)]
        [ReloadRequired]
        public bool EnableExamplePatch;

        /// <summary>
        /// Mainly for people who do not want to restart the game when patching it done
        /// Will add a button to manually reload later
        /// </summary>
        [DefaultValue(true)]
        [ReloadRequired]
        public bool ReloadUponPatching;
    }
}

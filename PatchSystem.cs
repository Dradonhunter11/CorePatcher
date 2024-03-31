using System;
using System.Diagnostics;
using Terraria.ModLoader;

namespace CorePatcher
{
    internal class PatchSystem : ModSystem
    {
        public override void PostSetupContent()
        {
            PatchLoader.PrePatch();
            PatchLoader.Apply();
            PatchLoader.PostPatch();
        }
    }
}

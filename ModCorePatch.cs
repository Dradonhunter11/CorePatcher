using System;
using Terraria.ModLoader;

namespace CorePatcher
{
    public class ModCorePatch : ModType
    {
        protected sealed override void Register()
        {
            ModTypeLookup<ModCorePatch>.Register(this);
            LoaderManager.Get<PatchLoader>().Register(this);
        }
    }
}

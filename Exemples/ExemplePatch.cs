﻿using CorePatcher.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System.Collections.Generic;
using System.Linq;

namespace CorePatcher.Exemples
{
    [PatchType("Terraria.Main")]
    internal class ExemplePatch : ModCorePatch
    {
        /// <summary>
        /// This patch will edit the title window of the game
        /// </summary>
        /// <param name="type"></param>
        /// <param name="terraria"></param>
        private static void PatchTitle(TypeDefinition type, AssemblyDefinition terraria)
        {
            // First we get the setTitle method from Main.cs
            var method = type.Methods.FirstOrDefault(m => m.Name == "SetTitle");

            // Then we make a cursor out of it
            ILCursor ilCursor = new ILCursor(new ILContext(method));

            // After we wanna move our cursor right before Platform.Get<IWindowService>().SetUnicodeTitle(base.Window, _cachedTitle); 
            ilCursor.GotoNext(MoveType.Before,
                i => i.MatchCall(out _),
                i => i.MatchLdarg0(),
                i => i.MatchCall(out _),
                i => i.MatchLdarg0());

            // And finally we emit the following instruction this._cachedTitle = "Terraria Exemple Core modding!"
            ilCursor.EmitLdarg0();
            ilCursor.Emit(OpCodes.Ldstr, "Terraria Exemple Core modding!");
            ilCursor.EmitStfld(type.Fields.FirstOrDefault(f => f.Name == "_cachedTitle"));
        }
    }
}

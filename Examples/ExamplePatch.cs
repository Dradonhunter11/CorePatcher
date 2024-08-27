using CorePatcher.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System.Collections.Generic;
using System.Linq;
using CorePatcher.Configs;
using CorePatcher.Examples;
using Terraria.ModLoader;

namespace CorePatcher.Examples
{
    [PatchType("Terraria.Main")]
    internal class ExamplePatch : ModCorePatch
    {
        /// <summary>
        /// This patch will edit the title window of the game
        /// </summary>
        /// <param name="type"></param>
        /// <param name="terraria"></param>
        private static void PatchTitle(TypeDefinition type, AssemblyDefinition terraria)
        {
            if (!ModContent.GetInstance<CorePatcherConfig>().EnableExamplePatch)
            {
                return;
            }

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

            // And finally we emit the following instruction this._cachedTitle = "Terraria Example Core modding!"
            ilCursor.EmitLdarg0();
            ilCursor.Emit(OpCodes.Ldstr, "Terraria Example Core modding!");
            ilCursor.EmitStfld(type.Fields.FirstOrDefault(f => f.Name == "_cachedTitle"));
        }

        /// <summary>
        /// This patch inject a new string field into Main.cs called myInjectedField
        /// </summary>
        /// <param name="type"></param>
        /// <param name="terraria"></param>
        private static void InjectField(TypeDefinition type, AssemblyDefinition terraria)
        {
            if (!ModContent.GetInstance<CorePatcherConfig>().EnableExamplePatch)
            {
                return;
            }

            // Get the static constructor
            MethodDefinition staticConstructor = type.Methods.FirstOrDefault(m => m.Name == ".cctor");

            // We then get a TypeReference of string
            TypeReference stringTypeReference = terraria.MainModule.TypeSystem.String;

            // Create a new FieldDefinition that is static and public
            var fieldDefinition = new FieldDefinition("MyInjectedField", Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static, stringTypeReference);
            // Add the field to the class
            type.Fields.Add(fieldDefinition);
            // Then set the value of the string
            Helper.EditStaticFieldString(fieldDefinition, "Hello core modding!");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using CorePatcher.Attributes;
using CorePatcher.Configs;
using log4net;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.UI;
using Terraria.Utilities;
using FieldAttributes = Mono.Cecil.FieldAttributes;

namespace CorePatcher
{
    public class PatchLoader : Loader<ModCorePatch>
    {
        public static int Count => _patchList.Count;

        private static readonly List<ModCorePatch> _patchList = new List<ModCorePatch>();
        private static readonly List<Action> _prePatchList = new List<Action>();
        private static readonly List<Action> _postPatchList = new List<Action>();

        public static void RegisterPrePatchOperation(Action prePatch)
        {
            _prePatchList.Add(prePatch);
        }

        public static void RegisterPostPatchOperation(Action postPatch)
        {
            _postPatchList.Add(postPatch);
        }

        public static void AddDeps(AssemblyDefinition asmInfo)
        {
            PatchDepsEditing.AddDependency(asmInfo);
        }

        public void Register(ModCorePatch patch)
        {
            _patchList.Add(patch);
        }

        internal static void PrePatch()
        {
            foreach (var action in _prePatchList)
            {
                action();
            }
        }

        internal static void PostPatch()
        {
            foreach (var action in _postPatchList)
            {
                action();
            }
        }

        internal static void Apply()
        {
            if (DetectPatchedAssembly())
            {
                return;
            }

            var currentFile = Path.Combine(Environment.CurrentDirectory, "tModLoader.dll");
            var newFile = Path.Combine(Environment.CurrentDirectory, "tmodloader2.dll");
            if (File.Exists(newFile))
            {
                File.Delete(newFile);
            }
            File.Copy(currentFile, newFile);
            currentFile = newFile;

            using var terrariaAssembly = AssemblyDefinition.ReadAssembly(currentFile, new ReaderParameters(ReadingMode.Immediate)
            {
                ReadWrite = true,
                InMemory = true
            });

            foreach (var modCorePatch in _patchList)
            {
                var attribute = modCorePatch.GetType().GetCustomAttribute(typeof(PatchType), true);
                if (attribute != null)
                {
                    var typeName = ((PatchType)attribute).GetTypeName();
                    var methods = modCorePatch.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                    foreach (var methodInfo in methods)
                    {
                        var @params = methodInfo.GetParameters();
                        if (@params.Length == 2)
                        {
                            if (@params[0].ParameterType == typeof(TypeDefinition) &&
                                @params[1].ParameterType == typeof(AssemblyDefinition))
                            {
                                methodInfo.Invoke(modCorePatch,
                                    new object[]
                                    {
                                        terrariaAssembly.MainModule.Types.First(p => p.FullName == typeName),
                                        terrariaAssembly
                                    });
                            }
                        }
                    }
                }
            }

            // Write the patched assembly
            terrariaAssembly.Write(Path.ChangeExtension(Path.Combine(Environment.CurrentDirectory, "tModLoader.dll"), ".patched.dll"));
            if (File.Exists(newFile))
            {
                File.Delete(newFile);
            }

            CopyRuntimeConfig();

            Restart();
        }

        public static bool DetectPatchedAssembly()
        {
            FieldInfo CorePatchedFieldInfo =
                typeof(Main).GetField("CorePatched", BindingFlags.Public | BindingFlags.Static);
            return CorePatchedFieldInfo != null;
        }

        /// <summary>
        /// TODO: Find a way to execute this after the game has exited, maybe a batch script? 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void DeleteOnceDone(object sender, EventArgs e)
        {
            if (DetectPatchedAssembly())
            {
                string runtimeConfigPath = Path.Combine(Environment.CurrentDirectory, "tModLoader.runtimeconfig.json");
                string runtimeConfigDev = Path.Combine(Environment.CurrentDirectory, "tModLoader.runtimeconfig.dev.json");
                if (File.Exists(runtimeConfigPath))
                {

                    File.Delete(Path.Combine(Environment.CurrentDirectory, "tModLoader.patched.runtimeconfig.json"));
                    File.Delete(Path.Combine(Environment.CurrentDirectory, "tModLoader.patched.runtimeconfig.dev.json"));
                }
            }
        }

        private static void CopyRuntimeConfig()
        {
            string runtimeConfigPath = Path.Combine(Environment.CurrentDirectory, "tModLoader.runtimeconfig.json");
            string runtimeConfigDev = Path.Combine(Environment.CurrentDirectory, "tModLoader.runtimeconfig.dev.json");
            if (File.Exists(runtimeConfigPath))
            {

                File.Copy(runtimeConfigPath, Path.Combine(Environment.CurrentDirectory, "tModLoader.patched.runtimeconfig.json"), true);
                File.Copy(runtimeConfigDev, Path.Combine(Environment.CurrentDirectory, "tModLoader.patched.runtimeconfig.dev.json"), true);
                PatchDepsEditing.PatchTargetRuntime();
            }
        }

        private static void Restart()
        {
            if (!ModContent.GetInstance<CorePatcherConfig>().ReloadUponPatching)
            {
                return;
            }
            Process process = new Process();
            process.StartInfo = new ProcessStartInfo("dotnet", "\"tModLoader.patched.dll\"")
            {
                WorkingDirectory = Environment.CurrentDirectory
            };
            process.Start();
            Thread.Sleep(1000);
            if (Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                MethodInfo quitGame = typeof(Main).GetMethod("QuitGame", BindingFlags.NonPublic | BindingFlags.Instance);
                if (quitGame != null)
                {
                    quitGame.Invoke(Main.instance, new object[] { });

                }
                Environment.Exit(0);
            }
            else
            {
                Environment.Exit(0);
            }
        }
    }

    [PatchType("Terraria.ModLoader.UI.Interface")]
    internal class InterfacePatch : ModCorePatch
    {
        private static void ModifyModLoaderMenus(TypeDefinition type, AssemblyDefinition terraria)
        {
            FieldDefinition definition =
            new FieldDefinition("CorePatched", FieldAttributes.Public | FieldAttributes.Static, type.Module.TypeSystem.Boolean);
            var main = terraria.MainModule.Types.First(i => i.FullName == "Terraria.Main").Fields;
            main.Add(definition);
            EditStaticFieldString(definition);

            if (!ModContent.GetInstance<CorePatcherConfig>().DevMode) return;

            FieldReference infoMessage = terraria.MainModule.Types.First(i => i.FullName == "Terraria.ModLoader.UI.Interface").Fields.First(i => i.Name == "infoMessage");
            FieldReference menuMode = terraria.MainModule.Types.First(i => i.FullName == "Terraria.Main").Fields.First(i => i.Name == "menuMode");

            FieldReference corePatcher = terraria.MainModule.Types.First(i => i.FullName == "Terraria.Main").Fields.FirstOrDefault(i => i.Name == "CorePatched");

            MethodReference show = terraria.MainModule.Types.First(i => i.FullName == "Terraria.ModLoader.UI.UIInfoMessage").Methods.First(i => i.Name == "Show");

            var method = type.Methods.First(i => i.Name == "ModLoaderMenus");

            var instructions = method.Body.GetILProcessor().Body.Instructions;

            ILContext context = new ILContext(method);
            ILCursor cursor = new ILCursor(context);
            
            Instruction target = cursor.Instrs[cursor.Index + 3];
            Instruction target2 = cursor.Instrs[2];
            instructions.Insert(3, Instruction.Create(OpCodes.Brtrue, target));
            instructions.Insert(3, Instruction.Create(OpCodes.Ldsfld, corePatcher));

            Instruction instruction = Instruction.Create(OpCodes.Br, (Instruction)target2.Operand);

            cursor.Index += 5;

            cursor.EmitLdcI4(1);
            cursor.EmitStsfld(corePatcher);

            cursor.Emit(OpCodes.Ldsfld, infoMessage);
            cursor.EmitLdstr(BuildMessage());
            cursor.EmitLdsfld(menuMode); 
            cursor.EmitLdnull();
            cursor.EmitLdstr("");
            cursor.EmitLdnull();
            cursor.EmitLdnull();
            cursor.EmitCallvirt(show);

            instructions.Insert(cursor.Index, instruction);
        }

        private static string BuildMessage()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Welcome to tModLoader - Core patcher dev mode!");
            builder.AppendLine("If you see this, this mean you have the dev mode option enabled in the config for Core patcher.");
            builder.AppendLine("Here are a couple tips to help you in your journey through core modding!");
            builder.AppendLine();
            builder.AppendLine("=== View your patches ===");
            builder.AppendLine("1. Open ILSpy, DNSpy or your favorite program to view C# assembly IL/Code");
            builder.AppendLine("2. Go in your tML installation folder");
            builder.AppendLine("3. Drag and drop the tModLoader.patched.dll into ILSpy");
            builder.AppendLine("4. Go to the method/class where you have done your patches.");
            builder.AppendLine();
            builder.AppendLine("=== Debugging your mod with tModLoader - core patcher (Require VS) ===");
            builder.AppendLine("0. Stay on this screen");
            builder.AppendLine("1. In VS with your mod project opened go in the Debugging tabs and click on \"Attach to process\" (or press CTRL+ALT+P)");
            builder.AppendLine("2. In the process list, find a dotnet.exe process with tmodloader as the title.");
            builder.AppendLine("3. Click on attach and it's done!");
            builder.AppendLine();
            builder.AppendLine("Thanks for using core patcher!");
            return builder.ToString();
        }

        private static void DelegateToInject()
        {
            Interface.infoMessage.Show("This is a test message", Main.menuMode);
        }

        private static void EditStaticFieldString(FieldDefinition definition)
        {
            MethodDefinition staticConstructor = definition.DeclaringType.Methods.FirstOrDefault(m => m.Name == ".cctor");

            if (staticConstructor != null)
            {
                ILProcessor processor = staticConstructor.Body.GetILProcessor();

                IList<Instruction> instructions = new List<Instruction>();
                instructions.Add(processor.Create(OpCodes.Ldc_I4, 0));
                instructions.Add(processor.Create(OpCodes.Stsfld, definition));
                foreach (Instruction instruction in instructions)
                {
                    processor.Body.Instructions.Insert(processor.Body.Instructions.Count - 2, instruction);
                }
            }
        }
        /*
    }

    [PatchType("Terraria.Main")]
    internal class MainMenuPatch : ModCorePatch
    {
        /// <summary>
        /// Patch the string version at the bottom left of the screen
        /// Mainly just to be able to tell if the patching was a success
        /// </summary>
        /// <param name="type"></param>
        /// <param name="terraria"></param>
        private static void ModifyVersionStringDefault(TypeDefinition type, AssemblyDefinition terraria)
        {
            MethodDefinition staticConstructor = type.Methods.FirstOrDefault(m => m.Name == ".cctor");


            if (staticConstructor == null)
            {
                staticConstructor = new MethodDefinition(".cctor", Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.SpecialName | Mono.Cecil.MethodAttributes.RTSpecialName, terraria.MainModule.TypeSystem.Void);
                type.Methods.Add(staticConstructor);
            }


            EditStaticFieldString(type.Fields.FirstOrDefault(p => p.Name == "versionNumber"), "v1.4.4.9 - Core patcher");
            EditStaticFieldString(type.Fields.FirstOrDefault(p => p.Name == "versionNumber2"), "v1.4.4.9 - Core patcher");
        }

        private static void EditStaticFieldString(FieldDefinition definition, string value)
        {
            MethodDefinition staticConstructor = definition.DeclaringType.Methods.FirstOrDefault(m => m.Name == ".cctor");

            if (staticConstructor != null)
            {
                ILProcessor processor = staticConstructor.Body.GetILProcessor();

                IList<Instruction> instructions = new List<Instruction>();
                instructions.Add(processor.Create(OpCodes.Ldstr, value));
                instructions.Add(processor.Create(OpCodes.Stsfld, definition));
                foreach (Instruction instruction in instructions)
                {
                    processor.Body.Instructions.Insert(processor.Body.Instructions.Count - 2, instruction);
                }
            }
        }
        /*
        private static void AddDetectionField(TypeDefinition type, AssemblyDefinition terraria)
        {
            FieldDefinition definition =
                new FieldDefinition("CorePatched", FieldAttributes.Public | FieldAttributes.Static, type.Module.TypeSystem.Boolean);
            type.Fields.Add(definition);
        }*/
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using CorePatcher.Attributes;
using CorePatcher.Configs;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Terraria;
using Terraria.ModLoader;
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
                    var methods = modCorePatch.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
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

        private static bool DetectPatchedAssembly()
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
            if (!ModContent.GetInstance<ExamplePatchConfig>().ReloadUponPatching)
            {
                return;
            }
            Process process;
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                    process = new Process();
                    process.StartInfo = new ProcessStartInfo("dotnet.exe", "\"tModLoader.patched.dll\"")
                    {
                        WorkingDirectory = Environment.CurrentDirectory,
                        UseShellExecute = true
                    };
                    break;
                default:
                    process = new Process();
                    process.StartInfo = new ProcessStartInfo("dotnet", "\"tModLoader.patched.dll\"")
                    {
                        WorkingDirectory = Environment.CurrentDirectory
                    };
                    break;
            }
            process.Start();
            Thread.Sleep(1000);
            Environment.Exit(0);
        }
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

        // Lazy way to do thing 
        private static void AddDetectionField(TypeDefinition type, AssemblyDefinition terraria)
        {
            FieldDefinition definition =
                new FieldDefinition("CorePatched", FieldAttributes.Public | FieldAttributes.Static, type.Module.TypeSystem.Boolean);
            type.Fields.Add(definition);
        }
    }
}

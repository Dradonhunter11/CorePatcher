using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CorePatcher.Attributes;
using Mono.Cecil;
using Terraria;
using Terraria.ModLoader;

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
            if (typeof(Main).Assembly.GetName().Version == new Version(2, 0, 0, 0))
            {
                return;
            }

            var currentFile = Path.Combine(Environment.CurrentDirectory, "tmodloader.dll");
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

            terrariaAssembly.Name.Version = new Version(2, 0, 0, 0);
            // Write the patched assembly
            terrariaAssembly.Write(Path.ChangeExtension(Path.Combine(Environment.CurrentDirectory, "tmodloader.dll"), ".patched.dll"));
            if (File.Exists(newFile))
            {
                File.Delete(newFile);
            }
        }
    }
}

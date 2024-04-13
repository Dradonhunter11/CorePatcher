using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Mono.Cecil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CorePatcher
{
    internal static class PatchDepsEditing
    {
        private static string depsPath = Path.Combine(Environment.CurrentDirectory, "tModLoader.deps.json");
        private static string depsPatchedPath = Path.Combine(Environment.CurrentDirectory, "tModLoader.patched.deps.json");
        private static string content;

        private static List<AssemblyDefinition> depsToInject = new List<AssemblyDefinition>();

        internal static void AddDependency(AssemblyDefinition info)
        {
            if (!depsToInject.Contains(info))
            {
                depsToInject.Add(info);
            }
            else
            {
                throw new Exception("Assembly already in queue to be injected!");
            }
        }

        static PatchDepsEditing()
        {
            if (!File.Exists(depsPath))
            {
                throw new FileNotFoundException("Failed to find tModLoader.deps.json! Verify your game file integrity to (re)acquire this file.");
            }
            content = File.ReadAllText(depsPath);
        }

        internal static bool PatchTargetRuntime()
        {
            if (!File.Exists(depsPath))
            {
                throw new FileNotFoundException("Failed to find tModLoader.deps.json! Verify your game file integrity to (re)acquire this file.");
            }
            JObject jsonObject = JObject.Parse(content);
            JArray array = new JArray();

            try
            {
                // jsonObject["targets"][".NETCoreApp,Version=v6.0"]["tModLoader/1.4.4.9"]["runtime"].AddAfterSelf(new JProperty("tModLoader.patched.dll", ""));
                var target = jsonObject["targets"];
                var netcoreapp = target[".NETCoreApp,Version=v6.0"];
                var tmodloader1449 = netcoreapp["tModLoader/1.4.4.9"];
                var runtime = (JObject)tmodloader1449["runtime"];
                var libraries = jsonObject["libraries"];

                runtime.Property("tModLoader.dll").Remove();
                runtime["tModLoader.patched.dll"] = new JValue("");


                foreach (var assemblyDefinition in depsToInject)
                {
                    InjectIntoDependencies(tmodloader1449, assemblyDefinition);
                    InjectIntoNetCoreApp(netcoreapp, assemblyDefinition);
                    InjectIntoLibraries(libraries, assemblyDefinition);
                }

                string modifiedJsonObject = jsonObject.ToString();
                File.WriteAllText(depsPatchedPath, modifiedJsonObject);
            }
            catch (Exception e)
            {
                ILog log = LogManager.GetLogger("DepsPatching");
                log.Error(e);
                return false;
            }

            return true;
        }

        private static void InjectIntoDependencies(JToken token, AssemblyDefinition asmInfo)
        {
            Version version = asmInfo.Name.Version;
            string name = asmInfo.Name.Name;

            JObject depsArray = (JObject)token["dependencies"];
            depsArray[name] = new JValue(version.ToString());
        }

        private static void InjectIntoNetCoreApp(JToken token, AssemblyDefinition asmInfo)
        {
            Version version = asmInfo.Name.Version;
            string name = asmInfo.Name.Name;
            string nameVersion = name + "/" + version;

            token[nameVersion] = new JObject();
            token[nameVersion]["runtime"] = new JObject();
            token[nameVersion]["runtime"][name + ".dll"] = new JObject();
        }

        private static void InjectIntoLibraries(JToken token, AssemblyDefinition asmInfo)
        {
            Version version = asmInfo.Name.Version;
            string name = asmInfo.Name.Name;
            string nameVersion = name + "/" + version;

            var tokenInfo = token[nameVersion] = new JObject();

            tokenInfo["type"] = new JValue("project");
            tokenInfo["serviceable"] = new JValue(false);
            tokenInfo["sha512"] = new JValue("");
        }
    }
}

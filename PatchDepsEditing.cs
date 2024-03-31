using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CorePatcher
{
    internal static class PatchDepsEditing
    {
        private static string depsPath = Path.Combine(Environment.CurrentDirectory, "tModLoader.deps.json");
        private static string depsPatchedPath = Path.Combine(Environment.CurrentDirectory, "tModLoader.patched.deps.json");
        private static string content;

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
                var netcoreapp =  target[".NETCoreApp,Version=v6.0"];
                var tmodloader1449 = netcoreapp["tModLoader/1.4.4.9"];
                var runtime = (JObject)tmodloader1449["runtime"];
                runtime.Property("tModLoader.dll").Remove();
                runtime["tModLoader.patched.dll"] = new JValue("");

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
    }
}

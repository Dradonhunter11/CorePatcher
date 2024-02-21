using System;

namespace CorePatcher.Attributes
{
    [System.AttributeUsage(AttributeTargets.Class)]
    public class PatchType : System.Attribute
    {
        private string FullTypeName;

        public PatchType(string fullTypeName)
        {
            FullTypeName = fullTypeName;
        }

        public string GetTypeName() { return FullTypeName; }
    }
}

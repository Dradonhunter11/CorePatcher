using System;

namespace CorePatcher.Attributes
{
    [System.AttributeUsage(AttributeTargets.Class)]
    public class PatchType : System.Attribute
    {
        private string _fullTypeName;

        public PatchType(string fullTypeName)
        {
            _fullTypeName = fullTypeName;
        }

        public string GetTypeName() { return _fullTypeName; }
    }
}

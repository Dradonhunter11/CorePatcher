using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CorePatcher.Utilities
{
    public static class PublicizeUtils
    {
        public static void Publicize(this TypeDefinition definition)
        {
            definition.Attributes |= TypeAttributes.Public;
            definition.Attributes &= ~TypeAttributes.NotPublic;
        }

        public static void Publicize(this MethodDefinition definition)
        {
            definition.Attributes |= MethodAttributes.Public;
            definition.Attributes &= ~MethodAttributes.Private;
        }

        public static void Publicize(this FieldDefinition definition)
        {
            definition.Attributes |= FieldAttributes.Public;
            definition.Attributes &= ~FieldAttributes.Private;
        }
    }
}

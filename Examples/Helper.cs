using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;

namespace CorePatcher.Examples
{
    internal class Helper
    {
        /// <summary>
        /// Helper method to modify static string value from the static constructor
        /// </summary>
        /// <param name="definition"></param>
        /// <param name="value"></param>
        internal static void EditStaticFieldString(FieldDefinition definition, string value)
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
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace CodeDucky.CustomInvokableBuilder
{
    public class CreateCustomInvokable
    {
        public static void Run()
        {
            // http://stackoverflow.com/questions/22427162/is-it-possible-to-overeride-the-way-my-delegate-wrapper-gets-invoked-so-i/22427809#22427809
            // http://www.cprogramdevelop.com/2780805/
            // http://stackoverflow.com/questions/10090350/dynamically-create-type-and-call-constructor-of-base-class
            var aName = new AssemblyName("Dynamic");
            var ab = AppDomain.CurrentDomain.DefineDynamicAssembly(aName, AssemblyBuilderAccess.RunAndSave);
            var mb = ab.DefineDynamicModule(aName.Name, aName.Name + ".dll");
            var delegateBuilder = mb.DefineType("MyType", TypeAttributes.Public | TypeAttributes.Sealed, typeof(MulticastDelegate));

             var constructorBuilder = delegateBuilder.DefineConstructor(
                 MethodAttributes.Public,
                 CallingConventions.Standard, 
                 Type.EmptyTypes
            );
             var ilGenerator = constructorBuilder.GetILGenerator();
             ilGenerator.Emit(OpCodes.Ldarg_0); // load this
             ilGenerator.Emit(OpCodes.Ldnull);
             ilGenerator.Emit(OpCodes.Ldstr, "fake method");
            var baseConstructor = typeof(MulticastDelegate).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(c => c.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(object), typeof(string) }));
             ilGenerator.Emit(OpCodes.Call, baseConstructor);

             delegateBuilder.CreateType();

        }
    }
}

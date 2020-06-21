using System;
using System.Reflection;
using System.Reflection.Emit;
using Lokad.ILPack;
using static System.Reflection.Emit.OpCodes;
using System.Collections.Generic;
using System.IO;

namespace ILGenerationTest
{
    class Program
    {
        static void Main(string[] args)
        {
            string code;
            if (args.Length == 0)
            {
                Console.Write("Please input BRAIN FUCK CODE!\nCODE:");
                code = Console.ReadLine();
            }
            else code = File.ReadAllText(args[0]);
            

            var asm = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Brainfuck.NET Assembly"),
                AssemblyBuilderAccess.Run);
            var module = asm.DefineDynamicModule("Brainfuck.NET Module");
            var type = module.DefineType("BrainfuckNETOutput", TypeAttributes.Class | TypeAttributes.Sealed, null,
                new[] {IBrainfuck.Type});
            var start = type.DefineMethod(nameof(IBrainfuck.Do), MethodAttributes.Public | MethodAttributes.Virtual,
                null, null);
            type.DefineMethodOverride(start,
                IBrainfuck.Type.GetMethod(nameof(IBrainfuck.Do)) ?? throw new ArgumentNullException());
            var il = start.GetILGenerator();
            var tape = il.DeclareLocal(typeof(byte[]));
            var tptr = il.DeclareLocal(typeof(int));
            var labelStack = new Stack<(Label, Label)>();
            var write = typeof(Console).GetMethod(nameof(Console.Write), new[] {typeof(char)});
            var read = typeof(Console).GetMethod(nameof(Console.Read), Type.EmptyTypes);

            il.Emit(Ldc_I4, ushort.MaxValue + 1);
            il.Emit(Newarr, typeof(byte));
            il.Emit(Stloc, tape); //init array
            il.Emit(Ldc_I4_0);
            il.Emit(Stloc, tptr); //init tptr

            foreach (var c in code)
            {
                switch (c)
                {
                    case '<':
                        il.Emit(Ldloc, tptr);
                        il.Emit(Ldc_I4_1);
                        il.Emit(Sub);
                        il.Emit(Ldc_I4, ushort.MaxValue);
                        il.Emit(And);
                        il.Emit(Stloc, tptr);
                        break;
                    case '>':
                        il.Emit(Ldloc, tptr);
                        il.Emit(Ldc_I4_1);
                        il.Emit(Add);
                        il.Emit(Ldc_I4, ushort.MaxValue);
                        il.Emit(And);
                        il.Emit(Stloc, tptr);
                        break;
                    case '+':
                        il.Emit(Ldloc, tape);
                        il.Emit(Ldloc, tptr);
                        il.Emit(Ldelema, typeof(byte));
                        il.Emit(Dup);
                        il.Emit(Ldind_U1);
                        il.Emit(Ldc_I4_1);
                        il.Emit(Add);
                        il.Emit(Conv_U1);
                        il.Emit(Stind_I1);
                        break;
                    case '-':
                        il.Emit(Ldloc, tape);
                        il.Emit(Ldloc, tptr);
                        il.Emit(Ldelema, typeof(byte));
                        il.Emit(Dup);
                        il.Emit(Ldind_U1);
                        il.Emit(Ldc_I4_1);
                        il.Emit(Sub);
                        il.Emit(Conv_U1);
                        il.Emit(Stind_I1);
                        break;
                    case '.':
                        il.Emit(Ldloc, tape);
                        il.Emit(Ldloc, tptr);
                        il.Emit(Ldelem_U1);
                        il.Emit(Call, write);
                        break;
                    case ',':
                        il.Emit(Ldloc, tape);
                        il.Emit(Ldloc, tptr);
                        il.Emit(Call, read);
                        il.Emit(Conv_U1);
                        il.Emit(Stelem_I1);
                        break;
                    case '[':
                        var lForward = il.DefineLabel();
                        var lBackward = il.DefineLabel();
                        il.MarkLabel(lBackward);
                        il.Emit(Ldloc, tape);
                        il.Emit(Ldloc, tptr);
                        il.Emit(Ldelem_U1);
                        il.Emit(Brfalse, lForward);
                        labelStack.Push((lBackward, lForward));
                        break;
                    case ']':
                        var (backward, forward) = labelStack.Pop();
                        il.Emit(Br, backward);
                        il.MarkLabel(forward);
                        break;
                }
            }

            il.Emit(Ret);

            if (labelStack.Count != 0)
                throw new Exception("Mismatched brackets!");

            var t = type.CreateType();
            if (t is null)
                throw new NullReferenceException();

            var filename = $"{t.Name}.dll";
            if (t.GetConstructor(Type.EmptyTypes)?.Invoke(new object[0]) is IBrainfuck instance)
            {
                new AssemblyGenerator().GenerateAssembly(t.Assembly, filename);
                Console.WriteLine("Generated assembly at {0}", filename);
                instance.Do();
            }
            else throw new MissingMemberException("Something has gone terribly wrong.");
        }
    }

    public interface IBrainfuck
    {
        public static readonly Type Type = typeof(IBrainfuck);
        public void Do();
    }
}
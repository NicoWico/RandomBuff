﻿using JetBrains.Annotations;
using Mono.Cecil.Cil;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Permissions;
using MonoMod.Cil;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MonoOpCodes = Mono.Cecil.Cil.OpCodes;
using PropertyAttributes = Mono.Cecil.PropertyAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

using CustomAttributeNamedArgument = Mono.Cecil.CustomAttributeNamedArgument;
using SecurityAction = System.Security.Permissions.SecurityAction;
using SecurityAttribute = Mono.Cecil.SecurityAttribute;
using RandomBuff.Core.Entry;
using RandomBuff.Core.SaveData;

namespace RandomBuff.Core.Buff
{
    internal static class BuffBuilder
    {
        internal static (TypeDefinition buffType, TypeDefinition dataType)
            GenerateBuffTypeWithCache(string pluginId, string usedId, bool needRegisterId = false,
                Action<ILProcessor> buffCtor = null,
                Action<ILProcessor> dataCtor = null)
        {
            return GenerateBuffType($"{pluginId}_dynamicCache", usedId, needRegisterId, buffCtor, dataCtor);
        }

        public static (TypeDefinition buffType, TypeDefinition dataType) 
            GenerateBuffType(string pluginId, string usedId,bool needRegisterId = false,
            Action<ILProcessor> buffCtor = null,
            Action<ILProcessor> dataCtor = null)
        {
            if(!File.Exists(Path.Combine(BuffPlugin.CacheFolder, $"{pluginId}.dll")))
            {
                if (!assemblyDefs.ContainsKey(pluginId))
                {
                    assemblyDefs.Add(pluginId, AssemblyDefinition.CreateAssembly(
                        new AssemblyNameDefinition($"DynamicBuff_{pluginId}", new Version(BuffPlugin.ModVersion)),
                        $"Main", ModuleKind.Dll));
                    var decl = new SecurityDeclaration(Mono.Cecil.SecurityAction.RequestMinimum);
                    assemblyDefs[pluginId].SecurityDeclarations.Add(decl);
                    var attr = new SecurityAttribute(assemblyDefs[pluginId].MainModule
                        .ImportReference(typeof(SecurityPermissionAttribute)));
                    decl.SecurityAttributes.Add(attr);
                    attr.Properties.Add(new CustomAttributeNamedArgument("SkipVerification",
                        new CustomAttributeArgument(assemblyDefs[pluginId].MainModule.TypeSystem.Boolean, true)));

                }
                var space = pluginId.Replace("_dynamicCache", "");

                var moduleDef = assemblyDefs[pluginId].MainModule;

                var buffType = new TypeDefinition(space, $"{usedId}Buff", TypeAttributes.Public | TypeAttributes.Class,
                    moduleDef.ImportReference(typeof(RuntimeBuff)));
                moduleDef.Types.Add(buffType);

                var staticField = new FieldDefinition(usedId, FieldAttributes.Static,
                    moduleDef.ImportReference(typeof(BuffID)));
                buffType.Fields.Add(staticField);
                {
                    buffType.DefineStaticConstructor((cctorIl) =>
                    {
                        cctorIl.Emit(MonoOpCodes.Ldstr, usedId);
                        cctorIl.Emit(needRegisterId ? MonoOpCodes.Ldc_I4_1 : MonoOpCodes.Ldc_I4_0);
                        cctorIl.Emit(MonoOpCodes.Newobj, buffType.Module.ImportReference(
                            typeof(BuffID).GetConstructor(new[] { typeof(string), typeof(bool) })));
                        cctorIl.Emit(MonoOpCodes.Stsfld, staticField);
                        cctorIl.Emit(MonoOpCodes.Ret);
                    });


                    buffType.DefinePropertyOverride("ID", typeof(BuffID), MethodAttributes.Public,
                        buffType == null ? null : (il) => BuildIdGet(il, staticField));
                    buffType.DefineConstructor(
                        buffType.Module.ImportReference(typeof(RuntimeBuff)
                            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).First()),
                        buffCtor == null ? null : (il) => buffCtor?.Invoke(il));
                }


                var dataType = new TypeDefinition(space, $"{usedId}BuffData",
                    TypeAttributes.Public | TypeAttributes.Class,
                    moduleDef.ImportReference(typeof(BuffData)));

                moduleDef.Types.Add(dataType);
                {
                    dataType.DefinePropertyOverride("ID", typeof(BuffID), MethodAttributes.Public,
                        (il) => BuildIdGet(il, staticField));

                    dataType.DefineConstructor(dataType.Module.ImportReference(typeof(BuffData)
                            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).First()),
                        dataCtor == null ? null : (il) => dataCtor?.Invoke(il));
                }

                return (buffType, dataType);
            }
            return (null, null);
        }

        public static MethodDefinition DefineMethodOverride(this TypeDefinition type, string methodName
            , Type returnType, Type[] argTypes, MethodAttributes extAttr, [NotNull] Action<ILProcessor> builder)
        {
            var method = new MethodDefinition(methodName, MethodAttributes.HideBySig | MethodAttributes.Virtual | extAttr,
                type.Module.ImportReference(returnType));
            type.Methods.Add(method);
            foreach (var arg in argTypes)
                method.Parameters.Add(new ParameterDefinition(type.Module.ImportReference(arg)));
            builder(method.Body.GetILProcessor());
            return method;
        }


        public static MethodDefinition DefineMethodOverride(this TypeDefinition type, string methodName
            , TypeReference returnType, TypeReference[] argTypes, MethodAttributes extAttr, Action<ILProcessor> builder = null)
        {
            var method = new MethodDefinition(methodName, MethodAttributes.HideBySig | MethodAttributes.Virtual | extAttr,
                returnType);
            type.Methods.Add(method);
            foreach (var arg in argTypes)
                method.Parameters.Add(new ParameterDefinition(arg));

            if(builder != null)
                builder(method.Body.GetILProcessor());
            return method;
        }

        public static MethodDefinition DefineConstructor(this TypeDefinition type,
            [NotNull] MethodReference baseConstructor, Action<ILProcessor> builder)
        {
            const MethodAttributes methodAttributes =
                MethodAttributes.Public | MethodAttributes.HideBySig |
                MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            var method = new MethodDefinition(".ctor", methodAttributes, type.Module.TypeSystem.Void);
            type.Methods.Add(method);
            if (builder != null)
            {
                builder(method.Body.GetILProcessor());
            }
            else
            {
                var il = method.Body.GetILProcessor();
                il.Emit(MonoOpCodes.Ldarg_0);
                il.Emit(MonoOpCodes.Call, baseConstructor);
                il.Emit(MonoOpCodes.Ret);
            }
            return method;
        }

        public static MethodDefinition DefineStaticConstructor(this TypeDefinition type,
            [NotNull] Action<ILProcessor> builder)
        {
            const MethodAttributes methodAttributes =
                MethodAttributes.Private | MethodAttributes.HideBySig |
                MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Static;
            var method = new MethodDefinition(".cctor", methodAttributes, type.Module.TypeSystem.Void);
            type.Methods.Add(method);
            builder(method.Body.GetILProcessor());
            return method;
        }


        public static PropertyDefinition DefinePropertyOverride(this TypeDefinition type, string propertyName, Type returnType
            , MethodAttributes extAttr, Action<ILProcessor> getBuilder = null, Action<ILProcessor> setBuilder = null)
        {
            MethodAttributes attr =
                MethodAttributes.SpecialName |
                MethodAttributes.HideBySig | MethodAttributes.Virtual | extAttr;

            var property = new PropertyDefinition($"{propertyName}", PropertyAttributes.None, type.Module.ImportReference(returnType));
            if (getBuilder != null)
            {
                var method = type.DefineMethodOverride($"get_{propertyName}", returnType, Type.EmptyTypes, attr, getBuilder);
                property.GetMethod = method;
            }
            if (setBuilder != null)
            {
                var method = type.DefineMethodOverride($"set_{propertyName}", returnType, Type.EmptyTypes, attr, setBuilder);
                property.SetMethod = method;
            }
            type.Properties.Add(property);
            return property;
        }

        public static IEnumerable<Assembly> FinishGenerate(string pluginId, string debugOutputPath = null)
        {
            if (hasUse.Contains(pluginId))
            {
                BuffPlugin.LogError($"Already load dynamic {pluginId}.dll!");
                yield break;
            }


            if (assemblyDefs.ContainsKey($"{pluginId}_dynamicCache"))
                assemblyDefs[$"{pluginId}_dynamicCache"].Write(Path.Combine(BuffPlugin.CacheFolder, $"{pluginId}_dynamicCache.dll"));

            if (File.Exists(Path.Combine(BuffPlugin.CacheFolder, $"{pluginId}_dynamicCache.dll")))
                yield return LoadOrGetAssembly(pluginId, $"{pluginId}_dynamicCache", () =>
                    Assembly.LoadFile(Path.Combine(BuffPlugin.CacheFolder, $"{pluginId}_dynamicCache.dll")));

            if (assemblyDefs.ContainsKey(pluginId))
            {
                yield return LoadOrGetAssembly(pluginId, pluginId, () =>
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        assemblyDefs[pluginId].Write(ms);
                        if (debugOutputPath != null)
                            assemblyDefs[pluginId].Write(debugOutputPath);
                        hasUse.Add(pluginId);
                        return Assembly.Load(ms.GetBuffer());
                    }
                });

            }

            //读取或获取现有的动态程序集
            Assembly LoadOrGetAssembly(string pluginId, string assemblyName, Func<Assembly> loadFunc)
            {
                var info = BuffConfigManager.GetPluginInfo(pluginId);
                if(info.dynamicAssemblies.TryGetValue(assemblyName,out var assembly))
                    return assembly;
                info.dynamicAssemblies.Add(assemblyName, assembly = loadFunc());
                return assembly;
            }
        }


        internal static void CleanAllDatas()
        {
            foreach (var keyValuePair in assemblyDefs) 
                keyValuePair.Value.Dispose();
            assemblyDefs.Clear();

            hasUse.Clear();
        }


        private static void BuildIdGet(ILProcessor il, FieldDefinition staticField)
        {
            il.Emit(MonoOpCodes.Ldsfld, staticField);
            il.Emit(MonoOpCodes.Ret);
        }

        private static readonly Dictionary<string, AssemblyDefinition> assemblyDefs = new();
        private static readonly HashSet<string> hasUse = new();


    }
}

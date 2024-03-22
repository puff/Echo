using System.Collections.Generic;
using System.Linq;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using Echo.Memory;

namespace Echo.Platforms.AsmResolver.Emulation.Dispatch.ObjectModel
{
    /// <summary>
    /// Implements a CIL instruction handler for <c>callvirt</c> operations.
    /// </summary>
    [DispatcherTableEntry(CilCode.Callvirt)]
    public class CallVirtHandler : CallHandlerBase
    {
        private static readonly SignatureComparer Comparer = new();

        /// <inheritdoc />
        protected override MethodDevirtualizationResult DevirtualizeMethodInternal(
            CilExecutionContext context,
            CilInstruction instruction, 
            IList<BitVector> arguments)
        {
            switch (arguments[0].AsSpan())
            {
                case { IsFullyKnown: false }:
                    return MethodDevirtualizationResult.Unknown(); 
                
                case { IsZero.Value: TrileanValue.True }:
                    return MethodDevirtualizationResult.Exception(context.Machine
                        .Heap.AllocateObject(
                            context.Machine.ValueFactory.NullReferenceExceptionType,
                            true)
                        .AsObjectHandle(context.Machine));
                
                case var objectPointer:
                    var method = (IMethodDescriptor) instruction.Operand!;
                    var objectType = objectPointer.AsObjectHandle(context.Machine).GetObjectType();
                    var implementation = FindMethodImplementationInType(objectType.Resolve(), method.Resolve());
                    
                    if (implementation is null)
                    {
                        return MethodDevirtualizationResult.Exception(context.Machine
                            .Heap.AllocateObject(
                                context.Machine.ValueFactory.MissingMethodExceptionType,
                                true)
                            .AsObjectHandle(context.Machine));
                    }
                    
                    // instantiate generics
                    var genericContext = GenericContext.FromMethod(method);
                    if (genericContext.IsEmpty)
                        return MethodDevirtualizationResult.Success(implementation);
                    
                    var methodRef = objectType.ToTypeDefOrRef().CreateMemberReference(implementation.Name!, implementation.Signature!);
                    return MethodDevirtualizationResult.Success(genericContext.Method != null ? methodRef.MakeGenericInstanceMethod(genericContext.Method.TypeArguments.ToArray()) : methodRef);
            }
        }

        private static MethodDefinition? FindMethodImplementationInType(TypeDefinition? type, MethodDefinition? baseMethod)
        {
            if (type is null || baseMethod is null)
                return baseMethod;

            var implementation = default(MethodDefinition);
            var declaringType = baseMethod.DeclaringType!;
            while (type is not null && !Comparer.Equals(type, declaringType))
            {
                // Prioritize interface implementations.
                if (declaringType.IsInterface)
                    implementation = TryFindExplicitInterfaceImplementationInType(type, baseMethod)
                                     ?? TryFindImplicitInterfaceImplementationInType(type, baseMethod);

                // Try to find other implicit implementations.
                implementation ??= TryFindImplicitImplementationInType(type, baseMethod);
                
                if (implementation is not null)
                    break;
                
                // Move up type hierarchy tree.
                type = type.BaseType?.Resolve();
            }

            // If there's no override, just use the base implementation (if available).
            if (implementation is null && !baseMethod.IsAbstract)
                implementation = baseMethod;
            
            return implementation;
        }

        private static MethodDefinition? TryFindImplicitImplementationInType(TypeDefinition type, MethodDefinition baseMethod)
        {
            foreach (var method in type.Methods)
            {
                if (method.IsVirtual
                    && method.IsReuseSlot
                    && method.Name == baseMethod.Name
                    && Comparer.Equals(method.Signature, baseMethod.Signature))
                {
                    return method;
                }
            }

            return null;
        }

        private static MethodDefinition? TryFindImplicitInterfaceImplementationInType(TypeDefinition type, MethodDefinition baseMethod)
        {
            // Find the correct interface implementation and instantiate any generics.
            MethodSignature? baseMethodSig = null;
            foreach (var interfaceImpl in type.Interfaces)
            {
                if (Comparer.Equals(interfaceImpl.Interface?.ToTypeSignature().GetUnderlyingTypeDefOrRef(), baseMethod.DeclaringType))
                {
                    baseMethodSig = baseMethod.Signature?.InstantiateGenericTypes(GenericContext.FromType(interfaceImpl.Interface!));
                    break;
                }
            }
            if (baseMethodSig is null)
                return null;

            // Find implemented method in type.
            for (int i = 0; i < type.Methods.Count; i++)
            {
                var method = type.Methods[i];
                // Only public virtual instance methods can implicity implement interface methods. (ECMA-335, 6th edition, II.12.2)
                if (method.IsPublic
                    && method.IsVirtual
                    && !method.IsStatic
                    && method.Name == baseMethod.Name
                    && Comparer.Equals(method.Signature, baseMethodSig))
                    return method;
            }

            return null;
        }

        private static MethodDefinition? TryFindExplicitInterfaceImplementationInType(TypeDefinition type, MethodDefinition baseMethod)
        {
            for (int i = 0; i < type.MethodImplementations.Count; i++)
            {
                var impl = type.MethodImplementations[i];

                // Compare underlying TypeDefOrRef and instantiate any generics to ensure correct comparison.
                if (!Comparer.Equals(impl.Declaration?.DeclaringType?.ToTypeSignature().GetUnderlyingTypeDefOrRef(), baseMethod.DeclaringType))
                    continue;

                var context = GenericContext.FromMethod(impl.Declaration!);
                var implMethodSig = impl.Declaration?.Signature?.InstantiateGenericTypes(context); 
                var baseMethodSig = baseMethod.Signature?.InstantiateGenericTypes(context);
                if (Comparer.Equals(baseMethodSig, implMethodSig))
                    return impl.Body?.Resolve();
            }

            return null;
        }
    }
}
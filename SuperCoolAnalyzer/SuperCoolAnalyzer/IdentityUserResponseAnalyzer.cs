using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace SuperCoolAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IdentityUserResponseAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "SCWS0001";

    private const string Category = "Security";
    private const string IdentityUserMetadataName = "Microsoft.AspNetCore.Identity.IdentityUser";
    private const string GenericIdentityUserMetadataName = "Microsoft.AspNetCore.Identity.IdentityUser`1";
    private const string ControllerBaseMetadataName = "Microsoft.AspNetCore.Mvc.ControllerBase";
    private const string ActionResultMetadataName = "Microsoft.AspNetCore.Mvc.ActionResult";
    private const string NonActionAttributeMetadataName = "Microsoft.AspNetCore.Mvc.NonActionAttribute";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Identity type exposed in an MVC response",
        "Do not return identity type '{0}' from an MVC endpoint; map it to a DTO to avoid exposing security-sensitive data",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
        "ASP.NET Core Identity user objects can contain security-sensitive fields. Return a purpose-built DTO instead.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var identityUser = compilationContext.Compilation.GetTypeByMetadataName(IdentityUserMetadataName);
            var genericIdentityUser =
                compilationContext.Compilation.GetTypeByMetadataName(GenericIdentityUserMetadataName);
            var controllerBase = compilationContext.Compilation.GetTypeByMetadataName(ControllerBaseMetadataName);
            var actionResult = compilationContext.Compilation.GetTypeByMetadataName(ActionResultMetadataName);
            var nonActionAttribute =
                compilationContext.Compilation.GetTypeByMetadataName(NonActionAttributeMetadataName);

            if (controllerBase is null || (identityUser is null && genericIdentityUser is null))
                return;

            var symbols = new FrameworkSymbols(
                identityUser,
                genericIdentityUser,
                controllerBase,
                actionResult,
                nonActionAttribute);

            compilationContext.RegisterSymbolAction(
                symbolContext => AnalyzeMethodContract(symbolContext, symbols),
                SymbolKind.Method);
            compilationContext.RegisterOperationBlockAction(
                operationContext => AnalyzeMethodBody(operationContext, symbols));
        });
    }

    private static void AnalyzeMethodContract(SymbolAnalysisContext context, FrameworkSymbols symbols)
    {
        if (context.Symbol is not IMethodSymbol method || !IsMvcAction(method, symbols))
            return;

        var identityType = FindIdentityType(method.ReturnType, symbols, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
        if (identityType is null)
            return;

        var location = method.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(context.CancellationToken))
            .OfType<MethodDeclarationSyntax>()
            .Select(declaration => declaration.ReturnType.GetLocation())
            .FirstOrDefault() ?? method.Locations.FirstOrDefault();

        if (location is not null)
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, identityType.ToDisplayString()));
    }

    private static void AnalyzeMethodBody(OperationBlockAnalysisContext context, FrameworkSymbols symbols)
    {
        if (context.OwningSymbol is not IMethodSymbol method || !IsMvcAction(method, symbols))
            return;

        // An unsafe declared contract already produces one diagnostic at the return type.
        if (FindIdentityType(method.ReturnType, symbols, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)) is not null)
            return;

        var collector = new MethodOperationCollector();
        foreach (var operationBlock in context.OperationBlocks)
            collector.Visit(operationBlock);

        var flowAnalysis = new ResponseFlowAnalysis(symbols, collector.LocalValues);
        foreach (var returnOperation in collector.Returns)
        {
            if (returnOperation.ReturnedValue is null)
                continue;

            var exposure = flowAnalysis.FindExposure(returnOperation.ReturnedValue);
            if (exposure is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    exposure.Operation.Syntax.GetLocation(),
                    exposure.IdentityType.ToDisplayString()));
            }
        }
    }

    private static bool IsMvcAction(IMethodSymbol method, FrameworkSymbols symbols)
    {
        if (method.MethodKind != MethodKind.Ordinary ||
            method.DeclaredAccessibility != Accessibility.Public ||
            method.IsStatic ||
            method.IsGenericMethod ||
            !IsOrDerivesFrom(method.ContainingType, symbols.ControllerBase))
        {
            return false;
        }

        for (var current = method; current is not null; current = current.OverriddenMethod)
        {
            if (current.GetAttributes().Any(attribute =>
                    IsOrDerivesFrom(attribute.AttributeClass, symbols.NonActionAttribute)))
            {
                return false;
            }
        }

        return true;
    }

    private static INamedTypeSymbol? FindIdentityType(
        ITypeSymbol? type,
        FrameworkSymbols symbols,
        HashSet<ITypeSymbol> visited)
    {
        if (type is null || !visited.Add(type))
            return null;

        if (type is IArrayTypeSymbol arrayType)
            return FindIdentityType(arrayType.ElementType, symbols, visited);

        if (type is IPointerTypeSymbol pointerType)
            return FindIdentityType(pointerType.PointedAtType, symbols, visited);

        if (type is ITypeParameterSymbol typeParameter)
        {
            foreach (var constraintType in typeParameter.ConstraintTypes)
            {
                var constrainedIdentity = FindIdentityType(constraintType, symbols, visited);
                if (constrainedIdentity is not null)
                    return constrainedIdentity;
            }

            return null;
        }

        if (type is not INamedTypeSymbol namedType)
            return null;

        if (IsIdentityType(namedType, symbols))
            return namedType;

        foreach (var typeArgument in namedType.TypeArguments)
        {
            var identityArgument = FindIdentityType(typeArgument, symbols, visited);
            if (identityArgument is not null)
                return identityArgument;
        }

        if (!ShouldInspectSerializableMembers(namedType))
            return null;

        foreach (var member in namedType.GetMembers())
        {
            ITypeSymbol? memberType = member switch
            {
                IPropertySymbol property when !property.IsStatic &&
                                              property.DeclaredAccessibility == Accessibility.Public &&
                                              property.GetMethod is not null => property.Type,
                IFieldSymbol field when !field.IsStatic &&
                                        field.DeclaredAccessibility == Accessibility.Public => field.Type,
                _ => null
            };

            var identityMember = FindIdentityType(memberType, symbols, visited);
            if (identityMember is not null)
                return identityMember;
        }

        return null;
    }

    private static bool ShouldInspectSerializableMembers(INamedTypeSymbol type)
    {
        if (type.IsAnonymousType || type.Locations.Any(location => location.IsInSource))
            return true;

        var rootNamespace = type.ContainingNamespace;
        while (rootNamespace?.ContainingNamespace is { IsGlobalNamespace: false } parent)
            rootNamespace = parent;

        return rootNamespace is null ||
               (rootNamespace.Name != "System" && rootNamespace.Name != "Microsoft");
    }

    private static bool IsIdentityType(INamedTypeSymbol type, FrameworkSymbols symbols)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, symbols.IdentityUser) ||
                SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, symbols.GenericIdentityUser))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOrDerivesFrom(INamedTypeSymbol? type, INamedTypeSymbol? expectedBaseType)
    {
        if (type is null || expectedBaseType is null)
            return false;

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, expectedBaseType) ||
                SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, expectedBaseType))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class ResponseFlowAnalysis
    {
        private static readonly ImmutableHashSet<string> PayloadParameterNames =
            ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "value", "error", "data", "payload", "model");

        private readonly FrameworkSymbols _symbols;
        private readonly IReadOnlyDictionary<ILocalSymbol, List<IOperation>> _localValues;

        public ResponseFlowAnalysis(
            FrameworkSymbols symbols,
            IReadOnlyDictionary<ILocalSymbol, List<IOperation>> localValues)
        {
            _symbols = symbols;
            _localValues = localValues;
        }

        public Exposure? FindExposure(IOperation operation) =>
            FindExposure(
                operation,
                new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default),
                new HashSet<IOperation>());

        private Exposure? FindExposure(
            IOperation operation,
            HashSet<ILocalSymbol> visitedLocals,
            HashSet<IOperation> visitedOperations)
        {
            if (!visitedOperations.Add(operation))
                return null;

            switch (operation)
            {
                case IConversionOperation conversion:
                    return FindExposure(conversion.Operand, visitedLocals, visitedOperations);
                case IParenthesizedOperation parenthesized:
                    return FindExposure(parenthesized.Operand, visitedLocals, visitedOperations);
                case IAwaitOperation awaitOperation:
                    return FindExposure(awaitOperation.Operation, visitedLocals, visitedOperations);
                case IArgumentOperation argument:
                    return FindExposure(argument.Value, visitedLocals, visitedOperations);
                case IVariableInitializerOperation initializer:
                    return FindExposure(initializer.Value, visitedLocals, visitedOperations);
                case ISimpleAssignmentOperation assignment:
                    return FindExposure(assignment.Value, visitedLocals, visitedOperations);
            }

            var directIdentity = FindIdentityType(
                operation.Type,
                _symbols,
                new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
            if (directIdentity is not null)
            {
                var nestedEvidence = FindNestedValueExposure(operation, visitedLocals, visitedOperations);
                return nestedEvidence ?? new Exposure(operation, directIdentity);
            }

            switch (operation)
            {
                case ILocalReferenceOperation localReference:
                    return FindLocalExposure(localReference.Local, visitedLocals, visitedOperations);
                case IConditionalOperation conditional:
                    return FindExposure(conditional.WhenTrue, visitedLocals, visitedOperations) ??
                           FindExposureOrNull(conditional.WhenFalse, visitedLocals, visitedOperations);
                case ICoalesceOperation coalesce:
                    return FindExposure(coalesce.Value, visitedLocals, visitedOperations) ??
                           FindExposure(coalesce.WhenNull, visitedLocals, visitedOperations);
                case ISwitchExpressionOperation switchExpression:
                    foreach (var arm in switchExpression.Arms)
                    {
                        var armExposure = FindExposure(arm.Value, visitedLocals, visitedOperations);
                        if (armExposure is not null)
                            return armExposure;
                    }

                    break;
                case IInvocationOperation invocation when IsTransparentTaskWrapper(invocation):
                    foreach (var argument in invocation.Arguments)
                    {
                        var wrappedExposure = FindExposure(argument.Value, visitedLocals, visitedOperations);
                        if (wrappedExposure is not null)
                            return wrappedExposure;
                    }

                    break;
                case IInvocationOperation invocation when IsMvcResponseFactory(invocation):
                    return FindPayloadArgumentExposure(invocation.Arguments, visitedLocals, visitedOperations);
                case IObjectCreationOperation creation when IsTransparentValueTaskWrapper(creation):
                    foreach (var argument in creation.Arguments)
                    {
                        var wrappedExposure = FindExposure(argument.Value, visitedLocals, visitedOperations);
                        if (wrappedExposure is not null)
                            return wrappedExposure;
                    }

                    break;
                case IObjectCreationOperation creation when IsActionResultType(creation.Type):
                    return FindPayloadArgumentExposure(creation.Arguments, visitedLocals, visitedOperations) ??
                           FindInitializerExposure(creation.Initializer, visitedLocals, visitedOperations);
                case IObjectCreationOperation creation:
                    return FindInitializerExposure(creation.Initializer, visitedLocals, visitedOperations);
                case IArrayCreationOperation arrayCreation:
                    return FindArrayExposure(arrayCreation, visitedLocals, visitedOperations);
                case IAnonymousObjectCreationOperation anonymousCreation:
                    foreach (var anonymousInitializer in anonymousCreation.Initializers)
                    {
                        var anonymousExposure =
                            FindExposure(anonymousInitializer, visitedLocals, visitedOperations);
                        if (anonymousExposure is not null)
                            return anonymousExposure;
                    }

                    break;
                case ITupleOperation tuple:
                    foreach (var element in tuple.Elements)
                    {
                        var tupleExposure = FindExposure(element, visitedLocals, visitedOperations);
                        if (tupleExposure is not null)
                            return tupleExposure;
                    }

                    break;
            }

            return null;
        }

        private Exposure? FindNestedValueExposure(
            IOperation operation,
            HashSet<ILocalSymbol> visitedLocals,
            HashSet<IOperation> visitedOperations)
        {
            if (operation.Type is INamedTypeSymbol namedType && IsIdentityType(namedType, _symbols))
                return null;

            switch (operation)
            {
                case ILocalReferenceOperation:
                    // The local use is the clearest endpoint-adjacent location when its declared type is unsafe.
                    return null;
                case IAnonymousObjectCreationOperation anonymousCreation:
                    foreach (var initializer in anonymousCreation.Initializers)
                    {
                        var exposure = FindExposure(initializer, visitedLocals, visitedOperations);
                        if (exposure is not null)
                            return exposure;
                    }

                    break;
                case IArrayCreationOperation arrayCreation:
                    return FindArrayExposure(arrayCreation, visitedLocals, visitedOperations);
                case IObjectCreationOperation objectCreation:
                    return FindInitializerExposure(objectCreation.Initializer, visitedLocals, visitedOperations);
                case ITupleOperation tuple:
                    foreach (var element in tuple.Elements)
                    {
                        var exposure = FindExposure(element, visitedLocals, visitedOperations);
                        if (exposure is not null)
                            return exposure;
                    }

                    break;
                case IConditionalOperation conditional:
                    return FindExposure(conditional.WhenTrue, visitedLocals, visitedOperations) ??
                           FindExposureOrNull(conditional.WhenFalse, visitedLocals, visitedOperations);
                case ICoalesceOperation coalesce:
                    return FindExposure(coalesce.Value, visitedLocals, visitedOperations) ??
                           FindExposure(coalesce.WhenNull, visitedLocals, visitedOperations);
            }

            return null;
        }

        private Exposure? FindExposureOrNull(
            IOperation? operation,
            HashSet<ILocalSymbol> visitedLocals,
            HashSet<IOperation> visitedOperations) =>
            operation is null ? null : FindExposure(operation, visitedLocals, visitedOperations);

        private Exposure? FindLocalExposure(
            ILocalSymbol local,
            HashSet<ILocalSymbol> visitedLocals,
            HashSet<IOperation> visitedOperations)
        {
            if (!visitedLocals.Add(local) || !_localValues.TryGetValue(local, out var values))
                return null;

            foreach (var value in values)
            {
                var exposure = FindExposure(value, visitedLocals, visitedOperations);
                if (exposure is not null)
                    return exposure;
            }

            return null;
        }

        private Exposure? FindPayloadArgumentExposure(
            ImmutableArray<IArgumentOperation> arguments,
            HashSet<ILocalSymbol> visitedLocals,
            HashSet<IOperation> visitedOperations)
        {
            foreach (var argument in arguments)
            {
                if (argument.Parameter is null || !PayloadParameterNames.Contains(argument.Parameter.Name))
                    continue;

                var exposure = FindExposure(argument.Value, visitedLocals, visitedOperations);
                if (exposure is not null)
                    return exposure;
            }

            return null;
        }

        private Exposure? FindInitializerExposure(
            IObjectOrCollectionInitializerOperation? initializer,
            HashSet<ILocalSymbol> visitedLocals,
            HashSet<IOperation> visitedOperations)
        {
            if (initializer is null)
                return null;

            foreach (var item in initializer.Initializers)
            {
                Exposure? exposure;
                if (item is ISimpleAssignmentOperation assignment)
                {
                    exposure = FindExposure(assignment.Value, visitedLocals, visitedOperations);
                }
                else if (item is IInvocationOperation invocation)
                {
                    exposure = null;
                    foreach (var argument in invocation.Arguments)
                    {
                        exposure = FindExposure(argument.Value, visitedLocals, visitedOperations);
                        if (exposure is not null)
                            break;
                    }
                }
                else
                {
                    exposure = FindExposure(item, visitedLocals, visitedOperations);
                }

                if (exposure is not null)
                    return exposure;
            }

            return null;
        }

        private Exposure? FindArrayExposure(
            IArrayCreationOperation arrayCreation,
            HashSet<ILocalSymbol> visitedLocals,
            HashSet<IOperation> visitedOperations)
        {
            if (arrayCreation.Initializer is null)
                return null;

            foreach (var element in arrayCreation.Initializer.ElementValues)
            {
                var exposure = FindExposure(element, visitedLocals, visitedOperations);
                if (exposure is not null)
                    return exposure;
            }

            return null;
        }

        private bool IsMvcResponseFactory(IInvocationOperation invocation) =>
            IsOrDerivesFrom(invocation.TargetMethod.ContainingType, _symbols.ControllerBase) &&
            IsActionResultType(invocation.Type);

        private static bool IsTransparentTaskWrapper(IInvocationOperation invocation) =>
            invocation.TargetMethod.Name == "FromResult" &&
            invocation.TargetMethod.ContainingType.ToDisplayString() == "System.Threading.Tasks.Task";

        private static bool IsTransparentValueTaskWrapper(IObjectCreationOperation creation) =>
            creation.Type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.ToDisplayString() == "System.Threading.Tasks.ValueTask<TResult>";

        private bool IsActionResultType(ITypeSymbol? type)
        {
            if (type is not INamedTypeSymbol namedType || _symbols.ActionResult is null)
                return false;

            return IsOrDerivesFrom(namedType, _symbols.ActionResult);
        }
    }

    private sealed class MethodOperationCollector : OperationWalker
    {
        private static readonly ImmutableHashSet<string> StoringMethodNames =
            ImmutableHashSet.Create(StringComparer.Ordinal, "Add", "AddRange", "Enqueue", "Insert", "Push", "TryAdd");

        public Dictionary<ILocalSymbol, List<IOperation>> LocalValues { get; } =
            new(SymbolEqualityComparer.Default);

        public List<IReturnOperation> Returns { get; } = new();

        public override void VisitReturn(IReturnOperation operation)
        {
            Returns.Add(operation);
            base.VisitReturn(operation);
        }

        public override void VisitVariableDeclarator(IVariableDeclaratorOperation operation)
        {
            if (operation.Initializer is not null)
                AddLocalValue(operation.Symbol, operation.Initializer.Value);

            base.VisitVariableDeclarator(operation);
        }

        public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
        {
            if (UnwrapLocal(operation.Target) is { } local)
                AddLocalValue(local, operation.Value);

            base.VisitSimpleAssignment(operation);
        }

        public override void VisitInvocation(IInvocationOperation operation)
        {
            if (StoringMethodNames.Contains(operation.TargetMethod.Name) &&
                UnwrapLocal(operation.Instance) is { } local)
            {
                foreach (var argument in operation.Arguments)
                    AddLocalValue(local, argument.Value);
            }

            base.VisitInvocation(operation);
        }

        public override void VisitAnonymousFunction(IAnonymousFunctionOperation operation)
        {
            // A nested delegate is not part of the surrounding MVC action's response flow.
        }

        public override void VisitLocalFunction(ILocalFunctionOperation operation)
        {
            // A local function has its own return statements and is not itself an MVC action.
        }

        private void AddLocalValue(ILocalSymbol local, IOperation value)
        {
            if (!LocalValues.TryGetValue(local, out var values))
            {
                values = new List<IOperation>();
                LocalValues.Add(local, values);
            }

            values.Add(value);
        }

        private static ILocalSymbol? UnwrapLocal(IOperation? operation)
        {
            while (operation is IConversionOperation conversion)
                operation = conversion.Operand;

            return (operation as ILocalReferenceOperation)?.Local;
        }
    }

    private sealed class FrameworkSymbols
    {
        public FrameworkSymbols(
            INamedTypeSymbol? identityUser,
            INamedTypeSymbol? genericIdentityUser,
            INamedTypeSymbol controllerBase,
            INamedTypeSymbol? actionResult,
            INamedTypeSymbol? nonActionAttribute)
        {
            IdentityUser = identityUser;
            GenericIdentityUser = genericIdentityUser;
            ControllerBase = controllerBase;
            ActionResult = actionResult;
            NonActionAttribute = nonActionAttribute;
        }

        public INamedTypeSymbol? IdentityUser { get; }
        public INamedTypeSymbol? GenericIdentityUser { get; }
        public INamedTypeSymbol ControllerBase { get; }
        public INamedTypeSymbol? ActionResult { get; }
        public INamedTypeSymbol? NonActionAttribute { get; }
    }

    private sealed class Exposure
    {
        public Exposure(IOperation operation, INamedTypeSymbol identityType)
        {
            Operation = operation;
            IdentityType = identityType;
        }

        public IOperation Operation { get; }
        public INamedTypeSymbol IdentityType { get; }
    }
}

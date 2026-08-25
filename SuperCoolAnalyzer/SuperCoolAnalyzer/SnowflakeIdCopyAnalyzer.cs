using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace SuperCoolAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SnowflakeIdCopyAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "SCWS0002";

    private const string Category = "Correctness";
    private const string SnowflakeMetadataName = "SuperCoolWebServer.Models.ISnowflake";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Snowflake identity copied to a new object",
        "Do not copy snowflake ID from '{0}' to new '{1}'; generate a new ID instead",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
        "Every new snowflake object must receive a newly generated identity. Copying an existing object's ID creates an identity collision.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var snowflakeType = compilationContext.Compilation.GetTypeByMetadataName(SnowflakeMetadataName);
            var snowflakeId = snowflakeType?
                .GetMembers("Id")
                .OfType<IPropertySymbol>()
                .FirstOrDefault(property => !property.IsStatic);

            if (snowflakeType is null || snowflakeId is null)
                return;

            compilationContext.RegisterOperationBlockAction(operationContext =>
                AnalyzeOperationBlock(operationContext, snowflakeType, snowflakeId));
        });
    }

    private static void AnalyzeOperationBlock(
        OperationBlockAnalysisContext context,
        INamedTypeSymbol snowflakeType,
        IPropertySymbol snowflakeId)
    {
        var collector = new OperationCollector();
        foreach (var operationBlock in context.OperationBlocks)
            collector.Visit(operationBlock);

        var analysis = new CopyAnalysis(snowflakeType, snowflakeId, collector.LocalValues);

        foreach (var creation in collector.ObjectCreations)
            AnalyzeInitializer(context, creation.Type, creation.Initializer, analysis);

        foreach (var withOperation in collector.WithOperations)
            AnalyzeInitializer(context, withOperation.Type, withOperation.Initializer, analysis);
    }

    private static void AnalyzeInitializer(
        OperationBlockAnalysisContext context,
        ITypeSymbol? targetType,
        IObjectOrCollectionInitializerOperation? initializer,
        CopyAnalysis analysis)
    {
        if (targetType is not INamedTypeSymbol namedTargetType ||
            initializer is null ||
            !analysis.IsSnowflakeType(namedTargetType))
        {
            return;
        }

        foreach (var item in initializer.Initializers)
        {
            if (item is not ISimpleAssignmentOperation assignment ||
                assignment.Target is not IPropertyReferenceOperation targetProperty ||
                !analysis.IsSnowflakeId(targetProperty.Property, namedTargetType))
            {
                continue;
            }

            var sourceType = analysis.FindCopiedSnowflakeType(assignment.Value);
            if (sourceType is null)
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                assignment.Value.Syntax.GetLocation(),
                sourceType.ToDisplayString(),
                namedTargetType.ToDisplayString()));
        }
    }

    private sealed class CopyAnalysis
    {
        private readonly INamedTypeSymbol _snowflakeType;
        private readonly IPropertySymbol _snowflakeId;
        private readonly IReadOnlyDictionary<ILocalSymbol, List<IOperation>> _localValues;

        public CopyAnalysis(
            INamedTypeSymbol snowflakeType,
            IPropertySymbol snowflakeId,
            IReadOnlyDictionary<ILocalSymbol, List<IOperation>> localValues)
        {
            _snowflakeType = snowflakeType;
            _snowflakeId = snowflakeId;
            _localValues = localValues;
        }

        public bool IsSnowflakeType(INamedTypeSymbol type) =>
            type.AllInterfaces.Any(@interface =>
                SymbolEqualityComparer.Default.Equals(@interface, _snowflakeType)) ||
            SymbolEqualityComparer.Default.Equals(type, _snowflakeType);

        public bool IsSnowflakeId(IPropertySymbol property, INamedTypeSymbol receiverType)
        {
            var implementation = receiverType.FindImplementationForInterfaceMember(_snowflakeId);
            return SymbolEqualityComparer.Default.Equals(property, _snowflakeId) ||
                   SymbolEqualityComparer.Default.Equals(property, implementation) ||
                   (implementation is IPropertySymbol implementationProperty &&
                    SymbolEqualityComparer.Default.Equals(
                        property.OriginalDefinition,
                        implementationProperty.OriginalDefinition));
        }

        public INamedTypeSymbol? FindCopiedSnowflakeType(IOperation operation) =>
            FindCopiedSnowflakeType(
                operation,
                new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default),
                new HashSet<IOperation>());

        private INamedTypeSymbol? FindCopiedSnowflakeType(
            IOperation operation,
            HashSet<ILocalSymbol> visitedLocals,
            HashSet<IOperation> visitedOperations)
        {
            if (!visitedOperations.Add(operation))
                return null;

            switch (operation)
            {
                case IConversionOperation conversion:
                    return FindCopiedSnowflakeType(conversion.Operand, visitedLocals, visitedOperations);
                case IParenthesizedOperation parenthesized:
                    return FindCopiedSnowflakeType(parenthesized.Operand, visitedLocals, visitedOperations);
                case IPropertyReferenceOperation propertyReference
                    when propertyReference.Instance?.Type is INamedTypeSymbol sourceType &&
                         IsSnowflakeType(sourceType) &&
                         IsSnowflakeId(propertyReference.Property, sourceType):
                    return sourceType;
                case ILocalReferenceOperation localReference:
                    if (!visitedLocals.Add(localReference.Local) ||
                        !_localValues.TryGetValue(localReference.Local, out var values) ||
                        values.Count != 1)
                    {
                        return null;
                    }

                    return FindCopiedSnowflakeType(values[0], visitedLocals, visitedOperations);
                case IConditionalOperation conditional:
                    return FindCopiedSnowflakeType(conditional.WhenTrue, visitedLocals, visitedOperations) ??
                           (conditional.WhenFalse is null
                               ? null
                               : FindCopiedSnowflakeType(conditional.WhenFalse, visitedLocals, visitedOperations));
                case ICoalesceOperation coalesce:
                    return FindCopiedSnowflakeType(coalesce.Value, visitedLocals, visitedOperations) ??
                           (coalesce.WhenNull is null
                               ? null
                               : FindCopiedSnowflakeType(coalesce.WhenNull, visitedLocals, visitedOperations));
                case ISwitchExpressionOperation switchExpression:
                    foreach (var arm in switchExpression.Arms)
                    {
                        var copiedType = FindCopiedSnowflakeType(arm.Value, visitedLocals, visitedOperations);
                        if (copiedType is not null)
                            return copiedType;
                    }

                    break;
            }

            return null;
        }
    }

    private sealed class OperationCollector : OperationWalker
    {
        public Dictionary<ILocalSymbol, List<IOperation>> LocalValues { get; } =
            new(SymbolEqualityComparer.Default);

        public List<IObjectCreationOperation> ObjectCreations { get; } = new();
        public List<IWithOperation> WithOperations { get; } = new();

        public override void VisitObjectCreation(IObjectCreationOperation operation)
        {
            ObjectCreations.Add(operation);
            base.VisitObjectCreation(operation);
        }

        public override void VisitWith(IWithOperation operation)
        {
            WithOperations.Add(operation);
            base.VisitWith(operation);
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

        public override void VisitAnonymousFunction(IAnonymousFunctionOperation operation)
        {
            // Nested functions have their own local flows and operation blocks.
        }

        public override void VisitLocalFunction(ILocalFunctionOperation operation)
        {
            // Nested functions have their own local flows and operation blocks.
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

        private static ILocalSymbol? UnwrapLocal(IOperation operation)
        {
            while (operation is IConversionOperation conversion)
                operation = conversion.Operand;

            return (operation as ILocalReferenceOperation)?.Local;
        }
    }
}

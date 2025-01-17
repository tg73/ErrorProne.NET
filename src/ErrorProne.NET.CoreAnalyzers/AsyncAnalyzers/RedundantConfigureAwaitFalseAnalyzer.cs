﻿using System.Diagnostics.ContractsLight;
using ErrorProne.NET.CoreAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace ErrorProne.NET.AsyncAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RedundantConfigureAwaitFalseAnalyzer : DiagnosticAnalyzerBase
    {
        /// <nodoc />
        public static string DiagnosticId => Rule.Id;

        /// <nodoc />
        public static DiagnosticDescriptor Rule => DiagnosticDescriptors.EPC14;

        /// <nodoc />
        public RedundantConfigureAwaitFalseAnalyzer()
            : base(Rule)
        {
        }

        /// <inheritdoc />
        protected override void InitializeCore(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeAwaitExpression, SyntaxKind.AwaitExpression);
        }

        private void AnalyzeAwaitExpression(SyntaxNodeAnalysisContext context)
        {
            var invocation = (AwaitExpressionSyntax)context.Node;

            var configureAwaitConfig = ConfigureAwaitConfiguration.TryGetConfigureAwait(context.Compilation);
            if (configureAwaitConfig == ConfigureAwait.DoNotUseConfigureAwait)
            {
                var operation = context.SemanticModel.GetOperation(invocation, context.CancellationToken);
                if (operation is IAwaitOperation awaitOperation &&
                    awaitOperation.Operation is IInvocationOperation configureAwaitOperation &&
                    configureAwaitOperation.TargetMethod.IsConfigureAwait(context.Compilation))
                {
                    if (configureAwaitOperation.Arguments.Length != 0 &&
                        configureAwaitOperation.Arguments[0].Value is ILiteralOperation literal &&
                        literal.ConstantValue.HasValue &&
                        literal.ConstantValue.Value.Equals(false))
                    {
                        var location = configureAwaitOperation.Syntax.GetLocation();

                        // Need to find 'ConfigureAwait' node.
                        if (configureAwaitOperation.Syntax is InvocationExpressionSyntax i &&
                            i.Expression is
                            MemberAccessExpressionSyntax mae)
                        {
                            // This is a really weird way for getting a location for 'ConfigureAwait(false)' span!

                            var argsLocation = i.ArgumentList.GetLocation();
                            var nameLocation = mae.Name.GetLocation().SourceSpan;
                            
                            Contract.Assert(argsLocation.SourceTree != null);
                            location = Location.Create(argsLocation.SourceTree,
                                TextSpan.FromBounds(nameLocation.Start, argsLocation.SourceSpan.End));
                        }

                        var diagnostic = Diagnostic.Create(Rule, location);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
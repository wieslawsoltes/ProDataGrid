// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProDataGrid.FormulaEngine
{
    // Shared by dependency and volatility analysis. LET/LAMBDA declarations are bindings,
    // not reads of workbook names. The visitors conservatively inspect lambda bodies.
    internal static class FormulaBindingTraversal
    {
        public static bool VisitArguments(FormulaFunctionCallExpression call, HashSet<string>? locals,
            Func<FormulaExpression, HashSet<string>?, bool> visit)
        {
            var args = call.Arguments;
            if (string.Equals(call.Name, "LAMBDA", StringComparison.OrdinalIgnoreCase) && args.Count > 0)
            {
                var scope = Copy(locals);
                for (var i = 0; i < args.Count - 1; i++)
                {
                    if (args[i] is FormulaNameExpression parameter) scope.Add(parameter.Name);
                    else if (visit(args[i], locals)) return true;
                }
                return visit(args[args.Count - 1], scope);
            }
            if (string.Equals(call.Name, "LET", StringComparison.OrdinalIgnoreCase) && args.Count >= 3 && (args.Count & 1) == 1)
            {
                var scope = Copy(locals);
                for (var i = 0; i < args.Count - 1; i += 2)
                {
                    if (visit(args[i + 1], scope)) return true;
                    if (args[i] is FormulaNameExpression name) scope.Add(name.Name);
                    else if (visit(args[i], scope)) return true;
                }
                return visit(args[args.Count - 1], scope);
            }
            foreach (var arg in args)
                if (visit(arg, locals)) return true;
            return false;
        }

        private static HashSet<string> Copy(HashSet<string>? source)
            => source == null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(source, StringComparer.OrdinalIgnoreCase);
    }
}

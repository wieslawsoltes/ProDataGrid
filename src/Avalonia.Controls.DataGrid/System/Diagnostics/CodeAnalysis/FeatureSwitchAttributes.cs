#pragma warning disable MA0048 // File name must match type name
// https://github.com/dotnet/runtime/tree/main/src/libraries/System.Private.CoreLib/src/System/Diagnostics/CodeAnalysis

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Diagnostics.CodeAnalysis
{
#nullable enable
#if !NET9_0_OR_GREATER
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    internal sealed class FeatureGuardAttribute : Attribute
    {
        public FeatureGuardAttribute(Type featureType)
        {
            FeatureType = featureType;
        }

        public Type FeatureType { get; }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    internal sealed class FeatureSwitchDefinitionAttribute : Attribute
    {
        public FeatureSwitchDefinitionAttribute(string switchName)
        {
            SwitchName = switchName;
        }

        public string SwitchName { get; }
    }
#endif
}

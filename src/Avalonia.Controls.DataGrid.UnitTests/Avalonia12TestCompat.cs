using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Input;

namespace Avalonia.Controls.DataGridTests
{
    internal static class Avalonia12TestCompat
    {
        public static IPropertyAccessor StartPropertyAccessor(object target, string path)
        {
            var plugin = GetBindingPluginsCollection("PropertyAccessors")
                .Cast<object>()
                .First(candidate => candidate.GetType().Name.Contains("TypeDescriptor"));
            var start = plugin.GetType().GetMethod(
                "Start",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("TypeDescriptorPropertyAccessorPlugin.Start was not found.");

            return start.Invoke(plugin, [new WeakReference<object?>(target), path]) as IPropertyAccessor
                ?? throw new InvalidOperationException("TypeDescriptorPropertyAccessorPlugin did not return an accessor.");
        }

        public static void EnsureDataValidator(string typeName)
        {
            var validators = GetBindingPluginsCollection("DataValidators");
            if (validators.Cast<object>().Any(plugin => plugin.GetType().Name == typeName))
            {
                return;
            }

            var bindingPluginsType = GetBindingPluginsType();
            var type = bindingPluginsType.Assembly.GetType($"Avalonia.Data.Core.Plugins.{typeName}")
                ?? throw new InvalidOperationException($"Unable to locate {typeName}.");
            var instance = Activator.CreateInstance(type, nonPublic: true)
                ?? throw new InvalidOperationException($"Unable to instantiate {typeName}.");
            validators.Add(instance);
        }

        public static void ResizeWindow(Window window, double? width = null, double? height = null)
        {
            var targetWidth = width ?? (window.Bounds.Width > 0 ? window.Bounds.Width : window.ClientSize.Width);
            if (targetWidth <= 0 || double.IsNaN(targetWidth) || double.IsInfinity(targetWidth))
            {
                targetWidth = window.Width;
            }

            var targetHeight = height ?? (window.Bounds.Height > 0 ? window.Bounds.Height : window.ClientSize.Height);
            if (targetHeight <= 0 || double.IsNaN(targetHeight) || double.IsInfinity(targetHeight))
            {
                targetHeight = window.Height;
            }

            window.Width = targetWidth;
            window.Height = targetHeight;

            var platformImpl = window.PlatformImpl;
            if (platformImpl != null)
            {
                var resize = platformImpl.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(method =>
                        method.Name.EndsWith("Resize", StringComparison.Ordinal) &&
                        method.GetParameters() is var parameters &&
                        parameters.Length == 2 &&
                        parameters[0].ParameterType == typeof(Size) &&
                        parameters[1].ParameterType == typeof(WindowResizeReason));

                resize?.Invoke(platformImpl, [new Size(targetWidth, targetHeight), WindowResizeReason.Application]);
            }

            var handleResized = window.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                    method.Name.EndsWith("HandleResized", StringComparison.Ordinal) &&
                    method.GetParameters() is var parameters &&
                    parameters.Length == 2 &&
                    parameters[0].ParameterType == typeof(Size) &&
                    parameters[1].ParameterType == typeof(WindowResizeReason));

            handleResized?.Invoke(window, [new Size(targetWidth, targetHeight), WindowResizeReason.Application]);
        }

        private static IList GetBindingPluginsCollection(string propertyName)
        {
            var bindingPluginsType = GetBindingPluginsType();
            var property = bindingPluginsType.GetProperty(
                propertyName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"BindingPlugins.{propertyName} was not found.");

            return property.GetValue(null) as IList
                ?? throw new InvalidOperationException($"BindingPlugins.{propertyName} is not an IList.");
        }

        private static Type GetBindingPluginsType()
        {
            return typeof(IPropertyAccessor).Assembly.GetType("Avalonia.Data.Core.Plugins.BindingPlugins")
                ?? throw new InvalidOperationException("BindingPlugins type was not found.");
        }
    }
}

namespace Avalonia.Controls
{
    internal static class VisualRootCompatibilityExtensions
    {
        public static TopLevel? GetVisualRoot(this Visual visual)
        {
            return TopLevel.GetTopLevel(visual);
        }
    }

    internal static class TopLevelInputCompatibilityExtensions
    {
        public static void SetPointerOverElementForTests(this TopLevel topLevel, IInputElement? element)
        {
            InputRootCompatibilityExtensions.SetPointerOverElementForTestsCore(topLevel, element);
        }
    }
}

namespace Avalonia.Input
{
    internal static class InputRootCompatibilityExtensions
    {
        public static void SetPointerOverElementForTests(this IInputRoot inputRoot, IInputElement? element)
        {
            SetPointerOverElementForTestsCore(inputRoot, element);
        }

        internal static void SetPointerOverElementForTestsCore(object inputRoot, IInputElement? element)
        {
            if (inputRoot is IInputRoot directInputRoot)
            {
                var interfaceProperty = typeof(IInputRoot).GetProperty(
                    "PointerOverElement",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (interfaceProperty?.CanWrite == true)
                {
                    interfaceProperty.SetValue(directInputRoot, element);
                    return;
                }
            }

            var type = inputRoot.GetType();
            var property = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name.EndsWith("PointerOverElement", StringComparison.Ordinal));

            if (property != null)
            {
                property.SetValue(inputRoot, element);
                return;
            }

            var inputRootProperty = type.GetProperty(
                "InputRoot",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("PointerOverElement property was not found.");
            var nestedInputRoot = inputRootProperty.GetValue(inputRoot)
                ?? throw new InvalidOperationException("InputRoot property was null.");

            SetPointerOverElementForTestsCore(nestedInputRoot, element);
        }
    }
}

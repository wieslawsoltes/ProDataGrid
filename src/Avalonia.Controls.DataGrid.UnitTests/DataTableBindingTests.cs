using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Core.Plugins;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests
{
    public class DataTableBindingTests
    {
        public DataTableBindingTests()
        {
            DataGridTypeDescriptorPlugin.EnsureRegistered();
        }

        private static IPropertyAccessor StartAccessor(object target, string path)
        {
            var plugin = BindingPlugins.PropertyAccessors.First(p => p.GetType().Name.Contains("TypeDescriptor"));
            return plugin.Start(new WeakReference<object?>(target), path)
                   ?? throw new InvalidOperationException("TypeDescriptorPropertyAccessorPlugin did not return an accessor.");
        }

        [Theory]
        [InlineData("Name")]
        public void TypeDescriptorPlugin_Reads_DataRowView(string path)
        {
            var table = new DataTable();
            table.Columns.Add(new DataColumn("Name", typeof(string)));
            table.Rows.Add("Alice");

            var row = table.DefaultView[0];

            using var accessor = StartAccessor(row, path);
            Assert.Equal("Alice", accessor.Value);

            var textBlock = new TextBlock { DataContext = row };
            textBlock.Bind(TextBlock.TextProperty, new Binding(path));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Alice", textBlock.Text);
        }

        [Fact]
        public void TypeDescriptorPlugin_Writes_DataRowView()
        {
            var table = new DataTable();
            table.Columns.Add(new DataColumn("Name", typeof(string)));
            table.Rows.Add("Alice");

            var row = table.DefaultView[0];

            using var accessor = StartAccessor(row, "Name");
            Assert.True(accessor.SetValue("Bob", BindingPriority.LocalValue));

            Assert.Equal("Bob", row["Name"]);
            Assert.Equal("Bob", accessor.Value);
        }

        [Fact]
        public void TypeDescriptorPlugin_Notifies_On_Change()
        {
            var table = new DataTable();
            table.Columns.Add(new DataColumn("Name", typeof(string)));
            table.Rows.Add("Alice");

            var row = table.DefaultView[0];

            using var accessor = StartAccessor(row, "Name");
            var values = new List<object?>();
            accessor.Subscribe(v => values.Add(v));

            // Initial push
            Assert.Equal("Alice", values.Last());

            row["Name"] = "Charlie";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Charlie", values.Last());
        }
    }
}

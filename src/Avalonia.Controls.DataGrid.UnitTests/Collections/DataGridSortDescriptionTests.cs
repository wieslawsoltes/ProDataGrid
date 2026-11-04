using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Collections
{

    public class DataGridSortDescriptionTests
    {
        [Fact]
        public void OrderBy_Orders_Correctly_When_Ascending()
        {
            var items = new[]
            {
                new Item("b", "b"),
                new Item("a", "a"),
                new Item("c", "c"),
            };
            var expectedResult = items.OrderBy(i => i.Prop1).ToList();
            var sortDescription = DataGridSortDescription.FromPath(nameof(Item.Prop1), ListSortDirection.Ascending);
            
            sortDescription.Initialize(typeof(Item));
            var result = sortDescription.OrderBy(items).ToList();
            
            Assert.Equal(expectedResult, result);
        }
        
        [Fact]
        public void OrderBy_Orders_Correctly_When_Descending()
        {
            var items = new[]
            {
                new Item("b", "b"),
                new Item("a", "a"),
                new Item("c", "c"),
            };
            var expectedResult = items.OrderByDescending(i => i.Prop1).ToList();
            var sortDescription = DataGridSortDescription.FromPath(nameof(Item.Prop1), ListSortDirection.Descending);
            
            sortDescription.Initialize(typeof(Item));
            var result = sortDescription.OrderBy(items).ToList();
            
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void ThenBy_Orders_Correctly_When_Ascending()
        {
            // Casting nonsense below because IOrderedEnumerable<T> isn't covariant in full framework and we need an
            // object of type IOrderedEnumerable<object> for DataGridSortDescription.ThenBy
            var items = new[]
            {
                (object)new Item("a", "b"),
                        new Item("a", "a"),
                        new Item("a", "c"), 
            }.OrderBy(i => ((Item)i).Prop1);
            var expectedResult = new[]
            {
                new Item("a", "a"),
                new Item("a", "b"),
                new Item("a", "c"),
            };
            var sortDescription = DataGridSortDescription.FromPath(nameof(Item.Prop2), ListSortDirection.Ascending);
            
            sortDescription.Initialize(typeof(Item));
            var result = sortDescription.ThenBy(items).ToList();
            
            Assert.Equal(expectedResult, result);
        }
        
        [Fact]
        public void ThenBy_Orders_Correctly_When_Descending()
        {
            // Casting nonsense below because IOrderedEnumerable<T> isn't covariant in full framework and we need an
            // object of type IOrderedEnumerable<object> for DataGridSortDescription.ThenBy
            var items = new[]
            {
                (object)new Item("a", "b"),
                        new Item("a", "a"),
                        new Item("a", "c"), 
            }.OrderBy(i => ((Item)i).Prop1);
            var expectedResult = new[]
            {
                new Item("a", "c"),
                new Item("a", "b"),
                new Item("a", "a"),
            };
            var sortDescription = DataGridSortDescription.FromPath(nameof(Item.Prop2), ListSortDirection.Descending);
            
            sortDescription.Initialize(typeof(Item));
            var result = sortDescription.ThenBy(items).ToList();
            
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void FromAccessor_Orders_Correctly_When_Ascending()
        {
            var items = new[]
            {
                new Item("b", "b"),
                new Item("a", "a"),
                new Item("c", "c"),
            };
            var expectedResult = items.OrderBy(i => i.Prop1).ToList();
            var accessor = new DataGridColumnValueAccessor<Item, string>(i => i.Prop1);
            var sortDescription = DataGridSortDescription.FromAccessor(accessor, ListSortDirection.Ascending);

            var result = sortDescription.OrderBy(items).ToList();

            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void FromAccessor_Preserves_PropertyPath()
        {
            const string propertyPath = nameof(Item.Prop1);
            var accessor = new DataGridColumnValueAccessor<Item, string>(i => i.Prop1);
            var sortDescription = DataGridSortDescription.FromAccessor(accessor, ListSortDirection.Descending, null, propertyPath);

            var comparerSort = Assert.IsType<DataGridComparerSortDescription>(sortDescription);

            Assert.Equal(propertyPath, comparerSort.PropertyPath);
        }

        [Fact]
        public void FromAccessor_Uses_Typed_Comparer_For_ValueType()
        {
            IDataGridColumnValueAccessor<NumericItem, int> accessor =
                new DataGridColumnValueAccessor<NumericItem, int>(i => i.Value);
            var sortDescription = DataGridSortDescription.FromAccessor(accessor, ListSortDirection.Ascending);

            var comparerSort = Assert.IsType<DataGridComparerSortDescription>(sortDescription);
            Assert.IsType<DataGridColumnValueAccessorComparer<NumericItem, int>>(comparerSort.SourceComparer);
        }

        [Theory]
        [InlineData(ListSortDirection.Ascending, "a", "b", "c")]
        [InlineData(ListSortDirection.Descending, "c", "b", "a")]
        public void FromAccessor_Extracts_Each_Sort_Key_Once(
            ListSortDirection direction,
            params string[] expected)
        {
            var items = new[]
            {
                new Item("b", "1"),
                new Item("a", "2"),
                new Item("c", "3"),
            };
            int getterCalls = 0;
            var accessor = new DataGridColumnValueAccessor<Item, string>(item =>
            {
                getterCalls++;
                return item.Prop1;
            });
            var sortDescription = DataGridSortDescription.FromAccessor(accessor, direction);

            string?[] result = sortDescription.OrderBy(items).Cast<Item>().Select(item => item.Prop1).ToArray();

            Assert.Equal(expected, result);
            Assert.Equal(items.Length, getterCalls);
        }

        [Fact]
        public void FromAccessor_ThenBy_Extracts_Each_Secondary_Key_Once()
        {
            var items = new[]
            {
                (object)new Item("a", "b"),
                new Item("a", "a"),
                new Item("a", "c"),
            }.OrderBy(item => ((Item)item).Prop1);
            int getterCalls = 0;
            var accessor = new DataGridColumnValueAccessor<Item, string>(item =>
            {
                getterCalls++;
                return item.Prop2;
            });
            var sortDescription = DataGridSortDescription.FromAccessor(accessor);

            string?[] result = sortDescription.ThenBy(items).Cast<Item>().Select(item => item.Prop2).ToArray();

            Assert.Equal(new[] { "a", "b", "c" }, result);
            Assert.Equal(3, getterCalls);
        }

        [Theory]
        [InlineData(ListSortDirection.Ascending, "Alice", "Bob", "Charlie")]
        [InlineData(ListSortDirection.Descending, "Charlie", "Bob", "Alice")]
        public void FromPath_Orders_Explicit_Interface_Property(
            ListSortDirection direction,
            params string[] expected)
        {
            IExplicitRow[] items =
            {
                new ExplicitRow("Charlie", 3),
                new ExplicitRow("Alice", 1),
                new ExplicitRow("Bob", 2)
            };
            var sortDescription = DataGridSortDescription.FromPath(nameof(IExplicitRow.Name), direction);

            sortDescription.Initialize(typeof(IExplicitRow));
            var result = sortDescription.OrderBy(items).Cast<IExplicitRow>().Select(item => item.Name).ToArray();

            Assert.Equal(expected, result);
        }

        [Fact]
        public void FromPath_Orders_Explicit_Interface_Property_When_Initialized_With_Concrete_Type()
        {
            var items = new[]
            {
                new ExplicitRow("Charlie", 3),
                new ExplicitRow("Alice", 1),
                new ExplicitRow("Bob", 2)
            };
            var sortDescription = DataGridSortDescription.FromPath(nameof(IExplicitRow.Name));

            sortDescription.Initialize(typeof(ExplicitRow));
            var result = sortDescription.OrderBy(items).Cast<IExplicitRow>().Select(item => item.Name).ToArray();

            Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, result);
        }

        [Fact]
        public void FromPath_Orders_Explicit_Interface_Property_Without_Initialize()
        {
            var items = new[]
            {
                new ExplicitRow("Charlie", 3),
                new ExplicitRow("Alice", 1),
                new ExplicitRow("Bob", 2)
            };
            var sortDescription = DataGridSortDescription.FromPath(nameof(IExplicitRow.Name));

            var result = sortDescription.OrderBy(items).Cast<IExplicitRow>().Select(item => item.Name).ToArray();

            Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, result);
        }

        [Fact]
        public void FromPath_Orders_Inherited_Explicit_Interface_Property()
        {
            IInheritedRow[] items =
            {
                new InheritedRow("Charlie"),
                new InheritedRow("Alice"),
                new InheritedRow("Bob")
            };
            var sortDescription = DataGridSortDescription.FromPath(nameof(IBaseRow.Name));

            sortDescription.Initialize(typeof(IInheritedRow));
            var result = sortDescription.OrderBy(items).Cast<IBaseRow>().Select(item => item.Name).ToArray();

            Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, result);
        }

        [Fact]
        public void FromPath_Orders_Nested_Explicit_Interface_Path_And_Nulls()
        {
            INestedRow[] items =
            {
                new NestedRow(new ExplicitRow("Charlie", 3)),
                new NestedRow(null),
                new NestedRow(new ExplicitRow("Alice", 1)),
                new NestedRow(new ExplicitRow("Bob", 2))
            };
            var sortDescription = DataGridSortDescription.FromPath(
                $"{nameof(INestedRow.Detail)}.{nameof(IExplicitRow.Rank)}");

            sortDescription.Initialize(typeof(INestedRow));
            var result = sortDescription.OrderBy(items).Cast<INestedRow>().Select(item => item.Detail?.Rank).ToArray();

            Assert.Equal(new int?[] { null, 1, 2, 3 }, result);
        }

        [Fact]
        public void FromPath_Orders_Polymorphic_Explicit_Interface_Implementations()
        {
            IExplicitRow[] items =
            {
                new AlternateExplicitRow("Charlie", 3),
                new ExplicitRow("Alice", 1),
                new AlternateExplicitRow("Bob", 2)
            };
            var sortDescription = DataGridSortDescription.FromPath(nameof(IExplicitRow.Name));

            sortDescription.Initialize(typeof(IExplicitRow));
            var result = sortDescription.OrderBy(items).Cast<IExplicitRow>().Select(item => item.Name).ToArray();

            Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, result);
        }

        [Fact]
        public void SwitchSortDirection_Preserves_Explicit_Interface_Item_Type()
        {
            IExplicitRow[] items =
            {
                new ExplicitRow("Charlie", 3),
                new ExplicitRow("Alice", 1),
                new ExplicitRow("Bob", 2)
            };
            var ascending = DataGridSortDescription.FromPath(nameof(IExplicitRow.Name));
            ascending.Initialize(typeof(IExplicitRow));

            var descending = ascending.SwitchSortDirection();
            var result = descending.OrderBy(items).Cast<IExplicitRow>().Select(item => item.Name).ToArray();

            Assert.Equal(new[] { "Charlie", "Bob", "Alice" }, result);
        }

        [Fact]
        public void FromPath_Uses_Declared_Interface_To_Disambiguate_Explicit_Properties()
        {
            IPrimaryLabel[] items =
            {
                new AmbiguousLabel("Charlie", "Alpha"),
                new AmbiguousLabel("Alice", "Zulu"),
                new AmbiguousLabel("Bob", "Mike")
            };
            var sortDescription = DataGridSortDescription.FromPath(nameof(IPrimaryLabel.Label));

            sortDescription.Initialize(typeof(IPrimaryLabel));
            var result = sortDescription.OrderBy(items).Cast<IPrimaryLabel>().Select(item => item.Label).ToArray();

            Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, result);
        }

        [Fact]
        public void FromPath_Falls_Back_To_Runtime_Type_When_Declared_Base_Type_Has_No_Path()
        {
            RuntimeBaseRow[] items =
            {
                new RuntimeDerivedRow("Charlie"),
                new RuntimeDerivedRow("Alice"),
                new RuntimeDerivedRow("Bob")
            };
            var sortDescription = DataGridSortDescription.FromPath(nameof(RuntimeDerivedRow.Name));

            sortDescription.Initialize(typeof(RuntimeBaseRow));
            var result = sortDescription.OrderBy(items).Cast<RuntimeDerivedRow>().Select(item => item.Name).ToArray();

            Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, result);
        }

        private class Item : IEquatable<Item>
        {
            public Item(string? prop1, string? prop2)
            {
                Prop1 = prop1;
                Prop2 = prop2;
            }

            public string? Prop1 { get; }
            public string? Prop2 { get; }

            public bool Equals(Item? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Prop1 == other.Prop1 && Prop2 == other.Prop2;
            }

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Item) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Prop1 != null ? Prop1.GetHashCode() : 0) * 397) ^ (Prop2 != null ? Prop2.GetHashCode() : 0);
                }
            }
        }

        private class NumericItem
        {
            public NumericItem(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private interface IExplicitRow
        {
            string Name { get; }

            int Rank { get; }
        }

        private interface IBaseRow
        {
            string Name { get; }
        }

        private interface IInheritedRow : IBaseRow
        {
        }

        private interface INestedRow
        {
            IExplicitRow? Detail { get; }
        }

        private interface IPrimaryLabel
        {
            string Label { get; }
        }

        private interface ISecondaryLabel
        {
            string Label { get; }
        }

        private sealed class ExplicitRow : IExplicitRow
        {
            private readonly string _name;
            private readonly int _rank;

            public ExplicitRow(string name, int rank)
            {
                _name = name;
                _rank = rank;
            }

            string IExplicitRow.Name => _name;

            int IExplicitRow.Rank => _rank;
        }

        private sealed class AlternateExplicitRow : IExplicitRow
        {
            private readonly string _name;
            private readonly int _rank;

            public AlternateExplicitRow(string name, int rank)
            {
                _name = name;
                _rank = rank;
            }

            string IExplicitRow.Name => _name;

            int IExplicitRow.Rank => _rank;
        }

        private sealed class InheritedRow : IInheritedRow
        {
            private readonly string _name;

            public InheritedRow(string name)
            {
                _name = name;
            }

            string IBaseRow.Name => _name;
        }

        private sealed class NestedRow : INestedRow
        {
            private readonly IExplicitRow? _detail;

            public NestedRow(IExplicitRow? detail)
            {
                _detail = detail;
            }

            IExplicitRow? INestedRow.Detail => _detail;
        }

        private sealed class AmbiguousLabel : IPrimaryLabel, ISecondaryLabel
        {
            private readonly string _primary;
            private readonly string _secondary;

            public AmbiguousLabel(string primary, string secondary)
            {
                _primary = primary;
                _secondary = secondary;
            }

            string IPrimaryLabel.Label => _primary;

            string ISecondaryLabel.Label => _secondary;
        }

        private abstract class RuntimeBaseRow
        {
        }

        private sealed class RuntimeDerivedRow : RuntimeBaseRow
        {
            public RuntimeDerivedRow(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }
    }
}

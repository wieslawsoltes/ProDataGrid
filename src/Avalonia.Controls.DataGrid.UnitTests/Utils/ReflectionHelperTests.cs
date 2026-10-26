
using Avalonia.Controls.Utils;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Utils
{
    public class ReflectionHelperTests
    {
        [Fact]
        public void SplitPropertyPath_Splits_PropertyPath_With_Cast()
        {
            var path = "(Type).Property";
            var expected = new [] { "Property" };

            var result = TypeHelper.SplitPropertyPath(path);
            
            Assert.Equal(expected, result);
        }

        [Fact]
        public void SplitPropertyPath_Splits_Indexers_And_Nested_Properties()
        {
            var path = "Root.Children[0].Name";
            var expected = new[] { "Root", "Children", "[0]", "Name" };

            var result = TypeHelper.SplitPropertyPath(path);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void SplitPropertyPath_Splits_Multiple_Indexers()
        {
            var path = "Matrix[1][2]";
            var expected = new[] { "Matrix", "[1]", "[2]" };

            var result = TypeHelper.SplitPropertyPath(path);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetPropertyOrIndexer_Prefers_Int_Indexer()
        {
            var type = typeof(IndexerType);

            var property = type.GetPropertyOrIndexer("[4]", out var index);

            Assert.NotNull(property);
            Assert.Equal("Item", property!.Name);
            Assert.Equal(new object[] { 4 }, index);
        }

        [Fact]
        public void GetPropertyOrIndexer_Returns_String_Indexer_When_No_Int()
        {
            var type = typeof(StringIndexerType);

            var property = type.GetPropertyOrIndexer("[key]", out var index);

            Assert.NotNull(property);
            Assert.Equal("Item", property!.Name);
            Assert.Equal(new object[] { "key" }, index);
        }

        [Fact]
        public void GetPropertyOrIndexer_Finds_Inherited_Interface_Property()
        {
            var type = typeof(IChildInterface);

            var property = type.GetPropertyOrIndexer(nameof(IParentInterface.Name), out var index);

            Assert.NotNull(property);
            Assert.Null(index);
            Assert.Equal(nameof(IParentInterface.Name), property!.Name);
        }

        [Fact]
        public void GetPropertyOrIndexer_Finds_Explicit_Interface_Property_On_Class()
        {
            var property = typeof(ExplicitInterfaceItem).GetPropertyOrIndexer(
                nameof(IExplicitInterfaceItem.Name),
                out var index);

            Assert.NotNull(property);
            Assert.Null(index);
            Assert.Equal(typeof(IExplicitInterfaceItem), property!.DeclaringType);
        }

        [Fact]
        public void GetPropertyOrIndexer_Finds_Inherited_Explicit_Interface_Property_On_Class()
        {
            var property = typeof(InheritedExplicitInterfaceItem).GetPropertyOrIndexer(
                nameof(IParentInterface.Name),
                out var index);

            Assert.NotNull(property);
            Assert.Null(index);
            Assert.Equal(typeof(IParentInterface), property!.DeclaringType);
        }

        [Fact]
        public void GetPropertyOrIndexer_Prefers_Public_Class_Property_Over_Interface_Property()
        {
            var property = typeof(PublicAndExplicitInterfaceItem).GetPropertyOrIndexer(
                nameof(IExplicitInterfaceItem.Name),
                out var index);

            Assert.NotNull(property);
            Assert.Null(index);
            Assert.Equal(typeof(PublicAndExplicitInterfaceItem), property!.DeclaringType);
        }

        [Fact]
        public void GetPropertyOrIndexer_Prefers_Most_Derived_Interface_Property()
        {
            var property = typeof(RedeclaredExplicitInterfaceItem).GetPropertyOrIndexer(
                nameof(IRedeclaredChildInterface.Value),
                out var index);

            Assert.NotNull(property);
            Assert.Null(index);
            Assert.Equal(typeof(IRedeclaredChildInterface), property!.DeclaringType);
            Assert.Equal(typeof(string), property.PropertyType);
        }

        [Fact]
        public void GetPropertyOrIndexer_Uses_Diamond_Interface_Redeclaration_To_Disambiguate_Property()
        {
            var property = typeof(DiamondExplicitInterfaceItem).GetPropertyOrIndexer(
                nameof(IDiamondInterface.Value),
                out var index);

            Assert.NotNull(property);
            Assert.Null(index);
            Assert.Equal(typeof(IDiamondInterface), property!.DeclaringType);
        }

        [Fact]
        public void GetPropertyOrIndexer_Returns_Null_For_Unrelated_Ambiguous_Interface_Properties()
        {
            var property = typeof(AmbiguousExplicitInterfaceItem).GetPropertyOrIndexer("Value", out var index);

            Assert.Null(property);
            Assert.Null(index);
        }

        [Fact]
        public void GetPropertyOrIndexer_Finds_Explicit_Interface_Indexer_On_Class()
        {
            var property = typeof(ExplicitInterfaceIndexer).GetPropertyOrIndexer("[2]", out var index);

            Assert.NotNull(property);
            Assert.Equal(typeof(IExplicitInterfaceIndexer), property!.DeclaringType);
            Assert.Equal(new object[] { 2 }, index);
        }

        [Fact]
        public void GetPropertyOrIndexer_Uses_Diamond_Interface_Redeclaration_To_Disambiguate_Indexer()
        {
            var property = typeof(DiamondExplicitInterfaceIndexer).GetPropertyOrIndexer("[2]", out var index);

            Assert.NotNull(property);
            Assert.Equal(typeof(IDiamondIndexer), property!.DeclaringType);
            Assert.Equal(new object[] { 2 }, index);
        }

        [Fact]
        public void GetNestedProperty_Resolves_Indexer_Path()
        {
            var model = new ParentWithChildren
            {
                Children = new[]
                {
                    new Child { Name = "first" },
                    new Child { Name = "second" }
                }
            };

            object? item = model;
            var property = typeof(ParentWithChildren).GetNestedProperty("Children[1].Name", ref item);

            Assert.NotNull(property);
            Assert.Equal("Name", property!.Name);
            Assert.Equal("second", item);
        }

        [Fact]
        public void GetNestedPropertyValue_Returns_Nested_Value()
        {
            var model = new Parent
            {
                Child = new Child { Name = "alpha" }
            };

            var value = TypeHelper.GetNestedPropertyValue(model, "Child.Name");

            Assert.Equal("alpha", value);
        }

        [Fact]
        public void GetNestedPropertyValue_Uses_Runtime_Type_For_Object_Path()
        {
            var model = new ObjectParent
            {
                Child = new ObjectChild { Name = "beta" }
            };

            var value = TypeHelper.GetNestedPropertyValue(model, "Child.Name");

            Assert.Equal("beta", value);
        }

        [Fact]
        public void GetNestedPropertyValue_Reads_Explicit_Interface_Property()
        {
            IExplicitInterfaceItem model = new ExplicitInterfaceItem("alpha");

            var value = TypeHelper.GetNestedPropertyValue(model, nameof(IExplicitInterfaceItem.Name));

            Assert.Equal("alpha", value);
        }

        [Fact]
        public void GetNestedPropertyValue_Reads_Nested_Explicit_Interface_Properties()
        {
            INestedExplicitContainer model = new NestedExplicitContainer(new ExplicitInterfaceItem("nested"));

            var value = TypeHelper.GetNestedPropertyValue(
                model,
                $"{nameof(INestedExplicitContainer.Item)}.{nameof(IExplicitInterfaceItem.Name)}");

            Assert.Equal("nested", value);
        }

        [Fact]
        public void GetNestedPropertyValue_Reads_Explicit_Interface_Indexer()
        {
            IExplicitInterfaceIndexer model = new ExplicitInterfaceIndexer();

            var value = TypeHelper.GetNestedPropertyValue(model, "[3]");

            Assert.Equal("item-3", value);
        }

        [Fact]
        public void TrySetNestedPropertyValue_Writes_Explicit_Interface_Property()
        {
            var model = new WritableExplicitInterfaceItem();

            var result = TypeHelper.TrySetNestedPropertyValue(
                model,
                nameof(IWritableExplicitInterfaceItem.Value),
                42,
                out var exception);

            Assert.True(result);
            Assert.Null(exception);
            Assert.Equal(42, ((IWritableExplicitInterfaceItem)model).Value);
        }

        [Fact]
        public void GetNestedPropertyValue_Returns_Null_For_Mismatched_Type()
        {
            var model = new Parent
            {
                Child = new Child { Name = "alpha" }
            };

            var value = TypeHelper.GetNestedPropertyValue(model, "Child.Name", typeof(int), out var exception);

            Assert.Null(value);
            Assert.Null(exception);
        }

        [Fact]
        public void GetNestedPropertyValue_Sets_Exception_For_WriteOnly_Property()
        {
            var model = new WriteOnlyParent();

            var value = TypeHelper.GetNestedPropertyValue(model, "WriteOnlyValue", typeof(int), out var exception);

            Assert.Null(value);
            Assert.NotNull(exception);
        }

        [Fact]
        public void GetNestedPropertyType_Returns_Nested_Type()
        {
            var type = typeof(Parent);

            var nestedType = type.GetNestedPropertyType("Child.Name");

            Assert.Equal(typeof(string), nestedType);
        }

        [Fact]
        public void GetNestedPropertyType_Returns_Parent_For_Null_Path()
        {
            var type = typeof(Parent);

            var nestedType = type.GetNestedPropertyType(null);

            Assert.Equal(typeof(Parent), nestedType);
        }

        [Fact]
        public void PrependDefaultMemberName_And_RemoveDefaultMemberName_Handle_Indexers()
        {
            var item = new IndexerType();

            var prepended = TypeHelper.PrependDefaultMemberName(item, "[5]");
            var removed = TypeHelper.RemoveDefaultMemberName("Item[5]");

            Assert.Equal("Item[5]", prepended);
            Assert.Equal("[5]", removed);
        }

        [Fact]
        public void GetIsReadOnly_Reflects_Attribute()
        {
            var readOnlyProperty = typeof(ReadOnlyContainer).GetProperty(nameof(ReadOnlyContainer.ReadOnlyValue));
            var writableProperty = typeof(ReadOnlyContainer).GetProperty(nameof(ReadOnlyContainer.WritableValue));

            Assert.True(readOnlyProperty.GetIsReadOnly());
            Assert.False(writableProperty.GetIsReadOnly());
        }

        [Fact]
        public void GetItemType_Uses_Generic_Enumerable_Type()
        {
            var list = new List<int> { 1, 2, 3 };

            var itemType = list.GetItemType();

            Assert.Equal(typeof(int), itemType);
        }

        [Fact]
        public void GetItemType_Uses_First_Element_For_Non_Generic_Enumerable()
        {
            var list = new ArrayList { "first", "second" };

            var itemType = list.GetItemType();

            Assert.Equal(typeof(string), itemType);
        }

        [Fact]
        public void GetItemType_Returns_Null_For_Empty_Non_Generic_Enumerable()
        {
            var list = new ArrayList();

            var itemType = list.GetItemType();

            Assert.Null(itemType);
        }

        [Fact]
        public void GetDisplayName_Uses_DisplayAttribute_ShortName()
        {
            var shortName = typeof(DisplayNameParent).GetDisplayName("Child.Label");

            Assert.Equal("ShortLabel", shortName);
        }

        private class IndexerType
        {
            public string this[int index] => $"int-{index}";

            public string this[string key] => $"string-{key}";
        }

        private class StringIndexerType
        {
            public string this[string key] => $"string-{key}";
        }

        private interface IParentInterface
        {
            string Name { get; }
        }

        private interface IChildInterface : IParentInterface
        {
        }

        private interface IExplicitInterfaceItem
        {
            string Name { get; }
        }

        private interface INestedExplicitContainer
        {
            IExplicitInterfaceItem Item { get; }
        }

        private interface IWritableExplicitInterfaceItem
        {
            int Value { get; set; }
        }

        private interface IRedeclaredParentInterface
        {
            object Value { get; }
        }

        private interface IRedeclaredChildInterface : IRedeclaredParentInterface
        {
            new string Value { get; }
        }

        private interface IAmbiguousLeftInterface
        {
            string Value { get; }
        }

        private interface IAmbiguousRightInterface
        {
            string Value { get; }
        }

        private interface IDiamondInterface : IAmbiguousLeftInterface, IAmbiguousRightInterface
        {
            new string Value { get; }
        }

        private interface IExplicitInterfaceIndexer
        {
            string this[int index] { get; }
        }

        private interface ILeftExplicitInterfaceIndexer
        {
            string this[int index] { get; }
        }

        private interface IRightExplicitInterfaceIndexer
        {
            string this[int index] { get; }
        }

        private interface IDiamondIndexer : ILeftExplicitInterfaceIndexer, IRightExplicitInterfaceIndexer
        {
            new string this[int index] { get; }
        }

        private sealed class ExplicitInterfaceItem : IExplicitInterfaceItem
        {
            private readonly string _name;

            public ExplicitInterfaceItem(string name)
            {
                _name = name;
            }

            string IExplicitInterfaceItem.Name => _name;
        }

        private sealed class InheritedExplicitInterfaceItem : IChildInterface
        {
            string IParentInterface.Name => "inherited";
        }

        private sealed class PublicAndExplicitInterfaceItem : IExplicitInterfaceItem
        {
            public string Name => "public";

            string IExplicitInterfaceItem.Name => "explicit";
        }

        private sealed class NestedExplicitContainer : INestedExplicitContainer
        {
            private readonly IExplicitInterfaceItem _item;

            public NestedExplicitContainer(IExplicitInterfaceItem item)
            {
                _item = item;
            }

            IExplicitInterfaceItem INestedExplicitContainer.Item => _item;
        }

        private sealed class WritableExplicitInterfaceItem : IWritableExplicitInterfaceItem
        {
            int IWritableExplicitInterfaceItem.Value { get; set; }
        }

        private sealed class RedeclaredExplicitInterfaceItem : IRedeclaredChildInterface
        {
            object IRedeclaredParentInterface.Value => "parent";

            string IRedeclaredChildInterface.Value => "child";
        }

        private sealed class AmbiguousExplicitInterfaceItem : IAmbiguousLeftInterface, IAmbiguousRightInterface
        {
            string IAmbiguousLeftInterface.Value => "left";

            string IAmbiguousRightInterface.Value => "right";
        }

        private sealed class DiamondExplicitInterfaceItem : IDiamondInterface
        {
            string IAmbiguousLeftInterface.Value => "left";

            string IAmbiguousRightInterface.Value => "right";

            string IDiamondInterface.Value => "diamond";
        }

        private sealed class ExplicitInterfaceIndexer : IExplicitInterfaceIndexer
        {
            string IExplicitInterfaceIndexer.this[int index] => $"item-{index}";
        }

        private sealed class DiamondExplicitInterfaceIndexer : IDiamondIndexer
        {
            string ILeftExplicitInterfaceIndexer.this[int index] => $"left-{index}";

            string IRightExplicitInterfaceIndexer.this[int index] => $"right-{index}";

            string IDiamondIndexer.this[int index] => $"diamond-{index}";
        }

        private class ParentWithChildren
        {
            public IReadOnlyList<Child>? Children { get; set; }
        }

        private class Parent
        {
            public Child? Child { get; set; }
        }

        private class Child
        {
            public string? Name { get; set; }
        }

        private class ObjectParent
        {
            public object? Child { get; set; }
        }

        private class ObjectChild
        {
            public string? Name { get; set; }
        }

        private class WriteOnlyParent
        {
            public int WriteOnlyValue
            {
                set { }
            }
        }

        private class ReadOnlyContainer
        {
            [ReadOnly(true)]
            public int ReadOnlyValue { get; set; }

            public int WritableValue { get; set; }
        }

        private class DisplayNameParent
        {
            public DisplayNameChild? Child { get; set; }
        }

        private class DisplayNameChild
        {
            [Display(Name = "LongName", ShortName = "ShortLabel")]
            public string? Label { get; set; }
        }

        [Fact]
        public void GetNestedPropertyValue_Resolves_Explicit_Generic_ReadOnlyList_Indexer()
        {
            var list = new ExplicitReadOnlyList<string>(new[] { "a", "b" });

            var value = TypeHelper.GetNestedPropertyValue(list, "[1]");

            Assert.Equal("b", value);
        }

        private sealed class ExplicitReadOnlyList<T> : IReadOnlyList<T>
        {
            private readonly T[] _items;

            public ExplicitReadOnlyList(T[] items)
            {
                _items = items;
            }

            T IReadOnlyList<T>.this[int index] => _items[index];

            int IReadOnlyCollection<T>.Count => _items.Length;

            IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
        }
    }
}

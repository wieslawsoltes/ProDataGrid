using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Filtering;

public class FilteringAdapterBranchCoverageTests
{
    public static IEnumerable<object[]> AdapterTypes()
    {
        yield return new object[] { typeof(DataGridFilteringAdapter) };
        yield return new object[] { typeof(DataGridAccessorFilteringAdapter) };
    }

    [Theory]
    [MemberData(nameof(AdapterTypes))]
    public void Static_String_And_Collection_Helpers_Cover_Branches(Type adapterType)
    {
        Assert.False(InvokeStatic<bool>(adapterType, "Contains", null, "x", null));
        Assert.False(InvokeStatic<bool>(adapterType, "Contains", "abc", null, null));
        Assert.True(InvokeStatic<bool>(adapterType, "Contains", "Alpha", "PH", StringComparison.OrdinalIgnoreCase));
        Assert.False(InvokeStatic<bool>(adapterType, "Contains", "Alpha", "PH", StringComparison.Ordinal));
        Assert.True(InvokeStatic<bool>(adapterType, "Contains", new[] { 1, 2, 3 }, 2, null));
        Assert.False(InvokeStatic<bool>(adapterType, "Contains", new[] { 1, 2, 3 }, 9, null));

        Assert.True(InvokeStatic<bool>(adapterType, "StartsWith", "Alpha", "Al", StringComparison.Ordinal));
        Assert.False(InvokeStatic<bool>(adapterType, "StartsWith", 1, "1", null));

        Assert.True(InvokeStatic<bool>(adapterType, "EndsWith", "Alpha", "ha", StringComparison.Ordinal));
        Assert.False(InvokeStatic<bool>(adapterType, "EndsWith", 1, "1", null));

        Assert.False(InvokeStatic<bool>(adapterType, "Between", 4, null, CultureInfo.InvariantCulture));
        Assert.False(InvokeStatic<bool>(adapterType, "Between", 4, new object[] { 3 }, CultureInfo.InvariantCulture));
        Assert.False(InvokeStatic<bool>(adapterType, "Between", 2, new object[] { 3, 5 }, CultureInfo.InvariantCulture));
        Assert.False(InvokeStatic<bool>(adapterType, "Between", 8, new object[] { 3, 5 }, CultureInfo.InvariantCulture));
        Assert.True(InvokeStatic<bool>(adapterType, "Between", 4, new object[] { 3, 5 }, CultureInfo.InvariantCulture));

        Assert.False(InvokeStatic<bool>(adapterType, "In", 2, null));
        Assert.False(InvokeStatic<bool>(adapterType, "In", 2, Array.Empty<object>()));
        Assert.True(InvokeStatic<bool>(adapterType, "In", 2, new object[] { 1, 2, 3 }));
        Assert.False(InvokeStatic<bool>(adapterType, "In", 9, new object[] { 1, 2, 3 }));
    }

    [Theory]
    [MemberData(nameof(AdapterTypes))]
    public void Static_TryCompare_Covers_Branches(Type adapterType)
    {
        AssertTryCompare(adapterType, null, null, null, true, 0);
        AssertTryCompare(adapterType, null, 1, null, true, -1);
        AssertTryCompare(adapterType, 1, null, null, true, 1);

        var turkish = CultureInfo.GetCultureInfo("tr-TR");
        var turkishCompare = InvokeTryCompare(adapterType, "i", "I", turkish);
        Assert.True(turkishCompare.Success);

        AssertTryCompare(adapterType, 5, 3, CultureInfo.InvariantCulture, true, 1);
        AssertTryCompare(adapterType, 5L, 4, CultureInfo.InvariantCulture, true, 1);

        var nonAssignableSuccess = InvokeTryCompare(adapterType, new LenientComparable(10), "9", CultureInfo.InvariantCulture);
        Assert.True(nonAssignableSuccess.Success);
        Assert.True(nonAssignableSuccess.Result > 0);

        Assert.False(InvokeTryCompare(adapterType, 5L, "x", CultureInfo.InvariantCulture).Success);
        Assert.False(InvokeTryCompare(adapterType, (byte)5, 1000, CultureInfo.InvariantCulture).Success);
        Assert.False(InvokeTryCompare(adapterType, new InvalidCastComparable(), 1, CultureInfo.InvariantCulture).Success);

        var fallbackSuccessOne = InvokeTryCompare(
            adapterType,
            new NonComparable("a"),
            new NonComparable("b"),
            CultureInfo.InvariantCulture);
        Assert.True(fallbackSuccessOne.Success);

        var fallbackSuccessTwo = InvokeTryCompare(
            adapterType,
            new NonComparable("a"),
            new NonComparable("b"),
            CultureInfo.InvariantCulture);
        Assert.True(fallbackSuccessTwo.Success);

        Assert.False(InvokeTryCompare(adapterType, new object(), new object(), null).Success);
    }

    [Theory]
    [MemberData(nameof(AdapterTypes))]
    public void Static_EvaluateDescriptor_Covers_All_Operator_Branches(Type adapterType)
    {
        Assert.True(InvokeEvaluate(adapterType, 5, new FilteringDescriptor("eq", FilteringOperator.Equals, value: 5)));
        Assert.True(InvokeEvaluate(adapterType, 5, new FilteringDescriptor("neq", FilteringOperator.NotEquals, value: 3)));

        Assert.True(InvokeEvaluate(
            adapterType,
            "Alphabet",
            new FilteringDescriptor("contains", FilteringOperator.Contains, value: "pha", stringComparison: StringComparison.Ordinal)));

        Assert.True(InvokeEvaluate(
            adapterType,
            "Alphabet",
            new FilteringDescriptor("starts", FilteringOperator.StartsWith, value: "Al", stringComparison: StringComparison.Ordinal)));

        Assert.True(InvokeEvaluate(
            adapterType,
            "Alphabet",
            new FilteringDescriptor("ends", FilteringOperator.EndsWith, value: "et", stringComparison: StringComparison.Ordinal)));

        Assert.True(InvokeEvaluate(adapterType, 5, new FilteringDescriptor("gt-true", FilteringOperator.GreaterThan, value: 4)));
        Assert.False(InvokeEvaluate(adapterType, 4, new FilteringDescriptor("gt-false", FilteringOperator.GreaterThan, value: 5)));
        Assert.False(InvokeEvaluate(adapterType, new object(), new FilteringDescriptor("gt-compare-false", FilteringOperator.GreaterThan, value: new object())));

        Assert.True(InvokeEvaluate(adapterType, 5, new FilteringDescriptor("gte-true", FilteringOperator.GreaterThanOrEqual, value: 5)));
        Assert.False(InvokeEvaluate(adapterType, 4, new FilteringDescriptor("gte-false", FilteringOperator.GreaterThanOrEqual, value: 5)));
        Assert.False(InvokeEvaluate(adapterType, new object(), new FilteringDescriptor("gte-compare-false", FilteringOperator.GreaterThanOrEqual, value: new object())));

        Assert.True(InvokeEvaluate(adapterType, 4, new FilteringDescriptor("lt-true", FilteringOperator.LessThan, value: 5)));
        Assert.False(InvokeEvaluate(adapterType, 5, new FilteringDescriptor("lt-false", FilteringOperator.LessThan, value: 4)));
        Assert.False(InvokeEvaluate(adapterType, new object(), new FilteringDescriptor("lt-compare-false", FilteringOperator.LessThan, value: new object())));

        Assert.True(InvokeEvaluate(adapterType, 5, new FilteringDescriptor("lte-true", FilteringOperator.LessThanOrEqual, value: 5)));
        Assert.False(InvokeEvaluate(adapterType, 5, new FilteringDescriptor("lte-false", FilteringOperator.LessThanOrEqual, value: 4)));
        Assert.False(InvokeEvaluate(adapterType, new object(), new FilteringDescriptor("lte-compare-false", FilteringOperator.LessThanOrEqual, value: new object())));

        Assert.True(InvokeEvaluate(
            adapterType,
            4,
            new FilteringDescriptor("between-true", FilteringOperator.Between, values: new object[] { 3, 5 })));

        Assert.False(InvokeEvaluate(
            adapterType,
            8,
            new FilteringDescriptor("between-false", FilteringOperator.Between, values: new object[] { 3, 5 })));

        Assert.True(InvokeEvaluate(
            adapterType,
            2,
            new FilteringDescriptor("in-true", FilteringOperator.In, values: new object[] { 1, 2, 3 })));

        Assert.False(InvokeEvaluate(
            adapterType,
            9,
            new FilteringDescriptor("in-false", FilteringOperator.In, values: new object[] { 1, 2, 3 })));

        Assert.True(InvokeEvaluate(
            adapterType,
            "anything",
            new FilteringDescriptor("default", (FilteringOperator)999, propertyPath: "Unused")));
    }

    [Theory]
    [MemberData(nameof(AdapterTypes))]
    public void PredicateCacheKey_Equals_And_HashCode_Cover_Branches(Type adapterType)
    {
        var keyType = adapterType.GetNestedType("PredicateCacheKey", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"PredicateCacheKey not found on {adapterType.Name}.");

        var descriptor = new FilteringDescriptor(
            columnId: "score",
            @operator: FilteringOperator.Equals,
            propertyPath: nameof(CoverageItem.Score),
            value: 1);

        var primary = new object();
        var secondary = new object();

        var keyA = CreatePredicateCacheKey(keyType, descriptor, primary, secondary);
        var keyB = CreatePredicateCacheKey(keyType, descriptor, primary, secondary);
        var keyC = CreatePredicateCacheKey(keyType, descriptor, new object(), secondary);
        var keyWithoutRefs = CreatePredicateCacheKey(keyType, descriptor, null, null);
        var keyWithNullDescriptor = CreatePredicateCacheKey(keyType, null, primary, secondary);

        var equalsTyped = keyType.GetMethod("Equals", BindingFlags.Instance | BindingFlags.Public, null, new[] { keyType }, null)
            ?? throw new InvalidOperationException("Typed Equals overload was not found.");

        Assert.True((bool)equalsTyped.Invoke(keyA, new[] { keyB })!);
        Assert.False((bool)equalsTyped.Invoke(keyA, new[] { keyC })!);

        Assert.True((bool)keyA.Equals(keyB));
        Assert.False((bool)keyA.Equals(new object()));

        var hashA = keyA.GetHashCode();
        var hashB = keyB.GetHashCode();
        var hashWithoutRefs = keyWithoutRefs.GetHashCode();
        var hashWithNullDescriptor = keyWithNullDescriptor.GetHashCode();

        Assert.Equal(hashA, hashB);
        Assert.NotEqual(0, hashWithoutRefs);
        Assert.NotEqual(0, hashWithNullDescriptor);
    }

    [Fact]
    public void DataGridFilteringAdapter_ComposePredicate_And_Private_Branches()
    {
        var model = new FilteringModel();
        var adapter = new DataGridFilteringAdapter(model, () => Array.Empty<DataGridColumn>());

        Assert.Null(InvokeComposePredicate(adapter, null));
        Assert.Null(InvokeComposePredicate(adapter, Array.Empty<FilteringDescriptor>()));

        var withNullDescriptor = new List<FilteringDescriptor> { null! };
        Assert.Null(InvokeComposePredicate(adapter, withNullDescriptor));

        var single = new FilteringDescriptor(
            columnId: "single",
            @operator: FilteringOperator.Custom,
            predicate: _ => true);

        var singlePredicate = InvokeComposePredicate(adapter, new[] { single });
        Assert.NotNull(singlePredicate);
        Assert.True(singlePredicate!(new object()));

        var callOrder = new List<string>();
        var first = new FilteringDescriptor(
            columnId: "first",
            @operator: FilteringOperator.Custom,
            predicate: _ =>
            {
                callOrder.Add("first");
                return false;
            });

        var second = new FilteringDescriptor(
            columnId: "second",
            @operator: FilteringOperator.Custom,
            predicate: _ =>
            {
                callOrder.Add("second");
                return true;
            });

        var combined = InvokeComposePredicate(adapter, new[] { first, second });
        Assert.NotNull(combined);
        Assert.False(combined!(new object()));
        Assert.Equal(new[] { "first" }, callOrder);

        var allTrueCombined = InvokeComposePredicate(adapter, new[]
        {
            new FilteringDescriptor(
                columnId: "all-true-1",
                @operator: FilteringOperator.Custom,
                predicate: _ => true),
            new FilteringDescriptor(
                columnId: "all-true-2",
                @operator: FilteringOperator.Custom,
                predicate: _ => true)
        });
        Assert.NotNull(allTrueCombined);
        Assert.True(allTrueCombined!(new object()));

        var firstTrueSecondFalse = InvokeComposePredicate(adapter, new[]
        {
            new FilteringDescriptor(
                columnId: "first-true",
                @operator: FilteringOperator.Custom,
                predicate: _ => true),
            new FilteringDescriptor(
                columnId: "second-false",
                @operator: FilteringOperator.Custom,
                predicate: _ => false)
        });
        Assert.NotNull(firstTrueSecondFalse);
        Assert.False(firstTrueSecondFalse!(new object()));

        var pathDescriptor = new FilteringDescriptor(
            columnId: "path",
            @operator: FilteringOperator.Equals,
            propertyPath: nameof(CoverageItem.Score),
            value: 10);

        var missingPathDescriptor = new FilteringDescriptor(
            columnId: "missing",
            @operator: FilteringOperator.Equals,
            propertyPath: "Missing",
            value: 10);

        var noPathDescriptor = new FilteringDescriptor(
            columnId: "no-path",
            @operator: FilteringOperator.Equals,
            value: 10);

        var firstCached = InvokeComposePredicate(adapter, new[] { pathDescriptor });
        var secondCached = InvokeComposePredicate(adapter, new[] { pathDescriptor });
        var cacheMiss = InvokeComposePredicate(adapter, new[] { missingPathDescriptor });
        var noPath = InvokeComposePredicate(adapter, new[] { noPathDescriptor });

        Assert.NotNull(firstCached);
        Assert.Same(firstCached, secondCached);
        Assert.NotNull(cacheMiss);
        Assert.False(cacheMiss!(new CoverageItem("A", 1)));
        Assert.Null(noPath);

        var getGetter = typeof(DataGridFilteringAdapter).GetMethod("GetGetter", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("GetGetter was not found.");

        Assert.Null(getGetter.Invoke(adapter, new object?[] { null }));

        var missingGetter = (Func<object, object>?)getGetter.Invoke(adapter, new object?[] { "Missing" });
        Assert.NotNull(missingGetter);
        Assert.Null(missingGetter!(new CoverageItem("A", 1)));

        var scoreGetter = (Func<object, object>?)getGetter.Invoke(adapter, new object[] { nameof(CoverageItem.Score) });
        Assert.NotNull(scoreGetter);
        Assert.Null(scoreGetter!(null!));
        Assert.Equal(1, scoreGetter(new CoverageItem("A", 1)));
        Assert.Equal(2, scoreGetter(new CoverageItem("B", 2)));

        var findColumn = typeof(DataGridFilteringAdapter).GetMethod("FindColumn", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("FindColumn was not found.");

        var nullColumnAdapter = new DataGridFilteringAdapter(model, () => null!);
        var findByIdDescriptor = new FilteringDescriptor("id", FilteringOperator.Equals, propertyPath: nameof(CoverageItem.Score), value: 2);
        Assert.Null(findColumn.Invoke(nullColumnAdapter, new object[] { findByIdDescriptor }));

        var byId = new DataGridTextColumn { ColumnKey = "id" };
        var byPath = new DataGridTextColumn { SortMemberPath = nameof(CoverageItem.Score) };
        var findColumnAdapter = new DataGridFilteringAdapter(model, () => new[] { byId, byPath });

        Assert.Same(byId, findColumn.Invoke(findColumnAdapter, new object[] { findByIdDescriptor }));

        var findByPathDescriptor = new FilteringDescriptor("path", FilteringOperator.Equals, propertyPath: nameof(CoverageItem.Score), value: 2);
        Assert.Same(byPath, findColumn.Invoke(findColumnAdapter, new object[] { findByPathDescriptor }));

        var missDescriptor = new FilteringDescriptor("none", FilteringOperator.Equals, propertyPath: "Missing", value: 2);
        Assert.Null(findColumn.Invoke(findColumnAdapter, new object[] { missDescriptor }));
    }

    [Fact]
    public void DataGridFilteringAdapter_Constructor_And_ApplyModel_Branches()
    {
        Assert.Throws<ArgumentNullException>(() => new DataGridFilteringAdapter(null!, () => Array.Empty<DataGridColumn>()));
        Assert.Throws<ArgumentNullException>(() => new DataGridFilteringAdapter(new FilteringModel(), null!));

        var model = new FilteringModel();
        var adapter = new DataGridFilteringAdapter(model, () => Array.Empty<DataGridColumn>());

        var descriptor = new FilteringDescriptor(
            columnId: "score",
            @operator: FilteringOperator.Equals,
            propertyPath: nameof(CoverageItem.Score),
            value: 1);

        var exception = Record.Exception(() => model.SetOrUpdate(descriptor));
        Assert.Null(exception);

        var before = 0;
        var after = 0;
        var handledAdapter = new HandledFalseAdapter(
            model,
            () => Array.Empty<DataGridColumn>(),
            beforeViewRefresh: () => before++,
            afterViewRefresh: () => after++);

        var view = new DataGridCollectionView(new[] { new CoverageItem("A", 1), new CoverageItem("B", 2) });
        handledAdapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: "name",
            @operator: FilteringOperator.Equals,
            propertyPath: nameof(CoverageItem.Name),
            value: "A"));

        Assert.True(before > 0);
        Assert.Equal(0, after);

        var handledTrueBefore = 0;
        var handledTrueAfter = 0;
        var handledTrueAdapter = new HandledTrueAdapter(
            model,
            () => Array.Empty<DataGridColumn>(),
            beforeViewRefresh: () => handledTrueBefore++,
            afterViewRefresh: () => handledTrueAfter++);

        var handledTrueView = new DataGridCollectionView(new[] { new CoverageItem("A", 1), new CoverageItem("B", 2) });
        handledTrueAdapter.AttachView(handledTrueView);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: "name",
            @operator: FilteringOperator.Equals,
            propertyPath: nameof(CoverageItem.Name),
            value: "B"));

        Assert.True(handledTrueBefore > 0);
        Assert.True(handledTrueAfter > 0);
    }

    [Fact]
    public void DataGridFilteringAdapter_ReconcileExternalFilter_Branches()
    {
        var items = new[] { new CoverageItem("A", 1), new CoverageItem("B", 2) };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel { OwnsViewFilter = false };
        var adapter = new DataGridFilteringAdapter(model, () => Array.Empty<DataGridColumn>());
        adapter.AttachView(view);

        var reconcileMethod = typeof(DataGridFilteringAdapter)
            .GetMethod("ReconcileExternalFilter", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ReconcileExternalFilter was not found.");

        var unattachedModel = new FilteringModel { OwnsViewFilter = false };
        var unattachedAdapter = new DataGridFilteringAdapter(unattachedModel, () => Array.Empty<DataGridColumn>());
        reconcileMethod.Invoke(unattachedAdapter, Array.Empty<object>());

        var ownsModel = new FilteringModel { OwnsViewFilter = true };
        var ownsAdapter = new DataGridFilteringAdapter(ownsModel, () => Array.Empty<DataGridColumn>());
        ownsAdapter.AttachView(new DataGridCollectionView(items));
        reconcileMethod.Invoke(ownsAdapter, Array.Empty<object>());

        var external = new Func<object, bool>(o => ((CoverageItem)o).Score > 0);

        model.Apply(new[]
        {
            new FilteringDescriptor(
                columnId: "external",
                @operator: FilteringOperator.Custom,
                predicate: external)
        });

        view.Filter = external;
        reconcileMethod.Invoke(adapter, Array.Empty<object>());

        Assert.Single(model.Descriptors);
        Assert.Same(external, model.Descriptors[0].Predicate);

        model.Apply(new[]
        {
            new FilteringDescriptor(
                columnId: "different",
                @operator: FilteringOperator.Custom,
                predicate: _ => true)
        });

        view.Filter = null;
        reconcileMethod.Invoke(adapter, Array.Empty<object>());
        Assert.Empty(model.Descriptors);

        view.Filter = external;
        reconcileMethod.Invoke(adapter, Array.Empty<object>());

        Assert.Single(model.Descriptors);
        Assert.Equal(FilteringOperator.Custom, model.Descriptors[0].Operator);
        Assert.Same(external, model.Descriptors[0].Predicate);
    }

    [AvaloniaFact]
    public void DataGridAccessorFilteringAdapter_Private_And_Missing_Accessor_Branches()
    {
        Assert.Throws<ArgumentNullException>(() => new DataGridAccessorFilteringAdapter(new FilteringModel(), null!));

        var model = new FilteringModel();
        var adapter = new DataGridAccessorFilteringAdapter(model, () => Array.Empty<DataGridColumn>());

        var tryApply = typeof(DataGridAccessorFilteringAdapter)
            .GetMethod("TryApplyModelToView", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("TryApplyModelToView was not found.");

        object?[] tryApplyArgs = { Array.Empty<FilteringDescriptor>(), null, false };
        var handled = (bool)tryApply.Invoke(adapter, tryApplyArgs)!;
        Assert.True(handled);
        Assert.False((bool)tryApplyArgs[2]!);

        Assert.Null(InvokeComposePredicate(adapter, null));
        Assert.Null(InvokeComposePredicate(adapter, Array.Empty<FilteringDescriptor>()));
        Assert.Null(InvokeComposePredicate(adapter, new List<FilteringDescriptor> { null! }));

        var predicateDescriptor = new FilteringDescriptor(
            columnId: "custom",
            @operator: FilteringOperator.Custom,
            predicate: o => ((CoverageItem)o).Score == 2);

        var predicate = InvokeComposePredicate(adapter, new[] { predicateDescriptor });
        Assert.NotNull(predicate);
        Assert.True(predicate!(new CoverageItem("B", 2)));

        var accessorCombinedFalse = InvokeComposePredicate(adapter, new[]
        {
            new FilteringDescriptor(
                columnId: "accessor-false-first",
                @operator: FilteringOperator.Custom,
                predicate: _ => false),
            new FilteringDescriptor(
                columnId: "accessor-false-second",
                @operator: FilteringOperator.Custom,
                predicate: _ => true)
        });
        Assert.NotNull(accessorCombinedFalse);
        Assert.False(accessorCombinedFalse!(new CoverageItem("B", 2)));

        var accessorCombinedTrue = InvokeComposePredicate(adapter, new[]
        {
            new FilteringDescriptor(
                columnId: "accessor-true-first",
                @operator: FilteringOperator.Custom,
                predicate: _ => true),
            new FilteringDescriptor(
                columnId: "accessor-true-second",
                @operator: FilteringOperator.Custom,
                predicate: _ => true)
        });
        Assert.NotNull(accessorCombinedTrue);
        Assert.True(accessorCombinedTrue!(new CoverageItem("B", 2)));

        var column = new DataGridTextColumn();
        DataGridColumnMetadata.SetValueAccessor(column, new DataGridColumnValueAccessor<CoverageItem, int>(p => p.Score));

        var accessorAdapter = new DataGridAccessorFilteringAdapter(model, () => new[] { column });

        var reusableDescriptor = new FilteringDescriptor(
            columnId: column,
            @operator: FilteringOperator.Equals,
            value: 2);

        var firstCached = InvokeComposePredicate(accessorAdapter, new[] { reusableDescriptor });
        var secondCached = InvokeComposePredicate(accessorAdapter, new[] { reusableDescriptor });

        Assert.NotNull(firstCached);
        Assert.Same(firstCached, secondCached);
        Assert.True(firstCached!(new CoverageItem("B", 2)));

        var missingWithoutOptions = new DataGridAccessorFilteringAdapter(model, () => Array.Empty<DataGridColumn>());
        var missingWithoutOptionsResult = InvokeComposePredicate(
            missingWithoutOptions,
            new[]
            {
                new FilteringDescriptor(
                    columnId: "missing-no-options",
                    @operator: FilteringOperator.Equals,
                    value: 1)
            });
        Assert.Null(missingWithoutOptionsResult);

        var throwOnMissing = new DataGridAccessorFilteringAdapter(
            model,
            () => Array.Empty<DataGridColumn>(),
            new DataGridFastPathOptions { ThrowOnMissingAccessor = true });

        var thrown = Assert.Throws<TargetInvocationException>(() =>
            InvokeComposePredicate(
                throwOnMissing,
                new[]
                {
                    new FilteringDescriptor(
                        columnId: "missing-column",
                        @operator: FilteringOperator.Equals,
                        value: 1)
                }));
        Assert.IsType<InvalidOperationException>(thrown.InnerException);

        var options = new DataGridFastPathOptions();
        DataGridFastPathMissingAccessorEventArgs captured = null;
        options.MissingAccessor += (_, args) => captured = args;

        var reportMissing = new DataGridAccessorFilteringAdapter(model, () => Array.Empty<DataGridColumn>(), options);
        var result = InvokeComposePredicate(
            reportMissing,
            new[]
            {
                new FilteringDescriptor(
                    columnId: "missing-column",
                    @operator: FilteringOperator.Equals,
                    value: 1)
            });

        Assert.Null(result);
        Assert.NotNull(captured);
        Assert.Equal(DataGridFastPathFeature.Filtering, captured.Feature);
        Assert.Null(captured.Column);

        var findColumnMethod = typeof(DataGridAccessorFilteringAdapter)
            .GetMethod("FindColumn", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("FindColumn was not found.");

        var findColumnNullProvider = new DataGridAccessorFilteringAdapter(model, () => null!);
        var descriptorNoProperty = new FilteringDescriptor(
            columnId: "missing-find-column",
            @operator: FilteringOperator.Equals,
            value: 1);
        Assert.Null(findColumnMethod.Invoke(findColumnNullProvider, new object[] { descriptorNoProperty }));

        var sortOnlyColumn = new DataGridTextColumn { SortMemberPath = nameof(CoverageItem.Score) };
        var findColumnAdapter = new DataGridAccessorFilteringAdapter(model, () => new[] { sortOnlyColumn });
        Assert.Null(findColumnMethod.Invoke(findColumnAdapter, new object[] { descriptorNoProperty }));

        var emptySortColumn = new DataGridTextColumn();
        var descriptorWithPath = new FilteringDescriptor(
            columnId: "find-with-path",
            @operator: FilteringOperator.Equals,
            propertyPath: nameof(CoverageItem.Score),
            value: 1);
        var emptySortAdapter = new DataGridAccessorFilteringAdapter(model, () => new[] { emptySortColumn });
        Assert.Null(findColumnMethod.Invoke(emptySortAdapter, new object[] { descriptorWithPath }));

        var mismatchedSortColumn = new DataGridTextColumn { SortMemberPath = nameof(CoverageItem.Name) };
        var mismatchSortAdapter = new DataGridAccessorFilteringAdapter(model, () => new[] { mismatchedSortColumn });
        Assert.Null(findColumnMethod.Invoke(mismatchSortAdapter, new object[] { descriptorWithPath }));

        var plainAccessor = new NonFilterAccessor();
        Assert.False(plainAccessor is IDataGridColumnFilterAccessor);
        var plainColumn = new DataGridTextColumn();
        DataGridColumnMetadata.SetValueAccessor(plainColumn, plainAccessor);
        Assert.Same(plainAccessor, DataGridColumnMetadata.GetValueAccessor(plainColumn));
        Assert.Null(DataGridColumnFilter.GetValueAccessor(plainColumn));
        var plainAdapter = new DataGridAccessorFilteringAdapter(model, () => new[] { plainColumn });
        var plainPredicate = InvokeComposePredicate(
            plainAdapter,
            new[]
            {
                new FilteringDescriptor(
                    columnId: plainColumn,
                    @operator: FilteringOperator.Equals,
                    value: 2)
            });
        Assert.NotNull(plainPredicate);
        Assert.True(plainPredicate!(new CoverageItem("B", 2)));
        Assert.True(plainAccessor.ValueCalls > 0);

        var fallbackAccessor = new FallbackFilterAccessor();
        var fallbackColumn = new DataGridTextColumn();
        DataGridColumnMetadata.SetValueAccessor(fallbackColumn, fallbackAccessor);

        var fallbackAdapter = new DataGridAccessorFilteringAdapter(model, () => new[] { fallbackColumn });
        var fallbackPredicate = InvokeComposePredicate(
            fallbackAdapter,
            new[]
            {
                new FilteringDescriptor(
                    columnId: fallbackColumn,
                    @operator: FilteringOperator.Equals,
                    value: 2)
            });

        Assert.NotNull(fallbackPredicate);
        Assert.True(fallbackPredicate!(new CoverageItem("B", 2)));
        Assert.True(fallbackAccessor.TryMatchCalls > 0);
        Assert.True(fallbackAccessor.ValueCalls > 0);

        var withFactory = new DataGridTextColumn();
        DataGridColumnFilter.SetPredicateFactory(withFactory, _ => _ => true);
        var factoryAdapter = new DataGridAccessorFilteringAdapter(model, () => new[] { withFactory });
        var factoryPredicate = InvokeComposePredicate(
            factoryAdapter,
            new[]
            {
                new FilteringDescriptor(
                    columnId: withFactory,
                    @operator: FilteringOperator.Equals,
                    value: 1)
            });

        Assert.NotNull(factoryPredicate);
        Assert.True(factoryPredicate!(new object()));
    }

    private static object CreatePredicateCacheKey(Type keyType, FilteringDescriptor? descriptor, object? primary, object? secondary)
    {
        return Activator.CreateInstance(keyType, descriptor, primary, secondary)
            ?? throw new InvalidOperationException("Failed to create PredicateCacheKey instance.");
    }

    private static bool InvokeEvaluate(Type type, object value, FilteringDescriptor descriptor)
    {
        return InvokeStatic<bool>(type, "EvaluateDescriptor", value, descriptor);
    }

    private static Func<object, bool>? InvokeComposePredicate(object adapter, IReadOnlyList<FilteringDescriptor>? descriptors)
    {
        var method = adapter.GetType().GetMethod("ComposePredicate", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"ComposePredicate was not found on {adapter.GetType().Name}.");

        return (Func<object, bool>?)method.Invoke(adapter, new object?[] { descriptors });
    }

    private static void AssertTryCompare(
        Type type,
        object? left,
        object? right,
        CultureInfo? culture,
        bool expectedSuccess,
        int expectedResult)
    {
        var result = InvokeTryCompare(type, left, right, culture);

        Assert.Equal(expectedSuccess, result.Success);

        if (expectedSuccess)
        {
            if (expectedResult < 0)
            {
                Assert.True(result.Result < 0);
            }
            else if (expectedResult > 0)
            {
                Assert.True(result.Result > 0);
            }
            else
            {
                Assert.Equal(0, result.Result);
            }
        }
    }

    private static (bool Success, int Result) InvokeTryCompare(Type type, object? left, object? right, CultureInfo? culture)
    {
        var method = type.GetMethod("TryCompare", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"TryCompare was not found on {type.Name}.");

        object?[] args = { left, right, culture, 0 };
        var success = (bool)method.Invoke(null, args)!;
        return (success, (int)args[3]!);
    }

    private static T InvokeStatic<T>(Type type, string methodName, params object?[] args)
    {
        var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Method {methodName} was not found on {type.Name}.");

        return (T)method.Invoke(null, args)!;
    }

    private sealed class CoverageItem
    {
        public CoverageItem(string name, int score)
        {
            Name = name;
            Score = score;
        }

        public string Name { get; }

        public int Score { get; }
    }

    private sealed class LenientComparable : IComparable
    {
        public LenientComparable(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public int CompareTo(object? obj)
        {
            return obj switch
            {
                string s when int.TryParse(s, out var parsed) => Value.CompareTo(parsed),
                int i => Value.CompareTo(i),
                _ => throw new ArgumentException("Unsupported type.", nameof(obj))
            };
        }
    }

    private sealed class InvalidCastComparable : IComparable
    {
        public int CompareTo(object? obj)
        {
            throw new InvalidCastException();
        }
    }

    private sealed class NonComparable
    {
        private readonly string _value;

        public NonComparable(string value)
        {
            _value = value;
        }

        public override string ToString()
        {
            return _value;
        }
    }

    private sealed class HandledFalseAdapter : DataGridFilteringAdapter
    {
        public HandledFalseAdapter(
            IFilteringModel model,
            Func<IEnumerable<DataGridColumn>> columnProvider,
            Action beforeViewRefresh,
            Action afterViewRefresh)
            : base(model, columnProvider, beforeViewRefresh, afterViewRefresh)
        {
        }

        protected override bool TryApplyModelToView(
            IReadOnlyList<FilteringDescriptor> descriptors,
            IReadOnlyList<FilteringDescriptor> previousDescriptors,
            out bool changed)
        {
            changed = false;
            return true;
        }
    }

    private sealed class HandledTrueAdapter : DataGridFilteringAdapter
    {
        public HandledTrueAdapter(
            IFilteringModel model,
            Func<IEnumerable<DataGridColumn>> columnProvider,
            Action beforeViewRefresh,
            Action afterViewRefresh)
            : base(model, columnProvider, beforeViewRefresh, afterViewRefresh)
        {
        }

        protected override bool TryApplyModelToView(
            IReadOnlyList<FilteringDescriptor> descriptors,
            IReadOnlyList<FilteringDescriptor> previousDescriptors,
            out bool changed)
        {
            changed = true;
            return true;
        }
    }

    private sealed class FallbackFilterAccessor : IDataGridColumnValueAccessor, IDataGridColumnFilterAccessor
    {
        public int TryMatchCalls { get; private set; }

        public int ValueCalls { get; private set; }

        public Type ItemType => typeof(CoverageItem);

        public Type ValueType => typeof(int);

        public bool CanWrite => false;

        public object GetValue(object item)
        {
            ValueCalls++;
            return item is CoverageItem row ? row.Score : 0;
        }

        public void SetValue(object item, object value)
        {
            throw new InvalidOperationException();
        }

        public bool TryMatch(object item, FilteringDescriptor descriptor, out bool match)
        {
            TryMatchCalls++;
            match = false;
            return false;
        }
    }

    private sealed class NonFilterAccessor : IDataGridColumnValueAccessor
    {
        public int ValueCalls { get; private set; }

        public Type ItemType => typeof(CoverageItem);

        public Type ValueType => typeof(int);

        public bool CanWrite => false;

        public object GetValue(object item)
        {
            ValueCalls++;
            return item is CoverageItem row ? row.Score : 0;
        }

        public void SetValue(object item, object value)
        {
            throw new InvalidOperationException();
        }
    }
}

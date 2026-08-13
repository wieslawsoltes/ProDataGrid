# Source-generator attribute reference

The generator injects these internal configuration attributes into the consuming compilation under `ProDataGrid.SourceGeneration`. Add `using ProDataGrid.SourceGeneration;` in source files that use type/property attributes.

## Generation attributes

| Attribute | Target | Purpose |
| --- | --- | --- |
| `GenerateDataGridColumns` | class, struct, interface, assembly | Generate one typed schema/provider. |
| `GenerateDataGridColumnsForNamespace` | assembly | Generate schemas for eligible types in a namespace. |
| `GenerateDataGridViewModel` | class, assembly | Add schema/column/fast-path properties to a partial ViewModel. |
| `GenerateDataGridViewModelsForNamespace` | assembly | Augment eligible partial ViewModels in a namespace. |
| `GenerateDataGridController` | partial class | Generate one named controller and optional source pipeline. |
| `GenerateDataGridView` | class, assembly | Generate a code-only Avalonia or ReactiveUI view. |
| `GenerateDataGridViewsForNamespace` | assembly | Generate views for eligible ViewModels in a namespace. |
| `GenerateDataGridRegistry` | assembly | Generate reflection-free schema/view lookup and optional DI registration. |
| `DataGridViewRegistration` | assembly | Register an existing XAML view in the generated registry. |
| `GenerateDataGridIndexedColumns` | class, struct | Generate typed factories for a runtime indexed field family. |
| `GenerateDataGridCellDrawCache` | partial class | Generate bounded per-item custom-drawing cache storage. |

## `GenerateDataGridColumns`

Constructors:

```csharp
[GenerateDataGridColumns]
[GenerateDataGridColumns(typeof(ExternalRow))]
```

| Option | Default | Meaning |
| --- | --- | --- |
| `ItemType` | annotated type | External row type for assembly requests. |
| `ProviderName` | deterministic type-based name | Emitted provider/facade type name. |
| `ProviderNamespace` | generated default | Emitted provider namespace. |
| `SchemaId` | item metadata name + `/v1` | Stable persisted/cross-assembly schema identity. |
| `StateVersion` | `1` | Persisted-state migration version. |
| `Discovery` | `PublicProperties` | Public-property or attributed-only discovery. |
| `IncludeInherited` | `true` | Include inherited class/interface properties. |
| `Strict` | `true` | Require reflection-free compatible metadata. |
| `Streaming` | `false` | Mark/configure the schema for streaming usage. |
| `HierarchicalRows` | `false` | Emit `HierarchicalNode.Item`-aware compiled column bindings. |
| `PerformanceProfile` | `Balanced` | Generated performance preset. |
| `DefaultPageSize` | `0` | Collection-view page size; zero disables paging. |
| `InitialPageIndex` | `0` | Initial collection-view page. |
| `InitialCurrency` | default enum value | Initial collection-view current-item policy. |
| `PreserveCurrentItemByKey` | `true` | Keep currency across refresh/page/replacement by key. |
| `PreserveSelectionByKey` | `true` | Keep selection across refresh/page/replacement by key. |
| `ImplementationType` | none | Complete `IDataGridGeneratedSchema<TItem>` replacement/runtime adapter. |
| `MutationHandlerType` | none | Default `IDataGridGeneratedCollectionMutationHandler<TItem>`. |
| `NewRowFactoryType` | none | Default `IDataGridGeneratedNewRowFactory<TItem>`. |
| `FormulaFillTranslatorType` | none | Default `IFormulaFillTranslator`. |
| `OperationPresetMethods` | empty | Validated static preset factory names. |
| `KeySelectorMethod` | none | Static typed item-key selector. |
| `UseReferenceIdentityKey` | `false` | Use reference identity for reference rows. |
| `ConfigureMethod` | none | Static completed-column-list hook. |
| `PivotConfigureMethod` | none | Static `PivotTableModel` policy hook. |
| `OutlineConfigureMethod` | none | Static `OutlineReportModel` policy hook. |

`KeySelectorMethod`, `[DataGridKey]`, and `UseReferenceIdentityKey` are mutually exclusive.

## `GenerateDataGridColumnsForNamespace`

Constructor: `GenerateDataGridColumnsForNamespace(string namespaceName)`.

It supports `IncludeNestedNamespaces` plus the discovery, strictness, streaming, hierarchy, performance, paging/currency, preservation, state-version, pivot, and outline defaults from `GenerateDataGridColumns`. Explicit item requests override namespace-owned settings.

## `GenerateDataGridViewModel`

Constructors:

```csharp
[GenerateDataGridViewModel(typeof(Row))]
[GenerateDataGridViewModel(typeof(MyViewModel), typeof(Row))]
```

| Option | Default | Meaning |
| --- | --- | --- |
| `ColumnDefinitionsPropertyName` | `ColumnDefinitions` | Generated definitions property. |
| `SchemaPropertyName` | `DataGridSchema` | Generated schema property. |
| `FastPathOptionsPropertyName` | `FastPathOptions` | Generated fast-path options property. |
| `ProviderName` | inferred | Existing/generated schema provider to use. |
| `Strict` | `true` | Strict schema projection. |
| `Streaming` | `false` | Streaming projection metadata. |

The ViewModel and all containing types must be partial. Existing requested members report `PDGSG006`.

`GenerateDataGridViewModelsForNamespace` additionally has `NamespaceName`, `IncludeNestedNamespaces`, and `ItemsPropertyName` for item-type inference.

## `GenerateDataGridController`

Constructor: `GenerateDataGridController(Type itemType, string name)`.

| Option | Meaning |
| --- | --- |
| `ProviderName` | Schema provider used by the named controller. |
| `SourceMember` | ViewModel source field/property. |
| `SourceKind` | Ordinary, DynamicData list/cache, async/channel, or remote source shape. |
| `Features` | Generated capability flags; defaults to columns + operations. |
| `OperationExecution` | View, external-pipeline, or remote execution owner. |
| `KeyMember` | Explicit controller key member. |
| `KeySelectorMethod` | Explicit static controller key selector. |
| `UseReferenceIdentityKey` | Reference identity for reference rows. |
| `ImplementationType` | `IDataGridGeneratedControllerFactory<TItem>` implementation. |
| `ConfigureMethod` | Static `ref DataGridGeneratedControllerOptions<TItem>` hook. |
| `PipelineTransformMethod` | Exact-shape typed DynamicData transform. |
| `Strict` | Strict generation; default `true`. |
| `Streaming` | Streaming controller configuration. |

The generated member prefix is `Name`. Names must not collide (`PDGSG117`).

## `GenerateDataGridView`

Constructors:

```csharp
[GenerateDataGridView(typeof(Row))]
[GenerateDataGridView(typeof(MyViewModel), typeof(Row))]
```

### Identity and framework

| Option | Meaning |
| --- | --- |
| `ViewName`, `ViewNamespace` | Emitted type identity. |
| `Framework` | `Avalonia` or `ReactiveUI`. |
| `BaseType` | Accessible compatible custom base type. |
| `Title`, `AutomationId` | View title and stable automation root. |
| `Recipe` | `GridOnly`, `SearchableGrid`, `OperationsToolbar`, `Explorer`, `Spreadsheet`, `Analytics`, or `MasterDetail`. |
| `ControllerName` | Named controller associated with the view. |

### Compiled ViewModel bindings

| Option | Bound member |
| --- | --- |
| `ItemsPropertyName` | Item collection. |
| `ColumnDefinitionsPropertyName` | Generated/application definitions. |
| `FastPathOptionsPropertyName` | Strict/accessor fast-path options. |
| `SortingModelPropertyName` | Sorting model. |
| `FilteringModelPropertyName` | Filtering model. |
| `SearchModelPropertyName` | Search model. |
| `SearchTextPropertyName` | Two-way search text. |
| `SelectionModelPropertyName` | Selection model. |
| `ClipboardImportModelPropertyName` | `IDataGridClipboardImportModel`. |
| `FillModelPropertyName` | `IDataGridFillModel`. |
| `FormulaModelPropertyName` | `IDataGridFormulaModel`. |
| `ConditionalFormattingModelPropertyName` | `IConditionalFormattingModel`. |
| `HierarchicalModelPropertyName` | Typed hierarchy model. |
| `StateControllerPropertyName` | Generated state controller. |

### Grid behavior

| Option | Default/purpose |
| --- | --- |
| `HierarchyFilterPolicy` | Keep ancestors of matches. |
| `SelectionMode` | `Single`. |
| `SelectionUnit` | `FullRow`. |
| `EditTriggers` | ProDataGrid defaults. |
| `RestrictTextInputEditToCells` | Restrict text-input editing. |
| `RequiredPointerEditModifiers` | Required pointer modifiers. |
| `RequireExactPointerEditModifiers` | Require exact modifier set. |
| `ClipboardCopyMode` | `ExcludeHeader`. |
| `IsReadOnly` | Generated grid read-only setting. |
| `CanUserAddRows`, `CanUserDeleteRows` | Disabled unless explicitly enabled. |
| `ShowTotalSummary`, `ShowGroupSummary` | Summary surface visibility. |
| `TotalSummaryPosition`, `GroupSummaryPosition` | Summary placement. |

### View state, events, and interactions

| Option | Meaning |
| --- | --- |
| `ViewStatePropertyName` | `DataGridGeneratedViewState`. |
| `ErrorMessagePropertyName` | Optional dynamic error string. |
| `RetryCommandPropertyName` | Retry `ICommand`. |
| `LoadingText`, `EmptyText`, `ErrorText`, `RetryText` | Static fallback labels. |
| `RoutedEvents` | Generated DataGrid event flags. |
| `RoutedEventCommandPropertyName` | Command receiving `DataGridGeneratedViewEvent<TItem>`. |
| `InteractionPropertyNames` | ReactiveUI interaction member names. |
| `InteractionHandlerTypes` | Matching generated-view handler types. |
| `NavigationInteractionPropertyName` | Typed current-cell/scroll interaction. |

### Performance and presentation

| Option | Meaning |
| --- | --- |
| `PerformanceProfile` | Generated performance preset. |
| `InputMapType` | `IDataGridGeneratedInputMap` implementation. |
| `InputCommandPropertyName` | Command receiving typed input events. |
| `DiagnosticsSinkType` | `IDataGridGeneratedMetricsSink` implementation. |
| `DiagnosticsStatusPropertyName` | Readable string status binding. |
| `ViewThemeKey`, `DataGridThemeKey`, `ToolbarThemeKey`, `RecipeContentThemeKey` | Dynamic resource keys. |
| `ViewClasses`, `DataGridClasses`, `ToolbarClasses`, `RecipeContentClasses` | Validated direct class tokens. |

### Layouts

| Option | Meaning |
| --- | --- |
| `LayoutModelPropertyName` | Compiled binding to a readable `IDataGridLayoutModel`; mutually exclusive with `Layout`. |
| `Layout` | `None`, `Stack`, `NonVirtualizingStack`, `UniformGrid`, or `Wrap`. |
| `LayoutOrientation` | Keep the model default, or select horizontal/vertical fill. |
| `LayoutSpacing`, `LayoutDisableVirtualization` | Stack configuration. |
| `LayoutHorizontalSpacing`, `LayoutVerticalSpacing` | Uniform-grid/wrap spacing. |
| `LayoutMinItemWidth`, `LayoutMinItemHeight` | Uniform-grid cell minimums. |
| `LayoutMaximumRowsOrColumns` | Uniform-grid line cap. |
| `LayoutItemsJustification`, `LayoutItemsStretch` | Uniform-grid alignment/stretch. |
| `LayoutMaximumCachedLines` | Variable-wrap exact-line cache bound. |

See [Generated layouts](layouts.md) for emitted types, defaults, validation, and examples.

### Row details

| Option | Meaning |
| --- | --- |
| `RowDetailsTemplateKey` | Dynamic resource template. |
| `RowDetailsTemplateImplementationType` | Accessible parameterless `IDataTemplate`. |
| `RowDetailsTemplateFactoryMethod` | Static recycling `(TItem, Control?) -> Control` factory. |
| `RowDetailsVisibilityMode`, `AreRowDetailsFrozen` | Grid detail behavior. |
| `RowDetailsNestedItemType` | Nested row schema type. |
| `RowDetailsNestedItemsMember` | `IEnumerable<TNested>` row property. |
| `RowDetailsNestedProviderName`, `RowDetailsNestedProviderNamespace` | Nested provider identity. |
| `RowDetailsSummaryMember` | Optional readable string summary. |
| `RowDetailsAutomationId` | Stable nested-details automation root. |

`GenerateDataGridViewsForNamespace` exposes the same shared view settings plus namespace matching and item inference.

## `GenerateDataGridRegistry`

| Option | Default |
| --- | --- |
| `RegistryName` | `GeneratedProDataGridRegistration` |
| `RegistryNamespace` | `ProDataGrid.Generated` |

One registry request is allowed per assembly.

## `DataGridViewRegistration`

Constructor: `DataGridViewRegistration(Type viewModelType, Type viewType)`. The view type must derive from `Control` and have an accessible parameterless constructor.

## `GenerateDataGridIndexedColumns`

| Option | Default/purpose |
| --- | --- |
| `Name` | `IndexedColumns`; emitted factory prefix. |
| `GetterMethod` | Required indexed getter. |
| `SetterMethod` | Optional indexed setter. |
| `NotificationNameMethod` | Optional slot-to-property-notification name. |

Multiple named indexed families may be generated for one class/struct when names do not collide.

## `GenerateDataGridCellDrawCache`

| Option | Default/purpose |
| --- | --- |
| `InitialCapacity` | Initial array capacity. |
| `MaximumCapacity` | `256`; hard retained-entry bound. |
| `GenerateSlotConstants` | `true`; emit custom-drawing property slot constants. |

The target must be a partial class.

## Property attributes

| Attribute | Purpose |
| --- | --- |
| `DataGridColumn` | Column, field, operation, rendering, transfer, localization, and accessibility metadata. |
| `DataGridIgnoreColumn` | Exclude a public property. |
| `DataGridKey` | Stable identity field/property. |
| `DataGridChildren` | Hierarchy children property and optional loader. |
| `DataGridExpanded` | Hierarchy expansion property. |
| `DataGridParentKey` | Optional parent identity. |
| `DataGridGroup` | Ordered typed grouping field. |
| `DataGridSummary` | One rendered/incremental aggregate. May repeat. |
| `DataGridConditionalFormat` | One generated conditional rule. May repeat. |
| `DataGridBand` | One nested band path. May repeat. |
| `DataGridPivotAxis` | Pivot row/column/filter role. May repeat. |
| `DataGridPivotValue` | Pivot value/calculated measure. May repeat. |
| `DataGridChartField` | Chart category/value/series role. May repeat. |
| `DataGridOutlineField` | Outline group/detail role. May repeat. |
| `DataGridFormulaField` | Stable formula name/dependencies. |

## `DataGridColumn`

`DataGridColumn` has a parameterless constructor and a constructor accepting `DataGridColumnKind`. The selected value is also available through the settable `Kind` property.

### Identity, order, and sizing

`Header`, `Description`, `Order`, `DisplayIndex`, `FrozenPlacement`, `ColumnKey`, `PreviousColumnKeys`, `SortMemberPath`, `Width`, `MinWidth`, `MaxWidth`, and `WidthSharingGroup`.

### User behavior

`CanUserSort`, `CanUserHide`, `CanUserResize`, `CanUserReorder`, `IsReadOnly`, `IsVisible`, `ShowFilterButton`, `IsSearchable`, and `SearchMemberPath`.

### Themes and resources

`HeaderTemplateKey`, `HeaderThemeKey`, `CellThemeKey`, `SummaryCellThemeKey`, `FilterThemeKey`, `FilterFlyoutKey`, `FilterEditorResourceKey`, `HeaderResourceKey`, and `DescriptionResourceKey`.

### Formatting and realization

`DisplayMode`, `FormatString`, `Watermark`, `UseDirectTextCell`, `UseDirectCell`, `UseDirectTextContent`, `UseOptimizedPresenter`, `TrackDirectTextValueChanges`, `UseDirectValueAccessor`, and `TrackDirectValueChanges`.

### Templates and custom drawing

`TemplateKey`, `EditingTemplateKey`, `TemplateFactoryMethod`, `EditingTemplateFactoryMethod`, `NewRowTemplateFactoryMethod`, `ReuseCellContent`, `DrawOperationFactoryType`, `DrawOperationFactoryMethod`, `DrawingMode`, `RenderBackend`, `TextLayoutCacheMode`, `SharedTextLayoutCacheCapacity`, and `DrawOperationLayoutFastPath`.

### Kind-specific values

`Formula`, `FormulaName`, `Mask`, `ItemsSourceMember`, `DisplayMemberPath`, `SelectedValuePath`, `Minimum`, `Maximum`, `Increment`, `IsThreeState`, and `IsEditable`.

### Button and toggle content/commands

`Content`, `CheckedContent`, `UncheckedContent`, `OnContent`, `OffContent`, `ContentMember`, `CheckedContentMember`, `UncheckedContentMember`, `OnContentMember`, `OffContentMember`, `CommandMember`, and `CommandParameterMember`.

### Parsing, validation, and customization

`ConfigureMethod`, `FactoryMethod`, `ParserMethod`, `FormatterMethod`, `ValidatorMethod`, `AsyncValidatorMethod`, `CoerceMethod`, and `CanEditMethod`.

### Transfer, backend, localization, and accessibility

`ExportFormat`, `ExportNullText`, `BackendFieldName`, `FilterEditor`, `HeaderProviderMethod`, `DescriptionProviderMethod`, `AutomationId`, `AutomationName`, `AutomationHelpText`, and `IsSensitive`.

## Other property-attribute options

- `DataGridChildren.LoaderMethod`: asynchronous/lazy children loader.
- `DataGridGroup`: `Order`, direction, and formatter method.
- `DataGridSummary`: aggregate constructor plus scope, format, and title.
- `DataGridConditionalFormat`: condition constructor plus rule ID, two operands, string comparison, cell theme, priority, stop behavior, predicate, and cell/row target.
- `DataGridBand`: path constructor and order.
- `DataGridPivotAxis`: analytics-role constructor plus order, name, format, configure method.
- `DataGridPivotValue`: aggregate constructor plus order, name, format, display mode, formula/dependencies, custom aggregator factory, and configure method.
- `DataGridChartField`: role constructor plus order, series, format, aggregate.
- `DataGridOutlineField`: role constructor plus order, name, format, aggregate, custom aggregator factory, configure method.
- `DataGridFormulaField`: stable-name constructor plus dependencies, order, and format.

## See also

- [Getting started and schema discovery](getting-started.md)
- [Schemas and columns](schemas-and-columns.md)
- [Generated views](generated-views.md)
- [Compile-time diagnostics](diagnostics-performance-testing.md#compile-time-diagnostics)

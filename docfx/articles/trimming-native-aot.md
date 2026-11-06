# Trimming and NativeAOT

ProDataGrid keeps reflection-based compatibility features enabled by default. This preserves existing automatic columns, property-path operations, TypeDescriptor integration, reflection bindings, and custom type conversions. These features require runtime metadata and can produce trimming or AOT diagnostics. The library enables the trim/AOT analyzers; `IsTrimmable` and `IsAotCompatible` do not make every compatibility feature safe.

## Explicit reflection-free applications

Applications using the .NET 10 ProDataGrid assembly can remove optional reflection compatibility branches with an application-level feature switch:

```xml
<ItemGroup>
  <RuntimeHostConfigurationOption Include="ProDataGrid.Reflection.IsEnabled"
                                  Value="false"
                                  Trim="true" />
</ItemGroup>
```

The setting affects the entire application, including ProDataGrid.Charting. It is opt-in and is not inferred from `PublishTrimmed` or `PublishAot`. `DataGridReflectionOptions.IsEnabled` exposes the effective setting. `AppContext.SetSwitch` can exercise the same behavior in an untrimmed application, but setting the switch at runtime does not prove that unsafe code was removed by the linker.

The .NET 8 assembly supports the runtime setting but does not contain `FeatureSwitchDefinitionAttribute`; it does not provide the same linker-substitution guarantee. Use the .NET 10 asset for the validated trim/AOT path.

Configure grids as follows:

- Set `AutoGenerateColumns="False"` and provide explicit column definitions.
- Use compiled bindings. `DataGridBindingDefinition.Create<TItem, TValue>` overloads accepting `IPropertyInfo` or `CompiledBindingPath`, together with typed getter/setter delegates, avoid expression compilation. The expression-based overload remains incompatible with NativeAOT.
- Provide value accessors for sortable, searchable, filterable, summary, and template columns. Generated schemas with `Strict = true` provide this metadata. Enable `FastPathOptions.StrictMode` to detect missing filtering/searching accessors early.
- Use `DataGridSortDescription.FromAccessor` or `FromComparer`; property-path sorting is a compatibility feature.
- Supply direct hierarchy selectors and typed chart field accessors instead of string property paths.

See [AOT-friendly column bindings](column-definitions-aot.md) and [fast-path integration](column-definitions-fast-path-overview.md).

## Disabled compatibility operations

When reflection is disabled, ProDataGrid does not discover item descriptors or public properties when assigning an item source. Reflection bindings and their edit/display clones, automatic columns, property-path sorting/filtering/access, TypeDescriptor item discovery, and chart property-path access reject unsupported use with `NotSupportedException`. Provide the corresponding compiled binding or typed accessor; missing metadata is not treated as a successful sort or edit.

The built-in collection view does not discover parameterless row constructors in this mode. Creation through that reflected constructor is unavailable (`CanAddNew` reports the capability); use an explicit generated new-row/mutation service or an application-provided editable collection view. Existing row edits through compiled bindings and typed setters remain supported. Editability uses the accessor's write capability and value type, the source collection's edit capability, and grid/column read-only settings. Encode additional read-only policy in column metadata when runtime attributes are unavailable.

Built-in value conversion supports identity/null values, string formatting and standard `IConvertible` primitives without reflection. Supply an explicit `IValueConverter` for custom types, enum parsing, implicit/explicit conversion operators, `Parse` methods and `TypeConverter` behavior.

Automatic generic hierarchy-factory discovery uses reflection and is disabled. The factory's non-generic `IDataGridHierarchicalAdapterFactory.Create` contract remains the dispatch path; implement that contract for custom factories in reflection-free applications. Scroll-state helpers use directly exposed collection counts and do not reflect over an opaque source's `Count` property.

## Validation and diagnostics

`tests/ProDataGrid.SourceGeneration.AotSmoke` explicitly disables reflection with `Trim="true"`, expands grouped dependency diagnostics, and treats its existing IL warning set as errors. It exercises generated schemas/controllers/views plus direct DataGrid item selection, typed sorting and setter updates. The unit suite additionally exercises descriptor avoidance, compiled-binding edit commits, conversion and compatibility-mode path sorting.

```bash
dotnet publish tests/ProDataGrid.SourceGeneration.AotSmoke/ProDataGrid.SourceGeneration.AotSmoke.csproj \
  -c Release -r win-x64 --self-contained true -p:TrimmerSingleWarn=false
```

Use the corresponding supported RID and execute the published binary. Compiler-analyzer diagnostics on enabled compatibility branches and final linker/native diagnostics are separate measurements. No broad warning suppression is required for the disabled-reflection path. To inspect the compatibility branches, publish with `-p:ProDataGridReflectionEnabled=true`; genuine reflection diagnostics remain visible.
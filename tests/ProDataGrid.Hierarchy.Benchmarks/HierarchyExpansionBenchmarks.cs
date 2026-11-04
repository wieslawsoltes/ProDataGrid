using System.Collections;
using Avalonia.Controls.DataGridHierarchical;
using BenchmarkDotNet.Attributes;

namespace ProDataGrid.Hierarchy.Benchmarks;

public enum HierarchyShape
{
    Wide2555Depth3,
    Binary4094Depth11,
    VeryDeep512Depth128,
    OptimizedSample149792Depth5,
}

[MemoryDiagnoser(displayGenColumns: false)]
[RankColumn]
public class HierarchyExpansionBenchmarks
{
    private IReadOnlyList<BenchmarkNode> _roots = null!;
    private HierarchicalModel<BenchmarkNode> _model = null!;
    private int _expectedCount;

    [Params(
        HierarchyShape.Wide2555Depth3,
        HierarchyShape.Binary4094Depth11,
        HierarchyShape.VeryDeep512Depth128)]
    public HierarchyShape Shape { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _roots = BenchmarkTreeFactory.Create(Shape);
        _expectedCount = BenchmarkTreeFactory.ExpectedCount(Shape);

        if (BenchmarkTreeFactory.CountNodes(_roots) != _expectedCount)
        {
            throw new InvalidOperationException("The generated hierarchy has an unexpected node count.");
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _model = CreateModel(_roots);
    }

    [Benchmark]
    public int ExpandAll()
    {
        _model.ExpandAll();
        int count = _model.Count;

        if (count != _expectedCount)
        {
            throw new InvalidOperationException(
                $"Expanded row count mismatch: expected {_expectedCount}, got {count}.");
        }

        return count;
    }

    private static HierarchicalModel<BenchmarkNode> CreateModel(IReadOnlyList<BenchmarkNode> roots)
    {
        var model = new HierarchicalModel<BenchmarkNode>(new HierarchicalOptions<BenchmarkNode>
        {
            ChildrenSelector = static node => node.Children,
            IsLeafSelector = static node => node.Children.Count == 0,
            VirtualizeChildren = true,
        });
        model.SetRoots(roots);
        return model;
    }
}

[MemoryDiagnoser(displayGenColumns: false)]
[RankColumn]
public class HierarchyCollapseBenchmarks
{
    private IReadOnlyList<BenchmarkNode> _roots = null!;
    private HierarchicalModel<BenchmarkNode> _model = null!;
    private int _expectedExpandedCount;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _roots = BenchmarkTreeFactory.Create(HierarchyShape.OptimizedSample149792Depth5);
        _expectedExpandedCount =
            BenchmarkTreeFactory.ExpectedCount(HierarchyShape.OptimizedSample149792Depth5);

        if (BenchmarkTreeFactory.CountNodes(_roots) != _expectedExpandedCount)
        {
            throw new InvalidOperationException("The generated hierarchy has an unexpected node count.");
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        BenchmarkTreeFactory.ResetExpandedState(_roots);
        _model = new HierarchicalModel<BenchmarkNode>(new HierarchicalOptions<BenchmarkNode>
        {
            ChildrenSelector = static node => node.Children,
            IsLeafSelector = static node => node.Children.Count == 0,
            IsExpandedSelector = static node => node.IsExpanded,
            IsExpandedSetter = static (node, value) => node.IsExpanded = value,
            VirtualizeChildren = false,
        });
        _model.SetRoots(_roots);
        _model.ExpandAll();

        if (_model.Count != _expectedExpandedCount)
        {
            throw new InvalidOperationException(
                $"Expanded row count mismatch: expected {_expectedExpandedCount}, got {_model.Count}.");
        }
    }

    [Benchmark]
    public int CollapseAll()
    {
        _model.CollapseAll();
        int count = _model.Count;
        if (count != _roots.Count)
        {
            throw new InvalidOperationException(
                $"Collapsed row count mismatch: expected {_roots.Count}, got {count}.");
        }

        return count;
    }
}

[MemoryDiagnoser(displayGenColumns: false)]
[RankColumn]
public class HierarchyExpansionScalingBenchmarks
{
    private IReadOnlyList<BenchmarkNode> _roots = null!;
    private HierarchicalModel<BenchmarkNode> _model = null!;
    private int _expectedCount;

    [Params(8, 9, 10, 11, 12, 13)]
    public int LevelCount { get; set; }

    public int NodeCount => (2 * ((1 << LevelCount) - 1));

    [GlobalSetup]
    public void GlobalSetup()
    {
        _roots = BenchmarkTreeFactory.CreateBinary(LevelCount);
        _expectedCount = NodeCount;

        if (BenchmarkTreeFactory.CountNodes(_roots) != _expectedCount)
        {
            throw new InvalidOperationException("The generated scaling hierarchy has an unexpected node count.");
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _model = new HierarchicalModel<BenchmarkNode>(new HierarchicalOptions<BenchmarkNode>
        {
            ChildrenSelector = static node => node.Children,
            IsLeafSelector = static node => node.Children.Count == 0,
            VirtualizeChildren = true,
        });
        _model.SetRoots(_roots);
    }

    [Benchmark]
    public int ExpandAll()
    {
        _model.ExpandAll();
        int count = _model.Count;
        if (count != _expectedCount)
        {
            throw new InvalidOperationException(
                $"Expanded row count mismatch: expected {_expectedCount}, got {count}.");
        }

        return count;
    }
}

public enum AsyncExpansionStrategy
{
    IncrementalInteractive,
    Batched,
}

[MemoryDiagnoser(displayGenColumns: false)]
[RankColumn]
public class AsyncHierarchyExpansionBenchmarks
{
    private IReadOnlyList<BenchmarkNode> _roots = null!;
    private HierarchicalModel _model = null!;
    private int _expectedCount;

    [Params(
        HierarchyShape.Wide2555Depth3,
        HierarchyShape.Binary4094Depth11,
        HierarchyShape.VeryDeep512Depth128)]
    public HierarchyShape Shape { get; set; }

    [Params(AsyncExpansionStrategy.IncrementalInteractive, AsyncExpansionStrategy.Batched)]
    public AsyncExpansionStrategy Strategy { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _roots = BenchmarkTreeFactory.Create(Shape);
        _expectedCount = BenchmarkTreeFactory.ExpectedCount(Shape);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelectorAsync = static (item, _) =>
                Task.FromResult<IEnumerable?>(((BenchmarkNode)item).Children),
            IsLeafSelector = static item => ((BenchmarkNode)item).Children.Count == 0,
            VirtualizeChildren = true,
        });
        _model.SetRoots(_roots);
    }

    [Benchmark]
    public async Task<int> ExpandAllAsync()
    {
        if (Strategy == AsyncExpansionStrategy.Batched)
        {
            await _model.ExpandAllAsync().ConfigureAwait(false);
        }
        else
        {
            await ExpandIncrementallyAsync(_model).ConfigureAwait(false);
        }

        int count = _model.Count;
        if (count != _expectedCount)
        {
            throw new InvalidOperationException(
                $"Expanded row count mismatch: expected {_expectedCount}, got {count}.");
        }

        return count;
    }

    private static async Task ExpandIncrementallyAsync(HierarchicalModel model)
    {
        var stack = new Stack<HierarchicalNode>();
        stack.Push(model.Root!);

        while (stack.Count > 0)
        {
            HierarchicalNode current = stack.Pop();
            await model.ExpandAsync(current).ConfigureAwait(false);

            IReadOnlyList<HierarchicalNode> children = current.Children;
            for (int i = children.Count - 1; i >= 0; i--)
            {
                stack.Push(children[i]);
            }
        }
    }
}

public sealed class BenchmarkNode
{
    public BenchmarkNode(int id, int depth)
    {
        Id = id;
        Depth = depth;
    }

    public int Id { get; }

    public int Depth { get; }

    public bool IsExpanded { get; set; }

    public List<BenchmarkNode> Children { get; } = new();
}

public static class BenchmarkTreeFactory
{
    public static IReadOnlyList<BenchmarkNode> Create(HierarchyShape shape)
    {
        return shape switch
        {
            HierarchyShape.Wide2555Depth3 => BuildWide(rootCount: 5, branchCount: 10, leafCount: 50),
            HierarchyShape.Binary4094Depth11 => BuildUniform(rootCount: 2, branching: 2, levelCount: 11),
            HierarchyShape.VeryDeep512Depth128 => BuildUniform(rootCount: 4, branching: 1, levelCount: 128),
            HierarchyShape.OptimizedSample149792Depth5 => BuildUniform(rootCount: 32, branching: 8, levelCount: 5),
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
    }

    public static int ExpectedCount(HierarchyShape shape)
    {
        return shape switch
        {
            HierarchyShape.Wide2555Depth3 => 2_555,
            HierarchyShape.Binary4094Depth11 => 4_094,
            HierarchyShape.VeryDeep512Depth128 => 512,
            HierarchyShape.OptimizedSample149792Depth5 => 149_792,
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
    }

    public static int CountNodes(IReadOnlyList<BenchmarkNode> roots)
    {
        int count = 0;
        var stack = new Stack<BenchmarkNode>();

        for (int i = roots.Count - 1; i >= 0; i--)
        {
            stack.Push(roots[i]);
        }

        while (stack.Count > 0)
        {
            BenchmarkNode node = stack.Pop();
            count++;

            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(node.Children[i]);
            }
        }

        return count;
    }

    public static void ResetExpandedState(IReadOnlyList<BenchmarkNode> roots)
    {
        var stack = new Stack<BenchmarkNode>(roots.Count);
        for (int i = roots.Count - 1; i >= 0; i--)
        {
            stack.Push(roots[i]);
        }

        while (stack.Count > 0)
        {
            BenchmarkNode node = stack.Pop();
            node.IsExpanded = false;
            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(node.Children[i]);
            }
        }
    }

    public static IReadOnlyList<BenchmarkNode> CreateBinary(int levelCount)
    {
        if (levelCount <= 0 || levelCount >= 31)
        {
            throw new ArgumentOutOfRangeException(nameof(levelCount));
        }

        return BuildUniform(rootCount: 2, branching: 2, levelCount);
    }

    public static IReadOnlyList<BenchmarkNode> CreateDeepChain(int nodeCount)
    {
        if (nodeCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeCount));
        }

        var root = new BenchmarkNode(id: 0, depth: 0);
        var current = root;
        for (int i = 1; i < nodeCount; i++)
        {
            var child = new BenchmarkNode(i, i);
            current.Children.Add(child);
            current = child;
        }

        return new[] { root };
    }

    private static IReadOnlyList<BenchmarkNode> BuildWide(int rootCount, int branchCount, int leafCount)
    {
        int id = 0;
        var roots = new List<BenchmarkNode>(rootCount);

        for (int rootIndex = 0; rootIndex < rootCount; rootIndex++)
        {
            var root = new BenchmarkNode(id++, depth: 0);
            roots.Add(root);

            for (int branchIndex = 0; branchIndex < branchCount; branchIndex++)
            {
                var branch = new BenchmarkNode(id++, depth: 1);
                root.Children.Add(branch);

                for (int leafIndex = 0; leafIndex < leafCount; leafIndex++)
                {
                    branch.Children.Add(new BenchmarkNode(id++, depth: 2));
                }
            }
        }

        return roots;
    }

    private static IReadOnlyList<BenchmarkNode> BuildUniform(int rootCount, int branching, int levelCount)
    {
        int id = 0;
        var roots = new List<BenchmarkNode>(rootCount);
        var queue = new Queue<BenchmarkNode>();

        for (int rootIndex = 0; rootIndex < rootCount; rootIndex++)
        {
            var root = new BenchmarkNode(id++, depth: 0);
            roots.Add(root);
            queue.Enqueue(root);
        }

        while (queue.Count > 0)
        {
            BenchmarkNode parent = queue.Dequeue();
            if (parent.Depth + 1 >= levelCount)
            {
                continue;
            }

            for (int childIndex = 0; childIndex < branching; childIndex++)
            {
                var child = new BenchmarkNode(id++, parent.Depth + 1);
                parent.Children.Add(child);
                queue.Enqueue(child);
            }
        }

        return roots;
    }
}

namespace GridHierarchyBenchmarks.Shared;

public enum TreeShape
{
    Wide2555Depth3,
    Deep4094Depth11,
    VeryDeep512Depth128,
    OptimizedSample149792Depth5,
}

public sealed class Node
{
    public Node(int id, int depth)
    {
        Id = id;
        Depth = depth;
        Name = $"Node {id:N0} at depth {depth}";
        Payload = $"Payload-{id % 997:D3}";
        Date = new DateTime(2020, 1, 1).AddDays(id % 3_650);
        Time = TimeSpan.FromSeconds(id % 86_400);
        Phone = $"(555) {id % 1_000:D3}-{id % 10_000:D4}";
        Category = $"Category-{id % 997:D3}";
        SliderValue = (id % 1_000) / 10d;
    }

    public int Id { get; }

    public int Depth { get; }

    public string Name { get; }

    public string Payload { get; }

    public DateTime Date { get; }

    public TimeSpan Time { get; }

    public string Phone { get; }

    public string Category { get; }

    public double SliderValue { get; }

    public int ChildCount => Children.Count;

    public bool HasChildren => Children.Count != 0;

    public List<Node> Children { get; } = new();
}

public static class TreeDataFactory
{
    public static IReadOnlyList<Node> Create(TreeShape shape)
    {
        return shape switch
        {
            TreeShape.Wide2555Depth3 => BuildWide(rootCount: 5, branchCount: 10, leafCount: 50),
            TreeShape.Deep4094Depth11 => BuildUniform(rootCount: 2, branching: 2, levelCount: 11),
            TreeShape.VeryDeep512Depth128 => BuildUniform(rootCount: 4, branching: 1, levelCount: 128),
            TreeShape.OptimizedSample149792Depth5 => BuildUniform(rootCount: 32, branching: 8, levelCount: 5),
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
    }

    public static int ExpectedCount(TreeShape shape)
    {
        return shape switch
        {
            TreeShape.Wide2555Depth3 => 2_555,
            TreeShape.Deep4094Depth11 => 4_094,
            TreeShape.VeryDeep512Depth128 => 512,
            TreeShape.OptimizedSample149792Depth5 => 149_792,
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
    }

    public static int CountNodes(IReadOnlyList<Node> roots)
    {
        var count = 0;
        var stack = new Stack<Node>(roots.Reverse());

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            ++count;

            for (var i = node.Children.Count - 1; i >= 0; --i)
                stack.Push(node.Children[i]);
        }

        return count;
    }

    public static IReadOnlyList<Node> CreateStressWide20420Depth3() =>
        BuildWide(rootCount: 20, branchCount: 20, leafCount: 50);

    private static IReadOnlyList<Node> BuildWide(int rootCount, int branchCount, int leafCount)
    {
        var id = 0;
        var roots = new List<Node>(rootCount);

        for (var rootIndex = 0; rootIndex < rootCount; ++rootIndex)
        {
            var root = new Node(id++, depth: 0);
            roots.Add(root);

            for (var branchIndex = 0; branchIndex < branchCount; ++branchIndex)
            {
                var branch = new Node(id++, depth: 1);
                root.Children.Add(branch);

                for (var leafIndex = 0; leafIndex < leafCount; ++leafIndex)
                    branch.Children.Add(new Node(id++, depth: 2));
            }
        }

        return roots;
    }

    private static IReadOnlyList<Node> BuildUniform(int rootCount, int branching, int levelCount)
    {
        var id = 0;
        var roots = new List<Node>(rootCount);
        var queue = new Queue<Node>();

        for (var rootIndex = 0; rootIndex < rootCount; ++rootIndex)
        {
            var root = new Node(id++, depth: 0);
            roots.Add(root);
            queue.Enqueue(root);
        }

        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            if (parent.Depth + 1 >= levelCount)
                continue;

            for (var childIndex = 0; childIndex < branching; ++childIndex)
            {
                var child = new Node(id++, parent.Depth + 1);
                parent.Children.Add(child);
                queue.Enqueue(child);
            }
        }

        return roots;
    }
}

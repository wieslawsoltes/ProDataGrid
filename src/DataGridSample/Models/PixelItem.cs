using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace DataGridSample.Models
{
    /// <summary>
    /// Simple model to showcase pixel-based column widths and horizontal scrolling.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
    public class PixelItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        public static PixelItem Create(int id, Random random)
        {
            string[] categories = { "Alpha", "Beta", "Gamma", "Delta", "Omega" };
            string[] adjectives = { "Bright", "Calm", "Swift", "Bold", "Misty", "Silent", "Verdant", "Crimson" };
            string[] nouns = { "Forest", "Canyon", "River", "Valley", "Harbor", "Summit", "Meadow", "Coast" };

            string name = $"{adjectives[random.Next(adjectives.Length)]} {nouns[random.Next(nouns.Length)]}";

            int lineCount = random.Next(1, 4);
            string description = string.Join(" ", System.Linq.Enumerable.Range(0, lineCount)
                .Select(i => $"{name} sample text line {i + 1}"));

            string notes = $"Note #{id}: " + string.Join(" ", System.Linq.Enumerable.Range(0, 3)
                .Select(i => adjectives[random.Next(adjectives.Length)]));

            return new PixelItem
            {
                Id = id,
                Name = name,
                Category = categories[random.Next(categories.Length)],
                Description = description,
                Notes = notes
            };
        }
    }
}

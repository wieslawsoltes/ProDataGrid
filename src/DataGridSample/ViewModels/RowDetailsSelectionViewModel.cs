using System.Collections.ObjectModel;
using Avalonia.Controls;
using DataGridSample.Models;
using ProDataGrid.SourceGeneration;
using ReactiveUI;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(BookDetail), ProviderName = "RowDetailsBookSchema")]
[GenerateDataGridView(
    typeof(BookDetail),
    ViewName = "RowDetailsSelectionPage",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.GridOnly,
    Title = "Row Details Selection",
    ItemsPropertyName = nameof(Books),
    RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.VisibleWhenSelected,
    RowDetailsNestedItemType = typeof(AuthorDetail),
    RowDetailsNestedItemsMember = nameof(BookDetail.Authors),
    RowDetailsNestedProviderName = "RowDetailsAuthorSchema",
    RowDetailsSummaryMember = nameof(BookDetail.Summary),
    RowDetailsAutomationId = "row-details-authors-grid",
    AutomationId = "row-details-books-grid")]
public sealed partial class RowDetailsSelectionViewModel : ReactiveObject
{
    public RowDetailsSelectionViewModel()
    {
        Books = new ObservableCollection<BookDetail>
        {
            new()
            {
                Title = "Avalonia in Practice",
                InStock = 12,
                Summary = "Practical patterns for building desktop and cross-platform apps.",
                Authors =
                {
                    new AuthorDetail { Name = "R. Lawson", Contribution = "Lead" },
                    new AuthorDetail { Name = "M. Chen", Contribution = "UI" }
                }
            },
            new()
            {
                Title = "Data Grids Deep Dive",
                InStock = 5,
                Summary = "Virtualization, selection, and row details explained with samples.",
                Authors =
                {
                    new AuthorDetail { Name = "S. Alvarez", Contribution = "Author" },
                    new AuthorDetail { Name = "K. Novak", Contribution = "Reviewer" }
                }
            },
            new()
            {
                Title = "Reactive MVVM",
                InStock = 8,
                Summary = "Techniques for responsive UI and data binding.",
                Authors =
                {
                    new AuthorDetail { Name = "J. Patel", Contribution = "Author" }
                }
            },
            new()
            {
                Title = "UI Testing Toolkit",
                InStock = 3,
                Summary = "Strategies for reliable UI automation in complex grids.",
                Authors =
                {
                    new AuthorDetail { Name = "C. Nguyen", Contribution = "Lead" },
                    new AuthorDetail { Name = "P. Ito", Contribution = "Contributor" }
                }
            }
        };
    }

    public ObservableCollection<BookDetail> Books { get; }
}

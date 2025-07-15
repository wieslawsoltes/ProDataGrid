using System.Collections.Generic;
namespace Avalonia.Controls.Utils
{
    internal static class ListExtensions
    {
        public static void InsertMany<T>(this List<T> list, int index, T item, int count)
        {
            for (int i = 0; i < count; i++)
            {
                list.Insert(index + i, item);
            }
        }
    }
}

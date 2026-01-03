using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace DataGridSample.Collections
{
    public sealed class FilteredObservableRangeCollection<T> : ObservableRangeCollection<T>, IDisposable
    {
        private readonly ObservableRangeCollection<T> _source;
        private Func<T, bool> _predicate;

        public FilteredObservableRangeCollection(ObservableRangeCollection<T> source, Func<T, bool> predicate)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));

            Refresh();
            _source.CollectionChanged += OnSourceChanged;
        }

        public void UpdatePredicate(Func<T, bool> predicate)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            Refresh();
        }

        public void Refresh()
        {
            if (_source.Count == 0)
            {
                ResetWith(Array.Empty<T>());
                return;
            }

            var filtered = new List<T>(_source.Count);
            foreach (var item in _source)
            {
                if (_predicate(item))
                {
                    filtered.Add(item);
                }
            }

            ResetWith(filtered);
        }

        public void Dispose()
        {
            _source.CollectionChanged -= OnSourceChanged;
        }

        private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Refresh();
        }
    }
}

// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using Avalonia.Media;

namespace Avalonia.Controls
{
    internal sealed class DataGridCustomDrawingTextLayoutCache
    {
        private readonly Dictionary<CacheKey, LinkedListNode<CacheEntry>> _entries = new();
        private readonly LinkedList<CacheEntry> _lru = new();
        private int _capacity;

        public DataGridCustomDrawingTextLayoutCache(int capacity)
        {
            _capacity = NormalizeCapacity(capacity);
        }

        public int Capacity
        {
            get => _capacity;
            set
            {
                var normalized = NormalizeCapacity(value);
                if (_capacity == normalized)
                {
                    return;
                }

                _capacity = normalized;
                TrimToCapacity();
            }
        }

        public void Clear()
        {
            _entries.Clear();
            _lru.Clear();
        }

        public FormattedText GetOrCreate(in CacheKey key, Func<FormattedText> factory)
        {
            if (_entries.TryGetValue(key, out LinkedListNode<CacheEntry> node))
            {
                _lru.Remove(node);
                _lru.AddFirst(node);
                return node.Value.Text;
            }

            var text = factory();
            var entry = new CacheEntry(key, text);
            var newNode = new LinkedListNode<CacheEntry>(entry);
            _lru.AddFirst(newNode);
            _entries[key] = newNode;
            TrimToCapacity();
            return text;
        }

        private static int NormalizeCapacity(int capacity)
        {
            return capacity <= 0 ? 1 : capacity;
        }

        private void TrimToCapacity()
        {
            while (_entries.Count > _capacity)
            {
                LinkedListNode<CacheEntry> last = _lru.Last;
                if (last == null)
                {
                    return;
                }

                _entries.Remove(last.Value.Key);
                _lru.RemoveLast();
            }
        }

        private readonly struct CacheEntry
        {
            public CacheEntry(CacheKey key, FormattedText text)
            {
                Key = key;
                Text = text;
            }

            public CacheKey Key { get; }

            public FormattedText Text { get; }
        }

        public readonly struct CacheKey : IEquatable<CacheKey>
        {
            private readonly string _text;
            private readonly string _fontFamilyName;
            private readonly FontStyle _fontStyle;
            private readonly FontWeight _fontWeight;
            private readonly FontStretch _fontStretch;
            private readonly TextAlignment _textAlignment;
            private readonly TextTrimming _textTrimming;
            private readonly FlowDirection _flowDirection;
            private readonly int _cultureLcid;
            private readonly double _fontSize;
            private readonly double _maxTextWidth;
            private readonly double _maxTextHeight;
            private readonly byte _foregroundKind;
            private readonly Color _foregroundColor;
            private readonly double _foregroundOpacity;
            private readonly int _foregroundIdentityHash;

            public CacheKey(
                string text,
                string fontFamilyName,
                FontStyle fontStyle,
                FontWeight fontWeight,
                FontStretch fontStretch,
                double fontSize,
                TextAlignment textAlignment,
                TextTrimming textTrimming,
                FlowDirection flowDirection,
                int cultureLcid,
                double maxTextWidth,
                double maxTextHeight,
                byte foregroundKind,
                Color foregroundColor,
                double foregroundOpacity,
                int foregroundIdentityHash)
            {
                _text = text;
                _fontFamilyName = fontFamilyName;
                _fontStyle = fontStyle;
                _fontWeight = fontWeight;
                _fontStretch = fontStretch;
                _fontSize = fontSize;
                _textAlignment = textAlignment;
                _textTrimming = textTrimming;
                _flowDirection = flowDirection;
                _cultureLcid = cultureLcid;
                _maxTextWidth = maxTextWidth;
                _maxTextHeight = maxTextHeight;
                _foregroundKind = foregroundKind;
                _foregroundColor = foregroundColor;
                _foregroundOpacity = foregroundOpacity;
                _foregroundIdentityHash = foregroundIdentityHash;
            }

            public bool Equals(CacheKey other)
            {
                return string.Equals(_text, other._text, StringComparison.Ordinal) &&
                       string.Equals(_fontFamilyName, other._fontFamilyName, StringComparison.Ordinal) &&
                       _fontStyle == other._fontStyle &&
                       _fontWeight == other._fontWeight &&
                       _fontStretch == other._fontStretch &&
                       _fontSize.Equals(other._fontSize) &&
                       _textAlignment == other._textAlignment &&
                       _textTrimming == other._textTrimming &&
                       _flowDirection == other._flowDirection &&
                       _cultureLcid == other._cultureLcid &&
                       _maxTextWidth.Equals(other._maxTextWidth) &&
                       _maxTextHeight.Equals(other._maxTextHeight) &&
                       _foregroundKind == other._foregroundKind &&
                       _foregroundColor.Equals(other._foregroundColor) &&
                       _foregroundOpacity.Equals(other._foregroundOpacity) &&
                       _foregroundIdentityHash == other._foregroundIdentityHash;
            }

            public override bool Equals(object obj)
            {
                return obj is CacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                var hash = new HashCode();
                hash.Add(_text, StringComparer.Ordinal);
                hash.Add(_fontFamilyName, StringComparer.Ordinal);
                hash.Add(_fontStyle);
                hash.Add(_fontWeight);
                hash.Add(_fontStretch);
                hash.Add(_fontSize);
                hash.Add(_textAlignment);
                hash.Add(_textTrimming);
                hash.Add(_flowDirection);
                hash.Add(_cultureLcid);
                hash.Add(_maxTextWidth);
                hash.Add(_maxTextHeight);
                hash.Add(_foregroundKind);
                hash.Add(_foregroundColor);
                hash.Add(_foregroundOpacity);
                hash.Add(_foregroundIdentityHash);
                return hash.ToHashCode();
            }
        }
    }
}

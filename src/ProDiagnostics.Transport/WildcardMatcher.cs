using System;
using System.Collections.Generic;

namespace ProDiagnostics.Transport;

public static class WildcardMatcher
{
    public static bool IsMatch(string value, IReadOnlyList<string> patterns)
    {
        if (patterns.Count == 0)
        {
            return true;
        }

        for (var i = 0; i < patterns.Count; i++)
        {
            if (IsMatch(value, patterns[i]))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsMatch(string value, string pattern)
    {
        if (pattern == "*" || pattern.Length == 0)
        {
            return true;
        }

        var valueIndex = 0;
        var patternIndex = 0;
        var starIndex = -1;
        var matchIndex = 0;

        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length && (pattern[patternIndex] == '?' || pattern[patternIndex] == value[valueIndex]))
            {
                valueIndex++;
                patternIndex++;
                continue;
            }

            if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starIndex = patternIndex++;
                matchIndex = valueIndex;
                continue;
            }

            if (starIndex != -1)
            {
                patternIndex = starIndex + 1;
                valueIndex = ++matchIndex;
                continue;
            }

            return false;
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }
}

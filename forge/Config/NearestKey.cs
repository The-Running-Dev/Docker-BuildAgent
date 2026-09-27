using System;
using System.Collections.Generic;
using System.Linq;

namespace Config;

/// <summary>
/// Finds the closest known key to an unrecognized one, by Levenshtein distance, for
/// <see cref="ConfigErrorCode.UnknownKey"/> messages (S6.5: "naming the key and the nearest known
/// key").
/// </summary>
internal static class NearestKey
{
    public static string? Find(string key, IEnumerable<string> knownKeys)
    {
        string? nearest = null;
        var bestDistance = int.MaxValue;

        foreach (var candidate in knownKeys)
        {
            var distance = Distance(key, candidate);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private static int Distance(string a, string b)
    {
        var lengths = new int[a.Length + 1, b.Length + 1];

        for (var i = 0; i <= a.Length; i++) lengths[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) lengths[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                lengths[i, j] = Math.Min(
                    Math.Min(lengths[i - 1, j] + 1, lengths[i, j - 1] + 1),
                    lengths[i - 1, j - 1] + cost);
            }
        }

        return lengths[a.Length, b.Length];
    }
}

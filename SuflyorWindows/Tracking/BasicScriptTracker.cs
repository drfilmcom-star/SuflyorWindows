using System.Text.RegularExpressions;

namespace SuflyorWindows.Tracking;

/// <summary>
/// Conservative first Windows port of the Suflyor tracking concept: normalize Russian words,
/// align a rolling ASR tail against script words, and require repeated evidence for distant jumps.
/// The full Android ScriptTracker port remains a separate parity task.
/// </summary>
public sealed class BasicScriptTracker
{
    private static readonly HashSet<string> Fillers = new(StringComparer.Ordinal)
        { "э", "ээ", "эээ", "эм", "эмм", "мм", "ммм", "хм", "ага", "угу" };
    private string[] _script = Array.Empty<string>();
    private readonly List<string> _finalized = new();
    private string[] _partial = Array.Empty<string>();
    private int _pendingTarget = -1;

    public int Position { get; private set; }
    public int Count => _script.Length;

    public void SetScript(string text)
    {
        _script = Words(text).ToArray();
        Reset(0);
    }

    public void Reset(int position = 0)
    {
        Position = Math.Clamp(position, 0, _script.Length);
        _pendingTarget = -1;
        _finalized.Clear();
        _partial = Array.Empty<string>();
    }

    public bool AddHypothesis(string text, bool isFinal, out int newPosition)
    {
        _partial = Words(text).Where(w => !Fillers.Contains(w)).ToArray();
        if (isFinal)
        {
            _finalized.AddRange(_partial);
            if (_finalized.Count > 16) _finalized.RemoveRange(0, _finalized.Count - 16);
            _partial = Array.Empty<string>();
        }
        var heard = _finalized.Concat(_partial).TakeLast(12).ToArray();
        newPosition = Position;
        if (heard.Length == 0 || _script.Length == 0) return false;

        var bestStart = -1;
        var bestScore = 0;
        var secondScore = 0;
        var maxLen = Math.Min(heard.Length, 8);

        // Search contiguous local windows; partial tail words are accepted only at a modest prefix threshold.
        for (var start = 0; start < _script.Length; start++)
        {
            var score = 0;
            var matches = 0;
            for (var h = 0; h < maxLen && start + h < _script.Length; h++)
            {
                var a = heard[heard.Length - maxLen + h];
                var b = _script[start + h];
                if (a == b || (a.Length >= 3 && b.StartsWith(a, StringComparison.Ordinal)) ||
                    (b.Length >= 4 && a.Length >= 4 && CommonPrefix(a, b) >= Math.Min(4, Math.Min(a.Length, b.Length))))
                {
                    score += b.Length >= 5 ? 3 : 1;
                    matches++;
                }
            }
            if (matches < 2) continue;
            if (score > bestScore) { secondScore = bestScore; bestScore = score; bestStart = start; }
            else if (score > secondScore) secondScore = score;
        }

        if (bestStart < 0) return false;
        var target = Math.Min(_script.Length, bestStart + maxLen);
        var delta = target - Position;
        var strong = bestScore >= 6 && bestScore - secondScore >= 2;
        if (Math.Abs(delta) > 12 && !strong)
        {
            _pendingTarget = -1;
            return false;
        }
        if (Math.Abs(delta) > 12)
        {
            if (_pendingTarget < 0 || Math.Abs(_pendingTarget - target) > 2)
            {
                _pendingTarget = target;
                return false;
            }
        }
        _pendingTarget = -1;
        if (target <= Position && delta < -2 && !isFinal) return false;
        var moved = target != Position;
        Position = target;
        newPosition = Position;
        return moved;
    }

    private static IEnumerable<string> Words(string text)
    {
        foreach (Match m in Regex.Matches(text.ToLowerInvariant().Replace('ё', 'е'), @"[\p{L}\p{N}]+"))
            yield return m.Value;
    }

    private static int CommonPrefix(string a, string b)
    {
        var i = 0;
        while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
        return i;
    }
}

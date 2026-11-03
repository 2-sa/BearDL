using System;
using System.Collections.Generic;

namespace Nickvision.Parabolic.Shared.Helpers;

/// <summary>A thread-safe tail of diagnostic output, bounded by characters.</summary>
public sealed class BoundedLog
{
    public const int DefaultCapacity = 256 * 1024;
    private readonly object _sync = new();
    private readonly Queue<string> _lines = new();
    private readonly int _capacity;
    private int _length;
    private string? _snapshot;

    public BoundedLog(int capacity = DefaultCapacity)
    {
        if (capacity <= Environment.NewLine.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
        _capacity = capacity;
    }

    public void Enqueue(string line)
    {
        lock (_sync)
        {
            var limit = _capacity - Environment.NewLine.Length;
            if (line.Length > limit)
            {
                line = line[^limit..];
            }
            var size = line.Length + Environment.NewLine.Length;
            while (_length + size > _capacity && _lines.TryDequeue(out var removed))
            {
                _length -= removed.Length + Environment.NewLine.Length;
            }
            _lines.Enqueue(line);
            _length += size;
            _snapshot = null;
        }
    }

    public override string ToString()
    {
        lock (_sync)
        {
            return _snapshot ??= string.Join(Environment.NewLine, _lines);
        }
    }
}

// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

namespace SharpEmu.Libs.VideoOut;

// One active snapshot and one replacement snapshot bound pending work.
internal sealed class PipelineCacheWriter(Action<byte[]> write, Action<Exception> reportFailure) : IDisposable
{
    private readonly object _gate = new();
    private byte[]? _pending;
    private Task? _worker;
    private bool _closed;

    public void Enqueue(byte[] snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            _pending = snapshot;
            _worker ??= Task.Run(Drain);
        }
    }

    private void Drain()
    {
        while (true)
        {
            byte[] snapshot;
            lock (_gate)
            {
                if (_pending is null)
                {
                    _worker = null;
                    return;
                }
                snapshot = _pending;
                _pending = null;
            }
            try { write(snapshot); }
            catch (Exception exception)
            {
                // A reporting failure must not strand a newer snapshot.
                try { reportFailure(exception); }
                catch { }
            }
        }
    }

    public void Dispose()
    {
        Task? worker;
        lock (_gate)
        {
            _closed = true;
            worker = _worker;
        }
        worker?.GetAwaiter().GetResult();
    }
}

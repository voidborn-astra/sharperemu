// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Libs.VideoOut;
using Xunit;

namespace SharpEmu.Libs.Tests.VideoOut;

public sealed class PipelineCacheWriterTests
{
    [Fact]
    public void PendingSnapshotIsReplacedAndShutdownDrainsTheLatest()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var written = new List<byte>();
        var failures = new List<Exception>();
        using var writer = new PipelineCacheWriter(snapshot =>
        {
            if (snapshot[0] == 1)
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            }
            written.Add(snapshot[0]);
        }, failures.Add);
        try
        {
            writer.Enqueue([1]);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            writer.Enqueue([2]);
            writer.Enqueue([3]);
        }
        finally { release.Set(); }
        writer.Dispose();
        Assert.Empty(failures);
        Assert.Equal(new byte[] { 1, 3 }, written);
        Assert.Throws<ObjectDisposedException>(() => writer.Enqueue([4]));
    }

    [Fact]
    public void FailedWriteDoesNotBlockTheNextSnapshot()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var written = new List<byte>();
        var failures = new List<Exception>();
        using var writer = new PipelineCacheWriter(snapshot =>
        {
            if (snapshot[0] == 1)
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                throw new IOException("Test write failure.");
            }
            written.Add(snapshot[0]);
        }, failures.Add);
        try
        {
            writer.Enqueue([1]);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            writer.Enqueue([2]);
        }
        finally { release.Set(); }
        writer.Dispose();
        Assert.IsType<IOException>(Assert.Single(failures));
        Assert.Equal(new byte[] { 2 }, written);
    }

    [Fact]
    public void DisposeWithoutWorkIsSafe()
    {
        using var writer = new PipelineCacheWriter(_ => throw new InvalidOperationException(), _ => { });
        writer.Dispose();
        Assert.Throws<ObjectDisposedException>(() => writer.Enqueue([1]));
    }
}

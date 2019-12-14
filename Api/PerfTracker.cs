﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace UAlbion.Api
{
    public static class PerfTracker
    {
        class FrameTimeTracker : IDisposable
        {
            readonly Stopwatch _stopwatch = Stopwatch.StartNew();
            readonly string _name;

            public FrameTimeTracker(string name) { _name = name; }

            public void Dispose()
            {
                lock (_syncRoot)
                {
                    long ticks = _stopwatch.ElapsedTicks;
                    if (!_frameTimes.ContainsKey(_name))
                        _frameTimes[_name] = new Stats { Fast = ticks, Med = ticks, Slow = ticks };

                    var stats = _frameTimes[_name];
                    stats.Total += ticks;
                    stats.Fast = (ticks + 8*stats.Fast) / 9.0f;
                    stats.Med = (ticks + 60*stats.Med) / 61.0f;
                    stats.Slow = (ticks + 600*stats.Slow) / 601.0f;
                    if (stats.Min > ticks) stats.Min = ticks;
                    if (stats.Max < ticks) stats.Max = ticks;
                }
            }
        }

        class InfrequentTracker : IDisposable
        {
            readonly Stopwatch _stopwatch = Stopwatch.StartNew();
            readonly string _name;

            public InfrequentTracker(string name)
            {
                _name = name;
#if DEBUG
                Console.WriteLine($"Starting {name}");
#endif
                CoreTrace.Log.StartupEvent(name);
            }

            public void Dispose()
            {
#if DEBUG
                Console.WriteLine($"Finished {_name} in {_stopwatch.ElapsedMilliseconds} ms");
#endif
                CoreTrace.Log.StartupEvent(_name);
            }
        }

        class Stats
        {
            public long Total { get; set; }
            public long Min { get; set; } = long.MaxValue;
            public long Max { get; set; } = long.MinValue;
            public float Fast { get; set; }
            public float Med { get; set; }
            public float Slow { get; set; }
        }

        static readonly Stopwatch _startupStopwatch = Stopwatch.StartNew();
        static readonly IDictionary<string, Stats> _frameTimes = new Dictionary<string, Stats>();
        static readonly object _syncRoot = new object();
        static int _frameCount;

        public static void BeginFrame() { _frameCount++; }

        public static void StartupEvent(string name)
        {
            if (_frameCount == 0)
            {
//#if DEBUG
                Console.WriteLine($"at {_startupStopwatch.ElapsedMilliseconds}: {name}");
//#endif
                CoreTrace.Log.StartupEvent(name);
            }
        }

        public static IDisposable InfrequentEvent(string name) => new InfrequentTracker(name);

        public static IDisposable FrameEvent(string name) => new FrameTimeTracker(name);
        public static string GetFrameStats()
        {
            var sb = new StringBuilder();
            lock(_syncRoot)
            {
                foreach (var kvp in _frameTimes.OrderBy(x => x.Key))
                {
                    sb.Append(kvp.Key);
                    sb.Append($" Avg: {(float) kvp.Value.Total / (10000 * _frameCount):F3}");
                    sb.Append($" Min: {(float) kvp.Value.Min / 10000:F3}");
                    sb.Append($" Max: {(float) kvp.Value.Max / 10000:F3}");
                    sb.Append($" F:{kvp.Value.Fast / 10000:F3}");
                    sb.Append($" M:{kvp.Value.Med / 10000:F3}");
                    sb.Append($" S:{kvp.Value.Slow / 10000:F3}");
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
    }
}

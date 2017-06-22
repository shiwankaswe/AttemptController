﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Tester
{
    public class DebugLogger
    {
        private readonly DateTime whenStarted;
        private long _lastMemory = 0;
        private DateTime _lastEventTime;

        public DebugLogger()
        {
            whenStarted = _lastEventTime = DateTime.UtcNow;
        }

        public void WriteStatus(string status, params Object[] args)
        {
            DateTime now = DateTime.UtcNow;
            TimeSpan eventTime = now - whenStarted;
            TimeSpan sinceLastEvent = now - _lastEventTime;
            long eventMemory = GC.GetTotalMemory(false);
            long memDiff = eventMemory - _lastMemory;
            long eventMemoryMB = eventMemory / (1024L * 1024L);
            long memDiffMB = memDiff / (1024L * 1024L);
            _lastMemory = eventMemory;
            _lastEventTime = now;
            Console.Out.WriteLine("Time: {0:00}:{1:00}:{2:00}.{3:000},  Memory: {5}MB",
                eventTime.Hours,
                eventTime.Minutes,
                eventTime.Seconds,
                eventTime.Milliseconds,
                sinceLastEvent.TotalSeconds,
                eventMemoryMB, memDiffMB);
            Console.Out.WriteLine(status, args);

        }
    }
}

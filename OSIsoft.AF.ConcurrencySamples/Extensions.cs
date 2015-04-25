// Copyright (c) OSIsoft, LLC. All rights reserved. See LICENSE for licensing information.

using System;
using System.Threading;
using System.Threading.Tasks;
using OSIsoft.AF;

namespace Samples
{
    public static class Extensions
    {
        public static T Find<T>(this PISystems systems, string path)
            where T : AFObject
        {
            if (systems.DefaultPISystem == null)
            {
                throw new InvalidOperationException("Default PISystem must be set.");
            }

            return AFObject.FindObject(path, systems.DefaultPISystem) as T;
        }

        public static Task RunConcurrent(
            this ConcurrentExclusiveSchedulerPair schedulerPair,
            Action action)
        {
            return RunActionOnScheduler(schedulerPair.ConcurrentScheduler, action);
        }

        public static Task RunExclusive(
            this ConcurrentExclusiveSchedulerPair schedulerPair,
            Action action)
        {
            return RunActionOnScheduler(schedulerPair.ExclusiveScheduler, action);
        }

        private static Task RunActionOnScheduler(TaskScheduler scheduler, Action action)
        {
            return Task.Factory.StartNew(
                action,
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                scheduler);
        }
    }
}

// Copyright (c) OSIsoft, LLC. All rights reserved. See LICENSE for licensing information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OSIsoft.AF;
using OSIsoft.AF.Asset;
using SimpleImpersonation;
using Xunit;

namespace Samples
{
    public class Assertions : IDisposable
    {
        private const string elementPath = @"\\MyAssets\MyDatabase\MyElement";
        private AFElement myElement;
        
        #region Plumbing
        
        // This region contains code that helps us restore to a known good state before
        // and after each assertion runs.

        public Assertions()
        {
            myElement = new PISystems(true).Find<AFElement>(elementPath);
            myElement.CheckOut();
            myElement.Elements.Add("MyChild");
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Clean up 'myElement'. Do this in a new PISystems context - the 
                // current context might be corrupt due to the nature of some
                // of the assertions we plan to make.
                myElement = new PISystems(true).Find<AFElement>(elementPath);
                myElement.UndoCheckOut(true);
                myElement.Elements.ToList().ForEach(x => x.Delete());
                myElement.Database.CheckIn();

                // Restore default values.
                AFGlobalSettings.CacheMaxObjects = 10000;
                AFGlobalSettings.CacheTime = 120;
            }
        }

        #endregion
        
        [Fact(DisplayName = "01: Read/Write Collision")]
        public async Task ReadAndWriteCollision()
        {
            Task writer = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    myElement.Elements.Add("Child Element " + i);
                }
            });

            Task reader = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    foreach (AFElement element in myElement.Elements)
                    {
                        JsonConvert.SerializeObject(element);
                    }
                }
            });

            Exception exception = await Assert.ThrowsAsync(
                typeof(InvalidOperationException),
                () => Task.WhenAll(reader, writer));

            Assert.Equal(
                "Collection was modified; enumeration operation may not execute.", 
                exception.Message);
        }

        [Fact(DisplayName = "02: Write/Refresh Deadlock")]
        public async Task WriteAndRefreshDeadlock()
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                Task refresh = Task.Run(() =>
                {
                    cts.Token.Register(Thread.CurrentThread.Abort);

                    for (int i = 0; i < 100; i++)
                    {
                        myElement.Refresh();
                    }
                });

                Task writer = Task.Run(() =>
                {
                    cts.Token.Register(Thread.CurrentThread.Abort);

                    for (int i = 0; i < 100; i++)
                    {
                        myElement.Elements.Add("Child Element " + i);
                        myElement.CheckIn();
                    }
                });

                Task timeout = Task.Delay(TimeSpan.FromSeconds(10));

                await Task.WhenAny(refresh, writer, timeout);

                Assert.True(timeout.IsCompleted);
                Assert.False(refresh.IsCompleted);
                Assert.False(writer.IsCompleted);

                cts.Cancel();
            }
        }

        [Fact(DisplayName = "03: Observe AF Cache")]
        public void ObserveAFCache()
        {
            AFElement element1 = AFObject.FindObject(elementPath) as AFElement;
            AFElement element2 = AFObject.FindObject(elementPath) as AFElement;

            Assert.Same(element1, element2);
        }

        [Fact(DisplayName = "04: Observe AF Cache Per User")]
        public void ObserveAFCachePerUser()
        {
            AFElement element1 = AFObject.FindObject(elementPath) as AFElement;
            AFElement element2 = AFObject.FindObject(elementPath) as AFElement;
            Assert.Same(element1, element2);

            // You'll need a local user named 'testuser' with password 
            // '@bcd1234' to run this case.
            using (Impersonation.LogonUser(
                null, "testuser", "@bcd1234", LogonType.Network))
            {
                element2 = AFObject.FindObject(elementPath) as AFElement;
            }

            Assert.NotSame(element1, element2);
        }

        [Fact(DisplayName = "05: Force New Instance")]
        public void ForceNewInstance()
        {
            PISystems systems1 = new PISystems(true);
            AFElement element1 = AFObject.FindObject(elementPath,
                systems1.DefaultPISystem) as AFElement;

            PISystems systems2 = new PISystems(true);
            AFElement element2 = AFObject.FindObject(elementPath,
                systems2.DefaultPISystem) as AFElement;

            Assert.NotSame(element1, element2);

            #region Shorthand
            // In shorthand (using an extension method)
            element1 = systems1.Find<AFElement>(elementPath);
            element2 = systems2.Find<AFElement>(elementPath);
            Assert.NotSame(element1, element2);
            #endregion
        }

        [Fact(DisplayName = "06: View Caching Defaults")]
        public void ViewCachingDefaults()
        {
            Assert.Equal(10000, AFGlobalSettings.CacheMaxObjects);
            Assert.Equal(120, AFGlobalSettings.CacheTime); // 120 seconds
        }

        [Fact(DisplayName = "07: 'Disable' AF Cache")]
        public void DisableAFCache()
        {
            AFGlobalSettings.CacheMaxObjects = 0;
            AFGlobalSettings.CacheTime = 0;

            PISystems systems = new PISystems(true);
            AFElement element1 = systems.Find<AFElement>(elementPath);
            AFElement element2 = systems.Find<AFElement>(elementPath);

            Assert.Same(element1, element2);
        }

        [Fact(DisplayName = "08: Observe Garbage Collector Behavior")]
        public void ObserveGarbageCollectorBehavior()
        {
            #region Case One
            AFGlobalSettings.CacheMaxObjects = 100;
            
            PISystems systems = new PISystems(true);
            AFElement myElement = systems.Find<AFElement>(elementPath);
            WeakReference<AFElement> myRef = new WeakReference<AFElement>(myElement);
            myElement = null;

            GC.Collect();
            Assert.True(myRef.TryGetTarget(out myElement));
            #endregion

            #region Case Two
            AFGlobalSettings.CacheMaxObjects = 0;

            systems = new PISystems(true);
            myElement = systems.Find<AFElement>(elementPath);
            myRef = new WeakReference<AFElement>(myElement);
            myElement = null;

            GC.Collect();
            Assert.False(myRef.TryGetTarget(out myElement));
            #endregion
        }

        [Fact(DisplayName = "09: Demonstrate Monitor")]
        public async Task DemonstrateMonitor()
        {
            object syncLock = new object();

            Task writer = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    lock (syncLock)
                    {
                        myElement.Elements.Add("Child Element " + i);
                    }
                }
            });

            Task reader = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    lock (syncLock)
                    {
                        foreach (AFElement element in myElement.Elements)
                        {
                            JsonConvert.SerializeObject(element);
                        }
                    }
                }
            });

            await Task.WhenAll(reader, writer);
        }

        [Fact(DisplayName = "10: Demonstrate Reader/Writer Lock")]
        public async Task DemonstrateReaderWriterLock()
        {
            using (ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim())
            {
                Task writer = Task.Run(() =>
                {
                    Parallel.For(1, 100, i =>
                    {
                        rwLock.EnterWriteLock();

                        try
                        {
                            myElement.Elements.Add("Child Element " + i);
                        }
                        finally
                        {
                            rwLock.ExitWriteLock();
                        }
                    });
                });

                Task reader = Task.Run(() =>
                {
                    Parallel.For(1, 100, i =>
                    {
                        rwLock.EnterReadLock();

                        try
                        {
                            foreach (AFElement element in myElement.Elements)
                            {
                                JsonConvert.SerializeObject(element);
                            }
                        }
                        finally
                        {
                            rwLock.ExitReadLock();
                        }
                    });
                });

                await Task.WhenAll(reader, writer);
            }
        }

        [Fact(DisplayName = "11: Demonstrate Concurrent/Exclusive Scheduler Pair")]
        public async Task DemonstrateConcurrentExclusiveSchedulerPair()
        {
            ConcurrentExclusiveSchedulerPair schedulerPair =
                new ConcurrentExclusiveSchedulerPair();

            Task writer = Task.Run(() =>
            {
                ParallelOptions options = new ParallelOptions();
                options.TaskScheduler = schedulerPair.ExclusiveScheduler;

                Parallel.For(1, 100, options, i =>
                {
                    myElement.Elements.Add("Child Element " + i);
                });
            });

            Task reader = Task.Run(() =>
            {
                ParallelOptions options = new ParallelOptions();
                options.TaskScheduler = schedulerPair.ConcurrentScheduler;

                Parallel.For(1, 100, options, i =>
                {
                    foreach (AFElement element in myElement.Elements)
                    {
                        JsonConvert.SerializeObject(element);
                    }
                });
            });

            await Task.WhenAll(reader, writer);
        }

        [Fact(DisplayName = "12: Concurrent/Exclusive Scheduler Pair Redux")]
        public async Task ConcurrentExclusiveSchedulerPairRedux()
        {
            ConcurrentExclusiveSchedulerPair schedulerPair =
                new ConcurrentExclusiveSchedulerPair();
            
            Task writer = Task.Factory.StartNew(
                () =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        myElement.Elements.Add("Child Element " + i);
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                schedulerPair.ExclusiveScheduler);


            Task reader = Task.Factory.StartNew(
                () =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        foreach (AFElement element in myElement.Elements)
                        {
                            JsonConvert.SerializeObject(element);
                        }
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                schedulerPair.ConcurrentScheduler);

            await Task.WhenAll(reader, writer);

            #region Shorthand
            // In shorthand (using an extension method)
            writer = schedulerPair.RunExclusive(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    myElement.Elements.Add("Another Child Element " + i);
                }
            });

            reader = schedulerPair.RunConcurrent(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    foreach (AFElement element in myElement.Elements)
                    {
                        JsonConvert.SerializeObject(element);
                    }
                }
            });

            await Task.WhenAll(reader, writer);

            #endregion
        }
    }
}

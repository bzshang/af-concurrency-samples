# AF Concurrency Samples

Programming concurrent and multi-user applications with the PI .NET Framework
(AF SDK) has historically been a tricky feat to manage. AF SDK was originally
designed as a single user access mechanism for retrieving data from the PI System,
and in general is not designed or intended for concurrent use. In this solution,
we look at some examples of how to parallelize applications that use the AF SDK.

In general, the advice contained in these samples applies to all versions of 
PI AF Client and PI AF Server. Some behaviors may be slightly different between 
versions of the PI AF Client and Server. These samples have been thoroughly
validated on a release candidate build of PI AF Client and Server 2015.

We avoid giving specific advice about how to parallelize your application. Rather,
we discuss various techniques and their safety. We leave it to the developer
to use this information to build a solution that best meets the needs of
his application, especially considering his use cases and data profile.

## Getting Started
The solution is a Visual Studio 2013 .sln file, which consists of a single C#
project. Any edition of Visual Studio 2013 (including the free "Community"
edition) should be sufficient for opening the solution and following the
tutorial. The project is built on .NET 4.5.2; as such, the .NET 4.5.2
developer pack will need to be installed.

The project contains a GAC reference to OSIsoft.AFSDK.dll. You will need to
install the PI AF Client in order to build and run this project.

You'll want to enable NuGet package restore in Visual Studio, as we depend on
three groups of NuGet packages:
 * _Xunit_: The Xunit unit testing framework. We'll use this framework to 
   lay out and prove assumptions about how the AF SDK operates.
 * _Newtonsoft.Json_: The community-standard JSON serializer for .NET. Used
   in this solution to demonstrate a unit of work that a user might want
   to perform on an object in the AF SDK.
 * _SimpleImpersonation_: A lightweight library for logging on as and
   impersonating other users. We'll use this to demonstrate some properties
   about how AF SDK works in multi-user scenarios.

You'll need access to a PI AF Server to run these samples (any version 2.2 or
later should do). The sample code needs to be able to manipulate a single
AF element, hardcoded in the source to `\\MyAssets\MyDatabase\MyElement`.
Replace this path with the path to an appropriate AF element in your own
server. The element should have no attributes and no child elements.

You'll also need a second user account with access to your PI AF Server to
run one of the test cases. Details are explained in that case.

## Tutorial
Follow along in the code in [Assertions.cs](Samples/Assertions.cs).

### 0. Setup and Plumbing
Note the hardcoded path `elementPath` to the element we'd like to
restore. There is also an object reference `myElement` to that element.

The _Plumbing_ is responsible for ensuring that `myElement` is populated
and ready before each assertion is run, and for ensuring that changes
are reverted after each assertion is run.


### 1. Colliding Readers and Writers
We're going to start off with a simple case. Imagine that you're building a
program that serves concurrent readers and writers in AF. These readers and
writers may at some point attempt to read from and write to the same
element simultaneously.

Within these examples where we intend to demonstrate a failure, we show our
readers and writers within a `for` loop that will execute the logic 100 times.
This is due to the very nature of the race conditions we wish to expose--
indeed, if we could deterministically guarantee the ordering of the operations
that will be performed, the race condition wouldn't exist at all! Executing
the logic in parallel loops provides us with a sort of crude Monte Carlo
simulation that will hopefully repeat the parallel operations enough times
and in enough different orders that eventually the race condition will
be captured.

In this case, the _writer_ is adding a child element to our test element,
and the reader is enumerating the child elements and serializing them. When
performing these operations concurrently, we expect the reader that is
iterating over the child elements to eventually have trouble when a new
child is inserted by the writer during the course of that iteration. This
results in an `InvalidOperationException`.

This outcome is of course possible in any .NET program that uses the non-
concurrent classes in the .NET collections framework in a parallel
setting. Indeed, the exception that is thrown ultimately originates from
a .NET Framework object.

Clearly, care must be taken to protect against concurrent reads and writes.

### 2. Write and Refresh Deadlock
Worse than the problem of colliding readers and writers, attempting to
write to and refresh an element at the same time can cause a deadlock--
the condition where one thread holds lock A and is waiting on lock B,
and another thread holds lock B and is waiting on lock A.

In our previous case of colliding readers and writers, the reader eventually
got an exception. This could conceivably be handled appropriately in a well
managed program. Trying to recover from a deadlock, on the other hand,
is likely to leave a program in a corrupt state.

In this case, we're going to create a `CancellationTokenSource` to allow
us to kill the threads of our child processes once we've determined that
they've deadlocked. Our _refresh_ task will simply refresh the element
definition from the PI AF Server. Our _writer_ looks similar to our
previous sample-- it will create a child element of our test element,
and this time, attempt to check it in.

We're also going to add another task -- a timeout task -- to measure a
duration after which we'll assume our _refresh_ and _writer_ tasks have
deadlocked. We're using 10 seconds as the timeout here.

Ultimately, this example isn't proof that we've deadlocked -- simply
that the operations haven't completed in 10 seconds. You could try
raising this timeout to satisfy your curiosity. If you'd like to see
conclusive proof for yourself, follow the guide in
[FindingDeadlocksWithWinDbg.md](FindingDeadlocksWithWinDbg.md).

### 3. Observe the AF Cache
One potential approach to solving either of these problems would be to
get separate copies of `myElement` to operate on. However, by default
the AF SDK's built-in caching mechanism will prevent this from 
occurring. Two separate attempts to restore the same AF object will
result in the same .NET object reference being returned -- as we 
demonstrate in this case.

(NB: Xunit's `Assert.Same` is a unit testing wrapper around the .NET
Framework's `object.ReferenceEquals`)

### 4. Observe the Per-User Nature of the AF Cache
Note, however, that while the _same_ element is restored on successive
calls to `AFObject.FindObject`, this will not be true across user
contexts. In the AF SDK, each user has his own cache. This example
demonstrates that indeed, the same element loaded by two different
users is represented by two separate object instances.

AF SDK is not designed for object references to be passed or shared
between different users. Doing so in production code could lead to
undesirable behavior.

Note the signature of the `Impersonation.LogonUser` call (from the
SimpleImpersonation library). The first argument is the domain (`null`,
in this case means a local user). The second is the username, and the
third is the password. For the sake of running this sample, you may 
set these to values that are appropriate in your environment.

You may need also need a higher logon level, such as `NetworkCleartext`,
if you are attempting to connect to a remote PI AF server.

This sample is the only treatment we will give to multiple users during
this demonstration. The techniques enumerated further on apply equally
well to single-user or multi-user concurrent applications, so long as
the fundamental rule (no sharing of objects between users) is
respected.

### 5. The _forceNewInstance_ PISystems Constructor Overload
In this sample, we demonstrate the _forceNewInstance_ argument in
the PISystems constructor. When `true`, an independent cache is created
for this PISystems instance. Overloads of the various AF SDK methods
will key off of an AF object to determine which cache should be used
when retrieving metadata from AF. In this example, we populate the 
_relativeFrom_ argument to AFObject.FindObject, which causes our test 
element to be separately retrieved and cached within the scopes of two
different PISystems instances. We therefore now have two separate .NET
objects, both of which belong to the current user, and both of which 
refer to the same element in AF.

In the _Shorthand_ region, we demonstrate the exact same property using
an extension method designed to simplify retrieving an object from
a specific PISystems instance.

In general, this approach solves the specific problems that we demonstrated
in Cases 1 and 2. However, there is significant overhead to creating a
new PISystems object and an independent cache. If you are building, for
example, a web application, it would not be appropriate to instantiate
a PISystems object for each request. Better approaches would combine
techniques discussed further on to safely share PISystems instances
between multiple, possibly concurrent requests. Another approach may be
to pool PISystems instances.

### 6. AF Caching Defaults
This case simply demonstrates the default properties of the AF Cache: that
it remembers a maximum of 10,000 objects, and it does so for a period of 120
seconds.

Note that these properties are static. Adjustments made to these properties
apply globally, to all PISystems instances in your application.

### 7. Disabling the AF Cache
This case demonstrates a non-trivial property of the AF Cache: that it cannot
be completely disabled. Setting CacheMaxObjects and CacheTime to zero still
results in the same object being retrieved by two successive calls to `Find`.

This merits additional explanation. The AF Cache is built on constructs
provided by the CLR's memory model and garbage collector. When garbage collection
is performed, objects in the runtime are inspected. If the collector determines
that an object is no longer live, it is scheduled to be destroyed so that the
memory it consumes may be reclaimed.

The AF Cache ties in to the .NET Framework's memory management model through the
use of both strong and weak references. A _weak_ reference allows an object
reference to be kept without preventing the object from being garbage collected.
By contrast, a _strong_ reference (or simply, a reference) will always prevent
an object from being garbage collected.

With this information in mind, we can revisit the meanings of the AF cache tuning
parameters:

 * `AFGlobalSettings.CacheMaxObjects`: The maximum number of _strong references_
 to keep in the AF cache. After this limit is reached, the least recently used 
 references in the cache are converted to weak references, so that this limit
 is not exceeded.
 * `AFGlobalSettings.CacheTime`: The time period after which any strong references
 in the AF cache may be converted to weak references. Each object in AF has its
 own 'timer' for this purpose, and the timer is reset each time the object is
 retrieved.

Tying into the .NET memory model is advantageous for the AF SDK. The use of strong
references . Eventually converting to weak references allows for objects retrieved
from the AF Server to be potentially reused between requests if garbage collection
has not occurred, or if there is still a strong reference elsewhere in the program,
sparing the expense of a round-trip to the AF server if these objects are still
available in memory when they are needed again. The design also allows leaves
decisions as to when space must be freed to the sophisticated garbage collection
algorithm implemented in the .NET Framework.

Now we can understand the outcome of this case. Even though there are no strong
references in the AF cache, there are weak references. And on our second call to
`systems.Find`, there is a strong reference to our test element outside of the AF
cache- on the line above! The internals of the AF SDK locate the weak reference
in the cache. The weak reference is clearly capable of being followed, as the
referenced object is still in memory. As such, the same object is retrieved on the
second call.

####References
 * For more details about the internals of the .NET Framework's garbage collector,
consult [Fundamentals of Garbage Collection](https://msdn.microsoft.com/en-us/library/ee787088%28v=vs.110%29.aspx).
 * For more details about weak references in the .NET Framework, see
[WeakReference<T>](https://msdn.microsoft.com/en-us/library/gg712738%28v=vs.110%29.aspx).

### 8. Observe the Garbage Collector in Action
In both of these cases, we explore the assertions made above about the nature of
the .NET Garbage Collector.

#####Case One
We set `AFGlobalSettings.CacheMaxObjects` to 100, and retrieve an element. After
creating a local weak reference to the element, we set the local variable `myElement`
to null, removing our local strong reference.

After garbage collection, the weak reference is still resolvable. This is because a
strong reference still exists in the AF cache.

#####Case Two
We set `AFGlobalSettings.CacheMaxObjects` to 0, and retrieve an element. After
creating a local weak reference to the element, we set the local variable `myElement`
to null, removing our local strong reference.

After garbage collection, the weak reference is no longer resolvable. This is because
the reference that previously existed in the AF cache was also a weak reference,
due to the object limit in the cache. Since there were no longer any strong references
to the object in the runtime, at the time that garabage collection occurred, our element
was determined to be a dead object and was cleared so that its memory could be reclaimed.

### 9. Demonstrate Monitor
Armed with a full understanding of the AF Cache, we now try to implement a concurrency
solution on top of the AF SDK to make our example from #1 safe. This first attempt
uses a _Monitor_, which causes all requests that wish to enter a critical block to
queue in a single file line.

##### Advantages
 * Straightforward to consume and reason about
 * Syntactic sugar built into the language (`lock` statement)
 * Extremely fast when uncontested (on the order of tens of microseconds)
 * No unmanaged resources must be used/disposed of.

##### Drawbacks
 * Does not allow for 'safe' operations (e.g., reads) to happen concurrently
 * Is thread-based, so protected operations must happen on a single thread, and
 may not be async.

If your application is low-volume, or the mix of reads and writes is not strongly
oriented toward reads, and the threading constraints are acceptable, a 
Monitor/lock statement may be a suitable approach for protecting access.

####References
 * [lock Statement](https://msdn.microsoft.com/en-us/library/c5kehkcz.aspx)
 * [Monitor](https://msdn.microsoft.com/en-us/library/system.threading.monitor%28v=vs.110%29.aspx)

### 10. Demonstrate Reader/Writer Lock
Implementing a Reader/Writer Lock is an incremental step forward over our previous
case. The reader/writer lock manages access so that operations can run concurrently
if possible. To demonstrate this, we've changed our previous `for` loop into a call
to `Parallel.For`, which will attempt to run the protected operations in parallel.
We've taken care to scope our lock acquisition _within_ the parallelized action, so
that two of the 'same' action executing in parallel must each acquire their own
lock.

##### Advantages
 * Allows for 'safe' operations (e.g., reads) to happen concurrently, while
 ensuring that unsafe operations get exclusive access.
 * Very fast (about 65-75% slower than acquiring a monitor, which is still quite fast)

##### Drawbacks
 * More difficult to program with: try/finally blocks must be written manually,
 which leaves more opportunities for error.
 * Is thread-based, so protected operations must happen on a single thread, and
 may not be async.
 * The lock consumes native resources, and must be disposed of properly when use
 is complete.

If your application is high-volume, and there are likely more reads than writes,
the threading constraints are acceptable, and you can take care to ensure that
the locks are acquired and released properly, then Reader/Writer Locks may be
appropriate for your application.

As of the 2015 R2 release, all releases of PI Web API use `ReaderWriterLockSlim`
to guarantee threadsafe access to the AF SDK.

####References
 * [ReaderWriterLockSlim](https://msdn.microsoft.com/en-us/library/system.threading.readerwriterlockslim%28v=vs.110%29.aspx)

### 11. Demonstrate Concurrent/Exclusive Scheduler Pair
`ConcurrentExclusiveSchedulerPair` is a class introduced in the 4.5 version of the
.NET Framework. The class consists of a pair of TPL `TaskScheduler`s. Concurrent-safe
operations may be scheduled on the Concurrent scheduler; others may be scheduled
on the Exclusive scheduler. This is essentially a TPL-based implementation of a
reader/writer lock.

To iterate on our previous example, we convert to ConcurrentExclusiveSchedulerPair,
and use a `Parallel.For` overload that takes a `ParallelOptions`. The `ParallelOptions`
is instructed to run its work on one of the two schedulers in the pair.

### 12. Concurrent/Exclusive Scheduler Pair (Redux)
In this final case, we demonstrate using a concurrent/exclusive scheduler pair
without `Parallel.For`. In the _Shorthand_ example, we define an extension method
on the scheduler pair to simplify task creation on the scheduler. We've now
seen the pair in action in two different scenarios.

##### Advantages
 * Allows for 'safe' operations (e.g., reads) to happen concurrently, while
 ensuring that unsafe operations get exclusive access.
 * Threads are not the unit of protection, so a protected operation can be
 parallelized.
 * No unmanaged resources must be used/disposed of.

##### Drawbacks
 * More cumbersome to program with if you're not used to working with the TPL,
 or you're not trying to parallelize execution.
 * No `Task.Run` overload takes a custom scheduler; dispatching must usually happen
 using `Task.Factory.StartNew`.
 * The overload of creating and scheduling a task is orders of magnitude higher
 than acquiring a Monitor or Reader/Writer Lock. There is also performance overhead
 associated with executing tasks; e.g. marshaling synchronization and execution
 contexts.

If your application is highly parallel, and the additional performance overhead of
creating a task and marshaling context is acceptable for your application, the
concurrent/exclusive scheduler pair may be a good choice for your application.

The PI Web API development team plans to prototype and performance test a
concurrent/exclusive pair-based approach to managing AF resources for possible
inclusion in a future PI Web API release.

#### References
 * [ConcurrentExclusiveSchedulerPair](https://msdn.microsoft.com/en-us/library/system.threading.tasks.concurrentexclusiveschedulerpair%28v=vs.110%29.aspx)

## Summary
In this tutorial we've demonstrated a number of properties about the AF SDK,
and how those properties influence attempts to use it in a concurrent manner.
We hope that we've given you sufficient strategies to begin determining the
approach most suitable for your own application.

For questions or comments, please visit [PI Developer's Club](https://pisquare.osisoft.com/community/developers-club)
on [PI Square](https://pisquare.osisoft.com).

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

## Getting Started
The solution is a Visual Studio 2013 .sln file, which consists of a single C#
project. Any edition of Visual Studio 2013 (including the free "Community"
edition) should be sufficient for opening the solution and following the
tutorial.

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
write to and refresh an element at the same time can cause a deadlock. In
the case of the colliding readers and writers, the reader eventually got
an exception. This could conceivably be handled appropriately in a well
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

Ultimately, this example isn't _proof_ that we've deadlocked -- simply
that the operations haven't completed in 10 seconds. If you'd like to see
proof for yourself, follow the guide in
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
Note, however, that 



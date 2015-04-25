# AF Concurrency Samples

Programming concurrent and multi-user applications with the PI .NET Framework
(AF SDK) has historically been a tricky feat to manage. In this solution, we
look at some examples of how to parallelize applications that use the AF SDK.

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


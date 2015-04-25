# AF Concurrency Samples

Programming concurrent and multi-user applications with the PI .NET Framework
(AF SDK) has historically been a tricky feat to manage. In this solution, we
look at some examples of how to parallelize applications that use the AF SDK.

## Getting Started
The solution is a Visual Studio 2013 .sln file, which consists of a single C#
project. Any edition of Visual Studio 2013 (including the free "Community"
edition) should be sufficient for opening the solution and executing the
assertions.

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

The examples may be followed linearly in [TestClass.cs](Samples/TestClass.cs).



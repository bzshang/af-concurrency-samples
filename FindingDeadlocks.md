#Finding Deadlocks
In this example, we'll use the code base provided in Sample #2 in this solution as a
starting point for viewing and detecting CLR deadlocks in WinDbg. You may install
WinDbg by installing the appropriate [Windows SDK](https://msdn.microsoft.com/en-us/windows/desktop/bg162891.aspx)
for your operating system on your machine.

```C#
    const string elementPath = @"\\MyAssets\MyDatabase\MyElement";
    AFElement myElement = AFObject.FindObject(elementPath) as AFElement;

    Task refresh = Task.Run(() =>
    {
        for (int i = 0; i < 100; i++)
        {
            myElement.Refresh();
        }
    });

    Task writer = Task.Run(() =>
    {
        for (int i = 0; i < 100; i++)
        {
            myElement.Elements.Add("Child Element " + i);
            myElement.CheckIn();
        }
    });

    await Task.WhenAll(refresh, writer);
```

Executing the code above ought to leave you with a program that appears unresponsive.
To view more about this state, let's fire up WinDbg. Be sure to choose the version
of WinDbg that's appropriate for the bitness of the .NET Framework that your
program is using.

Once you've started WinDbg, attach to your process using F6 or `File`, `Attach to a Process`,
and choosing the 'Non Invasive' checkbox. If your Visual Studio debugger is attached to the 
process, you'll need to detach it first.

Now, load the 'SOS' extension to provide commands specific to the .NET Framework by entering:
```
.cordll -ve -u -l
```

You should see output like:
```
Automatically loaded SOS Extension
CLRDLL: Loaded DLL C:\Windows\Microsoft.NET\Framework64\v4.0.30319\mscordacwks.dll
CLR DLL status: Loaded DLL C:\Windows\Microsoft.NET\Framework64\v4.0.30319\mscordacwks.dll
```

Verify that 'SOS' now appears in the list of loaded extensions by entering:
```
.chain
```

You should see output like:
```
Extension DLL search Path:
    C:\Program Files (x86)\Windows Kits\8.1\Debuggers\x64\WINXP;...
Extension DLL chain:
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\SOS.dll: image 4.0.30319.34209, API 1.0.0, built Fri Apr 11 22:03:15 2014
        [path: C:\Windows\Microsoft.NET\Framework64\v4.0.30319\SOS.dll]
    dbghelp: image 6.3.9600.17298, API 6.3.6, built Fri Oct 24 21:07:10 2014
        [path: C:\Program Files (x86)\Windows Kits\8.1\Debuggers\x64\dbghelp.dll]
    ext: image 6.3.9600.17298, API 1.0.0, built Fri Oct 24 21:15:44 2014
        [path: C:\Program Files (x86)\Windows Kits\8.1\Debuggers\x64\winext\ext.dll]
    exts: image 6.3.9600.17298, API 1.0.0, built Fri Oct 24 21:11:57 2014
        [path: C:\Program Files (x86)\Windows Kits\8.1\Debuggers\x64\WINXP\exts.dll]
    uext: image 6.3.9600.17298, API 1.0.0, built Fri Oct 24 21:11:55 2014
        [path: C:\Program Files (x86)\Windows Kits\8.1\Debuggers\x64\winext\uext.dll]
    ntsdexts: image 6.3.9600.17298, API 1.0.0, built Fri Oct 24 21:12:09 2014
        [path: C:\Program Files (x86)\Windows Kits\8.1\Debuggers\x64\WINXP\ntsdexts.dll]
```
You want to ensure that an entry like 'SOS.dll' exists in the Extension DLL chain.

Now, let's take a look at what's going on in the process by getting stack dumps of all the threads:
```
~*e!clrstack
```

You'll see a lot of output. What you're looking for are two threads waiting on a monitor.

```
OS Thread Id: 0xf30 (5)
        Child SP               IP Call Site
0000006deca5dcf8 00007ff9b0bf177a [GCFrame: 0000006deca5dcf8] 
0000006deca5def0 00007ff9b0bf177a [GCFrame: 0000006deca5def0] 
0000006deca5df28 00007ff9b0bf177a [HelperMethodFrame: 0000006deca5df28] System.Threading.Monitor.Enter(System.Object)
0000006deca5e020 00007ff940ad756b *** ERROR: Module load completed but symbols could not be loaded for C:\Windows\Microsoft.Net\assembly\GAC_MSIL\OSIsoft.AFSDK\v4.0_4.0.0.0__6238be57836698e6\OSIsoft.AFSDK.dll
OSIsoft.AF.Support.AFTransactable.AutoCheckOut(OSIsoft.AF.AFObject, Boolean, Boolean, Boolean)

---

OS Thread Id: 0x171c (6)
        Child SP               IP Call Site
0000006decb5dc18 00007ff9b0bf177a [GCFrame: 0000006decb5dc18] 
0000006decb5dd58 00007ff9b0bf177a [GCFrame: 0000006decb5dd58] 
0000006decb5dd98 00007ff9b0bf177a [HelperMethodFrame_1OBJ: 0000006decb5dd98] System.Threading.Monitor.Enter(System.Object)
0000006decb5de90 00007ff940ac25b3 OSIsoft.AF.Asset.AFBaseElement.get_TemplateID()
0000006decb5df20 00007ff940ac1cfb OSIsoft.AF.Asset.AFBaseElement.UpdateHeaderImplementation(OSIsoft.AF.Service.IdcObjectData, OSIsoft.AF.Service.IdcObjectHeader)
0000006decb5e270 00007ff940ac11d8 OSIsoft.AF.Asset.AFElement.UpdateHeaderImplementation(OSIsoft.AF.Service.IdcObjectData, OSIsoft.AF.Service.IdcObjectHeader)
```

We have two threads waiting on a monitor-- let's take a look at the monitors in the CLR:
```
!syncblk
```

My output looks like:
```
0:000> !syncblk
Index        SyncBlock  MonitorHeld Recursion Owning Thread Info          SyncBlock Owner
    7 0000006dd16e26b8            3         1 0000006dec2a1690 171c   6   0000006dd36272d0 OSIsoft.AF.Support.AFUpdateStamp
   10 0000006dd16e27a8            3         1 0000006dec2a0a10 f30   5   0000006dd3627280 OSIsoft.AF.Support.AFTransactable
```

The interesting column is 'MonitorHeld', which reports '1' for being held, and '2' for
each time it's being waited on. The value of 3 for each lock means that each lock is
held, and each lock is being waited on. Smells pretty strongly like a deadlock!

More information about `!syncblk`: http://blogs.msdn.com/b/tess/archive/2006/01/09/a-hang-scenario-locks-and-critical-sections.aspx

In a very small application, we might reasonably deduce this is a deadlock, and finish up here. But
what about in a larger application? The WinDbg extension SOSEX contains additional tools for detecting
deadlocks between Monitors and Reader/Writer Locks. You can download the tools here:
[Steve's Techspot](http://www.stevestechspot.com/default.aspx)

Once you've downloaded and extracted, load the platform-appropriate version into WinDbg:
```
.load C:\path\to\sosex.dll
```

Run a quick `.chain` and ensure that SOSEX is indeed loaded.

And run SOSEX's deadlock detection command:
```
!dlk
```

With any luck, you'll see output like the following:
```
Examining SyncBlocks...
Scanning for ReaderWriterLock instances...
Scanning for holders of ReaderWriterLock locks...
Scanning for ReaderWriterLockSlim instances...
Scanning for holders of ReaderWriterLockSlim locks...
Examining CriticalSections...
Could not find symbol ntdll!RtlCriticalSectionList.
Scanning for threads waiting on SyncBlocks...
*** ERROR: Module load completed but symbols could not be loaded for C:\Windows\Microsoft.Net\assembly\GAC_MSIL\OSIsoft.AFSDK\v4.0_4.0.0.0__6238be57836698e6\OSIsoft.AFSDK.dll
Scanning for threads waiting on ReaderWriterLock locks...
Scanning for threads waiting on ReaderWriterLocksSlim locks...
Scanning for threads waiting on CriticalSections...
*DEADLOCK DETECTED*
CLR thread 0x4 holds the lock on SyncBlock 0000006dd16e27a8 OBJ:0000006dd3627280[OSIsoft.AF.Support.AFTransactable]
...and is waiting for the lock on SyncBlock 0000006dd16e26b8 OBJ:0000006dd36272d0[OSIsoft.AF.Support.AFUpdateStamp]
CLR thread 0x5 holds the lock on SyncBlock 0000006dd16e26b8 OBJ:0000006dd36272d0[OSIsoft.AF.Support.AFUpdateStamp]
...and is waiting for the lock on SyncBlock 0000006dd16e27a8 OBJ:0000006dd3627280[OSIsoft.AF.Support.AFTransactable]
CLR Thread 0x4 is waiting at OSIsoft.AF.Support.AFTransactable.AutoCheckOut(OSIsoft.AF.AFObject, Boolean, Boolean, Boolean)(+0x12 IL,+0x7b Native)
CLR Thread 0x5 is waiting at OSIsoft.AF.Asset.AFBaseElement.get_TemplateID()(+0x11 IL,+0x83 Native)
```

__DEADLOCK DETECTED__. Yup.

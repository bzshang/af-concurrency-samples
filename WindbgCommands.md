#Commands
Ensure you're running the correct version of WinDbg for your architecture
The test runner in this solution typically uses x86

https://msdn.microsoft.com/en-us/library/windows/hardware/ff540665(v=vs.85).aspx
http://www.stevestechspot.com/default.aspx

Load the SOS extension
    .cordll -ve -u -l

Load the SOSEX extension
    .load C:\path\to\sosex_32\sosex.dll

Verify loaded extensions
    .chain

Useful commands
    !clrstack
    !syncblk
    !dlk (sosex)

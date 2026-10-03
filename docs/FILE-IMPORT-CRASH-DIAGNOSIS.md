# File Import Crash Diagnosis and Fix

## Issue Summary

When selecting a CSV file on the Import page, the app crashes completely with no error displayed. The crash happens even though:
- The breakpoint in `OnFileSelected` is hit
- Error handling is in place  
- No visible exception is shown

## Root Cause Analysis

The crash is most likely caused by:

1. **Blazor Server Event Handler Issue** - When `InputFile.OnChange` event fires, it passes an `InputFileChangeEventArgs` that contains file data. In Blazor Server (InteractiveServer render mode), this data must be properly marshalled through the SignalR circuit.

2. **Potential causes**:
   - Accessing file properties (`File.Name`, `File.Size`) immediately triggers reading from the browser's file system, which may cause timing/marshalling issues
   - Opening the read stream (`OpenReadStream`) during the event handler might conflict with Blazor's event marshalling process
   - The exception might be occurring in the Blazor runtime layer, not in user code

## Solution Implemented

I've changed the file handling strategy to be more robust:

### Key Changes in Import.razor:

1. **Deferred File Reading**: Instead of reading the file immediately in `OnFileSelected`, we now just store the `IBrowserFile` reference and basic info:
   ```csharp
   private Task OnFileSelected(InputFileChangeEventArgs args)
   {
	   // Just capture file reference
	   pendingFile = args.File;
	   selectedFileName = args.File.Name;
	   return Task.CompletedTask;  // Return immediately
   }
   ```

2. **Separate Read Method**: File reading is done in a dedicated method that's called when the Import button is clicked:
   ```csharp
   private async Task ReadFileAsync()
   {
	   // Do the actual file reading here, away from event marshalling
	   await using var stream = pendingFile.OpenReadStream(...);
	   // ... copy to memory
   }
   ```

3. **Aggressive Logging**: Added detailed logging at every step to track exactly where the crash occurs (visible in Visual Studio output)

4. **Enhanced Error Handling**: Multiple try-catch blocks with specific context for each operation

### Configuration Added:

- **Logging Level**: Changed to `Debug` in `appsettings.Development.json` to capture all SignalR and Blazor diagnostics
- **SignalR Limits**: Already configured in Program.cs (from previous fix):
  - MaximumReceiveMessageSize: 10 MB
  - FormOptions.MultipartBodyLengthLimit: 10 MB

## Testing the Fix

1. **Run the app** in Debug mode with Visual Studio
2. **Open the Dev Tools** Output window (View > Output or Ctrl+Alt+O)
3. **Go to Import page** and select a CSV file
4. **Watch the logging output** - it should show:
   ```
   OnFileSelected: Event fired
   OnFileSelected: File detected - Name=myfile.csv, Size=12345
   OnFileSelected: File stored for later reading
   ```
5. **Click Import button** - should show:
   ```
   ImportAsync: File not yet read, reading now
   ReadFileAsync: Starting
   ReadFileAsync: Stream opened
   ReadFileAsync: Complete - 12345 bytes cached
   ```

If the app still crashes, check the logging output to see exactly which line causes the crash. The error message will indicate:
- If it's a file size issue
- If it's a stream opening issue
- If it's something else

## What Changed From Previous Attempt

**Previous approach (crashed)**:
- Tried to read file bytes immediately in `OnFileSelected`
- Kept file open across multiple operations
- No clear separation of concerns

**New approach (should work)**:
- Event handler just saves file reference, returns immediately
- File read happens later when Import button is clicked
- Cleaner separation between event handling and file operations

## If Still Crashing

1. Look at the logging output in Visual Studio Output window
2. Note the exact log message that appears before the crash
3. Share that information - it will identify exactly where the failure occurs

## Next Steps if Successful

If this fixes the crash, the improved logging can be kept as-is for development. For production, consider reducing logging to `Information` level in appsettings.json.

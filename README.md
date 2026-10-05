# EstMEP Add-ins

Add-ins for Autodesk Fabrication ESTmep 2026.

## Submittal Dimension Check

Checks that an ITM's dimensions match the manufacturer's submittal.

1. In ESTmep, load the add-in. The **Submittal Dimension Check** window opens.
2. **Item (ITM): Browse…** opens a tree of the loaded database's item folders (with a search box). Pick the ITM.
3. **Submittal (PDF): Browse…** and pick the matching submittal.
4. Choose the ITM's units and a tolerance, then click **Draw all sizes and verify**.

The add-in loads the ITM, draws every size in its product list into the current job, and records the
dimensions, options and product list values ESTmep reports for each size. It sends those values and the
PDF to Claude, which finds the submittal's dimension table, matches each submittal dimension
(A, B, "Center to End"…) to the ITM dimension it represents, and compares every size.

The window then shows:

- **DIMENSIONALLY CORRECT**, or **DIMENSIONS DO NOT MATCH THE SUBMITTAL**, or **COULD NOT VERIFY**
  (for example, the PDF is for a different product).
- A table of every compared dimension per size, with mismatches highlighted.
- **How to fix**: for each wrong value, the size, the dimension, the current and required values, and
  where to change it in ESTmep's item editor (Product List tab vs Dimensions tab).
- Sizes on the submittal that the ITM's product list is missing.

**Save report…** writes all of it to a text file.

### Build

Needs Windows, the .NET 8 SDK and ESTmep 2026 installed.

```
dotnet build src\DimensionChecker.AddIn -c Release
```

The project references `FabricationAPI.dll` from
`C:\Program Files\Autodesk\Fabrication 2026\ESTmep\`. If ESTmep is installed elsewhere, copy the DLL
into `lib\` or pass `-p:FabricationApiPath="<path>\FabricationAPI.dll"`.

If your ESTmep build loads .NET Framework add-ins rather than .NET 8, add
`-p:EstmepTargetFramework=net48`.

The output is `src\DimensionChecker.AddIn\bin\Release\net8.0-windows\`. Keep every DLL in that folder
together; the add-in needs them next to it.

### Load in ESTmep

Use ESTmep's **Add-Ins** menu to load `DimensionChecker.AddIn.dll` from the build output folder.
ESTmep calls the add-in's `Execute()` on load, which opens the window.

### Anthropic API key

The check calls the Claude API, so it needs an API key from <https://console.anthropic.com>. Either set
the `ANTHROPIC_API_KEY` environment variable before starting ESTmep, or paste the key when the add-in
asks (button **API key…**). A pasted key is stored encrypted for your Windows account in
`%APPDATA%\EstMepAddins\anthropic.key`.

Each check is one request with the whole PDF attached. Submittals over about 23 MB are rejected;
trim the PDF to the pages for that product.

### Things to confirm on first run

These were written from Autodesk's Fabrication API samples without an ESTmep install to test against:

- The entry point implements `Autodesk.Fabrication.ApplicationServices.IExternalApplication`. If the
  build says that interface lives in another namespace or has more members, adjust
  `DimensionCheckerApp.cs`.
- Each size is drawn with `ContentManager.LoadItem`, `Item.LoadProductListEntry(row)`, `Item.Update()`
  and `Job.AddItem`. Every size lands in the job as its own item.
- Dimension and option values are read by reflection (`Name` / `Value`), so they survive small API
  differences. If a size shows no dimensions in the results table, that is the place to look
  (`FabricationItemDrawingService.cs`).
- The item browser starts at the database's item content folder when the API exposes it; otherwise
  **Browse…** falls back to a normal file dialog.

### Project layout

| Project | What it is |
|---|---|
| `src/DimensionChecker.AddIn` | The DLL ESTmep loads. The only code that touches `FabricationAPI.dll`. |
| `src/DimensionChecker.UI` | WinForms windows (main window, ITM browser, API key prompt). |
| `src/DimensionChecker.Core` | ITM snapshot model, the Claude request and the report format. |
| `tests/DimensionChecker.Core.Tests` | Unit tests for the Claude request/response handling. |

`dotnet test tests/DimensionChecker.Core.Tests` runs on any OS.

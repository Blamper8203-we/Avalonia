# DINBoard Release Checklist

This checklist is the final gate before cutting a release build of DINBoard.
Use it together with `.\scripts\Validate-Release.ps1`.

## 1. Automated Gate

Run:

```powershell
.\scripts\Validate-Release.ps1
```

Expected result:
- solution build passes in `Release`
- test suite passes in `Release`
- main app artifact exists in `bin\Release\net10.0\DINBoard.dll`
- test artifact exists in `Tests\bin\Release\net10.0\Avalonia.Tests.dll`

Do not continue to release packaging if the automated gate fails.

## 2. Manual Smoke Test

Run the desktop app in `Release` and verify the following:

1. Home screen opens without crash.
2. Trial or full-license badge renders correctly.
3. Local PRO activation shortcut is hidden in release by default.
4. Create a new project from the home screen.
5. Add or generate a DIN rail.
6. Drag several modules from the palette onto the schematic.
7. Duplicate and delete symbols, then verify undo and redo.
8. Edit project metadata and confirm the project becomes dirty.
9. Save the project.
10. Trigger `Save As`, cancel it, and confirm the app does not lose data.
11. Close the app with unsaved changes and confirm the save prompt appears.
12. Reopen the saved project and verify the project round-trip is correct.
13. Run phase balance and validation on a realistic sample project.
14. Export PDF and verify the output opens correctly.
15. Export BOM and verify the file is created correctly.
16. If PNG export is part of the release scope, export PNG and open the result.
17. Delete a custom module from the palette and confirm the deletion prompt appears.
18. Delete a custom category from the palette and confirm the deletion prompt appears.
19. If possible, test a locked file or permission failure and confirm the app shows a readable error.
20. Verify the app still responds correctly after any renderer warning or redraw issue.

## 3. Logs And Error Handling

Before release sign-off, verify:
- `%LOCALAPPDATA%\DINBoard\Logs` is being written
- no unexpected fatal errors appeared during smoke testing
- no renderer crash reports were generated unless you intentionally tested a failure path

Renderer crash report path:

```text
%LOCALAPPDATA%\DINBoard\Logs\render-crash-latest.txt
```

## 4. Release Decision

The build is release-ready only if all of the following are true:
- automated gate passed
- manual smoke test passed
- no known data-loss path remains
- no known licensing bypass remains in release
- no blocking regression was found in export, persistence, canvas, or undo/redo

## 5. Notes

This checklist is intentionally conservative because DINBoard contains
engineering logic, persistence, export, and interactive canvas behavior.

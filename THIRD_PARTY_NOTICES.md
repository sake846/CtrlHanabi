# Third-Party Notices

## Current dependency status

As of this revision, CtrlHanabi does not include any third-party NuGet packages or bundled external libraries.

- `CtrlHanabi.csproj` contains no `PackageReference`
- `obj/project.assets.json` resolves no external packages
- build output `CtrlHanabi.deps.json` contains only the application itself

## Platform components in use

The application uses platform components that are provided by Microsoft as part of Windows or the .NET runtime.

- `.NET 10` / `Microsoft.NETCore.App`
- `Microsoft.WindowsDesktop.App` (`WPF`, `Windows Forms` interop)
- Windows APIs via P/Invoke
- Direct3D 9 / Direct3D 11 APIs provided by Windows

These platform components are not redistributed as third-party libraries within this repository.

## Audio / FFT note

There is currently no `NAudio` usage in the tracked source, project file, restored assets, or generated dependency manifest. There is also no audio input or FFT implementation in this repository at this revision.

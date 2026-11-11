# Troubleshooting

## The app says it cannot find the .NET runtime

The release bundles are **framework-dependent** and require the **.NET 11 Runtime**
installed on your machine. The runtime is not included in the bundle. Install it from
<https://dotnet.microsoft.com/download/dotnet/11.0> and start the app again.

If you installed .NET in a non-standard location on Linux/macOS, set `DOTNET_ROOT` to
that directory before running 5DO. On Linux/macOS, also make the apphosts executable:

```sh
chmod +x FiveDo.Cli FiveDo.Frontend.Avalonia
```

## The app opens but the game does not start

- Confirm a BIOS is selected and the disc is a supported 3DO image.
- Try a known BIOS such as `panafz1.bin` and a known title.
- Use **Open Disc** rather than dragging an unsupported archive.
- Check **Help > View Log** and **About** for the current BIOS, disc, and renderer.

## Black screen

A black first frame can be normal while the game loads its OS. Run longer:

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 12000 --frame-out last.png
```

Some titles need a different BIOS/region. See
[Compatibility](Compatibility) and
[Known issues](https://purelogiccode.github.io/5DO/KnownIssues.html).

## No audio

- Check **Settings > Audio**, the selected OpenAL device, volume, and mute.
- Run `audio devices` to list playback devices.
- Try `--audio null` to isolate output problems, or `--audio wav --wav-out capture.wav`
  to capture the DSP output.

## OpenGL fails or the UI is slow

5DO falls back to the CPU presenter. Force a backend with
`--gl-backend auto|wgl|angle|software|vulkan`. Update the GPU driver and try
`--gl-backend angle` on Windows.

## A controller is not detected

- Windows uses XInput; Linux uses evdev (`/dev/input/event*`).
- On Linux, the user normally needs membership in the `input` group.
- macOS has no gamepad backend yet; use the keyboard.
- Check **Input** for bindings and **About** for connected controller count.

## I need to send a diagnostic bundle

Use **Help > Save Diagnostics Bundle...**. The archive includes environment details,
sanitized settings, recent logs, and optional state/frame/replay evidence. Review it
before uploading; it never includes a BIOS or disc image automatically.

## More help

See the [full troubleshooting guide](https://purelogiccode.github.io/5DO/Troubleshooting.html)
and [FAQ](FAQ).

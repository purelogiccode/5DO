using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Input;

namespace FiveDo.Tests.Input;

/// <summary>
/// Covers the controller-preview state mapping used by the input-bindings dialog.
/// </summary>
public sealed class ControllerPreviewTests
{
    [Fact]
    public void GetState_AppliesPressedSelectedMappedPrecedence()
    {
        Assert.Equal(
            ControllerPreviewState.Normal,
            ControllerPreview.GetState(ControllerButton.A, ControllerButton.None, ControllerButton.None, ControllerButton.None));

        Assert.Equal(
            ControllerPreviewState.Mapped,
            ControllerPreview.GetState(ControllerButton.A, ControllerButton.A | ControllerButton.B, ControllerButton.None, ControllerButton.None));

        Assert.Equal(
            ControllerPreviewState.Selected,
            ControllerPreview.GetState(ControllerButton.A, ControllerButton.A, ControllerButton.A, ControllerButton.None));

        Assert.Equal(
            ControllerPreviewState.Pressed,
            ControllerPreview.GetState(ControllerButton.A, ControllerButton.A, ControllerButton.A, ControllerButton.A));
    }

    [Fact]
    public void GetState_OnlyTouchesTheRequestedButton()
    {
        ControllerButton mapped = ControllerButton.A | ControllerButton.C | ControllerButton.Left;
        ControllerButton selected = ControllerButton.C;
        ControllerButton pressed = ControllerButton.Left | ControllerButton.R;

        Assert.Equal(ControllerPreviewState.Pressed, ControllerPreview.GetState(ControllerButton.Left, mapped, selected, pressed));
        Assert.Equal(ControllerPreviewState.Pressed, ControllerPreview.GetState(ControllerButton.R, mapped, selected, pressed));
        Assert.Equal(ControllerPreviewState.Selected, ControllerPreview.GetState(ControllerButton.C, mapped, selected, pressed));
        Assert.Equal(ControllerPreviewState.Mapped, ControllerPreview.GetState(ControllerButton.A, mapped, selected, pressed));
        Assert.Equal(ControllerPreviewState.Normal, ControllerPreview.GetState(ControllerButton.B, mapped, selected, pressed));
    }

    [Fact]
    public void GetState_RejectsNoneAndCombinedButtons()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ControllerPreview.GetState(ControllerButton.None, ControllerButton.None, ControllerButton.None, ControllerButton.None));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ControllerPreview.GetState(
                ControllerButton.A | ControllerButton.B,
                ControllerButton.None,
                ControllerButton.None,
                ControllerButton.None));
    }

    [Fact]
    public void GetMappedButtons_UnionsKeyboardAndGamepadBindings()
    {
        var profile = new PlayerProfile();
        profile.Keyboard["Z"] = ControllerButton.A;
        profile.Keyboard["Q"] = ControllerButton.L;
        profile.Gamepad["South"] = ControllerButton.B;
        profile.Gamepad["Start"] = ControllerButton.P;
        profile.Gamepad["Unused"] = ControllerButton.None;

        ControllerButton mapped = ControllerPreview.GetMappedButtons(profile);

        Assert.Equal(ControllerButton.A | ControllerButton.L | ControllerButton.B | ControllerButton.P, mapped);
    }

    [Fact]
    public void FindKeyboardKey_ReturnsTheBoundKey()
    {
        var profile = new PlayerProfile();
        profile.Keyboard["Q"] = ControllerButton.A;

        Assert.Equal("Q", ControllerPreview.FindKeyboardKey(profile, ControllerButton.A));
        Assert.Null(ControllerPreview.FindKeyboardKey(profile, ControllerButton.B));
        Assert.Null(ControllerPreview.FindKeyboardKey(profile, ControllerButton.None));
    }

    [Fact]
    public void FindGamepadNames_ReturnsEveryPhysicalControlInOrder()
    {
        var profile = new PlayerProfile();
        profile.Gamepad["South"] = ControllerButton.A;
        profile.Gamepad["LeftTrigger"] = ControllerButton.L;
        profile.Gamepad["RightTrigger"] = ControllerButton.L;

        IReadOnlyList<string> shoulder = ControllerPreview.FindGamepadNames(profile, ControllerButton.L);
        Assert.Equal(["LeftTrigger", "RightTrigger"], shoulder);
        Assert.Equal(["South"], ControllerPreview.FindGamepadNames(profile, ControllerButton.A));
        Assert.Empty(ControllerPreview.FindGamepadNames(profile, ControllerButton.B));
    }

    [Fact]
    public void FindKeyboardButton_IsCaseInsensitive()
    {
        var profile = new PlayerProfile();
        profile.Keyboard["z"] = ControllerButton.A;

        Assert.Equal(ControllerButton.A, ControllerPreview.FindKeyboardButton(profile, "Z"));
        Assert.Equal(ControllerButton.None, ControllerPreview.FindKeyboardButton(profile, "Q"));
        Assert.Equal(ControllerButton.None, ControllerPreview.FindKeyboardButton(profile, " "));
    }

    [Fact]
    public void PreviewTracksAKeyboardPressThroughTheInputMapper()
    {
        PlayerProfile player = PlayerProfile.CreateDefault(1);
        var mapper = new KeyboardInputMapper([player]);

        Assert.True(mapper.SetKey("Z", pressed: true));
        ControllerState pressed = mapper.GetState(0);
        Assert.True(pressed.IsDown(ControllerButton.A));
        Assert.Equal(
            ControllerPreviewState.Pressed,
            ControllerPreview.GetState(
                ControllerButton.A,
                ControllerPreview.GetMappedButtons(player),
                ControllerButton.None,
                pressed.Buttons));

        Assert.True(mapper.SetKey("Z", pressed: false));
        ControllerState released = mapper.GetState(0);
        Assert.False(released.IsDown(ControllerButton.A));
        Assert.Equal(
            ControllerPreviewState.Mapped,
            ControllerPreview.GetState(
                ControllerButton.A,
                ControllerPreview.GetMappedButtons(player),
                ControllerButton.None,
                released.Buttons));
    }

    [Fact]
    public void DialogEditingRoundTripsTheProfileAndPreviewState()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"fivedo-preview-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var profile = InputProfile.CreateDefault();
            PlayerProfile player = profile.Players[0];
            var editor = new KeyboardBindingEditor(player);
            editor.Rebind(ControllerButton.A, "Q");
            editor.Rebind(ControllerButton.Up, null);
            editor.ApplyTo(player);

            var path = Path.Combine(directory, "input.json");
            InputProfileStore.Save(path, profile);
            InputProfile loaded = InputProfileStore.Load(path);

            PlayerProfile loadedPlayer = loaded.Players[0];
            Assert.Equal("Q", ControllerPreview.FindKeyboardKey(loadedPlayer, ControllerButton.A));

            // Up was unbound from the keyboard but stays mapped by the default D-pad.
            Assert.Null(ControllerPreview.FindKeyboardKey(loadedPlayer, ControllerButton.Up));
            Assert.Contains("DpadUp", ControllerPreview.FindGamepadNames(loadedPlayer, ControllerButton.Up), StringComparer.Ordinal);
            Assert.True(
                (ControllerPreview.GetMappedButtons(loadedPlayer) & ControllerButton.Up) != ControllerButton.None);

            var mapper = new KeyboardInputMapper([loadedPlayer]);
            Assert.True(mapper.SetKey("Q", pressed: true));
            Assert.True(mapper.GetState(0).IsDown(ControllerButton.A));
            Assert.False(mapper.GetState(0).IsDown(ControllerButton.Up));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

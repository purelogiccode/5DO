using System;
using FiveDo.Input;

namespace FiveDo.Tests.Input;

public sealed class HotkeyRouterTests
{
    [Theory]
    [InlineData("F5", HotkeyAction.SaveState)]
    [InlineData("F8", HotkeyAction.LoadState)]
    [InlineData("F10", HotkeyAction.Reset)]
    [InlineData("F11", HotkeyAction.Fullscreen)]
    [InlineData("F12", HotkeyAction.Screenshot)]
    [InlineData("P", HotkeyAction.Pause)]
    [InlineData("F6", HotkeyAction.SlotPrevious)]
    [InlineData("F7", HotkeyAction.SlotNext)]
    [InlineData("F9", HotkeyAction.FrameAdvance)]
    [InlineData("R", HotkeyAction.Rewind)]
    [InlineData("Tab", HotkeyAction.FastForward)]
    [InlineData("Space", HotkeyAction.ToggleFastForward)]
    [InlineData("F1", HotkeyAction.ToggleGamegun)]
    [InlineData("F4", HotkeyAction.QuickMenu)]
    public void Resolve_ReturnsEveryDefaultHotkey(string key, HotkeyAction expected)
    {
        InputProfile profile = InputProfile.CreateDefault();

        Assert.Equal(expected, HotkeyRouter.Resolve(profile, key));
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        InputProfile profile = InputProfile.CreateDefault();

        Assert.Equal(HotkeyAction.SaveState, HotkeyRouter.Resolve(profile, "f5"));
        Assert.Equal(HotkeyAction.Screenshot, HotkeyRouter.Resolve(profile, "f12"));
    }

    [Fact]
    public void Resolve_ReturnsNoneForUnboundOrEmptyKeys()
    {
        InputProfile profile = InputProfile.CreateDefault();

        Assert.Equal(HotkeyAction.None, HotkeyRouter.Resolve(profile, "F13"));
        Assert.Equal(HotkeyAction.None, HotkeyRouter.Resolve(profile, string.Empty));
        Assert.Equal(HotkeyAction.None, HotkeyRouter.Resolve(profile, null));
    }

    [Fact]
    public void Resolve_UsesCustomBindingsAndIgnoresUnknownActionNames()
    {
        InputProfile profile = InputProfile.CreateDefault();
        profile.Hotkeys["SaveState"] = "F15";
        profile.Hotkeys["NotARealAction"] = "F14";

        Assert.Equal(HotkeyAction.SaveState, HotkeyRouter.Resolve(profile, "F15"));
        Assert.Equal(HotkeyAction.None, HotkeyRouter.Resolve(profile, "F5"));
        Assert.Equal(HotkeyAction.None, HotkeyRouter.Resolve(profile, "F14"));
    }

    [Fact]
    public void TryResolve_ReportsWhetherTheKeyIsBound()
    {
        InputProfile profile = InputProfile.CreateDefault();

        Assert.True(HotkeyRouter.TryResolve(profile, "F8", out HotkeyAction load));
        Assert.Equal(HotkeyAction.LoadState, load);
        Assert.False(HotkeyRouter.TryResolve(profile, "F13", out HotkeyAction none));
        Assert.Equal(HotkeyAction.None, none);
    }

    [Theory]
    [InlineData(HotkeyAction.SaveState, "SaveState")]
    [InlineData(HotkeyAction.LoadState, "LoadState")]
    [InlineData(HotkeyAction.Screenshot, "Screenshot")]
    [InlineData(HotkeyAction.Fullscreen, "Fullscreen")]
    [InlineData(HotkeyAction.Pause, "Pause")]
    [InlineData(HotkeyAction.Reset, "Reset")]
    [InlineData(HotkeyAction.SlotPrevious, "SlotPrevious")]
    [InlineData(HotkeyAction.SlotNext, "SlotNext")]
    [InlineData(HotkeyAction.FrameAdvance, "FrameAdvance")]
    [InlineData(HotkeyAction.Rewind, "Rewind")]
    [InlineData(HotkeyAction.FastForward, "FastForward")]
    [InlineData(HotkeyAction.ToggleFastForward, "ToggleFastForward")]
    [InlineData(HotkeyAction.ToggleGamegun, "ToggleGamegun")]
    [InlineData(HotkeyAction.QuickMenu, "QuickMenu")]
    [InlineData(HotkeyAction.None, "")]
    public void GetActionName_RoundtripsThePersistedName(HotkeyAction action, string expected)
    {
        Assert.Equal(expected, HotkeyRouter.GetActionName(action));
    }

    [Fact]
    public void DefaultHotkeys_UsePForPauseAndSpaceForToggleFastForward()
    {
        InputProfile profile = InputProfile.CreateDefault();

        Assert.Equal("P", profile.Hotkeys["Pause"]);
        Assert.Equal("Space", profile.Hotkeys["ToggleFastForward"]);
        Assert.Equal("F1", profile.Hotkeys["ToggleGamegun"]);
    }

    [Fact]
    public void Normalize_MigratesTheLegacyEscapePauseBinding()
    {
        InputProfile profile = InputProfile.CreateDefault();
        profile.Hotkeys["Pause"] = "Escape";

        profile.Normalize();

        Assert.Equal("P", profile.Hotkeys["Pause"]);
    }

    [Fact]
    public void Resolve_RejectsANullProfile()
    {
        Assert.Throws<ArgumentNullException>(() => HotkeyRouter.Resolve(null!, "F5"));
    }
}

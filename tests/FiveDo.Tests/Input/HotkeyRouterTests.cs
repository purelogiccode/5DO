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
    [InlineData("Escape", HotkeyAction.Pause)]
    [InlineData("F6", HotkeyAction.SlotPrevious)]
    [InlineData("F7", HotkeyAction.SlotNext)]
    [InlineData("F9", HotkeyAction.FrameAdvance)]
    [InlineData("R", HotkeyAction.Rewind)]
    [InlineData("Tab", HotkeyAction.FastForward)]
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

        Assert.Equal(HotkeyAction.None, HotkeyRouter.Resolve(profile, "F1"));
        Assert.Equal(HotkeyAction.None, HotkeyRouter.Resolve(profile, string.Empty));
        Assert.Equal(HotkeyAction.None, HotkeyRouter.Resolve(profile, null));
    }

    [Fact]
    public void Resolve_UsesCustomBindingsAndIgnoresUnknownActionNames()
    {
        InputProfile profile = InputProfile.CreateDefault();
        profile.Hotkeys["SaveState"] = "F2";
        profile.Hotkeys["NotARealAction"] = "F3";

        Assert.Equal(HotkeyAction.SaveState, HotkeyRouter.Resolve(profile, "F2"));
        Assert.Equal(HotkeyAction.None, HotkeyRouter.Resolve(profile, "F5"));
        Assert.Equal(HotkeyAction.None, HotkeyRouter.Resolve(profile, "F3"));
    }

    [Fact]
    public void TryResolve_ReportsWhetherTheKeyIsBound()
    {
        InputProfile profile = InputProfile.CreateDefault();

        Assert.True(HotkeyRouter.TryResolve(profile, "F8", out HotkeyAction load));
        Assert.Equal(HotkeyAction.LoadState, load);
        Assert.False(HotkeyRouter.TryResolve(profile, "F1", out HotkeyAction none));
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
    [InlineData(HotkeyAction.None, "")]
    public void GetActionName_RoundtripsThePersistedName(HotkeyAction action, string expected)
    {
        Assert.Equal(expected, HotkeyRouter.GetActionName(action));
    }

    [Fact]
    public void Resolve_RejectsANullProfile()
    {
        Assert.Throws<ArgumentNullException>(() => HotkeyRouter.Resolve(null!, "F5"));
    }
}

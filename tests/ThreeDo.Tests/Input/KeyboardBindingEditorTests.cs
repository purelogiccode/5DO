using System.Linq;
using ThreeDo.Input;

namespace ThreeDo.Tests.Input;

public sealed class KeyboardBindingEditorTests
{
    [Fact]
    public void GetKey_ReadsTheDefaultProfile()
    {
        var editor = new KeyboardBindingEditor(PlayerProfile.CreateDefault(1));

        Assert.Equal("Z", editor.GetKey(ControllerButton.A));
        Assert.Equal("Enter", editor.GetKey(ControllerButton.P));
        Assert.Null(editor.GetKey(ControllerButton.None));
        Assert.Empty(editor.FindConflicts());
    }

    [Fact]
    public void Rebind_MovesTheKeyAwayFromItsPreviousButton()
    {
        var editor = new KeyboardBindingEditor(PlayerProfile.CreateDefault(1));

        editor.Rebind(ControllerButton.B, "Z");

        Assert.Equal("Z", editor.GetKey(ControllerButton.B));
        Assert.Null(editor.GetKey(ControllerButton.A));
        Assert.Empty(editor.FindConflicts());
    }

    [Fact]
    public void Rebind_CanClearAndReset()
    {
        var editor = new KeyboardBindingEditor(PlayerProfile.CreateDefault(1));

        editor.Rebind(ControllerButton.A, null);
        Assert.Null(editor.GetKey(ControllerButton.A));

        editor.ResetDefaults();
        Assert.Equal("Z", editor.GetKey(ControllerButton.A));
        Assert.Equal("Up", editor.GetKey(ControllerButton.Up));
    }

    [Fact]
    public void ApplyTo_ReplacesTheProfileBindings()
    {
        var editor = new KeyboardBindingEditor(PlayerProfile.CreateDefault(1));
        editor.Rebind(ControllerButton.C, "Q");
        var profile = PlayerProfile.CreateDefault(2);

        editor.ApplyTo(profile);

        Assert.Equal(ControllerButton.C, profile.Keyboard["Q"]);
        Assert.Equal(ControllerButton.A, profile.Keyboard["Z"]);
        Assert.Equal(KeyboardBindingEditor.ControllerButtons.Count, profile.Keyboard.Count);
    }

    [Fact]
    public void Rebind_RejectsNone()
    {
        var editor = new KeyboardBindingEditor(PlayerProfile.CreateDefault(1));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => editor.Rebind(ControllerButton.None, "Q"));
    }

    [Fact]
    public void InputProfile_CloneIsDeep()
    {
        var profile = InputProfile.CreateDefault();
        profile.Players[0].Keyboard["Q"] = ControllerButton.C;
        profile.Hotkeys["SaveState"] = "F6";

        var clone = profile.Clone();
        clone.Players[0].Keyboard["Q"] = ControllerButton.A;
        clone.Players[1].Keyboard["W"] = ControllerButton.B;
        clone.Hotkeys["SaveState"] = "F7";

        Assert.Equal(ControllerButton.C, profile.Players[0].Keyboard["Q"]);
        Assert.False(profile.Players[1].Keyboard.ContainsKey("W"));
        Assert.Equal("F6", profile.Hotkeys["SaveState"]);
        Assert.Equal(ControllerButton.A, clone.Players[0].Keyboard["Q"]);
        Assert.Equal(ControllerButton.B, clone.Players[1].Keyboard["W"]);
        Assert.Equal("F7", clone.Hotkeys["SaveState"]);
    }

    [Fact]
    public void ControllerButtons_ContainsEveryGamepadButtonOnce()
    {
        var buttons = KeyboardBindingEditor.ControllerButtons;
        Assert.Equal(11, buttons.Count);
        Assert.Equal(buttons.Count, buttons.Distinct().Count());
        Assert.DoesNotContain(ControllerButton.None, buttons);
    }
}

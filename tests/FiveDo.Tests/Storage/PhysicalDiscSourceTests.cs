using FiveDo.Storage;

namespace FiveDo.Tests.Storage;

public sealed class PhysicalDiscSourceTests
{
    [Theory]
    [InlineData("D:")]
    [InlineData("d:")]
    [InlineData("D:\\")]
    [InlineData("D:/")]
    [InlineData("\\\\.\\D:")]
    [InlineData("\\\\?\\D:")]
    [InlineData("/dev/sr0")]
    [InlineData(" /dev/cdrom ")]
    [InlineData("\\\\?\\D:\\")]
    public void DriveRoots_AreRecognized(string path)
    {
        Assert.True(PhysicalDiscSource.IsOpticalDrivePath(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("D:\\Games\\game.iso")]
    [InlineData(@"C:\Users\player\game.chd")]
    [InlineData("/home/player/game.cue")]
    [InlineData("game.bin")]
    [InlineData("D")]
    [InlineData(@"\\?\Volume{00000000-0000-0000-0000-000000000000}\")]
    [InlineData(@"\\.\PhysicalDrive0")]
    public void ImagePathsAndFiles_AreNotMistakenForDrives(string? path)
    {
        Assert.False(PhysicalDiscSource.IsOpticalDrivePath(path));
    }

    [Fact]
    public void Open_RejectsANonDrivePath()
    {
        Assert.Throws<DiscImageException>(
            () => PhysicalDiscSource.Open(@"C:\Games\game.iso"));
    }

    [Fact]
    public void DriveEnumeration_ReturnsOnlyRecognizableDrivePaths()
    {
        foreach (var path in PhysicalDiscSource.GetOpticalDriveNames())
        {
            Assert.True(PhysicalDiscSource.IsOpticalDrivePath(path), path);
        }
    }
}

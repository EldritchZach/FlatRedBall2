using FlatRedBall2.Utilities;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Utilities;

public class LinuxVideoDriverTests
{
    [Fact]
    public void GetVideoDriverToSet_NotLinux_ReturnsNull()
    {
        var result = LinuxVideoDriver.GetVideoDriverToSet(isLinux: false, existingSdlVideoDriver: null);

        result.ShouldBeNull();
    }

    [Fact]
    public void GetVideoDriverToSet_LinuxWithNoExistingValue_ReturnsWaylandWithX11Fallback()
    {
        var result = LinuxVideoDriver.GetVideoDriverToSet(isLinux: true, existingSdlVideoDriver: null);

        result.ShouldBe("wayland,x11");
    }

    [Fact]
    public void GetVideoDriverToSet_LinuxWithExistingValue_ReturnsNullToRespectOverride()
    {
        var result = LinuxVideoDriver.GetVideoDriverToSet(isLinux: true, existingSdlVideoDriver: "x11");

        result.ShouldBeNull();
    }
}

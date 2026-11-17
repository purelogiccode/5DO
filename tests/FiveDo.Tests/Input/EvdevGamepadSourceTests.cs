using FiveDo.Frontend.Avalonia.Input;

namespace FiveDo.Tests.Input;

public sealed class EvdevGamepadSourceTests
{
    [Fact]
    public void EventIoctl_EncodesTypeAndRequestNumberLikeTheKernel()
    {
        // EVIOCGBIT(EV_KEY, 96): _IOC(_IOC_READ, 'E', 0x20 + 1, 96).
        Assert.Equal(0x80604521u, EvdevGamepadSource.EventIoctl(0x21, 96));

        // EVIOCGBIT(EV_ABS, 8): _IOC(_IOC_READ, 'E', 0x20 + 3, 8).
        Assert.Equal(0x80084523u, EvdevGamepadSource.EventIoctl(0x23, 8));

        // EVIOCGABS(ABS_X): _IOC(_IOC_READ, 'E', 0x40, sizeof(input_absinfo) = 20).
        Assert.Equal(0x80144540u, EvdevGamepadSource.EventIoctl(0x40, 20));
    }
}

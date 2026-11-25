using System;
using Avalonia.OpenGL;
using FiveDo.Frontend.Avalonia;

namespace FiveDo.Tests.Frontend;

public sealed class OpenGlFailureTests
{
    [Fact]
    public void FindOpenGlSurfaceFailure_UnwrapsAggregateException()
    {
        var failure = new OpenGlException(
            "Unable to configure OpenGL FBO failed with error GL_NO_ERROR (0x00000000)");
        var aggregate = new AggregateException(new InvalidOperationException("outer"), failure);

        Assert.Same(failure, App.FindOpenGlSurfaceFailure(aggregate));
    }

    [Fact]
    public void FindOpenGlSurfaceFailure_WalksInnerExceptions()
    {
        var failure = new OpenGlException("Unable to configure OpenGL FBO failed with error GL_NO_ERROR");
        var wrapped = new InvalidOperationException("wrapper", failure);

        Assert.Same(failure, App.FindOpenGlSurfaceFailure(wrapped));
    }

    [Fact]
    public void FindOpenGlSurfaceFailure_IgnoresUnrelatedErrors()
    {
        Assert.Null(App.FindOpenGlSurfaceFailure(new InvalidOperationException("boom")));
        Assert.Null(App.FindOpenGlSurfaceFailure(new OpenGlException("Unable to create the GL context")));
    }

    [Fact]
    public void TryHandleUnobservedOpenGlSurfaceFailure_LeavesTheFailureUnhandledWithoutAnApp()
    {
        var failure = new OpenGlException(
            "Unable to configure OpenGL FBO failed with error GL_NO_ERROR (0x00000000)");
        var aggregate = new AggregateException(failure);

        // The fallback needs the running App to post to the UI thread. Without one,
        // report the failure as unhandled so the caller logs it instead of silently
        // suppressing it.
        Assert.False(App.TryHandleUnobservedOpenGlSurfaceFailure(aggregate));
        Assert.False(App.TryHandleUnobservedOpenGlSurfaceFailure(new InvalidOperationException("boom")));
    }
}

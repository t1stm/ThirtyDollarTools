using System.Collections.Generic;
using ThirtyDollarConverter.Resamplers;
using ThirtyDollarGUI.Models;

namespace ThirtyDollarGUI.Services;

public static class ResamplerService
{
    public static IEnumerable<ResamplerModel> GetItems()
    {
        return new ResamplerModel[]
        {
            new(new HermiteResampler()),
            new(new LinearResampler()),
            new(new NoInterpolationResampler()),
            new(new ByteCruncherResampler())
        };
    }
}
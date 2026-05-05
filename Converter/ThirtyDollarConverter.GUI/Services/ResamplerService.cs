using System.Collections.Generic;
using ThirtyDollarEncoder.Resamplers;
using ThirtyDollarGUI.Models;

namespace ThirtyDollarGUI.Services;

public static class ResamplerService
{
    public static IEnumerable<ResamplerModel> GetItems()
    {
        return
        [
            new ResamplerModel(new HannSincResampler()),
            new ResamplerModel(new KaiserBestResampler()),
            new ResamplerModel(new KaiserFastResampler()),
            new ResamplerModel(new HermiteResampler()),
            new ResamplerModel(new LinearResampler()),
            new ResamplerModel(new NoInterpolationResampler()),
            new ResamplerModel(new ByteCruncherResampler())
        ];
    }
}
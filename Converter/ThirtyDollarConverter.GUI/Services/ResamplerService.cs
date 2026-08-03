using System.Collections.Generic;
using ThirtyDollarConverter.Encoder.Resamplers;
using ThirtyDollarConverter.GUI.Models;

namespace ThirtyDollarConverter.GUI.Services;

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
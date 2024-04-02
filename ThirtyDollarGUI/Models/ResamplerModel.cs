using System;
using System.Linq;
using ThirtyDollarConverter.Resamplers;

namespace ThirtyDollarGUI.Models;

public class ResamplerModel
{
    public readonly IResampler Resampler;

    public ResamplerModel(IResampler resampler)
    {
        Resampler = resampler;
    }

    public string? ResamplerName => GetReadableTypeName(Resampler.GetType())?.Split('.').Last();

    private static string? GetReadableTypeName(Type type)
    {
        if (!type.IsGenericType) return type.FullName;
        var genericArguments = type.GetGenericArguments();
        var genericTypeName = type.GetGenericTypeDefinition().FullName;

        if (genericTypeName == null) return type.FullName;
        genericTypeName = genericTypeName[..genericTypeName.IndexOf('`')];
        var argumentTypeNames = genericArguments.Select(GetReadableTypeName);
        return $"{genericTypeName}<{string.Join(", ", argumentTypeNames)}>";
    }
}
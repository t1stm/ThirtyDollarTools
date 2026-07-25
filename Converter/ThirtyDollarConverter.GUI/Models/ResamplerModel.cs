using System;
using System.Linq;
using ThirtyDollarEncoder.Resamplers;

namespace ThirtyDollarGUI.Models;

public class ResamplerModel(IResampler resampler)
{
    public readonly IResampler Resampler = resampler;

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
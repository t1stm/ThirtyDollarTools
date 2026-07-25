using System.Reflection;
using Sundex.Components.Bars;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract;

namespace Sundex.Components.Tests;

public static class ProgressBarTestExtensions
{
    public static void TestApplyStyleValue(this ProgressBar bar, StyleSheet styleSheet, IStyleValue? styleValue,
        PropertyInfo propertyInfo)
    {
        var method = typeof(ProgressBar).GetMethod("ApplyStyleValue", BindingFlags.Instance | BindingFlags.NonPublic);
        method!.Invoke(bar, [styleSheet, styleValue, propertyInfo]);
    }
}
using System.Collections;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;

namespace XAMLComposition.Core;

[MarkupExtensionReturnType(ReturnType = typeof(IList))]
public class IntSampleSource : MarkupExtension
{
    public int Count { get; set; }

    protected override object ProvideValue() => Enumerable.Range(0, 50).ToList();
}


[AttachedProperty<bool>("UseLights")]
[AttachedProperty<bool>("EnableDepthMatrix")]
[Bindable]
public static partial class Properties
{
    static List<Type> _lights { get; } = [typeof(AmbLight), typeof(SimpleSpotLight)];

    static partial void OnUseLightsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement u)
        {
            u.Lights.Clear();

            if (e.NewValue is bool b && b)
                foreach (var light in _lights)
                    u.Lights.Add((XamlLight)Activator.CreateInstance(light));
        }
    }

    static partial void OnEnableDepthMatrixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement u && e.NewValue is bool b)
        {
            if (b)
                CompositionFactory.EnableAutoPerspectiveMatrix(u);
            else
                u.GetElementVisual().StopAnimation(nameof(Visual.TransformMatrix));
        }
    }
}

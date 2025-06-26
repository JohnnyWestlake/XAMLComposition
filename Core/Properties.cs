using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace XAMLComposition.Core;


[AttachedProperty<bool>("UseLights")]
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
}

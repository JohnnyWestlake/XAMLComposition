using Microsoft.Graphics.Canvas.Effects;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using XAMLComposition.Core;

namespace XAMLComposition.Brushes;

[DependencyProperty<double>("BlurAmount")]
[DependencyProperty<Color>("TintColor", "Colors.Silver")]
public partial class TintedBlurBrush : XamlCompositionBrushBase
{
    partial void OnBlurAmountChanged(double o, double n)
    {
        if (CompositionBrush is not null)
            CompositionBrush.Properties.Insert(nameof(BlurAmount), (float)n);
    }

    partial void OnTintColorChanged(Color o, Color n)
    {
        if (CompositionBrush is not null)
            CompositionBrush.Properties.Insert(nameof(TintColor), n);
    }

    protected override void OnConnected()
    {
        Compositor compositor = Window.Current.Compositor;

        // CompositionCapabilities: Are Effects supported?
        var capabilities = CompositionCapabilities.GetForCurrentView();
        bool usingFallback = !capabilities.AreEffectsSupported();
        if (usingFallback)
        {
            // If Effects are not supported, use Fallback Solid Color
            CompositionBrush = compositor.CreateColorBrush(FallbackColor);
            return;
        }


        // Define Effect graph
        var graphicsEffect = new BlendEffect
        {
            Mode = BlendEffectMode.LinearBurn,
            Background = new ColorSourceEffect()
            {
                Name = "Tint",
                Color = Colors.Transparent,
            },
            Foreground = new GaussianBlurEffect()
            {
                Name = "Blur",
                Source = new CompositionEffectSourceParameter("Backdrop"),
                BlurAmount = 0,
                BorderMode = EffectBorderMode.Hard,
            }
        };

        // Create EffectFactory and EffectBrush
        CompositionEffectFactory effectFactory = compositor.CreateEffectFactory(graphicsEffect, ["Blur.BlurAmount", "Tint.Color"]);
        CompositionEffectBrush effectBrush = effectFactory.CreateBrush();

        // Create BackdropBrush
        CompositionBackdropBrush backdrop = compositor.CreateBackdropBrush();
        effectBrush.SetSourceParameter("backdrop", backdrop);

        // Set EffectBrush to paint Xaml UIElement
        CompositionBrush = effectBrush;

        // Ensure values are on the PropertySet before we call the animations below
        OnBlurAmountChanged(double.NaN, BlurAmount);
        OnTintColorChanged(TintColor, TintColor);

        // Bind Blur amount in a way that external XAMLCompositionAnimations can change
        effectBrush.StartAnimation("Blur.BlurAmount",
            compositor.CreateExpressionAnimation()
                .SetExpression("p.BlurAmount")
                .SetParameter("p", effectBrush.Properties));

        // Bind TintColor in a way that external XAMLCompositionAnimations can change
        effectBrush.StartAnimation("Tint.Color",
            compositor.CreateExpressionAnimation()
                .SetExpression("p.TintColor")
                .SetParameter("p", effectBrush.Properties));
    }

    protected override void OnDisconnected()
    {
        // Dispose CompositionBrushes if XamlCompBrushBase is removed from tree
        if (CompositionBrush != null)
        {
            CompositionBrush.Dispose();
            CompositionBrush = null;
        }
    }
}
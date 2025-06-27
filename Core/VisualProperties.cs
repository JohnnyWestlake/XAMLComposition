using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace XAMLComposition.Core;

/// <summary>
/// Allows setting Visual Properties in XAML.
/// Does NOT return the live property value from the Visual, 
/// it only returns the last value set by the AttachedProperty
/// </summary>
[Bindable]
[AttachedProperty<object>(nameof(Visual.Offset), "Vector3.Zero")]
[AttachedProperty<object>("Translation", "Vector3.Zero")]
[AttachedProperty<object>(nameof(Visual.RelativeOffsetAdjustment), "Vector3.Zero")]
[AttachedProperty<object>(nameof(Visual.RelativeSizeAdjustment), "Vector2.One")]
[AttachedProperty<object>(nameof(Visual.RotationAxis), "Vector3.UnitZ")]
[AttachedProperty<CompositionBackfaceVisibility>(nameof(Visual.BackfaceVisibility))]
[AttachedProperty<bool>(nameof(Visual.IsVisible), true)]
[AttachedProperty<bool>(nameof(Visual.IsPixelSnappingEnabled))]
[AttachedProperty<double>(nameof(Visual.Opacity), 1d)]
public static partial class VisualProperties
{
    static partial void OnOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement u)
        {
            Visual v = u.GetElementVisual();

            if (e.NewValue is Vector3 v3
                || (e.NewValue is string str && XAMLCore.TryParse(str, out v3)))
                v.Offset = v3;
        }
    }

    static partial void OnTranslationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement u)
        {
            Visual v = u.EnableCompositionTranslation().GetElementVisual();

            if (e.NewValue is Vector3 v3
                || (e.NewValue is string str && XAMLCore.TryParse(str, out v3)))
                v.SetTranslation(v3);
        }
    }

    static partial void OnRelativeOffsetAdjustmentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement u)
        {
            Visual v = u.GetElementVisual();

            if (e.NewValue is Vector3 v3
                || (e.NewValue is string str && XAMLCore.TryParse(str, out v3)))
                v.RelativeOffsetAdjustment = v3;
        }
    }

    static partial void OnRelativeSizeAdjustmentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement u)
        {
            Visual v = u.GetElementVisual();

            if (e.NewValue is Vector2 v2
                || (e.NewValue is string str && XAMLCore.TryParse(str, out v2)))
                v.RelativeSizeAdjustment = v2;
            else if (e.NewValue is Point p)
                v.RelativeSizeAdjustment = p.ToVector2();
        }
    }

    static partial void OnRotationAxisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement u)
        {
            Visual v = u.GetElementVisual();

            if (e.NewValue is Vector3 v3
                || (e.NewValue is string str && XAMLCore.TryParse(str, out v3)))
                v.RotationAxis = v3;
        }
    }

    static partial void OnBackfaceVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement u && e.NewValue is CompositionBackfaceVisibility vis)
            u.GetElementVisual().BackfaceVisibility = vis;
        else if (d is UIElement u2 && e.NewValue is int i)
                u2.GetElementVisual().BackfaceVisibility = (CompositionBackfaceVisibility)i;
    }

    static partial void OnIsPixelSnappingEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement u && e.NewValue is bool value)
            u.GetElementVisual().IsPixelSnappingEnabled = value;
    }

    static partial void OnIsVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement u && e.NewValue is bool value)
            u.GetElementVisual().IsVisible = value;
    }

    static partial void OnOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement u && e.NewValue is double value)
            u.GetElementVisual().Opacity = (float)value;
    }
}

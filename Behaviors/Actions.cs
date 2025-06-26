using Microsoft.Xaml.Interactivity;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Markup;
using XAMLComposition.Core;

namespace XAMLComposition.Behaviors;

[DependencyProperty<object>("Target", default, nameof(ResetDefault))]
[DependencyProperty<string>("Key", default, nameof(ResetDefault))]
[DependencyProperty("Value")]
[DependencyProperty("DefaultValue", default, nameof(UpdateDefault))]
public partial class SetPropertySetAction : DependencyObject, IAction
{
    bool defaultIsSet = false;

    void ResetDefault()
    {
        defaultIsSet = false;
        UpdateDefault();
    }

    void UpdateDefault()
    {
        if (defaultIsSet 
            || Target is null 
            || string.IsNullOrWhiteSpace(Key) 
            || ReadLocalValue(DefaultValueProperty) == DependencyProperty.UnsetValue)
            return;

        CompositionPropertySet set;

        if (Target is FrameworkElement f)
            set = f.GetElementVisual().Properties;
        else if (Target is CompositionPropertySet cps)
            set = cps;
        else if (Target is CompositionObject v)
            set = v.Properties;
        else
            return;

        // This helper method will insert the default value into the property set
        // only if it doesn't already exist, and will cast the source type to the
        // appropriate CompositionParameterType.
        defaultIsSet = Composition.TryInsertProperty(
            set,
            Key,
            DefaultValue,
            CompositionParameterType.Unknown,
            false);
    }

    public object Execute(object sender, object parameter)
    {
        if (Target is null || string.IsNullOrWhiteSpace(Key) || Value is null)
            return false;

        if (Target is FrameworkElement f)
        {
            var props = f.GetElementVisual().Properties;
            props.TryInsert(Key, Value, out bool s);
            return s;
        }
        else if (Target is CompositionPropertySet cps)
        {
            cps.TryInsert(Key, Value, out bool s);
            return s;
        }
        else if (Target is CompositionObject v)
        {
            v.Properties.TryInsert(Key, Value, out bool s);
            return s;
        }
        else
        {
            // Unsupported target type
            return false;
        }
    }
}

[ContentProperty(Name = nameof(Animations))]
[DependencyProperty<XAMLAnimationCollection>("Animations")]
[DependencyProperty<DependencyObject>("Target")]
public partial class SetAnimationCollectionAction : DependencyObject, IAction
{
    public object Execute(object sender, object parameter)
    {
        if (Target is not null)
            Composition.SetAnimations(Target, Animations);

        return true;
    }
}

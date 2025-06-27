using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace XAMLComposition.Core;

[Bindable]
public static class CompositionProperty
{
    #region Animations AttachedProperty

    public static XAMLAnimationCollection GetAnimations(DependencyObject obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        // Ensure there is always a collection when accessed via code
        XAMLAnimationCollection collection = (XAMLAnimationCollection)obj.GetValue(AnimationsProperty);
        if (collection == null)
        {
            collection = new();
            obj.SetValue(AnimationsProperty, collection);
        }

        return collection;
    }

    public static void SetAnimations(DependencyObject obj, XAMLAnimationCollection value)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));
        obj.SetValue(AnimationsProperty, value);

        if (value is not null)
        {
            value.Attach(obj);
        }
    }

    public static readonly DependencyProperty AnimationsProperty =
        DependencyProperty.RegisterAttached(
            "Animations",
            typeof(XAMLAnimationCollection),
            typeof(CompositionProperty),
            new PropertyMetadata(null, (s, e) =>
            {
                if (e.NewValue == e.OldValue || s is not DependencyObject obj)
                    return;

                if (e.OldValue is XAMLAnimationCollection old)
                {
                    if (s is FrameworkElement f)
                    {
                        f.Loaded -= Obj_Loaded;
                        f.Unloaded -= Obj_Unloaded;
                    }

                    old.Detach(obj);
                }

                if (e.NewValue is XAMLAnimationCollection collection)
                {
                    if (s is FrameworkElement f)
                    {
                        f.Loaded -= Obj_Loaded;
                        f.Unloaded -= Obj_Unloaded;

                        f.Loaded += Obj_Loaded;
                        f.Unloaded += Obj_Unloaded;
                    }

                    collection.Attach(obj);
                }
            }));

    private static void Obj_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement f)
        {
            if (GetAnimations(f) is { } a)
                a.Attach(f);
        }
    }

    private static void Obj_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement f && GetAnimations(f) is { } a)
            a.Detach(f);
    }



    #endregion


    #region PropertyBinders

    public static PropertyBinderCollection GetPropertyBinders(DependencyObject obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        // Ensure there is always a collection when accessed via code
        PropertyBinderCollection collection = (PropertyBinderCollection)obj.GetValue(PropertyBindersProperty);
        if (collection == null)
        {
            collection = [];
            obj.SetValue(PropertyBindersProperty, collection);
        }

        return collection;
    }

    public static void SetPropertyBinders(DependencyObject obj, PropertyBinderCollection value)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));
        obj.SetValue(PropertyBindersProperty, value);
        value?.Attach(obj);
    }

    public static readonly DependencyProperty PropertyBindersProperty =
        DependencyProperty.RegisterAttached(
            "PropertyBinders",
            typeof(PropertyBinderCollection),
            typeof(CompositionProperty),
            new PropertyMetadata(null, (s, e) =>
            {
                if (e.NewValue == e.OldValue || s is not DependencyObject obj)
                    return;

                if (e.OldValue is PropertyBinderCollection old)
                    old.Detach(obj);

                if (e.NewValue is PropertyBinderCollection collection)
                    collection.Attach(obj);
            }));

    #endregion

    #region PropertySetSetters

    public static CompositionParameterCollection GetPropertySetSetters(DependencyObject obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        // Ensure there is always a collection when accessed via code
        CompositionParameterCollection collection = (CompositionParameterCollection)obj.GetValue(PropertySetSettersProperty);
        if (collection == null)
        {
            collection = [];
            obj.SetValue(PropertySetSettersProperty, collection);
        }

        return collection;
    }

    public static void SetPropertySetSetters(DependencyObject obj, CompositionParameterCollection value)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));
        obj.SetValue(PropertySetSettersProperty, value);
    }

    public static readonly DependencyProperty PropertySetSettersProperty =
        DependencyProperty.RegisterAttached(
            "PropertySetSetters",
            typeof(CompositionParameterCollection),
            typeof(CompositionProperty),
            new PropertyMetadata(null, (s, e) =>
            {
                if (e.NewValue == e.OldValue || s is not DependencyObject obj)
                    return;

                if (e.OldValue is CompositionParameterCollection old)
                    old.SetTarget(null);

                if (e.NewValue is CompositionParameterCollection collection)
                {
                    if (obj is UIElement element)
                        collection.SetTarget(element.GetElementVisual().Properties);
                }
            }));

    #endregion

}

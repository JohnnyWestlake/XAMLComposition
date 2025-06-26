using System.Reflection;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;

namespace XAMLComposition.Core;

/// <summary>
/// Helpful extensions methods to enable you to write fluent Composition animations
/// </summary>
public static class Composition
{
    /*
     * NOTE
     * 
     * Type constraints on extension methods do not form part of the method
     * signature used for choosing a correct method. Therefore two extensions
     * with the same parameters but different type constraints will conflict
     * with each other.
     * 
     * Due to this, some methods here use type constraints whereas other that
     * conflict with the XAML storyboarding extensions use explicit type
     * extensions. When adding methods, please keep in mind whether it's 
     * possible some other toolkit might have a similar signature for extensions
     * to form your plan of attack
     */

    #region Fundamentals

    public static T SafeDispose<T>(T disposable) where T : IDisposable
    {
        disposable?.Dispose();
        return default;
    }

    public static Compositor Compositor { get; set; } = Window.Current.Compositor;

    public static void CreateScopedBatch(this Compositor compositor,
        CompositionBatchTypes batchType,
        Action<CompositionScopedBatch> action,
        Action<CompositionScopedBatch> onCompleteAction = null)
    {
        if (action == null)
            throw
              new ArgumentException("Cannot create a scoped batch on an action with null value!",
              nameof(action));

        // Create ScopedBatch
        var batch = compositor.CreateScopedBatch(batchType);

        //// Handler for the Completed Event
        void handler(object s, CompositionBatchCompletedEventArgs ea)
        {
            // Unsubscribe the handler from the Completed Event
            ((CompositionScopedBatch)s).Completed -= handler;

            try
            {
                // Invoke the post action
                onCompleteAction?.Invoke(batch);
            }
            finally
            {
                ((CompositionScopedBatch)s).Dispose();
            }
        }

        batch.Completed += handler;

        // Invoke the action
        action(batch);


        // End Batch
        batch.End();
    }

    private static Dictionary<Compositor, Dictionary<string, object>> _objCache { get; } = new();

    public static T GetCached<T>(this Compositor c, string key, Func<Compositor, T> create)
    {
//#if DEBUG
//        return create();
//#endif

        if (_objCache.TryGetValue(c, out Dictionary<string, object> dic) is false)
            _objCache[c] = dic = new();

        if (dic.TryGetValue(key, out object value) is false)
            dic[key] = value = create(c);

        return (T)value;
    }

    /// <summary>
    /// Gets a cached version of a CompositionObject per compositor
    /// (Each CoreWindow has it's own compositor). Allows sharing of animations
    /// without recreating everytime.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="c"></param>
    /// <param name="key"></param>
    /// <param name="create"></param>
    /// <returns></returns>
    public static T GetCached<T>(this CompositionObject c, string key, Func<Compositor, T> create) where T : CompositionObject
    {
        return GetCached<T>(c.Compositor, key, create);
    }

    public static CubicBezierEasingFunction GetCachedEntranceEase(this Compositor c)
    {
        return c.GetCached<CubicBezierEasingFunction>("EntranceEase",
            cc => cc.CreateEntranceEasingFunction());
    }

    public static CubicBezierEasingFunction GetCachedFluentEntranceEase(this Compositor c)
    {
        return c.GetCached<CubicBezierEasingFunction>("FluentEntranceEase",
            cc => cc.CreateFluentEntranceEasingFunction());
    }

    #endregion


    #region Element / Base Extensions

    public static IEnumerable<Visual> GetVisuals<T>(this IEnumerable<T> elements) where T : UIElement
    {
        foreach (var item in elements)
            yield return GetElementVisual(item);
    }

    /// <summary>
    /// Returns the Composition Hand-off Visual for this framework element
    /// </summary>
    /// <param name="element"></param>
    /// <returns>Composition Hand-off Visual</returns>
    public static Visual GetElementVisual(this UIElement element) => element == null ? null : ElementCompositionPreview.GetElementVisual(element);

    public static ContainerVisual GetContainerVisual(this UIElement element, bool linkSize = true)
    {
        if (element == null)
            return null;

        if (ElementCompositionPreview.GetElementChildVisual(element) is ContainerVisual container)
            return container;

        // Create a new container visual, link it's size to the element's and then set
        // the container as the child visual of the element.
        container = GetElementVisual(element).Compositor.CreateContainerVisual();
        CompositionExtensions.LinkSize(container, GetElementVisual(element));
        element.SetChildVisual(container);

        return container;
    }

    public static CompositionPropertySet GetScrollManipulationPropertySet(this ScrollViewer scrollViewer)
    {
        return ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scrollViewer);
    }

    public static void SetShowAnimation(this UIElement element, ICompositionAnimationBase animation)
    {
        ElementCompositionPreview.SetImplicitShowAnimation(element, animation);
    }

    public static void SetHideAnimation(this UIElement element, ICompositionAnimationBase animation)
    {
        ElementCompositionPreview.SetImplicitHideAnimation(element, animation);
    }

    public static T SetChildVisual<T>(this T element, Visual visual) where T : UIElement
    {
        ElementCompositionPreview.SetElementChildVisual(element, visual);
        return element;
    }

    public static bool SupportsAlphaMask(UIElement element)
    {
        return element switch
        {
            TextBlock _ or Shape _ or Image _ => true,
            _ => false,
        };
    }

    public static InsetClip ClipToBounds(UIElement element)
    {
        var v = GetElementVisual(element);
        var c = v.Compositor.CreateInsetClip();
        v.Clip = c;
        return c;
    }

    /// <summary>
    /// Attempts to get the AlphaMask from supported UI elements.
    /// Returns null if the element cannot create an AlphaMask.
    /// </summary>
    /// <param name="element"></param>
    /// <returns></returns>
    public static CompositionBrush GetAlphaMask(UIElement element)
    {
        switch (element)
        {
            case TextBlock t:
                return t.GetAlphaMask();

            case Shape s:
                return s.GetAlphaMask();

            case Image i:
                return i.GetAlphaMask();

            default:
                break;
        }

        //if (element is ISupportsAlphaMask mask)
        //    return mask.GetAlphaMask();

        return null;
    }

    #endregion


    #region Translation

    //public static IEnumerable<T> EnableCompositionTranslation<T>(this IEnumerable<T> elements) where T : UIElement
    //{
    //    foreach (var element in elements)
    //    {
    //        element.EnableCompositionTranslation();
    //        yield return element;
    //    }
    //}

    public static UIElement EnableCompositionTranslation(this UIElement element)
    {
        return EnableCompositionTranslation(element, null);
    }

    public static UIElement EnableCompositionTranslation(this UIElement element, float x, float y, float z)
    {
        return EnableCompositionTranslation(element, new Vector3(x, y, z));
    }

    public static UIElement EnableCompositionTranslation(this UIElement element, Vector3? defaultTranslation)
    {
        Visual visual = GetElementVisual(element);
        if (visual.Properties.TryGetVector3(CompositionFactory.TRANSLATION, out _) == CompositionGetValueStatus.NotFound)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(element, true);
            if (defaultTranslation.HasValue)
                visual.Properties.InsertVector3(CompositionFactory.TRANSLATION, defaultTranslation.Value);
            else
                visual.Properties.InsertVector3(CompositionFactory.TRANSLATION, new Vector3());
        }

        return element;
    }

    public static bool IsTranslationEnabled(this UIElement element)
    {
        return GetElementVisual(element).Properties.TryGetVector3(CompositionFactory.TRANSLATION, out _) != CompositionGetValueStatus.NotFound;
    }

    public static Vector3 GetTranslation(this Visual visual)
    {
        visual.Properties.TryGetVector3(CompositionFactory.TRANSLATION, out Vector3 translation);
        return translation;
    }

    public static Visual SetTranslation(this Visual visual, float x, float y, float z)
    {
        return SetTranslation(visual, new Vector3(x, y, z));
    }

    public static Visual SetTranslation(this Visual visual, Vector3 translation)
    {
        visual.Properties.InsertVector3(CompositionFactory.TRANSLATION, translation);
        return visual;
    }

    /// <summary>
    /// Sets the axis to rotate the visual around.
    /// </summary>
    /// <param name="visual"></param>
    /// <param name="axis"></param>
    /// <returns></returns>
    public static Visual SetRotationAxis(this Visual visual, Vector3 axis)
    {
        visual.RotationAxis = axis;
        return visual;
    }

    /// <summary>
    /// Sets the axis to rotate the visual around.
    /// </summary>
    /// <param name="visual"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public static Visual SetRotationAxis(this Visual visual, float x, float y, float z)
    {
        visual.RotationAxis = new Vector3(x, y, z);
        return visual;
    }

    public static Visual SetCenterPoint(this Visual visual, float x, float y, float z)
    {
        return SetCenterPoint(visual, new Vector3(x, y, z));
    }

    public static Visual SetCenterPoint(this Visual visual, Vector3 vector)
    {
        visual.CenterPoint = vector;
        return visual;
    }

    /// <summary>
    /// Sets the centre point of a visual to its current cartesian centre (relative 0.5f, 0.5f).
    /// </summary>
    /// <param name="visual"></param>
    /// <returns></returns>
    public static Visual SetCenterPoint(this Visual visual)
    {
        return SetCenterPoint(visual, new Vector3(visual.Size / 2f, 0f));
    }

    #endregion


    #region ICompositionAnimationBase

    public static void ClearParameter(this ICompositionAnimationBase animation, string p)
    {
        void Clear(CompositionAnimation a) => a.ClearParameter(p);

        if (animation is CompositionAnimationGroup group)
            foreach (var a in group)
                Clear(a);
        else if (animation is CompositionAnimation a)
            Clear(a);
    }

    public static void SetReferenceParameter(this ICompositionAnimationBase animation, string key, CompositionObject p)
    {
        void Set(CompositionAnimation a) => a.SetReferenceParameter(key, p);

        if (animation is CompositionAnimationGroup group)
            foreach (var a in group)
                Set(a);
        else if (animation is CompositionAnimation a)
            Set(a);
    }

    public static void SetScalarParameter(this ICompositionAnimationBase animation, string key, float v)
    {
        void Set(CompositionAnimation a) => a.SetScalarParameter(key, v);

        if (animation is CompositionAnimationGroup group)
            foreach (var a in group)
                Set(a);
        else if (animation is CompositionAnimation a)
            Set(a);
    }

    public static void SetVector2Parameter(this ICompositionAnimationBase animation, string key, Vector2 v)
    {
        void Set(CompositionAnimation a) => a.SetParameter(key, v);

        if (animation is CompositionAnimationGroup group)
            foreach (var a in group)
                Set(a);
        else if (animation is CompositionAnimation a)
            Set(a);
    }

    public static void SetVector3Parameter(this ICompositionAnimationBase animation, string key, Vector3 v)
    {
        void Set(CompositionAnimation a) => a.SetParameter(key, v);

        if (animation is CompositionAnimationGroup group)
            foreach (var a in group)
                Set(a);
        else if (animation is CompositionAnimation a)
            Set(a);
    }

    public static void SetVector4Parameter(this ICompositionAnimationBase animation, string key, Vector4 v)
    {
        void Set(CompositionAnimation a) => a.SetParameter(key, v);

        if (animation is CompositionAnimationGroup group)
            foreach (var a in group)
                Set(a);
        else if (animation is CompositionAnimation a)
            Set(a);
    }

    public static void SetMatrix3x2Parameter(this ICompositionAnimationBase animation, string key, Matrix3x2 v)
    {
        void Set(CompositionAnimation a) => a.SetParameter(key, v);

        if (animation is CompositionAnimationGroup group)
            foreach (var a in group)
                Set(a);
        else if (animation is CompositionAnimation a)
            Set(a);
    }

    public static void SetMatrix4x4Parameter(this ICompositionAnimationBase animation, string key, Matrix4x4 v)
    {
        void Set(CompositionAnimation a) => a.SetParameter(key, v);

        if (animation is CompositionAnimationGroup group)
            foreach (var a in group)
                Set(a);
        else if (animation is CompositionAnimation a)
            Set(a);
    }

    public static void SetQuaternionParameter(this ICompositionAnimationBase animation, string key, Quaternion v)
    {
        void Set(CompositionAnimation a) => a.SetParameter(key, v);

        if (animation is CompositionAnimationGroup group)
            foreach (var a in group)
                Set(a);
        else if (animation is CompositionAnimation a)
            Set(a);
    }

    public static void SetExpressionReferenceParameter(this ICompositionAnimationBase animation, string key, IAnimationObject v)
    {
        void Set(CompositionAnimation a) => a.SetExpressionReferenceParameter(key, v);

        if (animation is CompositionAnimationGroup group)
            foreach (var a in group)
                Set(a);
        else if (animation is CompositionAnimation a)
            Set(a);
    }

    public static void SetBooleanParameter(this ICompositionAnimationBase animation, string key, bool v)
    {
        void Set(CompositionAnimation a) => a.SetParameter(key, v);

        if (animation is CompositionAnimationGroup group)
            foreach (var a in group)
                Set(a);
        else if (animation is CompositionAnimation a)
            Set(a);
    }

    public static void SetColorParameter(this ICompositionAnimationBase animation, string key, Color v)
    {
        void Set(CompositionAnimation a) => a.SetParameter(key, v);

        if (animation is CompositionAnimationGroup group)
            foreach (var a in group)
                Set(a);
        else if (animation is CompositionAnimation a)
            Set(a);
    }

    #endregion


    #region SetTarget

    public static T SetTarget<T>(this T animation, string target) where T : CompositionAnimation
    {
        animation.Target = target;
        return animation;
    }

    public static T SetSafeTarget<T>(this T animation, string target) where T : ICompositionAnimationBase
    {
        if (!String.IsNullOrEmpty(target))
        {
            void Set(CompositionAnimation ani) => ani.Target = target;

            if (animation is CompositionAnimationGroup group)
                foreach (var a in group)
                    Set(a);
            else if (animation is CompositionAnimation a)
                Set(a);
        }

        return animation;
    }

    #endregion

    #region SetDelayTime

    /// <summary>
    /// Sets the delay time in seconds
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="animation"></param>
    /// <param name="delayTime">Delay Time in seconds</param>
    /// <returns></returns>
    public static T SetDelayTime<T>(this T animation, double delayTime)
        where T : KeyFrameAnimation
    {
        animation.DelayTime = TimeSpan.FromSeconds(delayTime);
        return animation;
    }

    public static T SetDelayTime<T>(this T animation, TimeSpan delayTime)
        where T : KeyFrameAnimation
    {
        animation.DelayTime = delayTime;
        return animation;
    }

    #endregion


    #region SetDelay

    /// <summary>
    /// Sets the delay time in seconds
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="animation"></param>
    /// <param name="delayTime">Delay Time in seconds</param>
    /// <returns></returns>
    public static T SetDelay<T>(this T animation, double delayTime, AnimationDelayBehavior behavior)
        where T : KeyFrameAnimation
    {
        animation.DelayTime = TimeSpan.FromSeconds(delayTime);
        animation.DelayBehavior = behavior;
        return animation;
    }

    public static T SetDelay<T>(this T animation, TimeSpan delayTime, AnimationDelayBehavior behavior)
        where T : KeyFrameAnimation
    {
        animation.DelayBehavior = behavior;
        animation.DelayTime = delayTime;
        return animation;
    }

    #endregion


    #region SetDelayBehaviour

    public static T SetDelayBehavior<T>(this T animation, AnimationDelayBehavior behavior)
        where T : KeyFrameAnimation
    {
        animation.DelayBehavior = behavior;
        return animation;
    }

    public static T SetInitialValueBeforeDelay<T>(this T animation)
       where T : KeyFrameAnimation
    {
        animation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        return animation;
    }

    #endregion


    #region SetDuration

    /// <summary>
    /// Sets the duration in seconds. If less than 0 the duration is not set.
    /// </summary>
    /// <param name="animation"></param>
    /// <param name="duration">Duration in seconds</param>
    /// <returns></returns>
    public static T SetDuration<T>(this T animation, double duration) where T : KeyFrameAnimation
    {
        if (duration >= 0)
            return SetDuration(animation, TimeSpan.FromSeconds(duration));
        else
            return animation;
    }

    public static T SetDuration<T>(this T animation, TimeSpan duration) where T : KeyFrameAnimation
    {
        animation.Duration = duration;
        return animation;
    }

    #endregion


    #region StopBehaviour

    public static T SetStopBehavior<T>(this T animation, AnimationStopBehavior stopBehavior) where T : KeyFrameAnimation
    {
        animation.StopBehavior = stopBehavior;
        return animation;
    }

    #endregion


    #region Direction

    public static T SetDirection<T>(this T animation, AnimationDirection direction) where T : KeyFrameAnimation
    {
        animation.Direction = direction;
        return animation;
    }

    #endregion


    #region Comment

    public static T SetComment<T>(this T obj, string comment) where T : CompositionObject
    {
        obj.Comment = comment;
        return obj;
    }

    #endregion


    #region IterationBehavior

    public static T SetIterationBehavior<T>(this T animation, AnimationIterationBehavior iterationBehavior) where T : KeyFrameAnimation
    {
        animation.IterationBehavior = iterationBehavior;
        return animation;
    }

    #endregion


    #region AddKeyFrame Builders

    public static T SetFinalValue<T>(this T animation, Vector3 finalValue) where T : Vector3NaturalMotionAnimation
    {
        animation.FinalValue = finalValue;
        return animation;
    }

    public static T SetFinalValue<T>(this T animation, float x, float y, float z) where T : Vector3NaturalMotionAnimation
    {
        animation.FinalValue = new Vector3(x, y, z);
        return animation;
    }

    public static T AddKeyFrame<T>(this T animation, float normalizedProgressKey, string expression, KeySpline spline) where T : KeyFrameAnimation
    {
        animation.InsertExpressionKeyFrame(normalizedProgressKey, expression, animation.Compositor.CreateCubicBezierEasingFunction(spline));
        return animation;
    }

    public static T AddKeyFrame<T>(this T animation, float normalizedProgressKey, string expression, CubicBezierPoints spline) where T : KeyFrameAnimation
    {
        animation.InsertExpressionKeyFrame(normalizedProgressKey, expression, animation.Compositor.CreateCubicBezierEasingFunction(spline));
        return animation;
    }

    public static T AddKeyFrame<T>(this T animation, float normalizedProgressKey, string expression, CompositionEasingFunction ease = null) where T : KeyFrameAnimation
    {
        animation.InsertExpressionKeyFrame(normalizedProgressKey, expression, ease);
        return animation;
    }

    public static ScalarKeyFrameAnimation AddKeyFrame(this ScalarKeyFrameAnimation animation, float normalizedProgressKey, float value, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, ease);
        return animation;
    }

    public static ScalarKeyFrameAnimation AddKeyFrame(this ScalarKeyFrameAnimation animation, float normalizedProgressKey, float value, KeySpline ease)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, animation.Compositor.CreateCubicBezierEasingFunction(ease));
        return animation;
    }

    public static ColorKeyFrameAnimation AddKeyFrame(this ColorKeyFrameAnimation animation, float normalizedProgressKey, Color value, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, ease);
        return animation;
    }

    public static Vector2KeyFrameAnimation AddKeyFrame(this Vector2KeyFrameAnimation animation, float normalizedProgressKey, Vector2 value, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, ease);
        return animation;
    }

    public static Vector2KeyFrameAnimation AddKeyFrame(this Vector2KeyFrameAnimation animation, float normalizedProgressKey, Vector2 value, KeySpline ease)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, animation.Compositor.CreateCubicBezierEasingFunction(ease));
        return animation;
    }

    #region Vector3

    public static Vector3KeyFrameAnimation AddKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, Vector3 value, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, ease);
        return animation;
    }

    public static Vector3KeyFrameAnimation AddKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, Vector3 value, KeySpline ease)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, animation.Compositor.CreateCubicBezierEasingFunction(ease));
        return animation;
    }

    public static Vector3KeyFrameAnimation AddKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, Vector3 value, CubicBezierPoints ease)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, animation.Compositor.CreateCubicBezierEasingFunction(ease));
        return animation;
    }

    /// <summary>
    /// Adds a Vector3KeyFrame where the X & Y components are set to the input value and the Z component defaults to 0f.
    /// </summary>
    /// <param name="animation"></param>
    /// <param name="normalizedProgressKey"></param>
    /// <param name="value"></param>
    /// <param name="ease"></param>
    /// <returns></returns>
    public static Vector3KeyFrameAnimation AddKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, float value, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, new Vector3(value, value, 0f), ease);
        return animation;
    }

    public static Vector3KeyFrameAnimation AddKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, float value, KeySpline ease)
    {
        animation.InsertKeyFrame(normalizedProgressKey, new Vector3(value, value, 0f), animation.Compositor.CreateCubicBezierEasingFunction(ease));
        return animation;
    }

    /// <summary>
    /// Adds a Vector3KeyFrame where the X & Y components are set to the input value and the Z component defaults to 1f.
    /// </summary>
    /// <param name="animation"></param>
    /// <param name="normalizedProgressKey"></param>
    /// <param name="value"></param>
    /// <param name="ease"></param>
    /// <returns></returns>
    public static Vector3KeyFrameAnimation AddScaleKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, float value, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, new Vector3(value, value, 1f), ease);
        return animation;
    }

    /// <summary>
    /// Adds a Vector3KeyFrame where the X & Y components are set to the input value and the Z component defaults to 1f.
    /// </summary>
    /// <param name="animation"></param>
    /// <param name="normalizedProgressKey"></param>
    /// <param name="value"></param>
    /// <param name="ease"></param>
    /// <returns></returns>
    public static Vector3KeyFrameAnimation AddScaleKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, float value, KeySpline ease)
    {
        animation.InsertKeyFrame(normalizedProgressKey, new Vector3(value, value, 1f), animation.Compositor.CreateCubicBezierEasingFunction(ease));
        return animation;
    }

    /// <summary>
    /// Adds a Vector3KeyFrame using the X & Y components. The Z component defaults to 0f.
    /// </summary>
    /// <param name="animation"></param>
    /// <param name="normalizedProgressKey"></param>
    /// <param name="x">X Component of the Vector3</param>
    /// <param name="y">Y Component of the Vector3</param>
    /// <param name="ease">Optional ease</param>
    /// <returns></returns>
    public static Vector3KeyFrameAnimation AddKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, float x, float y, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, new Vector3(x, y, 0f), ease);
        return animation;
    }

    public static Vector3KeyFrameAnimation AddKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, float x, float y, KeySpline ease)
    {
        animation.InsertKeyFrame(normalizedProgressKey, new Vector3(x, y, 0f), animation.Compositor.CreateCubicBezierEasingFunction(ease));
        return animation;
    }

    /// <summary>
    /// Adds a Vector3KeyFrame with X Y & Z components.
    /// </summary>
    /// <param name="animation"></param>
    /// <param name="normalizedProgressKey"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <param name="ease"></param>
    /// <returns></returns>
    public static Vector3KeyFrameAnimation AddKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, float x, float y, float z, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, new Vector3(x, y, z), ease);
        return animation;
    }

    #endregion

    public static Vector4KeyFrameAnimation AddKeyFrame(this Vector4KeyFrameAnimation animation, float normalizedProgressKey, Vector4 value, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, ease);
        return animation;
    }

    public static QuaternionKeyFrameAnimation AddKeyFrame(this QuaternionKeyFrameAnimation animation, float normalizedProgressKey, Quaternion value, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, ease);
        return animation;
    }

    public static QuaternionKeyFrameAnimation AddKeyFrame(this QuaternionKeyFrameAnimation animation, float normalizedProgressKey, Quaternion value, KeySpline ease)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, animation.Compositor.CreateCubicBezierEasingFunction(ease));
        return animation;
    }

    #endregion


    #region Compositor Create Builders

    private static T TryAddGroup<T>(CompositionObject obj, T animation) where T : CompositionAnimation
    {
        if (obj is CompositionAnimationGroup group)
            group.Add(animation);

        return animation;
    }

    public static SpringVector3NaturalMotionAnimation CreateSpringVector3Animation(
        this CompositionObject visual, string targetProperty = null)
    {
        return TryAddGroup(visual, visual.Compositor.CreateSpringVector3Animation().SetSafeTarget(targetProperty));
    }

    public static ColorKeyFrameAnimation CreateColorKeyFrameAnimation(this CompositionObject visual, string targetProperty = null)
    {
        return TryAddGroup(visual, visual.Compositor.CreateColorKeyFrameAnimation().SetSafeTarget(targetProperty));
    }

    public static ScalarKeyFrameAnimation CreateScalarKeyFrameAnimation(this CompositionObject visual, string targetProperty = null)
    {
        return TryAddGroup(visual, visual.Compositor.CreateScalarKeyFrameAnimation().SetSafeTarget(targetProperty));
    }

    public static Vector2KeyFrameAnimation CreateVector2KeyFrameAnimation(this CompositionObject visual, string targetProperty = null)
    {
        return TryAddGroup(visual, visual.Compositor.CreateVector2KeyFrameAnimation().SetSafeTarget(targetProperty));
    }

    public static Vector3KeyFrameAnimation CreateVector3KeyFrameAnimation(this CompositionObject visual, string targetProperty = null)
    {
        return TryAddGroup(visual, visual.Compositor.CreateVector3KeyFrameAnimation().SetSafeTarget(targetProperty));
    }

    public static Vector4KeyFrameAnimation CreateVector4KeyFrameAnimation(this CompositionObject visual, string targetProperty = null)
    {
        return TryAddGroup(visual, visual.Compositor.CreateVector4KeyFrameAnimation().SetSafeTarget(targetProperty));
    }

    public static QuaternionKeyFrameAnimation CreateQuaternionKeyFrameAnimation(this CompositionObject visual, string targetProperty = null)
    {
        return TryAddGroup(visual, visual.Compositor.CreateQuaternionKeyFrameAnimation().SetSafeTarget(targetProperty));
    }

    public static ExpressionAnimation CreateExpressionAnimation(this CompositionObject visual)
    {
        return TryAddGroup(visual, visual.Compositor.CreateExpressionAnimation());
    }

    public static ExpressionAnimation CreateExpressionAnimation(this CompositionObject visual, string targetProperty)
    {
        return TryAddGroup(visual, visual.Compositor.CreateExpressionAnimation().SetTarget(targetProperty));
    }

    public static CubicBezierEasingFunction CreateCubicBezierEasingFunction(this Compositor compositor, float x1, float y1, float x2, float y2)
    {
        return compositor.CreateCubicBezierEasingFunction(new Vector2(x1, y1), new Vector2(x2, y2));
    }

    public static CubicBezierEasingFunction CreateCubicBezierEasingFunction(this Compositor compositor, Windows.UI.Xaml.Media.Animation.KeySpline spline)
    {
        return compositor.CreateCubicBezierEasingFunction(spline.ControlPoint1.ToVector2(), spline.ControlPoint2.ToVector2());
    }

    public static CubicBezierEasingFunction CreateCubicBezierEasingFunction(this Compositor compositor, CubicBezierPoints points)
    {
        return compositor.CreateCubicBezierEasingFunction(points.Start, points.End);
    }

    #endregion


    #region SetExpression

    public static ExpressionAnimation SetExpression(this ExpressionAnimation animation, string expression)
    {
        if (!string.IsNullOrWhiteSpace(expression))
            animation.Expression = expression;

        return animation;
    }

    #endregion  


    #region SetParameter Builders

    public static T SetParameter<T>(this T animation, string key, UIElement parameter) where T : CompositionAnimation
    {
        if (parameter != null)
            animation.SetReferenceParameter(key, GetElementVisual(parameter));

        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, CompositionObject parameter) where T : CompositionAnimation
    {
        animation.SetReferenceParameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, float parameter) where T : CompositionAnimation
    {
        animation.SetScalarParameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, double parameter) where T : CompositionAnimation
    {
        animation.SetScalarParameter(key, (float)parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, bool parameter) where T : CompositionAnimation
    {
        animation.SetBooleanParameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, Color parameter) where T : CompositionAnimation
    {
        animation.SetColorParameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, Vector2 parameter) where T : CompositionAnimation
    {
        animation.SetVector2Parameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, Vector3 parameter) where T : CompositionAnimation
    {
        animation.SetVector3Parameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, Vector4 parameter) where T : CompositionAnimation
    {
        animation.SetVector4Parameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, Matrix3x2 parameter) where T : CompositionAnimation
    {
        animation.SetMatrix3x2Parameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, Matrix4x4 parameter) where T : CompositionAnimation
    {
        animation.SetMatrix4x4Parameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, Quaternion parameter) where T : CompositionAnimation
    {
        animation.SetQuaternionParameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, IAnimationObject parameter) where T : CompositionAnimation
    {
        animation.SetExpressionReferenceParameter(key, parameter);
        return animation;
    }

    #endregion


    #region PropertySet Builders

    /// <summary>
    /// Tries to insert a value into the CompositionPropertySet. Returns false if the value
    /// is not a supported type.
    /// </summary>
    /// <param name="set"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="success"></param>
    /// <returns></returns>
    public static CompositionPropertySet TryInsert(
        this CompositionPropertySet set, string key, object value, out bool success)
    {
        success = true;

        if (value is float f)
            set.Insert(key, f);
        else if (value is double d)
            set.Insert(key, (float)d);
        else if (value is int i)
            set.Insert(key, (float)i);
        else if (value is Point p)
            set.Insert(key, p.ToVector2());
        else if (value is Vector2 v2)
            set.Insert(key, v2);
        else if (value is Vector3 v3)
            set.Insert(key, v3);
        else if (value is Vector4 v4)
            set.Insert(key, v4);
        else if (value is Matrix3x2 m3)
            set.Insert(key, m3);
        else if (value is Matrix4x4 m4)
            set.Insert(key, m4);
        else if (value is Quaternion q)
            set.Insert(key, q);
        else if (value is bool b)
            set.Insert(key, b);
        else if (value is Color c)
            set.Insert(key, c);
        else if (value is string str && double.TryParse(str, out double dbl))
            set.Insert(key, (float)dbl);
        else
            success = false;

        return set;
    }

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, float value)
    {
        set.InsertScalar(name, value);
        return set;
    }

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, double value)
    {
        set.InsertScalar(name, (float)value);
        return set;
    }

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, bool value)
    {
        set.InsertBoolean(name, value);
        return set;
    }

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, Vector2 value)
    {
        set.InsertVector2(name, value);
        return set;
    }

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, Vector3 value)
    {
        set.InsertVector3(name, value);
        return set;
    }

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, Vector4 value)
    {
        set.InsertVector4(name, value);
        return set;
    }

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, Color value)
    {
        set.InsertColor(name, value);
        return set;
    }

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, Matrix3x2 value)
    {
        set.InsertMatrix3x2(name, value);
        return set;
    }

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, Matrix4x4 value)
    {
        set.InsertMatrix4x4(name, value);
        return set;
    }

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, Quaternion value)
    {
        set.InsertQuaternion(name, value);
        return set;
    }

    #endregion


    #region Animation Start / Stop

    public static void StartAnimation(this CompositionObject obj, ICompositionAnimationBase animation)
    {
        if (animation is null)
            return;

        if (animation is CompositionAnimationGroup group)
            obj.StartAnimationGroup(group);
        else if (animation is CompositionAnimation ani)
            obj.StartAnimation(ani);
        else
            XAMLCore.Trace($"Cannot start unsupported animation: {animation.GetType()}");
    }

    public static void StopAnimation(this CompositionObject obj, ICompositionAnimationBase animation)
    {
        if (animation is null)
            return;

        if (animation is CompositionAnimationGroup group)
            obj.StopAnimationGroup(group);
        else if (animation is CompositionAnimation ani)
            obj.StopAnimation(ani);
        else
            XAMLCore.Trace($"Cannot stop unsupported animation: {animation.GetType()}");
    }

    public static void StartAnimation(this CompositionObject compositionObject, CompositionAnimation animation)
    {
        if (string.IsNullOrWhiteSpace(animation.Target))
            throw new ArgumentNullException("Animation has no target");

        try
        {
            compositionObject.StartAnimation(animation.Target, animation);
        }
        catch (Exception ex)
        {
            XAMLCore.Trace(ex.ToString());
        }
    }

    public static void StartAnimation(this CompositionObject compositionObject, CompositionAnimationGroup animation)
    {
        compositionObject.StartAnimationGroup(animation);
    }

    public static void StopAnimation(this CompositionObject compositionObject, CompositionAnimation animation)
    {
        if (string.IsNullOrWhiteSpace(animation.Target))
            throw new ArgumentNullException("Animation has no target");

        compositionObject.StopAnimation(animation.Target);
    }

    #endregion


    #region Brushes

    public static CompositionGradientBrush AsCompositionBrush(this LinearGradientBrush brush, Compositor compositor)
    {
        var compBrush = compositor.CreateLinearGradientBrush();

        foreach (var stop in brush.GradientStops)
        {
            compBrush.ColorStops.Add(compositor.CreateColorGradientStop((float)stop.Offset, stop.Color));
        }

        // todo : try and copy transforms?

        return compBrush;
    }

    #endregion

    #region Extras

    public static CubicBezierEasingFunction CreateEase(this Compositor c, float x1, float y1, float x2, float y2)
    {
        return c.CreateCubicBezierEasingFunction(new(x1, y1), new(x2, y2));
    }

    public static CubicBezierEasingFunction CreateEntranceEasingFunction(this Compositor c)
    {
        return c.CreateCubicBezierEasingFunction(new(.1f, .9f), new(.2f, 1));
    }

    public static CubicBezierEasingFunction CreateFluentEntranceEasingFunction(this Compositor c)
    {
        return c.CreateCubicBezierEasingFunction(new(0f, 0f), new(0f, 1));
    }

    public static CompositionAnimationGroup CreateAnimationGroup(this Compositor c, params CompositionAnimation[] animations)
    {
        var group = c.CreateAnimationGroup();
        foreach (var a in animations)
            group.Add(a);
        return group;
    }


    public static bool HasImplicitAnimation<T>(this T c, string path) where T: CompositionObject
    {
        return c.ImplicitAnimations != null 
            && c.ImplicitAnimations.TryGetValue(path, out ICompositionAnimationBase v)
            && v != null;
    }

    public static T SetImplicitAnimation<T>(this T composition, string path, ICompositionAnimationBase animation)
        where T : CompositionObject
    {
        if (composition.ImplicitAnimations == null)
        {
            if (animation == null)
                return composition;

            composition.ImplicitAnimations = composition.Compositor.CreateImplicitAnimationCollection();
        }

        if (animation == null)
            composition.ImplicitAnimations.Remove(path);
        else
            composition.ImplicitAnimations[path] = animation;

        return composition;
    }

    public static FrameworkElement SetImplicitAnimation(this FrameworkElement element, string path, ICompositionAnimationBase animation)
    {
        SetImplicitAnimation(GetElementVisual(element), path, animation);
        return element;
    }

    #endregion

    #region Animations AttachedProperty

    public static XAMLAnimationCollection GetAnimations(DependencyObject obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        // Ensure there is always a collection when accessed via code
        XAMLAnimationCollection collection = (XAMLAnimationCollection)obj.GetValue(AnimationsProperty);
        if (collection == null)
        {
            collection = new ();
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
            typeof(Composition),
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

    public static bool TryInsertProperty(
        CompositionPropertySet set,
        string key,
        object value,
        CompositionParameterType type,
        bool overwrite = true)
    {
        // Attempts to parse a XAML property string to a supported type value
        

        if (type is CompositionParameterType.Scalar)
        {
            if (overwrite is false
                && set.TryGetScalar(key, out _) == CompositionGetValueStatus.Succeeded)
                return true;

            float f = float.NaN;

            if (value is string s && XAMLCore.TryParse(s, out float result))
                f = result;
            else if (value is double or int or long)
                Convert.ToSingle(value);
            else if (value is float v)
                f = v;

            if (float.IsNaN(f))
                return false;
            else
                set.Insert(key, f);

            return true;
        }
        else if (type is CompositionParameterType.Color)
        {
            if (overwrite is false
                && set.TryGetColor(key, out _) == CompositionGetValueStatus.Succeeded)
                return true;

            if (value is Color v)
            {
                set.InsertColor(key, v);
                return true;
            }
            else if (
                value is string s
                && XAMLCore.TryParse(s, out Color color))
            {
                set.InsertColor(key, color);
                return true;
            }
            else if (value is SolidColorBrush brush)
            {
                Color c = brush.Color with { A = (byte)(brush.Color.A * brush.Opacity) };
                set.InsertColor(key, c);
            }

            return false;
        }
        else if (type is CompositionParameterType.Boolean)
        {
            if (overwrite is false
                && set.TryGetBoolean(key, out _) == CompositionGetValueStatus.Succeeded)
                return true;

            if (value is bool v
                || value is string s && XAMLCore.TryParse(s, out v))
            {
                set.InsertBoolean(key, v);
                return true;
            }

            return false;
        }
        else if (type is CompositionParameterType.Vector2)
        {
            if (overwrite is false
                && set.TryGetVector2(key, out _) == CompositionGetValueStatus.Succeeded)
                return true;

            if (value is Vector2 v)
            {
                set.InsertVector2(key, v);
                return true;
            }
            else if (
                value is string s
                && XAMLCore.TryParse(s, out Point c))
            {
                set.InsertVector2(key, c.ToVector2());
                return true;
            }

            return false;
        }
        else if (type is CompositionParameterType.Vector3)
        {
            if (overwrite is false
                && set.TryGetVector3(key, out _) == CompositionGetValueStatus.Succeeded)
                return true;

            if (value is Vector3 v
                || value is string s && XAMLCore.TryParse(s, out v))
            {
                set.InsertVector3(key, v);
                return true;
            }

            return false;
        }
        else if (type is CompositionParameterType.Vector4)
        {
            if (overwrite is false
                && set.TryGetVector4(key, out _) == CompositionGetValueStatus.Succeeded)
                return true;

            if (value is Vector4 v
                || value is string s && XAMLCore.TryParse(s, out v))
            {
                set.InsertVector4(key, v);
                return true;
            }

            return false;
        }
        else if (type is CompositionParameterType.Matrix3x2)
        {
            if (overwrite is false
                && set.TryGetMatrix3x2(key, out _) == CompositionGetValueStatus.Succeeded)
                return true;

            if (value is not Matrix3x2 v)
                return false;

            set.InsertMatrix3x2(key, v);
            return true;
        }
        else if (type is CompositionParameterType.Matrix4x4)
        {
            if (overwrite is false
                && set.TryGetMatrix4x4(key, out _) == CompositionGetValueStatus.Succeeded)
                return true;

            if (value is not Matrix4x4 v)
                return false;

            set.InsertMatrix4x4(key, v);
            return true;
        }
        else if (type is CompositionParameterType.Quaternion)
        {
            if (overwrite is false
                && set.TryGetQuaternion(key, out _) == CompositionGetValueStatus.Succeeded)
                return true;

            if (value is not Quaternion v)
                return false;

            set.InsertQuaternion(key, v);
            return true;
        }
        else if (type is CompositionParameterType.Unknown)
        {
            if (value is Vector2)
                return TryInsertProperty(set, key, value, CompositionParameterType.Vector2, overwrite);
            else if (value is Point p)
                return TryInsertProperty(set, key, p.ToVector2(), CompositionParameterType.Vector2, overwrite);
            if (value is Vector3)
                return TryInsertProperty(set, key, value, CompositionParameterType.Vector3, overwrite);
            else if (value is Vector4)
                return TryInsertProperty(set, key, value, CompositionParameterType.Vector4, overwrite);
            else if (value is float f)
                return TryInsertProperty(set, key, f, CompositionParameterType.Scalar, overwrite);
            else if (value is double d)
                return TryInsertProperty(set, key, d, CompositionParameterType.Scalar, overwrite);
            else if (value is int i)
                return TryInsertProperty(set, key, i, CompositionParameterType.Scalar, overwrite);
            else if (value is bool b)
                return TryInsertProperty(set, key, b, CompositionParameterType.Boolean, overwrite);
            else if (value is Color c)
                return TryInsertProperty(set, key, c, CompositionParameterType.Color, overwrite);
            else if (value is Matrix3x2 m3)
                return TryInsertProperty(set, key, m3, CompositionParameterType.Matrix3x2, overwrite);
            else if (value is Matrix4x4 m4)
                return TryInsertProperty(set, key, m4, CompositionParameterType.Matrix4x4, overwrite);
            else if (value is Quaternion q)
                return TryInsertProperty(set, key, q, CompositionParameterType.Quaternion, overwrite);
            else if (value is string s)
            {
                if (XAMLCore.TryParse(s, out float ft))
                    return TryInsertProperty(set, key, ft, CompositionParameterType.Scalar, overwrite);
                else if (s is "TRUE" or "True" or "true")
                    return TryInsertProperty(set, key, true, CompositionParameterType.Boolean, overwrite);
                else if (s is "FALSE" or "False" or "false")
                    return TryInsertProperty(set, key, false, CompositionParameterType.Boolean, overwrite);
                else if (s.StartsWith("#")
                    && XAMLCore.TryParse(s, out Color cl))
                    return TryInsertProperty(set, key, cl, CompositionParameterType.Color, overwrite);
                else if (XAMLCore.TryParse(s, out Vector3 v3))
                    return TryInsertProperty(set, key, v3, CompositionParameterType.Vector3, overwrite);
                else if (XAMLCore.TryParse(s, out Vector4 v4))
                    return TryInsertProperty(set, key, v4, CompositionParameterType.Vector4, overwrite);
            }
        }

        XAMLCore.Trace($"Unable to insert property for key: {key}, value: {value}");
        return false;
    }
}

public enum CompositionParameterType
{
    Unknown,
    Scalar,
    Vector2,
    Vector3,
    Vector4,
    Boolean,
    Color,
    Quaternion,
    Matrix3x2,
    Matrix4x4
}

public record class CubicBezierPoints
{
    public Vector2 Start { get; }
    public Vector2 End { get; }

    public CubicBezierPoints(float x1, float y1, float x2, float y2)
    {
        Start = new(x1, y1);
        End = new(x2, y2);
    }

    public CubicBezierPoints(Vector2 start, Vector2 end)
    {
        Start = start;
        End = end;
    }






    //------------------------------------------------------
    //
    //  Fluent Splines
    //
    //------------------------------------------------------

    /* 
        These splines are taken from Microsoft's official animation documentation for 
        fluent animation design system.

        For reference recommended durations are:
            Exit Animations         : 150ms
            Entrance Animations     : 300ms
            Translation Animations  : <= 500ms
    */


    /// <summary>
    /// Analogous to Exponential EaseIn, Exponent 4.5
    /// </summary>
    public static CubicBezierPoints FluentAccelerate { get; } = new(0.07f, 0, 1, 0.5f);

    /// <summary>
    /// Analogous to Exponential EaseOut, Exponent 7
    /// </summary>
    public static CubicBezierPoints FluentDecelerate { get; } = new(0.1f, 0.9f, 0.2f, 1);

    /// <summary>
    /// Analogous to Circle EaseInOut
    /// </summary>
    public static CubicBezierPoints FluentStandard { get; } = new(0.8f, 0, 0.2f, 1);

    public static CubicBezierPoints FluentEntrance { get; } = new(0, 0, 0, 1f);
}

public interface IXamlCompositionAnimationBase
{
    void Start(object target);

    void Stop(object target);
}

public enum UIElementReferenceType
{
    /// <summary>
    /// Gets the composition handoff visual for the UIElement and uses it as the reference.
    /// </summary>
    ElementVisual,

    /// <summary>
    /// Adds the UIElements as an IAnimationObject reference.
    /// </summary>
    AnimationObject,

    /// <summary>
    /// Adds the UIElements as reference to it's own handoff visual's PropertySet
    /// </summary>
    VisualPropertySet,

    /// <summary>
    /// Adds the ScrollManipulationPropertySet of a ScrollViewer
    /// </summary>
    ScrollManipulationPropertySet,

    /// <summary>
    /// Adds the PointerPositionPropertySet of a UIElement
    /// </summary>
    PointerPositionPropertySet,
}

public enum ParameterBindingMode
{
    /// <summary>
    /// Default composition behaviour - the parameter is set at the start of the animation.
    /// </summary>
    AtStart,
    /// <summary>
    /// Updates the value by re-applying the animation on change
    /// </summary>
    Live
}

public interface IHandleableEvent
{
    bool Handled { get; set; }
}

public class ParameterUpdatedEventArgs(AnimationParameterBase parameter) 
    : EventArgs, IHandleableEvent
{
    public AnimationParameterBase Parameter { get; } = parameter;

    public bool Handled { get; set; }
}

public class AnimationUpdatedEventArgs : EventArgs, IHandleableEvent
{
    public AnimationUpdatedEventArgs(XamlCompositionAnimationBase animation)
    {
        Animation = animation;
    }

    /// <summary>
    /// If this is not null, we are requesting a STOP animation on this property
    /// </summary>
    public string OldTarget { get; set; }

    public XamlCompositionAnimationBase Animation { get; }

    public bool Handled { get; set; }
}

public interface IAnimationParameter
{
    event EventHandler<ParameterUpdatedEventArgs> Updated;

    bool IsValid { get; }
}

[DependencyProperty<string>("Key")]
[DependencyProperty<ParameterBindingMode>("BindingMode", ParameterBindingMode.AtStart)]
public abstract partial class AnimationParameterBase : DependencyObject, IEquatable<AnimationParameterBase>
{
    protected WeakReference<ICompositionAnimationBase> _animation;

    /// <summary>
    /// Fires only when BindingMode is set to Live.
    /// </summary>
    public event EventHandler<ParameterUpdatedEventArgs> Updated;

    public bool IsValid { get; private set; }

    public void AttachTo(ICompositionAnimationBase ani)
    {
        _animation = new(ani);
        Update();
    }

    public void Detach()
    {
        // Called when removed from - clear out the value we set
        if (_animation?.TryGetTarget(out ICompositionAnimationBase animation) is true)
        {
            if (!string.IsNullOrWhiteSpace(Key))
                animation.ClearParameter(Key);
        }

        _animation = null;
    }

    partial void OnKeyChanged(string o, string n)
    {
        if (_animation?.TryGetTarget(out ICompositionAnimationBase animation) is true)
        {
            if (!string.IsNullOrWhiteSpace(o))
                animation.ClearParameter(o);

            if (!string.IsNullOrWhiteSpace(n))
                Update();
        }
    }

    public abstract DependencyProperty GetValueProperty();

    protected void Update()
    {
        if (CheckIsValid() is false)
        {
            IsValid = false;
            return;
        }

        if (_animation?.TryGetTarget(out ICompositionAnimationBase animation) is true)
        {
            UpdateInternal(animation);

            // If the binding mode is live, we need to update the parameter when it changes
            if (BindingMode == ParameterBindingMode.Live || IsValid == false)
            {
                IsValid = true;
                Updated?.Invoke(this, new(this));
            }
        }
    }

    protected virtual bool CheckIsValid()
    {
        if (_animation is null
            || ReadLocalValue(KeyProperty) == DependencyProperty.UnsetValue
            || ReadLocalValue(GetValueProperty()) == DependencyProperty.UnsetValue
            || GetValue(GetValueProperty()) == null)
            return false;

        return true;
    }

    protected abstract void UpdateInternal(ICompositionAnimationBase animation);


    #region Comparison

    public override bool Equals(object obj)
    {
        return Equals(obj as AnimationParameterBase);
    }

    public virtual bool Equals(AnimationParameterBase other)
    {
        return other is not null &&
               Key == other.Key &&
               EqualityComparer<object>.Default.Equals(GetValue(GetValueProperty()), other.GetValue(other.GetValueProperty()));
    }

    public override int GetHashCode()
    {
        int hashCode = -70591176;
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Key);
        hashCode = hashCode * -1521134295 + EqualityComparer<object>.Default.GetHashCode(GetValue(GetValueProperty()));
        return hashCode;
    }

    public static bool operator ==(AnimationParameterBase left, AnimationParameterBase right)
    {
        return EqualityComparer<AnimationParameterBase>.Default.Equals(left, right);
    }

    public static bool operator !=(AnimationParameterBase left, AnimationParameterBase right)
    {
        return !(left == right);
    }

    #endregion

}

[DependencyProperty<double>("Value", 0d, "Update")]
public partial class DoubleParameter : AnimationParameterBase
{
    public override DependencyProperty GetValueProperty() => ValueProperty;

    protected override bool CheckIsValid() => base.CheckIsValid() && double.IsNaN(Value) is false;

    protected override void UpdateInternal(ICompositionAnimationBase animation)
    {
        animation.SetScalarParameter(Key, (float)Value);
    }
}

[DependencyProperty<UIElement>("Element", default, "Update")]
public partial class ElementVisualParameter : AnimationParameterBase
{
    public override DependencyProperty GetValueProperty() => ElementProperty;

    protected override void UpdateInternal(ICompositionAnimationBase animation)
    {
        animation.SetReferenceParameter(Key, Element.GetElementVisual());
    }
}

[DependencyProperty<UIElement>("Element", default, "Update")]
public partial class ElementVisualPropertySetParameter : AnimationParameterBase
{
    // TODO : Support DefaultValues collection

    public override DependencyProperty GetValueProperty() => ElementProperty;

    protected override void UpdateInternal(ICompositionAnimationBase animation)
    {
        animation.SetReferenceParameter(Key, Element.GetElementVisual().Properties);
    }
}

[DependencyProperty<UIElement>("Element", default, "Update")]
public partial class PointerSetParameter : AnimationParameterBase
{
    public override DependencyProperty GetValueProperty() => ElementProperty;

    protected override void UpdateInternal(ICompositionAnimationBase animation)
    {
        animation.SetReferenceParameter(Key, ElementCompositionPreview.GetPointerPositionPropertySet(Element));
    }
}

[DependencyProperty<UIElement>("Element", default, "Update")]
public partial class ScrollManipulationSetParameter : AnimationParameterBase
{
    public override DependencyProperty GetValueProperty() => ElementProperty;

    protected override bool CheckIsValid() => base.CheckIsValid() && Element is ScrollViewer;

    protected override void UpdateInternal(ICompositionAnimationBase animation)
    {
        // TODO: At some point we can support automatically going through the element's visual tree
        // to find the first ScrollViewer, allowing us to easily support ListView/GridView, etc.

        animation.SetReferenceParameter(Key, 
            ElementCompositionPreview.GetScrollViewerManipulationPropertySet((ScrollViewer)Element));
    }
}

[DependencyProperty<object>("Value", default, "Update")]
[DependencyProperty<UIElementReferenceType>("UIElementReferenceType", UIElementReferenceType.ElementVisual, nameof(Update))]
public partial class AnimationParameter : AnimationParameterBase
{
    public override DependencyProperty GetValueProperty() => ValueProperty;

    protected override void UpdateInternal(ICompositionAnimationBase animation)
    {
        if (Value is float f)
            animation.SetScalarParameter(Key, f);
        else if (Value is double d)
            animation.SetScalarParameter(Key, (float)d);
        else if (Value is int i)
            animation.SetScalarParameter(Key, (float)i);
        else if (Value is Point p)
            animation.SetVector2Parameter(Key, p.ToVector2());
        else if (Value is Vector2 v2)
            animation.SetVector2Parameter(Key, v2);
        else if (Value is Vector3 v3)
            animation.SetVector3Parameter(Key, v3);
        else if (Value is Vector4 v4)
            animation.SetVector4Parameter(Key, v4);
        else if (Value is Matrix3x2 m3)
            animation.SetMatrix3x2Parameter(Key, m3);
        else if (Value is Matrix4x4 m4)
            animation.SetMatrix4x4Parameter(Key, m4);
        else if (Value is Quaternion q)
            animation.SetQuaternionParameter(Key, q);
        else if (Value is bool b)
            animation.SetBooleanParameter(Key, b);
        else if (Value is Color c)
            animation.SetColorParameter(Key, c);
        else if (Value is CompositionObject compObj)
            animation.SetReferenceParameter(Key, compObj);
        else if (Value is IAnimationObject ani && UIElementReferenceType == UIElementReferenceType.AnimationObject)
            animation.SetExpressionReferenceParameter(Key, ani);
        else if (Value is UIElement element && UIElementReferenceType == UIElementReferenceType.ElementVisual)
            animation.SetReferenceParameter(Key, ElementCompositionPreview.GetElementVisual(element));
        else if (Value is UIElement e2 && UIElementReferenceType == UIElementReferenceType.PointerPositionPropertySet)
            animation.SetReferenceParameter(Key, ElementCompositionPreview.GetPointerPositionPropertySet(e2));
        else if (Value is UIElement e3 && UIElementReferenceType == UIElementReferenceType.VisualPropertySet)
        {
            var props = ElementCompositionPreview.GetElementVisual(e3).Properties;
            animation.SetReferenceParameter(Key, props);
        }
        else if (Value is ScrollViewer sv && UIElementReferenceType == UIElementReferenceType.ScrollManipulationPropertySet)
            animation.SetReferenceParameter(Key, sv.GetScrollManipulationPropertySet());
        else if (Value is string s && double.TryParse(s, out double ds))
            animation.SetScalarParameter(Key, (float)ds);
        else
        {
            if (Value is not null)
                XAMLCore.Trace($"AnimationParameter: could not handle for {Value.GetType()}");
        }
    }

    public override int GetHashCode()
    {
        return base.GetHashCode() * -1521134295 + UIElementReferenceType.GetHashCode();
    }

    public override bool Equals(AnimationParameterBase other)
    {
        return base.Equals(other)
             && other is AnimationParameter o
             && EqualityComparer<UIElementReferenceType>.Default.Equals(o.UIElementReferenceType, this.UIElementReferenceType);
    }
}

public enum AnimationTarget
{
    CompositionObject,
    CompositionObjectProperties
}

public interface ICompositionAnimationTarget
{
    CompositionObject GetCompositionAnimationTarget();
}

[ContentProperty(Name = nameof(Parameters))]
[DependencyProperty<string>("Target")]
[DependencyProperty<AnimationTarget>("AnimationTarget")]
public partial class XamlCompositionAnimationBase : DependencyObject, IXamlCompositionAnimationBase
{
    public AnimationParameterCollection Parameters
    {
        get {
            if (GetValue(ParametersProperty) is null)
                SetValue(ParametersProperty, new AnimationParameterCollection());
            return (AnimationParameterCollection)GetValue(ParametersProperty); }
        private set { SetValue(ParametersProperty, value); }
    }

    public static readonly DependencyProperty ParametersProperty =
        DependencyProperty.Register(nameof(Parameters), typeof(AnimationParameterCollection), typeof(XamlCompositionAnimationBase), new PropertyMetadata(null, (d,e) =>
        {
            if (d is XamlCompositionAnimationBase x)
            {
                if (e.OldValue is AnimationParameterCollection old)
                    x.ClearCollection(old);

                if (e.NewValue is AnimationParameterCollection c)
                    x.SetCollection(c);
            }
        }));


    public event EventHandler<ParameterUpdatedEventArgs> ParameterBindingUpdated;


    public event EventHandler<AnimationUpdatedEventArgs> AnimationUpdated;

    protected Compositor Compositor => Window.Current.Compositor;

    /// <summary>
    /// The underlying CompositionAnimation used
    /// </summary>
    public ICompositionAnimationBase Animation { get; protected set; }

    protected virtual DependencyProperty GetDefaultValueProperty() => null;

    protected virtual CompositionParameterType DefaultValueType => CompositionParameterType.Unknown;

    protected void FireAnimationUpdated(string old = null)
    {
        AnimationUpdated?.Invoke(this, new (this) {  OldTarget = null });
    }

    protected void SetAnimation(CompositionAnimation animation)
    {
        Animation = animation;

        // Detaches parameters from any previous animation and attaches them to the new one
        Parameters.SetTarget(animation);

        // Set properties
        OnTargetChanged(null, Target);
    }



    private void SetCollection(AnimationParameterCollection c)
    {
        c.BindingUpdated -= ParameterBinding_Updated;
        c.BindingUpdated += ParameterBinding_Updated;
        c.SetTarget(this.Animation);
    }

    private void ClearCollection(AnimationParameterCollection old)
    {
        old.BindingUpdated -= ParameterBinding_Updated;
        old.SetTarget(null);
    }

    private void ParameterBinding_Updated(object sender, ParameterUpdatedEventArgs e)
    {
        if (e.Handled is false)
            this.ParameterBindingUpdated?.Invoke(this, e);
    }




    /* Animation Properties */

    partial void OnTargetChanged(string o, string n)
    {
        // Trigger parent collection to stop the old animation
        if (string.IsNullOrWhiteSpace(o) is false)
            FireAnimationUpdated(o);

        // Set the new target
        Animation.SetSafeTarget(n);

        // Trigger parent collection to start the new animation
        FireAnimationUpdated();
    }




    /* Animation Control */

    static PropertyInfo _lightProperty = null;
    static PropertyInfo _brushProperty = null;

    protected CompositionObject GetAnimationObject(object o)
    {
        if (o is ICompositionAnimationTarget i)
            return i.GetCompositionAnimationTarget();

        if (o is UIElement u)
        {
            if (this.Target == CompositionFactory.TRANSLATION)
                u.EnableCompositionTranslation();

            return u.GetElementVisual();
        }

        if (o is XamlLight light)
        {
            _lightProperty ??= light.GetType().GetProperty("CompositionLight",
                BindingFlags.NonPublic | BindingFlags.Instance);

            return _lightProperty.GetValue(light) as CompositionLight;
        }
        
        if (o is XamlCompositionBrushBase brush)
        {
            _brushProperty ??= brush.GetType().GetProperty("CompositionBrush",
                BindingFlags.NonPublic | BindingFlags.Instance);

            return _brushProperty.GetValue(brush) as CompositionBrush;
        }

        return null;
    }

    public virtual void Start(object target)
    {
        if (GetAnimationObject(target) is not CompositionObject obj)
            return;

        // Cannot play if parameters are not set
        if (Parameters
                .OfType<AnimationParameterBase>()
                .Any(p => p.IsValid is false || p.GetValue(p.GetValueProperty()) is null))
            return;

        // Cannot play blank target
        if (Animation is CompositionAnimation ca && string.IsNullOrWhiteSpace(ca.Target))
            return;

        if (AnimationTarget == AnimationTarget.CompositionObjectProperties)
        {
            /* 
             * If the value has been set in XAML it's more than likely
             * been set as a string, because DefaultValue's type is object
             * and XAML doesn't know what else to do. So even if you set
             * DefaultValue="0" in XAML, it will not be read as a float/double/int,
             * but as a string with the value "0". 
             * Hence we need to send it through this method to try and parse an
             * appropriate value for it
             */

            if (Animation is CompositionAnimation cat
                && GetDefaultValueProperty() is DependencyProperty dp
                && GetValue(dp) is object defaultValue)
                Composition.TryInsertProperty(
                    obj.Properties,
                    cat.Target,
                    defaultValue,
                    DefaultValueType,
                    false);

            obj.Properties.StartAnimation(Animation);
        }
        else if (AnimationTarget == AnimationTarget.CompositionObject)
        {
            obj.StartAnimation(Animation);
        }
    }

    public virtual void Stop(object target)
    {
        if (GetAnimationObject(target) is not CompositionObject o)
            return;

        if (AnimationTarget == AnimationTarget.CompositionObject)
            o.StopAnimation(Animation);
        else if (AnimationTarget == AnimationTarget.CompositionObjectProperties)
            o.Properties.StopAnimation(Animation);
    }
}

public partial class XAMLExpressionAnimationBase : XamlCompositionAnimationBase
{
    protected ExpressionAnimation CreateAnimation(string expression)
    {
        return Compositor.CreateExpressionAnimation().SetExpression(expression);
    }
}

[DependencyProperty<string>("Expression")]
[DependencyProperty<object>("DefaultValue")]
public partial class XAMLExpressionAnimation : XAMLExpressionAnimationBase
{
    public XAMLExpressionAnimation()
    {
        SetAnimation(CreateAnimation(Fix(Expression)));
    }

    protected override DependencyProperty GetDefaultValueProperty() => DefaultValueProperty;

    string Fix(string s)
    {
        return s?.Replace("'", "\"");
    }

    partial void OnExpressionChanged(string o, string n)
    {
        if (Animation is ExpressionAnimation e)
        {
            e.SetExpression(Fix(n));
            base.FireAnimationUpdated();
        }
    }
}


[DependencyProperty<double>("X", 0.5d)] // Relative center point
[DependencyProperty<double>("Y", 0.5d)] // Relative center point
[DependencyProperty<double>("Z", 0d)] // Absolute center point
public partial class CenterPointExpressionAnimation : XamlCompositionAnimationBase
{
    CompositionPropertySet _set;

    const string EXPRESSION = "Vector3(this.Target.Size.X * props.X, this.Target.Size.Y * props.Y, props.Z)";

    public CenterPointExpressionAnimation()
    {
        SetAnimation(CreateAnimation(EXPRESSION));
    }

    protected ExpressionAnimation CreateAnimation(string expression)
    {
        _set = Compositor.CreatePropertySet();
        _set.Insert("X", X);
        _set.Insert("Y", Y);
        _set.Insert("Z", Z);

        return Compositor.CreateExpressionAnimation()
            .SetExpression(expression)
            .SetTarget(nameof(Visual.CenterPoint))
            .SetParameter("props", _set);
    }

    partial void OnXChanged(double o, double n) => _set?.Insert("X", n);

    partial void OnYChanged(double o, double n) => _set?.Insert("Y", n);

    partial void OnZChanged(double o, double n) => _set?.Insert("Z", n);
}

public sealed partial class XAMLAnimationCollection : DependencyObjectCollection
{
    // After a VectorChanged event we need to compare the current state of the collection
    // with the old collection so that we can call Detach on all removed items.
    private List<IXamlCompositionAnimationBase> _oldCollection { get; } = new ();

    private List<WeakReference<DependencyObject>> _associated { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="XAMLAnimationCollection"/> class.
    /// </summary>
    public XAMLAnimationCollection()
    {
        this.VectorChanged += this.OnVectorChanged;
    }

    void Trim()
    {
        // Remove any dead WeakReferences from the associated list
        for (int i = _associated.Count - 1; i >= 0; i--)
            if (!_associated[i].TryGetTarget(out DependencyObject target) || target is null)
                _associated.RemoveAt(i);
    }

    /// <summary>
    /// Attaches the collection of animations to the specified <see cref="DependencyObject"/>.
    /// </summary>
    /// <param name="associatedObject">The <see cref="DependencyObject"/> to which to attach.</param>
    /// </exception>
    public void Attach(DependencyObject associatedObject)
    {
        Trim();

        // Check if we're loaded first, otherwise x:Bind will not have run yet
        if (associatedObject is not XamlLight 
            && VisualTreeHelper.GetParent(associatedObject) is null)
            return;

        if (associatedObject is FrameworkElement { IsLoaded: false })
            return;

        // Do not attach if the object is already associated
        if (_associated.Any(x => x.TryGetTarget(out DependencyObject target) && target == associatedObject))
            return;

        // Store a WeakReference to the associated object
        _associated.Add(new WeakReference<DependencyObject>(associatedObject));

        foreach (DependencyObject item in this)
        {
            IXamlCompositionAnimationBase animation = (IXamlCompositionAnimationBase)item;
            animation.Start(associatedObject);
        }
    }

    /// <summary>
    /// Detaches the collection of animations 
    /// </summary>
    public void Detach(DependencyObject associatedObject)
    {
        // Remove the WeakReference
        if (_associated.FirstOrDefault(f => f.TryGetTarget(out DependencyObject target) && target == associatedObject) 
            is { } weakReference)
            _associated.Remove(weakReference);

        // Stop all animations associated with the object
        foreach (DependencyObject item in this)
        {
            IXamlCompositionAnimationBase animation = (IXamlCompositionAnimationBase)item;
            animation.Stop(associatedObject);
        }

        // Trim the associated list to remove any dead references
        Trim();
    }

    private void OnVectorChanged(IObservableVector<DependencyObject> sender, IVectorChangedEventArgs eventArgs)
    {
        Trim();

        var associated = _associated
               .Select(x => x.TryGetTarget(out DependencyObject target) ? target : null)
               .Where(x => x != null)
               .ToList();

        if (eventArgs.CollectionChange == CollectionChange.Reset)
        {
            // Stop all existing animations
            foreach (var item in associated)
            {
                foreach (IXamlCompositionAnimationBase behavior in this._oldCollection)
                    behavior.Stop(item);
            }

            this._oldCollection.Clear();

            foreach (var item in associated)
            {
                foreach (IXamlCompositionAnimationBase behavior in this)
                    behavior.Start(item);
            }

            foreach (IXamlCompositionAnimationBase newItem in this.OfType<IXamlCompositionAnimationBase>())
            {
                this._oldCollection.Add(newItem);
            }

            return;
        }

        int eventIndex = (int)eventArgs.Index;
        DependencyObject changedItem = this[eventIndex];

        switch (eventArgs.CollectionChange)
        {
            case CollectionChange.ItemInserted:

                this._oldCollection.Insert(eventIndex, changedItem as IXamlCompositionAnimationBase);

                if (changedItem is XamlCompositionAnimationBase x)
                {
                    x.AnimationUpdated -= Child_AnimationUpdated;
                    x.AnimationUpdated += Child_AnimationUpdated;

                    x.ParameterBindingUpdated -= Child_ParameterBindingUpdated;
                    x.ParameterBindingUpdated += Child_ParameterBindingUpdated;
                }

                foreach (var a in associated)
                    this.VerifiedAttach(changedItem, a);

                break;

            case CollectionChange.ItemChanged:
                IXamlCompositionAnimationBase oldItem = this._oldCollection[eventIndex];

                foreach (var a in associated)
                    oldItem.Stop(a);

                Detach(oldItem);

                this._oldCollection[eventIndex] = changedItem as IXamlCompositionAnimationBase;

                foreach (var a in associated)
                    this.VerifiedAttach(changedItem, a);

                break;

            case CollectionChange.ItemRemoved:
                oldItem = this._oldCollection[eventIndex];

                foreach (var a in associated)
                    oldItem.Stop(a);

                this._oldCollection.RemoveAt(eventIndex);
                Detach(oldItem);
                break;

            default:
                Debug.Assert(false, "Unsupported collection operation attempted.");
                break;
        }
    }


    private IXamlCompositionAnimationBase VerifiedAttach(DependencyObject item, DependencyObject associated)
    {
        IXamlCompositionAnimationBase animation = item as IXamlCompositionAnimationBase;
        if (animation == null)
        {
            throw new InvalidOperationException("NonAnimationAddedToAnimationCollection");
        }

        //if (this._oldCollection.Contains(animation))
        //{
        //    throw new InvalidOperationException("DuplicateAnimationInCollection");
        //}

        if (associated != null)
            animation.Start(associated);

        return animation;
    }

    void Detach(IXamlCompositionAnimationBase i)
    {
        if (i is XamlCompositionAnimationBase x)
        {
            x.AnimationUpdated -= Child_AnimationUpdated;
            x.ParameterBindingUpdated -= Child_ParameterBindingUpdated;
        }
    }




    /* Respond to changes from child animations */

    void Child_ParameterBindingUpdated(object sender, ParameterUpdatedEventArgs e)
    {
        Handle(sender, e);
    }

    void Child_AnimationUpdated(object sender, AnimationUpdatedEventArgs e)
    {
        Handle(sender, e);
    }

    void Handle(object sender, IHandleableEvent e)
    {
        if (sender is XamlCompositionAnimationBase animation
            && e.Handled is false)
        {
            e.Handled = true;

            var associated = _associated
             .Select(x => x.TryGetTarget(out DependencyObject target) ? target : null)
             .Where(x => x != null)
             //.OfType<DependencyObject>()
             .ToList();

            AnimationUpdatedEventArgs ae = e as AnimationUpdatedEventArgs;

            foreach (var item in associated)
            {
                if (ae is not null && string.IsNullOrWhiteSpace(ae.OldTarget) is false)
                {
                    animation.Stop(item);
                    continue;
                }

                // Skip if the item is not in the visual tree
                if (item is not XamlLight  
                    && VisualTreeHelper.GetParent(item) is null)
                    continue; 

                // Skip if item is not loaded, as x:Bind may not have run
                if (item is FrameworkElement { IsLoaded: false })
                    return;

                //Debug.WriteLine($"Changed Trigger {animation.Target}");
                animation.Start(item);
            }
        }
    }
}

public sealed partial class AnimationParameterCollection : DependencyObjectCollection
{
    /// <summary>
    /// Fires when a child parameter value is updated and has a live bindingmode
    /// </summary>
    public event EventHandler<ParameterUpdatedEventArgs> BindingUpdated;

    // After a VectorChanged event we need to compare the current state of the collection
    // with the old collection so that we can call Detach on all removed items.
    private List<AnimationParameterBase> _oldCollection { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimationReferenceCollection"/> class.
    /// </summary>
    public AnimationParameterCollection()
    {
        this.VectorChanged += this.InternalVectorChanged;
    }

    public ICompositionAnimationBase Target { get; private set; }

    public void SetTarget(ICompositionAnimationBase target)
    {
        if (this.Target == target)
            return;

        // Detach old parameters
        if (this.Target is not null)
            foreach (AnimationParameterBase item in this._oldCollection)
                item.Detach();

        this.Target = target;

        // Attach new parameters
        foreach (AnimationParameterBase item in this)
            item.AttachTo(this.Target);

        this._oldCollection.Clear();
        this._oldCollection.AddRange(this.Cast<AnimationParameterBase>());
    }


    private void InternalVectorChanged(IObservableVector<DependencyObject> sender, IVectorChangedEventArgs eventArgs)
    {
        if (eventArgs.CollectionChange == CollectionChange.Reset)
        {
            // Stop all existing animations

            foreach (AnimationParameterBase a in this._oldCollection)
                a.Detach();

            this._oldCollection.Clear();

            foreach (AnimationParameterBase a in this)
            {
                a.AttachTo(Target);
                this._oldCollection.Add(a);
            }

            return;
        }

        int eventIndex = (int)eventArgs.Index;
        AnimationParameterBase changedItem = this[eventIndex] as AnimationParameterBase;

        switch (eventArgs.CollectionChange)
        {
            case CollectionChange.ItemInserted:

                this._oldCollection.Insert(eventIndex, changedItem);
                this.VerifiedAttach(changedItem);
                changedItem.AttachTo(Target);

                break;

            case CollectionChange.ItemChanged:
                AnimationParameterBase oldItem = this._oldCollection[eventIndex];

                oldItem.Detach();
                this._oldCollection[eventIndex] = changedItem;
                this.VerifiedAttach(changedItem);

                break;

            case CollectionChange.ItemRemoved:
                oldItem = this._oldCollection[eventIndex];
                oldItem.Detach();

                this._oldCollection.RemoveAt(eventIndex);
                break;

            default:
                Debug.Assert(false, "Unsupported collection operation attempted.");
                break;
        }
    }

    private AnimationParameterBase VerifiedAttach(DependencyObject item)
    {
        AnimationParameterBase animation = item as AnimationParameterBase;
        if (animation == null)
        {
            throw new InvalidOperationException("NonAnimationParameterAddedToAnimationParameterCollection");
        }

        animation.Updated -= Item_Updated;
        animation.Updated += Item_Updated;

        //if (this._oldCollection.Contains(animation))
        //{
        //    throw new InvalidOperationException("DuplicateAnimationParameterInCollection");
        //}

        return animation;
    }

    void Detach(AnimationParameterBase item)
    {
        item.Detach();
        item.Updated -= Item_Updated;
    }

    void Item_Updated(object sender, ParameterUpdatedEventArgs e)
    {
        this.BindingUpdated?.Invoke(this, e);
    }
}
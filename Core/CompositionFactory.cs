using System.Globalization;
using Windows.UI.Composition;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace XAMLComposition.Core;

[Bindable]
[AttachedProperty<double>("BounceDuration", 0.15)]
[AttachedProperty<bool>("EnableDepthMatrix")]
public partial class CompositionFactory : DependencyObject
{
    public const double DefaultOffsetDuration = 0.325;

    public static bool AnimationEnabled { get; set; }

    public static UISettings UISettings { get; }

    private static string CENTRE_EXPRESSION =>
        $"({nameof(Vector3)}(this.Target.{nameof(Visual.Size)}.{nameof(Vector2.X)} * {{0}}f, " +
        $"this.Target.{nameof(Visual.Size)}.{nameof(Vector2.Y)} * {{1}}f, 0f))";

    public const string TRANSLATION = "Translation";
    public const string STARTING_VALUE = "this.StartingValue";
    public const string FINAL_VALUE = "this.FinalValue";
    public const int DEFAULT_STAGGER_MS = 83;

    #region Attached Properties

    static partial void OnBounceDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement f)
        {
            Visual v = f.GetElementVisual();
            if (e.NewValue is double w && w > 0)
            {
                CompositionFactory.EnableStandardTranslation(v, w);
            }
            else
            {
                v.Properties.SetImplicitAnimation(CompositionFactory.TRANSLATION, null);
            }
        }
    }

    public static bool GetEnableBounceScale(DependencyObject obj)
    {
        return (bool)obj.GetValue(EnableBounceScaleProperty);
    }

    public static void SetEnableBounceScale(DependencyObject obj, bool value)
    {
        obj.SetValue(EnableBounceScaleProperty, value);
    }

    public static readonly DependencyProperty EnableBounceScaleProperty =
        DependencyProperty.RegisterAttached("EnableBounceScale", typeof(bool), typeof(CompositionFactory), new PropertyMetadata(false, (d, e) =>
        {
            if (d is FrameworkElement f)
            {
                Visual v = f.GetElementVisual();
                if (e.NewValue is bool b && b)
                {
                    CompositionFactory.EnableStandardTranslation(v, 0.15);
                }
                else
                {
                    v.Properties.SetImplicitAnimation(CompositionFactory.TRANSLATION, null);
                }
            }
        }));

    public static Duration GetOpacityDuration(DependencyObject obj)
    {
        return (Duration)obj.GetValue(OpacityDurationProperty);
    }

    public static void SetOpacityDuration(DependencyObject obj, Duration value)
    {
        obj.SetValue(OpacityDurationProperty, value);
    }

    public static readonly DependencyProperty OpacityDurationProperty =
        DependencyProperty.RegisterAttached("OpacityDuration", typeof(Duration), typeof(CompositionFactory), new PropertyMetadata(new Duration(TimeSpan.FromSeconds(0)), (d, e) =>
        {
            if (d is FrameworkElement element && e.NewValue is Duration t)
            {
                SetOpacityTransition(element, t.HasTimeSpan ? t.TimeSpan : TimeSpan.Zero);
            }
        }));

    public static double GetCornerRadius(DependencyObject obj)
    {
        return (double)obj.GetValue(CornerRadiusProperty);
    }

    public static void SetCornerRadius(DependencyObject obj, double value)
    {
        obj.SetValue(CornerRadiusProperty, value);
    }

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.RegisterAttached("CornerRadius", typeof(double), typeof(CompositionFactory), new PropertyMetadata(0d, (d, e) =>
        {
            if (d is FrameworkElement element && e.NewValue is double v)
            {
                SetCornerRadius(element, (float)v);
            }
        }));

    #endregion


    //------------------------------------------------------
    //
    //  Expression Animation : PERSPECTIVE
    //
    //------------------------------------------------------

    #region Perspective Expression

    static partial void OnEnableDepthMatrixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement u && e.NewValue is bool b)
        {
            if (b)
                EnableAutoPerspectiveMatrix(u);
            else
                u.GetElementVisual().StopAnimation(nameof(Visual.TransformMatrix));
        }
    }

    // Creates a basic 4x4 Matrix for use in perspective matrix multiplication. 
    // Depth of the matrix is automatically bound to the width of the visual.
    static string PERSPECTIVE_AUTO_DEPTH_MATRIX { get; } =
        $"Matrix4x4.CreateTranslation(-this.Target.Size.X /2f, -this.Target.Size.Y /2f, 0f) " +
        $"* Matrix4x4(1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, -1.0f / this.Target.Size.X, 0.0f, 0.0f, 0.0f, 1.0f) " +
        $"* Matrix4x4.CreateTranslation(this.Target.Size.X /2f, this.Target.Size.Y /2f, 0f)";


    // Creates a basic 4x4 Matrix for use in perspective matrix multiplication.
    // To match the default matrix created by XAML PerspectiveTransform3D, set depth to 1000f
    static string PERSPECTIVE_DEPTH_MATRIX(float depth) =>
        $"Matrix4x4.CreateTranslation(-this.Target.Size.X /2f, -this.Target.Size.Y /2f, 0f) " +
        $"* Matrix4x4(1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, -1.0f / {depth}f, 0.0f, 0.0f, 0.0f, 1.0f)" +
        $"* Matrix4x4.CreateTranslation(this.Target.Size.X /2f, this.Target.Size.Y /2f, 0f)";

    /// <summary>
    /// Creates a perspective matrix animation whose depth matches that of the Target
    /// Visual's width.
    /// </summary>
    /// <param name="visual"></param>
    /// <returns></returns>
    public static ExpressionAnimation CreateAutoPerspectiveAnimation(Visual visual)
    {
        return visual.Compositor.GetCached("__AUTOPER", 
            c => c.CreateExpressionAnimation()
                .SetExpression(PERSPECTIVE_AUTO_DEPTH_MATRIX)
                .SetTarget(nameof(Visual.TransformMatrix)));
    }

    /// <summary>
    /// Creates a perspective matrix animation with the specified depth.
    /// </summary>
    /// <param name="visual"></param>
    /// <param name="depth"></param>
    /// <returns></returns>
    public static ExpressionAnimation CreatePerspectiveAnimation(Visual visual, float depth = 1000f)
    {
        return visual.Compositor.CreateExpressionAnimation()
            .SetExpression(PERSPECTIVE_DEPTH_MATRIX(depth))
            .SetTarget(nameof(Visual.TransformMatrix));
    }

    /// <summary>
    /// Applies a perspective transform matrix allowing CompositeTransform3D style 
    /// faux 3D rotation with a depth that is bound to the width of the object.
    /// </summary>
    /// <param name="visual"></param>
    /// <returns></returns>
    public static CompositionAnimation EnableAutoPerspectiveMatrix(Visual visual)
    {
        var ani = CreateAutoPerspectiveAnimation(visual);
        visual.StartAnimation(ani);
        return ani;
    }

    public static CompositionAnimation EnableAutoPerspectiveMatrix(UIElement element)
    {
        var visual = element.GetElementVisual();
        var ani = CreateAutoPerspectiveAnimation(visual);
        visual.StartAnimation(ani);
        return ani;
    }

    /// <summary>
    /// Applies a perspective transform matrix allowing CompositeTransform3D style 
    /// faux 3D rotation. The default depth makes this comparable to the default
    /// matrix created by <see cref="PerspectiveTransform3D"/>
    /// </summary>
    /// <param name="visual"></param>
    /// <param name="depth">Depth of the animation. 1000 is the default. Should be greater than 0.</param>
    /// <returns></returns>
    public static CompositionAnimation EnablePerspectiveMatrix(Visual visual, float depth = 1000f)
    {
        var ani = CreatePerspectiveAnimation(visual, depth);
        visual.StartAnimation(ani);
        return ani;
    }

    #endregion








    static CompositionFactory()
    {
        UISettings = new UISettings();
    }

    public static ImplicitAnimationCollection GetRepositionCollection(Compositor c)
    {
        return c.GetCached("RepoColl", cc =>
        {
            var g = cc.CreateAnimationGroup();
            g.Add(cc.CreateVector3KeyFrameAnimation()
                        .SetTarget(nameof(Visual.Offset))
                        .AddKeyFrame(1f, FINAL_VALUE)
                        .SetDuration(CompositionFactory.DefaultOffsetDuration));

            var s = cc.CreateImplicitAnimationCollection();
            s.Add(nameof(Visual.Offset), g);
            return s;
        });
    }

    public static ICompositionAnimationBase CreateScaleAnimation(Compositor c1)
    {
        return c1.GetCached("ScaleAni", c =>
        {
            return c.CreateVector3KeyFrameAnimation()
                .AddKeyFrame(1f, FINAL_VALUE)
                .SetDuration(CompositionFactory.DefaultOffsetDuration)
                .SetTarget(nameof(Visual.Scale));
        });
    }

    public static void PokeUIElementZIndex(UIElement e, XamlDirect xamlDirect = null)
    {
        if (xamlDirect != null)
        {
            var o = xamlDirect.GetXamlDirectObject(e);
            var i = xamlDirect.GetInt32Property(o, XamlPropertyIndex.Canvas_ZIndex);
            xamlDirect.SetInt32Property(o, XamlPropertyIndex.Canvas_ZIndex, i + 1);
            xamlDirect.SetInt32Property(o, XamlPropertyIndex.Canvas_ZIndex, i);
        }
        else
        {
            var index = Canvas.GetZIndex(e);
            Canvas.SetZIndex(e, index + 1);
            Canvas.SetZIndex(e, index);
        }
    }

    private static void SetOpacityTransition(FrameworkElement e, TimeSpan t)
    {
        if (!UISettings.AnimationsEnabled)
            return;

        if (t.TotalMilliseconds > 0)
        {
            var v = e.GetElementVisual();
            var ani = v.CreateScalarKeyFrameAnimation(nameof(Visual.Opacity))
                .AddKeyFrame(1, FINAL_VALUE, v.Compositor.GetLinearEase())
                .SetDuration(t);

            e.SetImplicitAnimation(nameof(Visual.Opacity), ani);
        }
        else
        {
            e.SetImplicitAnimation(nameof(Visual.Opacity), null);
        }
    }

    public static void SetupOverlayPanelAnimation(UIElement e)
    {
        if (!UISettings.AnimationsEnabled)
            return;

        Visual v = e.EnableTranslation(true).GetElementVisual();


        //if (ResourceHelper.IsMaterialTheme)
        //{
        //    var g = v.GetCached("MTOPH", () =>
        //    {
        //        var t = v.CreateVector3KeyFrameAnimation("Scale")
        //                .AddKeyFrame(1, new Vector3(0.8f, 0.8f, 1), CubicBezierPoints.FluentAccelerate)
        //                .SetDuration(0.2);

        //        var o = CompositionFactory.CreateFade(v.Compositor, 0, null, 165);
        //        return v.Compositor.CreateAnimationGroup(t, o);
        //    });

        //    e.SetHideAnimation(g);
        //}
        //else
        {
            var g = v.GetCached("OPA", c =>
            {
                var t = v.CreateVector3KeyFrameAnimation(CompositionFactory.TRANSLATION)
                        .AddKeyFrame(1, 0, 200)
                        .SetDuration(0.375);

                var o = CompositionFactory.CreateFade(c, 0, null, 200);
                return c.CreateAnimationGroup(t, o);
            });

            e.SetHideAnimation(g);
            e.SetShowAnimation(CompositionFactory.CreateEntranceAnimation(e, new Vector3(0, 200, 0), 0, 550));
        }
        
    }

    public static void SetupOverlayPanelAnimationX(UIElement e)
    {
        if (!UISettings.AnimationsEnabled)
            return;

        Visual v = e.EnableTranslation(true).GetElementVisual();

        //if (ResourceHelper.IsMaterialTheme)
        //{
        //    var g = v.GetCached("MOPH", () =>
        //    {
        //        var t = v.CreateVector3KeyFrameAnimation("Scale")
        //                .AddKeyFrame(1, new Vector3(0.8f, 0.8f, 1), KeySplines.FluentAccelerate)
        //                .SetDuration(0.3);

        //        var o = CompositionFactory.CreateFade(v.Compositor, 0, null, 300);
        //        return v.Compositor.CreateAnimationGroup(t, o);
        //    });

        //    e.SetHideAnimation(g);
        //}
        //else
        {
            var g = v.GetCached("OPAX", c =>
            {
                var t = c.CreateVector3KeyFrameAnimation()
                        .SetTarget(CompositionFactory.TRANSLATION)
                        .AddKeyFrame(1, 200, 0)
                        .SetDuration(0.375);

                var o = CompositionFactory.CreateFade(c, 0, null, 200);
                return c.CreateAnimationGroup(t, o);
            });

            e.SetHideAnimation(g);
            e.SetShowAnimation(CompositionFactory.CreateEntranceAnimation(e, new Vector3(200, 0, 0), 0, 550));
        }
    }

    public static void PlayEntrance(UIElement target, int delayMs = 0, int fromOffsetY = 40, int fromOffsetX = 0, int durationMs = 1000)
    {
        if (!UISettings.AnimationsEnabled)
            return;

        var animation = CreateEntranceAnimation(target, new Vector3(fromOffsetX, fromOffsetY, 0), delayMs, durationMs);
        target.GetElementVisual().StartAnimationGroup(animation);
    }

    public static void SetStandardEntrance(FrameworkElement sender, object args)
    {
        if (!UISettings.AnimationsEnabled)
            return;

        if (sender is FrameworkElement e)
            e.SetShowAnimation(CreateEntranceAnimation(e, new Vector3(100, 0, 0), 200));
    }

    public static void PlayStandardEntrance(object sender, RoutedEventArgs args)
    {
        if (!UISettings.AnimationsEnabled)
            return;

        if (sender is FrameworkElement e)
            e.GetElementVisual().StartAnimationGroup(CreateEntranceAnimation(e, new Vector3(100, 0, 0), 200));
    }

    public static ICompositionAnimationBase CreateEntranceAnimation(UIElement target, Vector3 from, int delayMs, int durationMs = 700)
    {
        string key = $"CEA{from.X}{from.Y}{delayMs}{durationMs}";
        Compositor c1 = target.EnableTranslation(true).GetElementVisual().Compositor;

        return c1.GetCached(key, c =>
        {
            TimeSpan delay = TimeSpan.FromMilliseconds(delayMs);
            var e = c.GetCachedFluentEntranceEase();
            var t = c.CreateVector3KeyFrameAnimation()
                .SetTarget(TRANSLATION)
                .SetInitialValueBeforeDelay()
                .SetDelayTime(delay)
                .AddKeyFrame(0, from)
                .AddKeyFrame(1, 0, e)
                .SetDuration(TimeSpan.FromMilliseconds(durationMs));

            var o = CreateFade(c, 1, 0, (int)(durationMs * 0.33), delayMs);
            return c.CreateAnimationGroup(t, o);
        });
    }

    public static void SetCornerRadius(UIElement target, float size)
    {
        var vis = target.GetElementVisual();
        var rec = vis.Compositor.CreateRoundedRectangleGeometry();
        rec.CornerRadius = new(size);
        rec.LinkShapeSize(vis);
        var clip = vis.Compositor.CreateGeometricClip(rec);
        vis.Clip = clip;
    }

    public static void PlayEntrance(List<UIElement> targets, int delayMs = 0, int fromOffsetY = 40, int fromOffsetX = 0, int durationMs = 1000, int staggerMs = 83)
    {
        if (!UISettings.AnimationsEnabled)
            return;

        int start = delayMs;

        foreach (var target in targets)
        {
            if (target is null) continue;
            var animation = CreateEntranceAnimation(target, new Vector3(fromOffsetX, fromOffsetY, 0), start, durationMs);
            target.GetElementVisual().StartAnimationGroup(animation);
            start += staggerMs;
        }
    }

    public static CompositionAnimation CreateFade(Compositor c1, float to, float? from, int durationMs, int delayMs = 0)
    {
        string key = $"SFade{to}{from}{durationMs}{delayMs}";
        return c1.GetCached(key, c =>
        {
            var o = c.CreateScalarKeyFrameAnimation()
                     .SetTarget(nameof(Visual.Opacity));

            if (from != null && from.HasValue)
                o.AddKeyFrame(0, from.Value);

            o.AddKeyFrame(1, to, c.GetCachedFluentEntranceEase())
             .SetInitialValueBeforeDelay()
             .SetDelayTime(TimeSpan.FromMilliseconds(delayMs))
             .SetDuration(TimeSpan.FromMilliseconds(durationMs));
           
            return o;
        });
    }

    public static ExpressionAnimation StartCentering(Visual v, float x = 0.5f, float y = 0.5f)
    {
        v.StopAnimation(nameof(Visual.CenterPoint));

        var e = v.GetCached($"CP{x}{y}",
                    c => c.CreateExpressionAnimation()
                            .SetTarget(nameof(Visual.CenterPoint))
                            .SetExpression(string.Format(
                                CENTRE_EXPRESSION,
                                x.ToString(CultureInfo.InvariantCulture.NumberFormat),
                                y.ToString(CultureInfo.InvariantCulture.NumberFormat))));

        v.StartAnimationGroup(e);
        return e;
    }

    public static void PlayScaleEntrance(FrameworkElement target, float from, float to, double duration = 0.6)
    {
        if (!UISettings.AnimationsEnabled)
            return;

        Visual v = target.GetElementVisual();

        if (target.Tag == null)
        {
            StartCentering(v);
            target.Tag = target;
        }

        var e = CubicBezierPoints.FluentDecelerate;// v.Compositor.CreateEntranceEasingFunction();

        var t = v.CreateVector3KeyFrameAnimation(nameof(Visual.Scale))
            .AddKeyFrame(0, new Vector3(from, from, 0))
            .AddKeyFrame(1, new Vector3(to, to, 0), e)
            .SetDuration(duration);

        var o = CreateFade(v.Compositor, 1, 0, 200);

        var g = v.Compositor.CreateAnimationGroup(t, o);
        v.StartAnimationGroup(g);
    }

    public static void SetStandardReposition(UIElement e)
    {
        if (!UISettings.AnimationsEnabled)
            return;

        Visual v = e.GetElementVisual();

        var value = v.GetCached("DefaultOffsetAnimation",
                        c => c.CreateVector3KeyFrameAnimation()
                                .SetTarget(nameof(Visual.Offset))
                                .AddKeyFrame(0, STARTING_VALUE)
                                .AddKeyFrame(1, FINAL_VALUE)
                                .SetDuration(DefaultOffsetDuration));

        v.SetImplicitAnimation(nameof(Visual.Offset), value);
    }

    public static void DisableStandardReposition(FrameworkElement f)
    {
        f.GetElementVisual().ImplicitAnimations?.Remove(nameof(Visual.Offset));
    }

    public static Visual EnableStandardTranslation(Visual v, double? duration = null)
    {
        if (!UISettings.AnimationsEnabled)
            return v;

        var o = v.GetCached($"__ST{(duration.HasValue ? duration.Value : DefaultOffsetDuration)}",
            c => c.CreateVector3KeyFrameAnimation()
                    .SetTarget(CompositionFactory.TRANSLATION)
                   .AddKeyFrame(0, STARTING_VALUE)
                   .AddKeyFrame(1, FINAL_VALUE, CubicBezierPoints.FluentDecelerate)
                   .SetDuration(duration ?? DefaultOffsetDuration));

        v.Properties.SetImplicitAnimation(CompositionFactory.TRANSLATION, o);
        return v;
    }

    public static void SetDropInOut(FrameworkElement background, IList<FrameworkElement> children, FrameworkElement container = null)
    {
        if (background is null || children.Count == 0)
            return;

        if (AnimationEnabled is false)
        {
            background.SetShowAnimation(null);
            background.SetHideAnimation(null);
            foreach (var child in children)
            {
                child.SetShowAnimation(null);
                child.SetHideAnimation(null);
            }

            return;
        }

        double delay = 0.15;

        var bv = background.EnableTranslation(true).GetElementVisual();
        var ease = bv.Compositor.GetCachedFluentEntranceEase();

        var bt = bv.CreateVector3KeyFrameAnimation(TRANSLATION)
            .AddKeyFrame(0, "Vector3(0, -this.Target.Size.Y, 0)")
            .AddKeyFrame(1, Vector3.Zero, ease)
            .SetInitialValueBeforeDelay()
            .SetDelayTime(delay)
            .SetDuration(0.7);

        background.SetShowAnimation(bt);

        delay += 0.15;

        foreach (var child in children)
        {
            var v = child.EnableTranslation(true).GetElementVisual();
            var t = v.CreateVector3KeyFrameAnimation(TRANSLATION)
                .AddKeyFrame(0, "Vector3(0, -this.Target.Size.Y, 0)")
                .AddKeyFrame(1, Vector3.Zero, ease)
                .SetInitialValueBeforeDelay()
                .SetDelayTime(delay)
                .SetDuration(0.7);

            child.SetShowAnimation(t);
            delay += 0.075;
        }

        if (container != null)
        {
            var c = container.GetElementVisual();
            var clip = c.Compositor.CreateInsetClip();
            c.Clip = clip;
        }


        // Create hide animation
        List<FrameworkElement> list = [background];
        list.AddRange(children);

        var ht = bv.Compositor.CreateVector3KeyFrameAnimation()
            .SetTarget(TRANSLATION)
            .AddKeyFrame(1, "Vector3(0, -this.Target.Size.Y, 0)", ease)
            .SetDuration(0.5);

        foreach (var child in list)
            child.SetHideAnimation(ht);
    }

    public static void SetStandardFadeInOut(object sender, RoutedEventArgs args)
    {
        if (!UISettings.AnimationsEnabled)
            return;

        if (sender is FrameworkElement e)
            SetFadeInOut(e, 200);
    }

    private static void SetFadeInOut(FrameworkElement e, int durationMs)
    {
        var v = e.GetElementVisual();
        e.SetHideAnimation(CreateFade(v.Compositor, 0, null, durationMs));
        e.SetShowAnimation(CreateFade(v.Compositor, 1, null, durationMs));
    }

    public static void PlayFluentStartupAnimation(
        FrameworkElement bar,
        FrameworkElement content)
    {
        var bv = bar.EnableTranslation(true).GetElementVisual();
        bv.StartAnimation(
            bv.CreateScalarKeyFrameAnimation(nameof(Visual.Opacity))
                .AddKeyFrame(0, 0)
                .AddKeyFrame(1, 1)
                .SetDelay(0.1, AnimationDelayBehavior.SetInitialValueBeforeDelay)
                .SetDuration(0.3));

        bv.StartAnimation(
            bv.CreateVector3KeyFrameAnimation(TRANSLATION)
                .AddKeyFrame(0, new Vector3(-200, 0, 0))
                .AddKeyFrame(1, Vector3.Zero, bv.Compositor.GetCachedEntranceEase())
                .SetDelay(0.1, AnimationDelayBehavior.SetInitialValueBeforeDelay)
                .SetDuration(1.2));

        var cv = content.EnableCompositionTranslation().GetElementVisual();
        cv.StartAnimation(
            cv.CreateScalarKeyFrameAnimation(nameof(Visual.Opacity))
                .AddKeyFrame(0, 0)
                .AddKeyFrame(1, 1)
                .SetDelay(0.1, AnimationDelayBehavior.SetInitialValueBeforeDelay)
                .SetDuration(0.3));

        cv.StartAnimation(
            cv.CreateVector3KeyFrameAnimation(TRANSLATION)
                .AddKeyFrame(0, new Vector3(0, 140, 0))
                .AddKeyFrame(1, Vector3.Zero, cv.Compositor.GetCachedEntranceEase())
                .SetDelay(0.1, AnimationDelayBehavior.SetInitialValueBeforeDelay)
                .SetDuration(1.2));
    }

    public static void PlayStartUpAnimation(
        List<FrameworkElement> barElements,
        List<UIElement> contentElements)
    {
        if (!UISettings.AnimationsEnabled)
            return;

        TimeSpan duration1 = TimeSpan.FromSeconds(0.7);

        var c = barElements[0].GetElementVisual().Compositor;
        var backOut = c.CreateEase(0.2f, 0.885f, 0.25f, 1.125f);

        double delay = 0.1;
        foreach (var element in barElements)
        {
            var v = element.EnableTranslation(true).GetElementVisual();
            v.StartAnimationGroup(
                v.CreateVector3KeyFrameAnimation(TRANSLATION)
                 .AddKeyFrame(0, 0, -100)
                 .AddKeyFrame(1, 0, backOut)
                 .SetInitialValueBeforeDelay()
                 .SetDelayTime(TimeSpan.FromSeconds(delay))
                 .SetDuration(duration1));

            delay += 0.055;
        }

        PlayEntrance(contentElements, 200);
    }

  
    public static void PlayFullHeightSlideUpEntrance(FrameworkElement target)
    {
        if (!UISettings.AnimationsEnabled)
            return;

        Visual v = target.EnableTranslation(true).GetElementVisual();
        var t = v.GetCached("_FHSU", c =>
            c.CreateVector3KeyFrameAnimation()
                .SetTarget(TRANSLATION)
                .AddKeyFrame(0, "Vector3(0, this.Target.Size.Y, 0)")
                .AddKeyFrame(1, "Vector3(0, 0, 0)")
                .SetDuration(DefaultOffsetDuration));

        v.StartAnimationGroup(t);
    }

    public static Vector3KeyFrameAnimation CreateSlideOut(UIElement e, float x, float y)
    {
        Visual v = e.EnableTranslation(true).GetElementVisual();
        return v.GetCached("_SLDO",
                c => c.CreateVector3KeyFrameAnimation()
                        .SetTarget(TRANSLATION)
                        .AddKeyFrame(0, STARTING_VALUE)
                        .AddKeyFrame(1, x, y, 0)
                        .SetDuration(DefaultOffsetDuration));
    }

    public static Vector3KeyFrameAnimation CreateSlideOutX(UIElement e)
    {
        Visual v = e.EnableTranslation(true).GetElementVisual();
        return v.GetCached("SOX",
                c => v.CreateVector3KeyFrameAnimation()
                        .SetTarget(TRANSLATION)
                        .AddKeyFrame(0, STARTING_VALUE)
                        .AddKeyFrame(1, "Vector3(this.Target.Size.X, 0, 0)")
                        .SetDuration(DefaultOffsetDuration));
    }

    public static Vector3KeyFrameAnimation CreateSlideOutY(UIElement e)
    {
        Visual v = e.EnableTranslation(true).GetElementVisual();
        return v.GetCached("SOY",
                c => c.CreateVector3KeyFrameAnimation()
                        .SetTarget(TRANSLATION)
                        .AddKeyFrame(0, STARTING_VALUE)
                        .AddKeyFrame(1, "Vector3(0, this.Target.Size.Y, 0)")
                        .SetDuration(DefaultOffsetDuration));
    }

    public static Vector3KeyFrameAnimation CreateSlideIn(UIElement e)
    {
        Visual v = e.EnableTranslation(true).GetElementVisual();
        return v.GetCached("_SLDI",
                c => c.CreateVector3KeyFrameAnimation()
                        .SetTarget(TRANSLATION)
                        .AddKeyFrame(1, Vector3.Zero)
                        .SetDuration(DefaultOffsetDuration));
    }


    #region Default Composition Transitions 

    /// <summary>
    /// Creates the detault Forward composition animation
    /// </summary>
    /// <param name="outElement"></param>
    /// <param name="inElement"></param>
    /// <returns></returns>
    public static void StartCompositionExpoZoomForwardTransition(FrameworkElement outElement, FrameworkElement inElement)
    {
        if (!UISettings.AnimationsEnabled)
            return;

        Compositor compositor = ElementCompositionPreview.GetElementVisual(outElement).Compositor;

        Visual outVisual = ElementCompositionPreview.GetElementVisual(outElement);
        Visual inVisual = ElementCompositionPreview.GetElementVisual(inElement);

        CompositionAnimationGroup outgroup = compositor.CreateAnimationGroup();
        CompositionAnimationGroup ingroup = compositor.CreateAnimationGroup();

        TimeSpan outDuration = TimeSpan.FromSeconds(0.3);
        TimeSpan inStart = TimeSpan.FromSeconds(0.25);
        TimeSpan inDuration = TimeSpan.FromSeconds(0.6);

        CubicBezierEasingFunction ease = compositor.GetCached("ExpoZoomEase",
            c=> c.CreateCubicBezierEasingFunction(0.95f, 0.05f, 0.79f, 0.04f));

        CubicBezierEasingFunction easeOut = compositor.GetCached("ExpoZoomOutEase",
            c => c.CreateCubicBezierEasingFunction(0.13f, 1.0f, 0.49f, 1.0f));

        // OUT ELEMENT
        {
            outVisual.CenterPoint = outVisual.Size.X > 0
               ? new Vector3(outVisual.Size / 2f, 0f)
               : new Vector3((float)Window.Current.Bounds.Width / 2f, (float)Window.Current.Bounds.Height / 2f, 0f);

            // SCALE OUT
            var sout = compositor.CreateVector3KeyFrameAnimation();
            sout.InsertKeyFrame(1, new Vector3(1.3f, 1.3f, 1f), ease);
            sout.Duration = outDuration;
            sout.Target = nameof(outVisual.Scale);

            // FADE OUT
            var oout = compositor.CreateScalarKeyFrameAnimation();
            oout.InsertKeyFrame(1, 0f, ease);
            oout.Duration = outDuration;
            oout.Target = nameof(outVisual.Opacity);
        }

        // IN ELEMENT
        {
            inVisual.CenterPoint = inVisual.Size.X > 0
                  ? new Vector3(inVisual.Size / 2f, 0f)
                  : new Vector3(outVisual.Size / 2f, 0f);


            // SCALE IN
            var sO = inVisual.Compositor.CreateVector3KeyFrameAnimation();
            sO.Duration = inDuration;
            sO.Target = nameof(inVisual.Scale);
            sO.InsertKeyFrame(0, new Vector3(0.7f, 0.7f, 1.0f), easeOut);
            sO.InsertKeyFrame(1, new Vector3(1.0f, 1.0f, 1.0f), easeOut);
            sO.DelayTime = inStart;
            ingroup.Add(sO);

            // FADE IN
            inVisual.Opacity = 0f;
            var op = inVisual.Compositor.CreateScalarKeyFrameAnimation();
            op.DelayTime = inStart;
            op.Duration = inDuration;
            op.Target = nameof(outVisual.Opacity);
            op.InsertKeyFrame(1, 0f, easeOut);
            op.InsertKeyFrame(1, 1f, easeOut);
            ingroup.Add(op);

        }

        outVisual.StartAnimationGroup(outgroup);
        inVisual.StartAnimationGroup(ingroup);
    }

    /// <summary>
    /// Creates the default backwards composition animation
    /// </summary>
    /// <param name="outElement"></param>
    /// <param name="inElement"></param>
    /// <returns></returns>
    //CompositionStoryboard CreateCompositionExpoZoomBackward(FrameworkElement outElement, FrameworkElement inElement)
    //{
    //    Compositor compositor = ElementCompositionPreview.GetElementVisual(outElement).Compositor;

    //    Visual outVisual = ElementCompositionPreview.GetElementVisual(outElement);
    //    Visual inVisual = ElementCompositionPreview.GetElementVisual(inElement);

    //    CompositionAnimationGroup outgroup = compositor.CreateAnimationGroup();
    //    CompositionAnimationGroup ingroup = compositor.CreateAnimationGroup();

    //    TimeSpan outDuration = TimeSpan.FromSeconds(0.3);
    //    TimeSpan inDuration = TimeSpan.FromSeconds(0.4);

    //    CubicBezierEasingFunction ease = compositor.CreateCubicBezierEasingFunction(
    //        new Vector2(0.95f, 0.05f),
    //        new Vector2(0.79f, 0.04f));

    //    CubicBezierEasingFunction easeOut = compositor.CreateCubicBezierEasingFunction(
    //        new Vector2(0.19f, 1.0f),
    //        new Vector2(0.22f, 1.0f));


    //    // OUT ELEMENT
    //    {
    //        outVisual.CenterPoint = outVisual.Size.X > 0
    //            ? new Vector3(outVisual.Size / 2f, 0f)
    //            : new Vector3((float)this.ActualWidth / 2f, (float)this.ActualHeight / 2f, 0f);

    //        // SCALE OUT
    //        var sO = compositor.CreateVector3KeyFrameAnimation();
    //        sO.Duration = outDuration;
    //        sO.Target = nameof(outVisual.Scale);
    //        sO.InsertKeyFrame(1, new Vector3(0.7f, 0.7f, 1.0f), ease);
    //        outgroup.Add(sO);

    //        // FADE OUT
    //        var op = compositor.CreateScalarKeyFrameAnimation();
    //        op.Duration = outDuration;
    //        op.Target = nameof(outVisual.Opacity);
    //        op.InsertKeyFrame(1, 0f, ease);
    //        outgroup.Add(op);
    //    }

    //    // IN ELEMENT
    //    {
    //        inVisual.CenterPoint = inVisual.Size.X > 0
    //             ? new Vector3(inVisual.Size / 2f, 0f)
    //             : new Vector3((float)this.ActualWidth / 2f, (float)this.ActualHeight / 2f, 0f);


    //        // SCALE IN
    //        ingroup.Add(
    //            inVisual.CreateVector3KeyFrameAnimation(nameof(Visual.Scale))
    //                .AddScaleKeyFrame(0, 1.3f)
    //                .AddScaleKeyFrame(1, 1f, easeOut)
    //                .SetDuration(inDuration)
    //                .SetDelayTime(outDuration)
    //                .SetDelayBehavior(AnimationDelayBehavior.SetInitialValueBeforeDelay));

    //        // FADE IN
    //        inVisual.Opacity = 0f;
    //        var op = inVisual.Compositor.CreateScalarKeyFrameAnimation();
    //        op.DelayTime = outDuration;
    //        op.Duration = inDuration;
    //        op.Target = nameof(outVisual.Opacity);
    //        op.InsertKeyFrame(1, 0f, easeOut);
    //        op.InsertKeyFrame(1, 1f, easeOut);
    //        ingroup.Add(op);

    //    }

    //    CompositionStoryboard group = new CompositionStoryboard();
    //    group.Add(new CompositionTimeline(outVisual, outgroup, ease));
    //    group.Add(new CompositionTimeline(inVisual, ingroup, easeOut));
    //    return group;
    //}

    #endregion

    /* Adding or removing Receivers is glitchy AF */

    //public static void TryAddRecievers(UIElement target, params UIElement[] recievers)
    //{
    //    if (!Utils.Supports1903)
    //        return;

    //    if (target.Shadow is ThemeShadow t)
    //    {
    //        foreach (var r in recievers)
    //            if (!t.Receivers.Any(c => c == r))
    //                t.Receivers.Add(r);
    //    }
    //}

    //public static void TryRemoveRecievers(UIElement target, params UIElement[] recievers)
    //{
    //    if (!Utils.Supports1903)
    //        return;

    //    if (target.Shadow is ThemeShadow t)
    //    {
    //        target.Shadow = null;
    //        ThemeShadow nt = new ThemeShadow();
    //        foreach (var s in t.Receivers)
    //        {
    //            if (!recievers.Contains(s))
    //                nt.Receivers.Add(s);
    //        }

    //        target.Shadow = nt;
    //    }
    //}
}

# Overview
Allows declaring Composition `ExpressionAnimations` in XAML, and allows setting them in styles and templates to enable efficient animation reuse.

# Animating UIElements

Use the `Composition.Animations` `AttachedProperty` to attach a `XamlAnimationCollection` to a UIElement. This will automatically run the `CompositionAnimation`s on the handoff `Visual` provided by `ElementCompositionPreview.GetElementVisual(...)`.

The most common target properties will likely be `Translation`, `Scale`, `Opacity`, `CenterPoint`, and less commonly `Offset`, but any animateble Composition property is valid.

### Targetting `Translation` on UIElements

Targetting `Translation` will automatically call `ElementCompositionPreview.SetIsTranslationEnabled(..., true)`.

To reference the `Translation` property from an expression animation, you will have to target the `CompositionPropertySet` / `Properties` value of the handoff `Visual`, where the framework stores the `Translation` value. 

For example, we can bind `Translation` of one UIElement to another by referencing the source `UIElement`s VisualPropertySet

```
 <c:XAMLExpressionAnimation Expression="props.Translation" Target="Translation">
     <c:ElementVisualPropertySetParameter Key="props" Element="{x:Bind Source}" />
 </c:XAMLExpressionAnimation>
 ```

### Storing values on a `UIElement`

We can use behaviours to allow a `UIElement` to store its values in a way that allows `ExpressionAnimations` to reference them.

Here we create a slider, and use a behavior to set its own value as `CurrentValue` on its own `CompositionPropertySet`/`Properties` property of its Composition handoff `Visual`

```
 <Slider x:Name="mySlider" Maximum="40">
     <i:Interaction.Behaviors>
         <i:EventTriggerBehavior EventName="ValueChanged">
             <b:SetPropertySetAction
                 Key="CurrentValue"
                 DefaultValue="0"
                 Target="{x:Bind mySlider}"
                 Value="{x:Bind mySlider.Value, Mode=OneWay}" />
         </i:EventTriggerBehavior>
     </i:Interaction.Behaviors>
 </Slider>
 ```

 We insert the Slider current value as `CurrentValue` in handoff `Visual`'s `CompositionPropertySet`

Now we can use this value in other animations by referenceing the sliders `CompositionPropertySet`.

 For example, if we want to bind the `Translation` of another `UIElement` to the `Slider`s value, we could set this `XamlExpressionAnimation` on the `UIElements` `Composition.Animations` collection.
 ```
<c:XAMLExpressionAnimation Expression="Vector3(props.CurrentValue, props.CurrentValue, 1)" 
                            Target="Translation">
    <c:ElementVisualPropertySetParameter Key="props" Element="{x:Bind Slider}" />
</c:XAMLExpressionAnimation>
 ``` 

# Animating Lights

ExpressionAnimations can be attached to `XAMLLight`s, but as we have no event for when the `XAMLLight` is connected to the VisualTree to trigger the animation, it requires the FrameworkElement they're attached to be loaded first. 



One way of doing this in pure XAML is to use a XAML `Behavior` to attach the `XAMLAnimationCollection` after the `FrameworkElement` that the light is attached too is loaded.

For example:

```
<Border>
    <i:Interaction.Behaviors>
        <i:EventTriggerBehavior EventName="Loaded">
            <b:SetAnimationCollectionAction Target="{x:Bind Spotlight}">
                <c:XAMLAnimationCollection>
                    <c:XAMLExpressionAnimation
                        AnimationTarget="CompositionObjectProperties"
                        Expression="p.Rotation"
                        Target="Height">
                        <c:ElementVisualPropertySetParameter Key="p" Element="{x:Bind Slider}" />
                    </c:XAMLExpressionAnimation>
                </c:XAMLAnimationCollection>
            </b:SetAnimationCollectionAction>
        </i:EventTriggerBehavior>
    </i:Interaction.Behaviors>
    <Border.Lights>
        <c:AmbLight />
        <c:SimpleSpotLight x:Name="Spotlight" />
    </Border.Lights>
</Border>
```

# Notes

### `{x:Bind }`
To enable `{x:Bind }`, animations only run after a `UIElement`'s `Loaded` event fires.

`XAMLLight` and `XAMLCompositionBrushBase` do not have events we can hook on to know they are in the VisualTree or that `{x:Bind }` has run - so it is advised you only assign the `XAMLAnimationCollection` after their display UIElements have loaded.

You can do this in pure XAML using the behavior example in the **Animating Lights** section above.

### StartAnimation
Currently, we call `StartAnimation` NOT on the XAML `UIElement`, but on the `Visual` provided by `ElementCompositionPreview.GetElementVisual(...)`

This allows better interoperability with any exsiting Composition-layer animations you're using.
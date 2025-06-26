using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace XAMLComposition;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        this.InitializeComponent();
    }

    string RotationExpression()
    {
        var positiveRotateCheck = "(p.Position.Y  < src.CenterPoint.Y) || (p.Position.X > src.CenterPoint.X && p.Position.Y == src.CenterPoint.Y) ? "; 
        var positiveRotateValue = $"45 * ((Clamp(Distance(src.CenterPoint, p.Position), 0, props.D2C) % props.D2C)/props.D2C) : ";
        var negativeRotateCheck = "(p.Position.Y > src.CenterPoint.Y) || (p.Position.X < src.CenterPoint.X && p.Position.Y == src.CenterPoint.Y) ? ";
        var negativeRotateValue = $"-45 * ((Clamp(Distance(src.CenterPoint, p.Position), 0, props.D2C) % props.D2C)/props.D2C) : this.CurrentValue";
        return positiveRotateCheck + positiveRotateValue + negativeRotateCheck + negativeRotateValue;
    }

    string RotationAxisExpression()
    {
        // The axis is dependent on which quadrant or axis the pointer position is on.
        var quad2Check = "(p.Position.Y < src.CenterPoint.Y && p.Position.X < src.CenterPoint.X) ? Vector3(-(-src.CenterPoint.Y + p.Position.Y), -src.CenterPoint .X + p.Position.X, 0) : ";
        var quad1Check = "(p.Position.Y < src.CenterPoint.Y && p.Position.X > src.CenterPoint.X) ? Vector3(-(-src.CenterPoint.Y + p.Position.Y), p.Position.X - src.CenterPoint.X, 0) : ";
        var quad4Check = "(p.Position.Y > src.CenterPoint.Y && p.Position.X < src.CenterPoint.X) ? Vector3((p.Position.Y - src.CenterPoint.Y), src.CenterPoint.X - p.Position.X, 0) : ";
        var quad3Check = "(p.Position.Y > src.CenterPoint.Y && p.Position.X > src.CenterPoint.X) ? Vector3((p.Position.Y - src.CenterPoint.Y), -(p.Position.X - src.CenterPoint.X), 0) : ";
        var xAxisCheck = "(p.Position.Y == src.CenterPoint.Y && p.Position.X != src.CenterPoint.X) ? Vector3(0, src.CenterPoint.X, 0) : ";
        var yAxisCheck = "(p.Position.Y != src.CenterPoint.Y && p.Position.X == src.CenterPoint.X) ? Vector3(src.CenterPoint.Y, 0, 0) : this.CurrentValue";
        return quad1Check + quad2Check + quad3Check + quad4Check + xAxisCheck + yAxisCheck;
    }
}

public class MyLight : XamlLight
{
    protected override void OnConnected(UIElement newElement) => base.OnConnected(newElement);
}

global using System;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.IO;
global using System.Linq;
global using System.Numerics;
global using System.Runtime.InteropServices.WindowsRuntime;
global using System.Threading.Tasks;

global using Windows.Foundation;


using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace XAMLComposition;

sealed partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }
   
    protected override void OnLaunched(LaunchActivatedEventArgs e)
    {
        Frame rootFrame = Window.Current.Content as Frame;
      
        if (rootFrame == null)
        {
            rootFrame = new Frame();
            Window.Current.Content = rootFrame;
        }

        if (e.PrelaunchActivated == false)
        {
            if (rootFrame.Content == null)
                rootFrame.Navigate(typeof(MainPage), e.Arguments);
            Window.Current.Activate();
        }
    }
}

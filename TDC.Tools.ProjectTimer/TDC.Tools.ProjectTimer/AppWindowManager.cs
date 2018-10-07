using System;
using System.ComponentModel.Composition;
using System.Reflection;
using Caliburn.Micro;
using MahApps.Metro.Controls;
using TDC.Tools.ProjectTimer.Caliburn.Metro;

namespace TDC.Tools.ProjectTimer
{
    [Export(typeof(IWindowManager))]
    public class AppWindowManager : MetroWindowManager
    {
        public override MetroWindow CreateCustomWindow(object view, bool windowIsView)
        {
            if (windowIsView)
            {
                return view as MainWindow;
            }

            Version version = Assembly.GetEntryAssembly().GetName().Version;
            return new MainWindow
            {
                Content = view,
                Title = $"Project Timer ( {version} ) - for private usage only"
            };
        }
    }
}
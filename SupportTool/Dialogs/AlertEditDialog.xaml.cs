using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.ComponentModel;
using System.Runtime.CompilerServices;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SupportTool.Dialogs
{
    public sealed partial class AlertEditDialog : ContentDialog
    {
        public AlertEditDialog()
        {
            this.InitializeComponent();
        }

        public void SetData(string appName, string carrierName, bool pdAlert, bool erAlert, string notes = "")
        {
            AppNameText.Text = appName;
            CarrierNameText.Text = carrierName;
            PDAlertCheck.IsChecked = pdAlert;
            ERAlertCheck.IsChecked = erAlert;
            NotesText.Text = notes;
        }

        public (bool pdAlert, bool erAlert, string notes) GetResults()
        {
            return (
                PDAlertCheck.IsChecked ?? false,
                ERAlertCheck.IsChecked ?? false,
                NotesText.Text
            );
        }
    }
}

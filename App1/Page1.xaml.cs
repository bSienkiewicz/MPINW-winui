using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App1
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Page1 : Page
    {
        private List<RetailersCheck> dynamicCheckBoxes = new();
        public Page1()
        {
            this.InitializeComponent();
            FetchAndGenerateCheckboxes(new[] { "Option 1", "Option 2", "Option 3", "Option 4", "Option 5 XD" });
        }

        private void FetchAndGenerateCheckboxes(IEnumerable<string> options)
        {
            dynamicCheckBoxes.Clear();

            foreach (var option in options)
            {
                var checkBox = new RetailersCheck
                {
                    Content = option
                };

                dynamicCheckBoxes.Add(checkBox);
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox check)
            {
                var item = dynamicCheckBoxes.FirstOrDefault(c => c.Content == check.Content.ToString());
                if (item != null)
                {
                    item.IsChecked = true;
                    Debug.WriteLine($"Checked: {item.Content}");
                }
            }
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox check)
            {
                var item = dynamicCheckBoxes.FirstOrDefault(c => c.Content == check.Content.ToString());
                if (item != null)
                {
                    item.IsChecked = false;
                    Debug.WriteLine($"Unchecked: {item.Content}");
                }
            }
        }
    }

    public class RetailersCheck
    {
        public string Content { get; set; } = string.Empty;
        public bool IsChecked { get; set; } = false;
    }
}

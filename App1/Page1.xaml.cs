using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public Page1()
        {
            this.InitializeComponent();
            Items = new ObservableCollection<ExpandItem>();
            PopulateItems();
        }

        private void PopulateItems()
        {
            Items.Add(new ExpandItem
            {
                Title = "Item 1",
                Content = "Item 1 description"
            });
            Items.Add(new ExpandItem
            {
                Title = "Item 2",
                Content = "Content 2"
            });
            Items.Add(new ExpandItem
            {
                Title = "Item 3",
                Content = "Content 2"
            });
        }

        void Button_Click(object sender, RoutedEventArgs e)
        {
            Items.Add(new ExpandItem
            {
                Title = "Item Added",
                Content = "Content 2"
            });
        }

        public ObservableCollection<ExpandItem> Items
        {
            get; private set;
        }
    }

    public class ExpandItem
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}

using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.ComponentModel;
using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App1
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Page2 : Page
    {
        public Page2ViewModel ViewModel { get; set; }
        public Page2()
        {
            this.InitializeComponent();
            this.ViewModel = new Page2ViewModel();
            this.DataContext = this.ViewModel;
        }
        private async void OnFetchDataClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            string providerId = ProviderID.Text;
            await ViewModel.MakeTestRequestAsync(providerId);
        }
    }
    public partial class Page2ViewModel : INotifyPropertyChanged
    {
        private string? responseData;
        public string ResponseData
        {
            get => responseData ?? string.Empty;
            set
            {
                responseData = value;
                OnPropertyChanged(nameof(ResponseData));
            }
        }

        public async Task MakeTestRequestAsync(string providerId)
        {
            using HttpClient client = new();


            if (providerId == null)
            {
                Debug.WriteLine("ProviderID TextBox is null");
                return;
            }

            try
            {
                string url = $"https://jsonplaceholder.typicode.com/posts/{providerId}";
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode(); // Throw if status code is not 2xx

                ResponseData = await response.Content.ReadAsStringAsync();
                Debug.WriteLine(ResponseData);
            }
            catch (HttpRequestException e)
            {
                ResponseData = $"Request error: {e.Message}";
            }
        }

        // Notify the UI when the property changes
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

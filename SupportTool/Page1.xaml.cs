using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using SupportTool.Services;
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

namespace SupportTool
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Page1 : Page
    {
        private ObservableCollection<RetailersCheck> RetailerDynamicCheckBoxes = new();
        private ObservableCollection<RetailerData> retailersData = new();
        private bool isUpdatingCheckboxes = false;

        public Page1()
        {
            this.InitializeComponent();
            FetchAndGenerateCheckboxes(new[] { "Option 1", "Option 2", "Option 3", "Option 4", "Option 5 XD" });
            UpdateSelectAllState();

            string savedText = DataService.Instance.GetData<string>("savedText");
            if (!string.IsNullOrEmpty(savedText))
            {
                TestTextBox.Text = savedText;
            }
        }


        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DataService.Instance.SetData("savedText", TestTextBox.Text);
        }

        private void FetchAndGenerateCheckboxes(IEnumerable<string> options)
        {
            RetailerDynamicCheckBoxes.Clear();

            foreach (var option in options)
            {
                var checkBox = new RetailersCheck
                {
                    Content = option
                };

                RetailerDynamicCheckBoxes.Add(checkBox);
            }
            UpdateSelectAllState();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (isUpdatingCheckboxes) return;

            if (sender is CheckBox check)
            {
                var item = RetailerDynamicCheckBoxes.FirstOrDefault(c => c.Content == check.Content.ToString());
                if (item != null)
                {
                    item.IsChecked = true;
                    retailersData.Add(new RetailerData
                    {
                        RetailerName = item.Content,
                        ApiCalls = new List<ApiCall>
                    {
                        new ApiCall
                        {
                            Timestamp = DateTime.Now,
                            Endpoint = "https://api.example.com/data",
                            RequestParameters = "date=2025-01-01",
                            ResponseData = "{ 'key': 'value' }"
                        }
                    }
                    });
                }
            }
            UpdateSelectAllState();
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isUpdatingCheckboxes) return;

            if (sender is CheckBox check)
            {
                var item = RetailerDynamicCheckBoxes.FirstOrDefault(c => c.Content == check.Content.ToString());
                if (item != null)
                {
                    item.IsChecked = false;
                    var retailerToRemove = retailersData.FirstOrDefault(r => r.RetailerName == item.Content);
                    if (retailerToRemove != null)
                    {
                        retailersData.Remove(retailerToRemove);
                    }
                }
            }
            UpdateSelectAllState();
        }

        private void UpdateSelectAllState()
        {
            int totalCount = RetailerDynamicCheckBoxes.Count;
            int checkedCount = RetailerDynamicCheckBoxes.Count(x => x.IsChecked);

            isUpdatingCheckboxes = true;
            try
            {
                if (checkedCount == 0)
                    OptionsAllCheckBox.IsChecked = false;
                else if (checkedCount == totalCount)
                    OptionsAllCheckBox.IsChecked = true;
                else
                    OptionsAllCheckBox.IsChecked = null;
            }
            finally
            {
                isUpdatingCheckboxes = false;
            }
        }
        private void OptionsAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (isUpdatingCheckboxes) return;

            isUpdatingCheckboxes = true;
            try
            {
                foreach (var checkbox in RetailerDynamicCheckBoxes)
                {
                    checkbox.IsChecked = true;
                }
            }
            finally
            {
                isUpdatingCheckboxes = false;
            }
            UpdateSelectAllState();
        }

        private void OptionsAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isUpdatingCheckboxes) return;

            isUpdatingCheckboxes = true;
            try
            {
                foreach (var checkbox in RetailerDynamicCheckBoxes)
                {
                    checkbox.IsChecked = false;
                }
            }
            finally
            {
                isUpdatingCheckboxes = false;
            }
            UpdateSelectAllState();
        }

        private void RetailerListBox_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is RetailerData retailerData)
            {
                content.Text = retailerData.RetailerName;
            }
        }

    }

    public class RetailersCheck
    {
        public string Content { get; set; } = string.Empty;
        public bool IsChecked { get; set; } = false;
    }

    public class RetailerData
    {
        public string RetailerName { get; set; } = string.Empty;
        public List<ApiCall> ApiCalls { get; set; } = new List<ApiCall>();
    }

    public class ApiCall
    {
        public DateTime Timestamp { get; set; }
        public string Endpoint { get; set; } = string.Empty; // API endpoint that was called
        public string RequestParameters { get; set; } = string.Empty; // Any parameters sent
        public string ResponseData { get; set; } = string.Empty; // JSON or other response
    }
}

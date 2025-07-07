using Microsoft.UI.Xaml.Controls;

namespace SupportTool.Dialogs
{
    public sealed partial class BatchAddOptionsDialog : ContentDialog
    {
        public string NamePrefix => NamePrefixTextBox.Text;
        public string FacetBy => (FacetComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "None";

        public BatchAddOptionsDialog()
        {
            this.InitializeComponent();
        }
    }
} 
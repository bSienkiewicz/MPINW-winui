using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SupportTool.Features.Alerts.Services;
using SupportTool.Features.Alerts.Models;

namespace SupportTool.Alerts.Dialogs
{
    public sealed partial class BatchAddOptionsDialog : ContentDialog
    {
        private readonly AlertService _alertService = new();
        
        public string NamePrefix => NamePrefixTextBox.Text;
        public string FacetBy => (FacetComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "None";

        public BatchAddOptionsDialog(string selectedStack)
        {
            this.InitializeComponent();
            AnalyzeExistingAlerts(selectedStack);
        }

        private void AnalyzeExistingAlerts(string selectedStack)
        {
            if (string.IsNullOrEmpty(selectedStack))
                return;

            try
            {
                var allAlerts = _alertService.GetAlertsForStack(selectedStack);
                
                var printDurationAlerts = allAlerts.Where(alert =>
                {
                    bool hasAverageDuration = alert.NrqlQuery?.ToLower().Contains("average(duration)") == true;
                    bool hasCarrierInTitle = !string.IsNullOrEmpty(AlertService.ExtractCarrierFromTitle(alert.Name));
                    return hasAverageDuration && hasCarrierInTitle;
                }).ToList();

                if (!printDurationAlerts.Any())
                    return;

                // Extract name prefixes and facets from NRQL queries
                var namePrefixCounts = new Dictionary<string, int>();
                var facetCounts = new Dictionary<string, int>();

                foreach (var alert in printDurationAlerts)
                {
                    if (string.IsNullOrEmpty(alert.NrqlQuery))
                        continue;

                    // Extract name pattern from "name = '...'"
                    var nameMatch = Regex.Match(alert.NrqlQuery, @"name\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
                    if (nameMatch.Success)
                    {
                        string namePattern = nameMatch.Groups[1].Value;
                        
                        if (!string.IsNullOrEmpty(namePattern))
                        {
                            namePrefixCounts.TryGetValue(namePattern, out int count);
                            namePrefixCounts[namePattern] = count + 1;
                        }
                    }

                    // Extract facet from "FACET ..."
                    var facetMatch = Regex.Match(alert.NrqlQuery, @"FACET\s+(\w+)", RegexOptions.IgnoreCase);
                    if (facetMatch.Success)
                    {
                        string facet = facetMatch.Groups[1].Value;
                        facetCounts.TryGetValue(facet, out int count);
                        facetCounts[facet] = count + 1;
                    }
                    else
                    {
                        // No facet found, count as "None"
                        facetCounts.TryGetValue("None", out int count);
                        facetCounts["None"] = count + 1;
                    }
                }

                // Set the most frequently used name prefix
                if (namePrefixCounts.Any())
                {
                    var mostCommonName = namePrefixCounts.OrderByDescending(kvp => kvp.Value).First().Key;
                    NamePrefixTextBox.Text = mostCommonName;
                }

                // Set the most frequently used facet
                if (facetCounts.Any())
                {
                    var mostCommonFacet = facetCounts.OrderByDescending(kvp => kvp.Value).First().Key;
                    
                    // Find and select the matching ComboBoxItem
                    bool found = false;
                    foreach (ComboBoxItem item in FacetComboBox.Items)
                    {
                        if (item.Content?.ToString() == mostCommonFacet)
                        {
                            FacetComboBox.SelectedItem = item;
                            found = true;
                            break;
                        }
                    }
                    
                    // If not found and it's "None", select by index
                    if (!found && mostCommonFacet == "None")
                    {
                        FacetComboBox.SelectedIndex = 2; // "None" is the third item (index 2)
                    }
                    // If the facet doesn't match any item, leave default selection
                }
            }
            catch
            {
                // If analysis fails, use defaults
            }
        }
    }
} 
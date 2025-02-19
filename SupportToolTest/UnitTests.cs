using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using SupportTool.Helpers;

namespace SupportToolTest
{

    [TestClass]
    public class HclParserTests
    {
        private HclParser? _parser;

        [TestInitialize]
        public void Setup()
        {
            _parser = new HclParser();
        }

        [TestMethod]
        public void ParseAlerts_WithSquareBracketInErrorMessage_ShouldParseCorrectly()
        {
            string testContent = @"nr_nrql_alerts = [
  {
    ""name"" = ""Test Alert""
    ""nrql_query"" = ""SELECT filter(count(*), WHERE errorMessage != '[Error] Shipping definition  not found.') FROM Transaction""
    ""severity"" = ""CRITICAL""
    ""enabled"" = ""true""
  }
]";

            var alerts = _parser.ParseAlerts(testContent);

            Assert.AreEqual(1, alerts.Count);
            Assert.AreEqual("Test Alert", alerts[0].Name);
            StringAssert.Contains(alerts[0].NrqlQuery, "[Error] Shipping definition  not found.");
        }


        [TestMethod]
        public void ParseAlerts_StandardAlert_ShouldParseCorrectly()
        {
            string testContent = @"nr_nrql_alerts = [
  {
    ""name"" = ""Test Alert""
    ""nrql_query"" = ""SELECT average(duration) FROM Transaction where appName = 'pvh.mpm.metapack.net_BlackBox' and name = 'WebTransaction/WCF/XLogics.BlackBox.ServiceContracts.IBlackBoxContract.PrintParcel' and PrintOperation like '%Create%' and CarrierCode = 'DHL-15'""
    ""severity"" = ""CRITICAL""
    ""enabled"" = ""true""
  }
]";

            var alerts = _parser.ParseAlerts(testContent);

            Assert.AreEqual(1, alerts.Count);
            Assert.AreEqual("Test Alert", alerts[0].Name);
            StringAssert.Contains(alerts[0].NrqlQuery, "Transaction where appName = 'pvh.mpm.metapack.net_BlackBox' and name = 'WebTransaction/WCF/XLogics.BlackBox.ServiceContracts.IBlackBoxContract.PrintParcel' and PrintOperation like '%Create%' and CarrierCode = 'DHL-15'");
        }

        [TestMethod]
        public void ParseAlerts_WithMultipleAlerts_ShouldParseAllCorrectly()
        {
            string testContent = @"nr_nrql_alerts = [
  {
    ""name"" = ""Alert 1""
    ""nrql_query"" = ""Query 1""
  },
  {
    ""name"" = ""Alert 2""
    ""nrql_query"" = ""Query 2 with [special] characters""
  }
]";

            var alerts = _parser.ParseAlerts(testContent);

            Assert.AreEqual(2, alerts.Count);
            Assert.AreEqual("Alert 1", alerts[0].Name);
            Assert.AreEqual("Alert 2", alerts[1].Name);
        }

        [TestMethod]
        public void ParseAlerts_WithCommentedAlerts_ShouldParseCorrectly()
        {
            string testContent = @"nr_nrql_alerts = [
  # First alert comment
  {
    ""name"" = ""Commented Alert""
    ""nrql_query"" = ""Test query""
  }
]";

            var alerts = _parser.ParseAlerts(testContent);

            Assert.AreEqual(1, alerts.Count);
            Assert.AreEqual("Commented Alert", alerts[0].Name);
        }

        [TestMethod]
        public void ReplaceNrqlAlertsSection_WithSpecialCharacters_ShouldPreserveContent()
        {
            // Arrange
            var parser = new HclParser();
            string originalContent = @"nr_nrql_alerts = [
  {
    ""name"" = ""Test Alert""
    ""nrql_query"" = ""SELECT filter(count(*), WHERE errorMessage != '[Error] Shipping definition  not found.') FROM Transaction""
    ""severity"" = ""CRITICAL""
    ""enabled"" = ""true""
  }
]";

            var alerts = new List<NrqlAlert>
        {
            new NrqlAlert
            {
                Name = "Test Alert",
                NrqlQuery = "SELECT filter(count(*), WHERE errorMessage != '[Error] Shipping definition  not found.') FROM Transaction",
                Severity = "CRITICAL",
                Enabled = true
            }
        };

            var updatedContent = parser.ReplaceNrqlAlertsSection(originalContent, alerts);

            Assert.IsTrue(updatedContent.Contains("[Error] Shipping definition  not found."),
                "Special characters should be preserved");

            StringAssert.Contains(updatedContent, "\"nrql_query\" = \"SELECT filter(count(*), WHERE errorMessage != '[Error] Shipping definition  not found.')");
        }



        public string ReplaceNrqlAlertsSection(string originalContent, List<NrqlAlert> alerts)
        {
            var regex = new Regex(@"nr_nrql_alerts\s*=\s*\[.*?\]", RegexOptions.Singleline);

            var updatedAlertsSection = SerializeAlerts(alerts, true);

            return regex.Replace(originalContent, updatedAlertsSection);
        }

        public string SerializeAlerts(List<NrqlAlert> alerts, bool ignoreEmptyValues)
        {
            var sb = new StringBuilder();
            sb.AppendLine("nr_nrql_alerts = [");

            foreach (var alert in alerts)
            {
                sb.AppendLine("  {");

                _parser.AppendIfNotEmpty(sb, "name", EscapeHclString(alert.Name), ignoreEmptyValues);
                _parser.AppendIfNotEmpty(sb, "nrql_query", EscapeHclString(alert.NrqlQuery), ignoreEmptyValues);

                sb.AppendLine("  },");
            }

            sb.AppendLine("]");
            return sb.ToString();
        }

        private string EscapeHclString(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Escape backslashes first
            input = input.Replace(@"\", @"\\");

            // Escape quotes
            input = input.Replace("\"", "\\\"");

            return input;
        }
    }
}

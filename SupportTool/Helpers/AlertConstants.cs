using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SupportTool.Helpers
{
    public static class AlertConstants
    {
        public static readonly string[] Severities = new[]
        {
        "CRITICAL",
        "WARNING",
        "INFO"
    };

        public static readonly string[] AggregationMethods = new[]
        {
        "EVENT_FLOW",
        "EVENT_TIMER",
        "CADENCE"
    };

        public static readonly string[] CriticalOperators = new[]
        {
        "ABOVE",
        "BELOW",
        "EQUALS"
    };

        public static readonly string[] ThresholdOccurrences = new[]
        {
        "ALL",
        "AT_LEAST_ONCE"
    };
    }
}

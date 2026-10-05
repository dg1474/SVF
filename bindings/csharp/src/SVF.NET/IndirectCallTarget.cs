using System;
using System.Collections.Generic;

namespace SVF.NET
{
    /// <summary>
    /// Represents an indirect function call site and its resolved target functions.
    /// </summary>
    public class IndirectCallTarget
    {
        /// <summary>
        /// Gets or sets the call site identifier or signature description.
        /// </summary>
        public string CallSite { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of resolved target function names.
        /// </summary>
        public List<string> Targets { get; set; } = new List<string>();

        /// <summary>
        /// Returns a formatted string representation of the indirect call targets.
        /// </summary>
        public override string ToString()
        {
            return $"{CallSite} -> [{string.Join(", ", Targets)}]";
        }
    }

    /// <summary>
    /// Result metrics and data returned by an Andersen pointer analysis execution.
    /// </summary>
    public class AndersenResult
    {
        /// <summary>
        /// Gets or sets whether the analysis completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the total number of points-to sets computed.
        /// </summary>
        public int PointsToSetCount { get; set; }

        /// <summary>
        /// Gets or sets the resolved indirect function call targets.
        /// </summary>
        public List<IndirectCallTarget> IndirectCallTargets { get; set; } = new List<IndirectCallTarget>();

        /// <summary>
        /// Gets or sets the raw standard output produced by the SVF analysis.
        /// </summary>
        public string RawOutput { get; set; } = string.Empty;
    }
}

using System;

namespace EstMepAddins.DimensionChecker.Models
{
    /// <summary>
    /// The ESTmep side of the add-in. The UI only talks to this interface, so everything that touches
    /// FabricationAPI.dll lives in the add-in project and the UI can be built and tested without ESTmep.
    /// </summary>
    public interface IItemDrawingService
    {
        /// <summary>The loaded database's item content folder, or null if it can't be determined.</summary>
        string? ItemContentFolder { get; }

        /// <summary>
        /// Loads the ITM, draws every product list size into the current job, and returns the
        /// dimensions ESTmep reports for each size. Must be called on ESTmep's UI thread.
        /// </summary>
        ItemSnapshot DrawAllSizes(string itmPath, string units, IProgress<string>? progress);
    }
}

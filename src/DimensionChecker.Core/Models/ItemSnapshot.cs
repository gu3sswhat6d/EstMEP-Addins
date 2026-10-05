using System.Collections.Generic;

namespace EstMepAddins.DimensionChecker.Models
{
    /// <summary>
    /// Everything we read off one ITM: its identity and the dimensions of every product list size
    /// as they were after that size was loaded and drawn. This is the "drawn" side of the comparison.
    /// </summary>
    public sealed class ItemSnapshot
    {
        public string ItemName { get; set; } = "";
        public string ItemPath { get; set; } = "";

        /// <summary>Units the dimension values are expressed in, e.g. "inches" or "millimeters".</summary>
        public string Units { get; set; } = "inches";

        public List<ItemSizeSnapshot> Sizes { get; set; } = new List<ItemSizeSnapshot>();
    }

    public sealed class ItemSizeSnapshot
    {
        /// <summary>Product list entry name (the size name shown in ESTmep, e.g. "2in" or "4x2").</summary>
        public string SizeName { get; set; } = "";

        /// <summary>Zero-based row index in the ITM's product list; -1 if the ITM has no product list.</summary>
        public int ProductListIndex { get; set; }

        /// <summary>Product list database id / order number, if the row has one.</summary>
        public string? DatabaseId { get; set; }

        /// <summary>True when the size was added to the job successfully.</summary>
        public bool Drawn { get; set; }

        public List<NamedValue> Dimensions { get; set; } = new List<NamedValue>();

        /// <summary>Item options (e.g. "Length", "Angle") after the size was loaded.</summary>
        public List<NamedValue> Options { get; set; } = new List<NamedValue>();

        /// <summary>Product list data columns other than dimensions (alias, weight, etc.).</summary>
        public List<NamedValue> ProductData { get; set; } = new List<NamedValue>();
    }

    public sealed class NamedValue
    {
        public NamedValue() { }

        public NamedValue(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
    }
}

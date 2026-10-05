using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Fabrication;
using Autodesk.Fabrication.Content;
using EstMepAddins.DimensionChecker.Models;

namespace EstMepAddins.DimensionChecker.AddIn
{
    /// <summary>
    /// Everything that talks to FabricationAPI.dll. Calls whose shape is documented in Autodesk's
    /// samples (ContentManager.LoadItem, Item.ProductList / LoadProductListEntry / Update,
    /// Job.AddItem) are made directly. Values whose property names vary between Fabrication
    /// releases (dimension and option values, product list columns) are read by reflection so the
    /// add-in keeps working if a name moves.
    /// </summary>
    public sealed class FabricationItemDrawingService : IItemDrawingService
    {
        public string? ItemContentFolder => FindItemContentFolder();

        public ItemSnapshot DrawAllSizes(string itmPath, string units, IProgress<string>? progress)
        {
            if (!File.Exists(itmPath))
                throw new FileNotFoundException("ITM not found.", itmPath);

            var snapshot = new ItemSnapshot
            {
                ItemName = Path.GetFileNameWithoutExtension(itmPath),
                ItemPath = itmPath,
                Units = units,
            };

            var rows = ReadProductListRows(itmPath);
            if (rows.Count == 0)
            {
                // No product list: the ITM has a single size, defined by its own dimensions.
                progress?.Report("The ITM has no product list; drawing its single size…");
                snapshot.Sizes.Add(DrawSize(itmPath, -1, "(no product list)", null));
                return snapshot;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                progress?.Report($"Drawing size {i + 1} of {rows.Count}: {rows[i].Name}");
                snapshot.Sizes.Add(DrawSize(itmPath, i, rows[i].Name, rows[i].Data));
            }

            return snapshot;
        }

        private sealed class RowInfo
        {
            public string Name = "";
            public List<NamedValue> Data = new List<NamedValue>();
        }

        private static List<RowInfo> ReadProductListRows(string itmPath)
        {
            var rows = new List<RowInfo>();
            using (var item = ContentManager.LoadItem(itmPath))
            {
                if (item == null)
                    throw new InvalidOperationException("ESTmep could not load " + itmPath + ".");

                if (!(item.ProductList is ItemProductList productList) || productList.Rows == null)
                    return rows;

                var index = 0;
                foreach (ItemProductListDataRow row in productList.Rows)
                {
                    var name = GetString(row, "Name");
                    rows.Add(new RowInfo
                    {
                        Name = string.IsNullOrWhiteSpace(name) ? "Row " + (index + 1) : name!,
                        Data = ReadAllValues(row),
                    });
                    index++;
                }
            }
            return rows;
        }

        private static ItemSizeSnapshot DrawSize(string itmPath, int rowIndex, string sizeName, List<NamedValue>? rowData)
        {
            var size = new ItemSizeSnapshot { SizeName = sizeName, ProductListIndex = rowIndex };
            if (rowData != null)
            {
                size.ProductData = rowData.Where(v => !IsDimensionColumn(v.Name)).ToList();
                size.DatabaseId = rowData.FirstOrDefault(v => v.Name == "DatabaseId")?.Value;
            }

            // A fresh copy per size: Job.AddItem copies the item into the job, and loading a new
            // product list entry on an item that is already in the job would resize that one instead.
            using (var item = ContentManager.LoadItem(itmPath))
            {
                if (item == null)
                    throw new InvalidOperationException("ESTmep could not load " + itmPath + ".");

                if (rowIndex >= 0)
                    item.LoadProductListEntry(rowIndex);
                item.Update();

                size.Dimensions = ReadNamedValues(item.Dimensions);
                size.Options = ReadNamedValues(GetProperty(item, "Options"));

                // Product list dimension columns (where the ITM keeps per-size values) are the
                // ones the user will edit, so keep them alongside the item dimensions.
                if (rowData != null)
                {
                    foreach (var column in rowData.Where(v => IsDimensionColumn(v.Name)))
                    {
                        if (!size.Dimensions.Any(d => string.Equals(d.Name, column.Name, StringComparison.OrdinalIgnoreCase)))
                            size.Dimensions.Add(new NamedValue("Product list: " + column.Name, column.Value));
                    }
                }

                var result = Job.AddItem(item);
                size.Drawn = result != null && string.Equals(
                    Convert.ToString(GetProperty(result, "Status"), CultureInfo.InvariantCulture), "Succeeded", StringComparison.OrdinalIgnoreCase);
            }

            return size;
        }

        // Product list rows expose their dimension columns as a nested collection; ReadAllValues
        // flattens those to "Dimensions.<name>".
        private static bool IsDimensionColumn(string name) => name.StartsWith("Dimensions.", StringComparison.Ordinal);

        /// <summary>Reads an Item.Dimensions / Item.Options style collection into name/value pairs.</summary>
        private static List<NamedValue> ReadNamedValues(object? collection)
        {
            var values = new List<NamedValue>();
            if (!(collection is IEnumerable enumerable) || collection is string)
                return values;

            foreach (var entry in enumerable)
            {
                if (entry == null) continue;
                var name = EntryName(entry);
                var value = EntryValue(entry);
                if (!string.IsNullOrEmpty(name) && value != null)
                    values.Add(new NamedValue(name!, value));
            }
            return values;
        }

        /// <summary>Simple-typed public properties of an object, plus one level of named sub-collections.</summary>
        private static List<NamedValue> ReadAllValues(object source)
        {
            var values = new List<NamedValue>();
            foreach (var property in source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0) continue;
                object? value;
                try { value = property.GetValue(source); }
                catch (Exception) { continue; }
                if (value == null) continue;

                if (IsSimple(value))
                {
                    values.Add(new NamedValue(property.Name, Format(value)));
                }
                else if (value is IEnumerable nested && !(value is string))
                {
                    foreach (var pair in ReadNamedValues(nested))
                        values.Add(new NamedValue(property.Name + "." + pair.Name, pair.Value));
                }
            }
            return values;
        }

        private static string? EntryName(object entry)
        {
            var name = GetString(entry, "Name");
            if (!string.IsNullOrEmpty(name)) return name;
            var definition = GetProperty(entry, "Definition");
            return definition == null ? null : GetString(definition, "Name");
        }

        private static string? EntryValue(object entry)
        {
            foreach (var property in new[] { "Value", "SelectedValue", "Text" })
            {
                var value = GetProperty(entry, property);
                if (value != null && IsSimple(value))
                    return Format(value);
            }
            return null;
        }

        private static string? FindItemContentFolder()
        {
            var application = Type.GetType("Autodesk.Fabrication.ApplicationServices.Application, FabricationAPI");
            if (application == null) return null;

            var paths = application.GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.PropertyType == typeof(string))
                .Select(p => new { p.Name, Value = SafeGetStatic(p) })
                .Where(p => !string.IsNullOrEmpty(p.Value) && Directory.Exists(p.Value))
                .ToList();

            var itemPath = paths.FirstOrDefault(p => p.Name.IndexOf("Item", StringComparison.OrdinalIgnoreCase) >= 0);
            if (itemPath != null) return itemPath.Value;

            // Standard content layout: <content>\Database next to <content>\Items.
            var database = paths.FirstOrDefault(p => p.Name.IndexOf("Database", StringComparison.OrdinalIgnoreCase) >= 0);
            if (database != null)
            {
                var parent = Directory.GetParent(database.Value!.TrimEnd('\\', '/'));
                var items = parent == null ? null : Path.Combine(parent.FullName, "Items");
                if (items != null && Directory.Exists(items)) return items;
            }
            return null;
        }

        private static string? SafeGetStatic(PropertyInfo property)
        {
            try { return property.GetValue(null) as string; }
            catch (Exception) { return null; }
        }

        private static object? GetProperty(object source, string name)
        {
            var property = source.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || property.GetIndexParameters().Length > 0) return null;
            try { return property.GetValue(source); }
            catch (Exception) { return null; }
        }

        private static string? GetString(object source, string name)
        {
            var value = GetProperty(source, name);
            return value == null ? null : Format(value);
        }

        private static bool IsSimple(object value) =>
            value is string || value is bool || value is Enum || value.GetType().IsPrimitive || value is decimal;

        private static string Format(object value) =>
            value is double d ? d.ToString("0.######", CultureInfo.InvariantCulture)
            : value is float f ? f.ToString("0.######", CultureInfo.InvariantCulture)
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }
}

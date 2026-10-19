using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace WindowsFormsApp1
{
    /// <summary>
    /// Minimal .xlsx reader/writer (one sheet, plain values) using only the zip + XML in the framework.
    /// ponytail: no styles, formulas or multiple sheets; switch to ClosedXML if those are ever needed.
    /// </summary>
    static class Xlsx
    {
        static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
        const string DocRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public static void Write(string path, IEnumerable<object[]> rows)
        {
            var sheet = new XElement(Ns + "worksheet",
                new XElement(Ns + "sheetData",
                    rows.Select((row, r) => new XElement(Ns + "row", new XAttribute("r", r + 1),
                        row.Select((v, c) => Cell(ColName(c) + (r + 1), v))))));

            using (var zip = new ZipArchive(File.Create(path), ZipArchiveMode.Create))
            {
                Put(zip, "[Content_Types].xml",
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                    "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                    "</Types>");
                Put(zip, "_rels/.rels",
                    $"<Relationships xmlns=\"{Rel}\"><Relationship Id=\"rId1\" Type=\"{DocRel}/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                Put(zip, "xl/workbook.xml",
                    $"<workbook xmlns=\"{Ns}\" xmlns:r=\"{DocRel}\"><sheets><sheet name=\"Guests\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                Put(zip, "xl/_rels/workbook.xml.rels",
                    $"<Relationships xmlns=\"{Rel}\"><Relationship Id=\"rId1\" Type=\"{DocRel}/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
                Put(zip, "xl/worksheets/sheet1.xml", sheet.ToString(SaveOptions.DisableFormatting));
            }
        }

        /// <summary>Reads the first sheet as rows of text. Missing cells come back as "".</summary>
        public static List<string[]> Read(string path)
        {
            using (var zip = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read))
            {
                var shared = new List<string>();
                var sharedEntry = zip.GetEntry("xl/sharedStrings.xml");
                if (sharedEntry != null)
                    using (var s = sharedEntry.Open())
                        shared = XDocument.Load(s).Root.Elements(Ns + "si")
                            .Select(si => string.Concat(si.Descendants(Ns + "t").Select(t => (string)t))).ToList();

                // ponytail: assumes the first sheet is sheet1.xml, which is what Excel writes
                var sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml") ?? throw new InvalidDataException("The file has no worksheet.");
                XDocument doc;
                using (var s = sheetEntry.Open()) doc = XDocument.Load(s);

                var rows = new List<string[]>();
                foreach (var row in doc.Descendants(Ns + "row"))
                {
                    var cells = new List<string>();
                    foreach (var c in row.Elements(Ns + "c"))
                    {
                        string r = (string)c.Attribute("r");
                        int col = r == null ? cells.Count : ColIndex(r);
                        while (cells.Count < col) cells.Add(""); // Excel omits empty cells
                        cells.Add(Value(c, shared));
                    }
                    rows.Add(cells.ToArray());
                }
                return rows;
            }
        }

        static XElement Cell(string r, object v) =>
            v is int || v is long || v is decimal || v is double
                ? new XElement(Ns + "c", new XAttribute("r", r),
                    new XElement(Ns + "v", Convert.ToString(v, CultureInfo.InvariantCulture)))
                : new XElement(Ns + "c", new XAttribute("r", r), new XAttribute("t", "inlineStr"),
                    new XElement(Ns + "is", new XElement(Ns + "t", Convert.ToString(v) ?? "")));

        static string Value(XElement c, List<string> shared)
        {
            string type = (string)c.Attribute("t");
            string v = (string)c.Element(Ns + "v");
            if (type == "s") return int.TryParse(v, out int i) && i < shared.Count ? shared[i] : "";
            if (type == "inlineStr") return string.Concat(c.Descendants(Ns + "t").Select(t => (string)t));
            return v ?? "";
        }

        static string ColName(int index)
        {
            string name = "";
            for (int n = index + 1; n > 0; n = (n - 1) / 26)
                name = (char)('A' + (n - 1) % 26) + name;
            return name;
        }

        static int ColIndex(string cellRef)
        {
            int n = 0;
            foreach (char ch in cellRef.TakeWhile(char.IsLetter))
                n = n * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
            return n - 1;
        }

        static void Put(ZipArchive zip, string name, string xml)
        {
            using (var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)))
                w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" + xml);
        }
    }
}

using HtmlAgilityPack;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using UglyToad.PdfPig;

using iText.Kernel.Pdf; // for writing PDF
using iText.Layout;
using iText.Layout.Element;
using iText.IO.Font.Constants;
using iText.Kernel.Font;

namespace Files_Search
{
    public partial class ExportPdf : Form
    {
        public ExportPdf()
        {
            InitializeComponent();
            _ = LoadDataFromMongoAsync();
        }

        private void Form2_Load(object sender, EventArgs e)
        {
        }

        private async void btn_Download_Click(object sender, EventArgs e)
        {
            string url = txtUrl.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;

            try
            {
                string pdfUrl = await FindFirstPdfUrl(url);
                if (string.IsNullOrEmpty(pdfUrl))
                {
                    MessageBox.Show("No PDF found at the URL.");
                    return;
                }

                string pdfPath = Path.Combine(Path.GetTempPath(), "downloaded.pdf");

                using (var client = new WebClient())
                {
                    await client.DownloadFileTaskAsync(new Uri(pdfUrl), pdfPath);
                }

                string text = ExtractTextFromPdf(pdfPath);
                string first100Words = GetFirstNWords(text, 100);

                FileInfo fileInfo = new FileInfo(pdfPath);
                double fileSizeInKb = fileInfo.Length / 1024.0;
                string downloadDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                var result = new BsonDocument
                {
                    { "Url", url },
                    { "PdfUrl", pdfUrl },
                    { "ExtractedText", first100Words },
                    { "DownloadDate", downloadDate },
                    { "FileSizeKb", fileSizeInKb }
                };

                var collection = GetMongoCollection();
                await collection.InsertOneAsync(result);

                await LoadDataFromMongoAsync(); // Refresh DataGridView
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private async Task<string> FindFirstPdfUrl(string websiteUrl)
        {
            var web = new HtmlWeb();
            var doc = await Task.Run(() => web.Load(websiteUrl));

            var pdfLink = doc.DocumentNode.SelectNodes("//a[@href]")
                ?.Select(node => node.GetAttributeValue("href", null))
                ?.FirstOrDefault(href => href != null && href.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(pdfLink))
            {
                return pdfLink.StartsWith("http") ? pdfLink : new Uri(new Uri(websiteUrl), pdfLink).ToString();
            }
            return null;
        }

        private string ExtractTextFromPdf(string path)
        {
            using (UglyToad.PdfPig.PdfDocument pdf = UglyToad.PdfPig.PdfDocument.Open(path))
            {
                return string.Join(" ", pdf.GetPages().Select(p => p.Text));
            }
        }

        private string GetFirstNWords(string input, int wordCount)
        {
            var words = Regex.Split(input, @"\W+").Where(w => !string.IsNullOrWhiteSpace(w)).Take(wordCount);
            return string.Join(" ", words);
        }

        private IMongoCollection<BsonDocument> GetMongoCollection()
        {
            var client = new MongoClient("mongodb://localhost:27017");
            var db = client.GetDatabase("PdfExtractorDB");
            return db.GetCollection<BsonDocument>("ExtractedTexts");
        }

        private async Task LoadDataFromMongoAsync()
        {
            try
            {
                var collection = GetMongoCollection();
                var documents = await collection.Find(new BsonDocument()).ToListAsync();

                dataGridView1.Rows.Clear();
                dataGridView1.Columns.Clear();

                dataGridView1.Columns.Add("Url", "URL");
                dataGridView1.Columns.Add("PdfUrl", "PDF URL");
                dataGridView1.Columns.Add("ExtractedText", "Extracted Text");
                dataGridView1.Columns.Add("DownloadDate", "Download Date");
                dataGridView1.Columns.Add("FileSizeKb", "File Size (KB)");

                foreach (var doc in documents)
                {
                    dataGridView1.Rows.Add(
                        doc.GetValue("Url", "").AsString,
                        doc.GetValue("PdfUrl", "").AsString,
                        doc.GetValue("ExtractedText", "").AsString,
                        doc.GetValue("DownloadDate", "").AsString,
                        doc.GetValue("FileSizeKb", 0).ToDouble()
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load data: " + ex.Message);
            }
        }

        private void btnExportPdf_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a row first.");
                return;
            }

            var row = dataGridView1.SelectedRows[0];
            string url = row.Cells["Url"].Value?.ToString();
            string pdfUrl = row.Cells["PdfUrl"].Value?.ToString();
            string extractedText = row.Cells["ExtractedText"].Value?.ToString();
            string downloadDate = row.Cells["DownloadDate"].Value?.ToString();
            string fileSize = row.Cells["FileSizeKb"].Value?.ToString();

            // Save dialog to choose where to save the PDF
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "PDF files (*.pdf)|*.pdf";
                sfd.FileName = "ExportedData.pdf";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    string filePath = sfd.FileName;

                    // Remove CompressionLevel property (no longer needed)
                    using (var writer = new PdfWriter(filePath))
                    {
                        // Fully qualify the PdfDocument class to avoid ambiguity
                        using (var pdfDoc = new iText.Kernel.Pdf.PdfDocument(writer))
                        {
                            var document = new Document(pdfDoc);

                            // Optional: Set a bold font
                            var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

                            document.Add(new Paragraph("Exported PDF Information")
                                .SetFont(boldFont)
                                .SetFontSize(16));

                            document.Add(new Paragraph($"URL: {url}"));
                            document.Add(new Paragraph($"PDF URL: {pdfUrl}"));
                            document.Add(new Paragraph($"Download Date: {downloadDate}"));
                            document.Add(new Paragraph($"File Size: {fileSize} KB"));
                            document.Add(new Paragraph("Extracted Text (First 100 Words):"));
                            document.Add(new Paragraph(extractedText));

                            document.Close();
                        }
                    }

                    MessageBox.Show("PDF exported successfully.");
                }
            }
        }
    }
}
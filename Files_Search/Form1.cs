using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using DocumentFormat.OpenXml.Packaging;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;

namespace Files_Search
{
    public partial class Form1 : Form
    {
        private string matchedFilePath = null;
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            labelResult.Click += labelResult_Click;
        }

        private string ReadWordDoc(string filepath)
        {
            using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(filepath, false))
            {
                return wordDoc.MainDocumentPart.Document.Body.InnerText;
            }
        }

        private string ReadPdf(string filePath)
        {
            StringBuilder text = new StringBuilder();

            using (PdfReader reader = new PdfReader(filePath))
            {
                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    text.Append(PdfTextExtractor.GetTextFromPage(reader, i));
                }
            }

            return text.ToString();
        }

        private void labelResult_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(matchedFilePath) && File.Exists(matchedFilePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(matchedFilePath)
                {
                    UseShellExecute = true
                });
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // 1. Folder path where files are stored
            string folderPath = @"D:\Advance DBMS\Files_Search - Copy\sear"; // 🔁 Change this if needed

            // 2. Get user selections from combo boxes
            string searchBy = comboSearchBy.SelectedItem?.ToString();
            string matchType = comboMatchType.SelectedItem?.ToString();

            // 3. Get search term from user
            string searchTerm = Interaction.InputBox("Enter search term:", "Search").Trim();

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                MessageBox.Show("Please enter a search term.");
                return;
            }

            // 4. Get all files in the folder (you can filter *.docx if needed)
            string[] files = Directory.GetFiles(folderPath);
            string matchedFile = null;

            // 5. Search based on title
            if (searchBy == "Search by Title")
            {
                foreach (string file in files)
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(file);

                    if (matchType == "Exactly" && fileName.Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedFile = file;
                        break;
                    }
                    else if (matchType == "Similar" && fileName.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchedFile = file;
                        break;
                    }
                }
            }

            // 6. Search inside document content
            else if (searchBy == "Search in Document")
            {
                foreach (string file in files)
                {
                    try
                    {
                        string content = "";

                        if (System.IO.Path.GetExtension(file).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                        {
                            content = File.ReadAllText(file);
                        }
                        else if (System.IO.Path.GetExtension(file).Equals(".docx", StringComparison.OrdinalIgnoreCase))
                        {
                            content = ReadWordDoc(file);
                        }
                        else if (System.IO.Path.GetExtension(file).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                        {
                            content = ReadPdf(file);
                        }
                        else
                        {
                            continue;
                        }

                        if (matchType == "Exactly" && content.Contains(searchTerm))
                        {
                            matchedFile = file;
                            break;
                        }
                        else if (matchType == "Similar" && content.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchedFile = file;
                            break;
                        }
                    }
                    catch
                    {
                        // Skip unreadable files (like binary or locked files)
                        continue;
                    }
                }
            }

            // 7. Show result and allow file open when clicked
            if (matchedFile != null)
            {
                // Store matched file path
                matchedFilePath = matchedFile;

                // Update label to show match result and make it clickable
                labelResult.Text = $"Match found: {System.IO.Path.GetFileNameWithoutExtension(matchedFile)} (Click to open)";

                // Change the color and cursor to indicate it can be clicked
                labelResult.ForeColor = Color.Blue;
                labelResult.Cursor = Cursors.Hand;
            }
            else
            {
                labelResult.Text = "No match found.";
                matchedFilePath = null;
                labelResult.ForeColor = Color.Black;
                labelResult.Cursor = Cursors.Default;
            }
        }
    }
}

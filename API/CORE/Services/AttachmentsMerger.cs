using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace API.CORE.Services
{
    /// <summary>
    /// Fusionne les documents joints A LA FIN du PDF rendu.
    /// </summary>
    /// <remarks>
    /// Le moteur reste sans etat : les documents joints arrivent dans la requete, en base64 (data URL ou
    /// base64 nu), et ne sont jamais stockes. Un PDF est importe page par page (le texte reste du texte),
    /// une image devient une page A4 pleine. Une piece illisible est ignoree : elle ne doit jamais faire
    /// echouer la generation du document.
    /// </remarks>
    public static class AttachmentsMerger
    {
        /// <summary>Ajoute les pieces jointes a la fin du PDF rendu. Renvoie le PDF inchange s'il n'y en a aucune.</summary>
        /// <param name="pdf">PDF produit par le moteur.</param>
        /// <param name="attachments">Pieces jointes en base64 (data URL acceptee), PDF ou image.</param>
        /// <param name="logger">Journalise les pieces ignorees.</param>
        public static byte[] Append(byte[] pdf, IEnumerable<string>? attachments, ILogger? logger = null)
        {
            var files = (attachments ?? Enumerable.Empty<string>())
                .Select(Decode)
                .Where(bytes => bytes is { Length: > 0 })
                .ToList();
            if (files.Count == 0) return pdf;

            PdfDocument document;
            try
            {
                using var source = new MemoryStream(pdf);
                document = PdfReader.Open(source, PdfDocumentOpenMode.Modify);
            }
            catch (Exception e)
            {
                // Sans document ouvrable, mieux vaut livrer le PDF nu que rien du tout.
                logger?.LogWarning(e, "Pieces jointes ignorees : le PDF rendu n'a pas pu etre reouvert.");
                return pdf;
            }

            using (document)
            {
                var appended = 0;
                foreach (var file in files)
                {
                    try
                    {
                        if (IsPdf(file!)) AppendPdf(document, file!);
                        else AppendImage(document, file!);
                        appended++;
                    }
                    catch (Exception e)
                    {
                        logger?.LogWarning(e, "Piece jointe ignoree : format illisible ou fichier protege.");
                    }
                }

                if (appended == 0) return pdf;

                using var output = new MemoryStream();
                document.Save(output, false);
                return output.ToArray();
            }
        }

        /// <summary>Importe toutes les pages d'un PDF joint (le contenu reste vectoriel).</summary>
        private static void AppendPdf(PdfDocument target, byte[] file)
        {
            using var stream = new MemoryStream(file);
            using var source = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
            foreach (var page in source.Pages) target.AddPage(page);
        }

        /// <summary>Ajoute une image en pleine page A4, proportions conservees et centree.</summary>
        private static void AppendImage(PdfDocument target, byte[] file)
        {
            var page = target.AddPage();
            page.Size = PdfSharp.PageSize.A4;

            using var stream = new MemoryStream(file);
            using var image = XImage.FromStream(stream);
            using var gfx = XGraphics.FromPdfPage(page);

            var scale = Math.Min(page.Width.Point / image.PixelWidth, page.Height.Point / image.PixelHeight);
            var width = image.PixelWidth * scale;
            var height = image.PixelHeight * scale;
            gfx.DrawImage(image, (page.Width.Point - width) / 2, (page.Height.Point - height) / 2, width, height);
        }

        /// <summary>Signature « %PDF » en tete de fichier — le type declare dans la data URL n'est pas fiable.</summary>
        private static bool IsPdf(byte[] file)
            => file.Length > 4 && file[0] == 0x25 && file[1] == 0x50 && file[2] == 0x44 && file[3] == 0x46;

        /// <summary>Decode une piece jointe base64, avec ou sans prefixe « data:…;base64, ».</summary>
        private static byte[]? Decode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var raw = value.Trim();
            var marker = raw.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
            if (marker >= 0) raw = raw[(marker + 7)..];
            try { return Convert.FromBase64String(raw); }
            catch { return null; }
        }
    }
}

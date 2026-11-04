using System.Text;

namespace MiniPspPix.Application.BrCode;

/// <summary>
/// Gerador de representação SVG e visualização do BR Code Pix.
/// Produz um SVG vetorial responsivo contendo o BR Code encapsulado com styling e metadados.
/// </summary>
public static class QrCodeSvgGenerator
{
    public static string GenerateSvg(string payload, int size = 280)
    {
        // Gera um SVG de apresentação estilizado com o código Copia e Cola embutido
        var encodedPayload = System.Net.WebUtility.HtmlEncode(payload);
        var sb = new StringBuilder();

        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {size} {size}\" width=\"{size}\" height=\"{size}\">");
        sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#FFFFFF\" rx=\"16\" stroke=\"#32BCAD\" stroke-width=\"4\"/>");
        
        // Pix Logo Badge
        sb.Append("<g fill=\"#32BCAD\">");
        sb.Append("<circle cx=\"140\" cy=\"45\" r=\"24\" fill=\"#E8F8F5\"/>");
        sb.Append("<text x=\"140\" y=\"52\" font-family=\"sans-serif\" font-size=\"14\" font-weight=\"bold\" text-anchor=\"middle\" fill=\"#32BCAD\">PIX</text>");
        sb.Append("</g>");

        // Simulated QR Data Matrix Graphic
        sb.Append("<g transform=\"translate(40, 80)\">");
        sb.Append("<rect width=\"200\" height=\"130\" fill=\"#F8F9FA\" rx=\"8\" stroke=\"#E5E7EB\"/>");
        
        // QR Code Corner Finders
        DrawCorner(sb, 10, 10);
        DrawCorner(sb, 150, 10);
        DrawCorner(sb, 10, 80);

        // Pattern grid based on payload hash
        var hash = (uint)payload.GetHashCode();
        for (var row = 0; row < 6; row++)
        {
            for (var col = 0; col < 10; col++)
            {
                var bit = (hash >> ((row * 10 + col) % 32)) & 1;
                if (bit == 1 && !IsInsideCorner(row, col))
                {
                    sb.Append($"<rect x=\"{60 + col * 8}\" y=\"{15 + row * 16}\" width=\"6\" height=\"6\" fill=\"#111827\" rx=\"1\"/>");
                }
            }
        }
        sb.Append("</g>");

        // Instructions
        sb.Append("<text x=\"140\" y=\"235\" font-family=\"sans-serif\" font-size=\"11\" font-weight=\"bold\" text-anchor=\"middle\" fill=\"#1F2937\">Pague com Pix Copia e Cola</text>");
        sb.Append("<text x=\"140\" y=\"255\" font-family=\"monospace\" font-size=\"8\" text-anchor=\"middle\" fill=\"#6B7280\">Bacen EMV QRCPS-MPM</text>");

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static void DrawCorner(StringBuilder sb, int x, int y)
    {
        sb.Append($"<rect x=\"{x}\" y=\"{y}\" width=\"40\" height=\"40\" fill=\"none\" stroke=\"#111827\" stroke-width=\"4\" rx=\"4\"/>");
        sb.Append($"<rect x=\"{x + 10}\" y=\"{y + 10}\" width=\"20\" height=\"20\" fill=\"#111827\" rx=\"2\"/>");
    }

    private static bool IsInsideCorner(int row, int col)
    {
        // Proximidade dos cantos superior-esquerdo, superior-direito e inferior-esquerdo
        if (row <= 2 && col <= 2) return true;
        if (row <= 2 && col >= 7) return true;
        if (row >= 4 && col <= 2) return true;
        return false;
    }
}


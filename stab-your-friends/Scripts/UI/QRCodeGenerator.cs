using Godot;
using QRCoder;

namespace StabYourFriends.UI;

public static class QRCodeGenerator
{
    public static ImageTexture Generate(string text, int pixelsPerModule = 10)
    {
        using var qrGenerator = new QRCoder.QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(text, QRCoder.QRCodeGenerator.ECCLevel.M);

        // Get the QR code matrix
        var moduleCount = qrCodeData.ModuleMatrix.Count;

        // Add quiet zone (4 modules on each side)
        int quietZone = 4;
        int totalSize = (moduleCount + quietZone * 2) * pixelsPerModule;

        var image = Image.CreateEmpty(totalSize, totalSize, false, Image.Format.Rgb8);

        // Fill with white
        image.Fill(Colors.White);

        // Draw the QR code modules
        for (int y = 0; y < moduleCount; y++)
        {
            for (int x = 0; x < moduleCount; x++)
            {
                if (qrCodeData.ModuleMatrix[y][x]) // Dark module
                {
                    int px = (x + quietZone) * pixelsPerModule;
                    int py = (y + quietZone) * pixelsPerModule;

                    for (int dy = 0; dy < pixelsPerModule; dy++)
                    {
                        for (int dx = 0; dx < pixelsPerModule; dx++)
                        {
                            image.SetPixel(px + dx, py + dy, Colors.Black);
                        }
                    }
                }
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}

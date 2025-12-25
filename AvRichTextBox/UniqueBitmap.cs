using Avalonia.Media.Imaging;

namespace AvRichTextBox;

public class UniqueBitmap(Bitmap ubmap, int w, int h, int cIndex)
{
   internal Bitmap UBitmap = ubmap;
    internal int MaxWidth = w;
    internal int MaxHeight = h;
    internal int ConsecutiveIndex = cIndex;

}


using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media.Imaging;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AvRichTextBox;

public class UniqueBitmap(Bitmap ubmap, int w, int h, int cIndex)
{
   internal Bitmap UBitmap = ubmap;
    internal int MaxWidth = w;
    internal int MaxHeight = h;
    internal int ConsecutiveIndex = cIndex;

}


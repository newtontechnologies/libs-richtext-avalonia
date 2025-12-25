using Avalonia;
using Avalonia.Media;
using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using static AvRichTextBox.HelperMethods;
using Avalonia.Media.Imaging;
using Avalonia.Controls;
using System.IO;

namespace AvRichTextBox;

internal static partial class RtfConversions
{
   internal static string GetRtfFromFlowDocument(FlowDocument fdoc)
   {
      var sb = new StringBuilder();

      //Build font map
      var fontMap = new Dictionary<string, int>();
      var colorMap = new Dictionary<Color, int>();
      sb.Append(GetFontAndColorTables(fdoc.Blocks, ref fontMap, ref colorMap));

      string margl = Math.Round(PixToTwip(fdoc.PagePadding.Left)).ToString();
      string margr = Math.Round(PixToTwip(fdoc.PagePadding.Right)).ToString();
      string margt = Math.Round(PixToTwip(fdoc.PagePadding.Top)).ToString();
      string margb = Math.Round(PixToTwip(fdoc.PagePadding.Bottom)).ToString();
      sb.Append(@$"\margl{margl}\margr{margr}\margt{margt}\margb{margb}");

      bool boldOn = false;
      bool italicOn = false;
      bool underlineOn = false;
      int currentLang = 1033;

      foreach (Block block in fdoc.Blocks)
      {
         if (block.GetType() == typeof(Paragraph))
         {
            Paragraph p = (Paragraph)block;
            sb.Append(@"\pard");
            sb.Append(p.TextAlignment switch 
            { 
               TextAlignment.Center => @"\qc", 
               TextAlignment.Left => @"\ql", 
               TextAlignment.Right => @"\qr", 
               TextAlignment.Justify => @"\qj", 
               _=> @"\ql"
            });


            double maxHeight = p.Inlines.Max(il => il.IsRun ? ((EditableRun)il).FontSize : p.LineHeight);
            double lineHeightPx = maxHeight == 0 ? 0 : (int)(p.LineHeight / maxHeight * 2 * 240D);
            //Debug.WriteLine("\nlineheightPx = " + lineHeightPx + "\nmaxHeight= " + maxHeight + "\nlineHeight = " + p.LineHeight);

            sb.Append(@$"\sl{lineHeightPx}\slmult0");

            if (p.BorderBrush != null && p.BorderBrush.Color != Colors.Transparent)
            {
               int brdrColIdx = 0;
               if (p.BorderBrush is SolidColorBrush borderBrush && colorMap.TryGetValue(borderBrush.Color, out int colorIndexF))
                  brdrColIdx = colorIndexF;

               string leftBorderWidth = PixToTwip(p.BorderThickness.Left).ToString();
               string rightBorderWidth = PixToTwip(p.BorderThickness.Right).ToString();
               string topBorderWidth = PixToTwip(p.BorderThickness.Top).ToString();
               string bottomBorderWidth = PixToTwip(p.BorderThickness.Bottom).ToString();
               sb.Append(@$"\brdrt\brdrs\brdrw{topBorderWidth}\brdrcf{brdrColIdx}");
               sb.Append(@$"\brdrl\brdrs\brdrw{leftBorderWidth}\brdrcf{brdrColIdx}");
               sb.Append(@$"\brdrb\brdrs\brdrw{bottomBorderWidth}\brdrcf{brdrColIdx}");
               sb.Append(@$"\brdrr\brdrs\brdrw{rightBorderWidth}\brdrcf{brdrColIdx}");
            }

            if (p.Background != null && p.Background.Color != Colors.Transparent)
            {
               int bkColIdx = 0;
               if (p.Background is SolidColorBrush backgroundBrush && colorMap.TryGetValue(backgroundBrush.Color, out int colorIndexF))
                  bkColIdx = colorIndexF;
               sb.Append(@$"\cbpat{bkColIdx}");
            }

            foreach (IEditable ied in p.Inlines)
            sb.Append(GetIEditableRtf(ied, ref boldOn, ref italicOn, ref underlineOn, ref currentLang, fontMap, colorMap));

            sb.Append(@"\par ");
         }
      }
      sb.Remove(sb.Length - 5, 5);  // remove final \par
      sb.Append('}');
      return sb.ToString();
   }

   internal static string GetRtfFromInlines(List<IEditable> inlines)
   {
      var sb = new StringBuilder();

      //Build font map
      var fontMap = new Dictionary<string, int>();
      var colorMap = new Dictionary<Color, int>();
      sb.Append(GetFontAndColorTables(inlines, ref fontMap, ref colorMap));

      bool boldOn = false;
      bool italicOn = false;
      bool underlineOn = false;
      int currentLang = 1033;

      foreach (IEditable ied in inlines)
      {

         sb.Append(GetIEditableRtf(ied, ref boldOn, ref italicOn, ref underlineOn, ref currentLang, fontMap, colorMap));
         
         if (ied.InlineText.EndsWith('\r'))
            sb.Append(@"\par ");
      }
      
      sb.Append('}');

      return sb.ToString();
   }

   private static string GetIEditableRtf(IEditable ied, ref bool boldOn, ref bool italicOn, ref bool underlineOn, ref int currentLang, Dictionary<string, int> fontMap, Dictionary<Color, int> colorMap)
   {
      StringBuilder iedSb = new();

      if (ied.GetType() == typeof(EditableLineBreak)) return @"\line";


      if (ied.GetType() == typeof(EditableInlineUiContainer))
      {
         EditableInlineUiContainer eIuc = (EditableInlineUiContainer)ied;
         if (eIuc.Child != null)
         {
            if (eIuc.Child.GetType() == typeof(Image))
            {
               Image? thisImg = eIuc.Child as Image;
               Bitmap imgbitmap = (Bitmap)thisImg!.Source!;

               int picw = imgbitmap.PixelSize.Width;
               int pich = imgbitmap.PixelSize.Height;
               int picwgoal = (int)PixToTwip(thisImg.Width);
               int pichgoal = (int)PixToTwip(thisImg.Height);

               using MemoryStream memoryStream = new();

               var renderTarget = new RenderTargetBitmap(new PixelSize(picw, pich));
               using (var context = renderTarget.CreateDrawingContext())
                  context.DrawImage(imgbitmap, new Rect(0, 0, picw, pich));
               
               renderTarget.Save(memoryStream);  // png by default
               memoryStream.Seek(0, SeekOrigin.Begin);

               byte[] imgbytes = new byte[memoryStream.Length];
               memoryStream.Read(imgbytes, 0, imgbytes.Length);

               // add image to rtf code:
               iedSb.AppendLine($@"{{\pict\pngblip\picw{picw}\pich{pich}\picwgoal{picwgoal}\pichgoal{pichgoal}");

               foreach (byte b in imgbytes)
                  iedSb.Append(b.ToString("x2"));  // hex encoding

               iedSb.AppendLine("}");

            }
         }
      }

      if (ied.GetType() == typeof(EditableRun))
      {
         EditableRun run = (EditableRun)ied;
         
         if (!boldOn && run.FontWeight == FontWeight.Bold) { iedSb.Append(@"\b "); boldOn = true; }
         if (!italicOn && run.FontStyle == FontStyle.Italic) { iedSb.Append(@"\i "); ; italicOn = true; }
         if (!underlineOn && run.TextDecorations == TextDecorations.Underline) { iedSb.Append(@"\ul "); ; underlineOn = true; }
         
         if (boldOn && run.FontWeight == FontWeight.Normal) { iedSb.Append(@"\b0 "); boldOn = false; }
         if (italicOn && run.FontStyle == FontStyle.Normal) { iedSb.Append(@"\i0 "); italicOn = false; }
         if (underlineOn && run.TextDecorations != TextDecorations.Underline) { iedSb.Append(@"\ul0 "); underlineOn = false; }

         if (run.FontSize > 0) iedSb.Append($@"\fs{(int)(run.FontSize * 2)} ");

         if (fontMap.TryGetValue(run.FontFamily.Name, out int fontIndex))
            iedSb.Append($@"\f{fontIndex} ");

         if (run.Foreground is SolidColorBrush foregroundBrush && colorMap.TryGetValue(foregroundBrush.Color, out int colorIndexF))
            iedSb.Append($@"\cf{colorIndexF} ");
         else
            iedSb.Append(@"\cf0 "); // Reset to default

         if (run.Background is SolidColorBrush backgroundBrush && backgroundBrush.Color != Colors.Transparent && colorMap.TryGetValue(backgroundBrush.Color, out int colorIndexB))
            iedSb.Append($@"\highlight{colorIndexB} ");
         else
            iedSb.Append(@"\highlight0 "); // Reset background to default

         if (!string.IsNullOrEmpty(run.Text))
            iedSb.Append(GetRtfRunText(run.Text!, ref currentLang));
      }

      return iedSb.ToString();
   }


   private static string GetFontAndColorTables(IEnumerable<Block> allBlocks, ref Dictionary<string, int> fontMap, ref Dictionary<Color, int> colorMap)
   {
      StringBuilder fontAndColorTableSb = new ();

      int fontIndex = 0;
      int colorIndex = 1;

      foreach (Paragraph par in allBlocks.Where(b=>b.IsParagraph))
      {
         if (par.BorderBrush is SolidColorBrush borderBrush && par.BorderBrush.Color != Colors.Transparent)
            if (!colorMap.ContainsKey(borderBrush.Color))
               colorMap[borderBrush.Color] = colorIndex++;

         if (par.Background is SolidColorBrush parBackground && par.Background.Color != Colors.Transparent)
            if (!colorMap.ContainsKey(parBackground.Color))
               colorMap[parBackground.Color] = colorIndex++;

      }

      foreach (IEditable ied in allBlocks.SelectMany(b => ((Paragraph)b).Inlines))
      {
         if (ied is EditableRun run)
         {
            if (run.FontFamily != null && !fontMap.ContainsKey(run.FontFamily.Name))
               fontMap[run.FontFamily.Name] = fontIndex++;

            if (run.Foreground is SolidColorBrush foregroundBrush)
               if (!colorMap.ContainsKey(foregroundBrush.Color))
                  colorMap[foregroundBrush.Color] = colorIndex++;

            if (run.Background is SolidColorBrush backgroundBrush)
               if (!colorMap.ContainsKey(backgroundBrush.Color))
                  colorMap[backgroundBrush.Color] = colorIndex++;
         }
      }
         
      fontAndColorTableSb.Append(@"{\rtf1\ansi\deff0 {\fonttbl");
      foreach (var kvp in fontMap)
         fontAndColorTableSb.Append($@"{{\f{kvp.Value}\fnil {kvp.Key};}}");
      fontAndColorTableSb.Append('}');

      fontAndColorTableSb.Append(@"{\colortbl;");
      foreach (var kvp in colorMap)
         fontAndColorTableSb.Append($@"\red{kvp.Key.R}\green{kvp.Key.G}\blue{kvp.Key.B};");
      fontAndColorTableSb.Append('}');

      return fontAndColorTableSb.ToString();

   }

   private static string GetFontAndColorTables(IEnumerable<IEditable> inlinesToMap, ref Dictionary<string, int> fontMap, ref Dictionary<Color, int> colorMap)
   {
   
      int fontIndex = 0;
      int colorIndex = 1;

      foreach (IEditable ied in inlinesToMap)
      {
         if (ied is EditableRun run)
         {
            if (run.FontFamily != null && !fontMap.ContainsKey(run.FontFamily.Name))
               fontMap[run.FontFamily.Name] = fontIndex++;

            if (run.Foreground is SolidColorBrush foregroundBrush)
               if (!colorMap.ContainsKey(foregroundBrush.Color))
                  colorMap[foregroundBrush.Color] = colorIndex++;

            if (run.Background is SolidColorBrush backgroundBrush)
               if (!colorMap.ContainsKey(backgroundBrush.Color))
                  colorMap[backgroundBrush.Color] = colorIndex++;
         }
      }

      StringBuilder fontAndColorTableSb = new();

      fontAndColorTableSb.Append(@"{\rtf1\ansi\deff0 {\fonttbl");
      foreach (var kvp in fontMap)
         fontAndColorTableSb.Append($@"{{\f{kvp.Value}\fnil {kvp.Key};}}");
      fontAndColorTableSb.Append('}');

      fontAndColorTableSb.Append(@"{\colortbl;");
      foreach (var kvp in colorMap)
         fontAndColorTableSb.Append($@"\red{kvp.Key.R}\green{kvp.Key.G}\blue{kvp.Key.B};");
      fontAndColorTableSb.Append('}');

      return fontAndColorTableSb.ToString();

   }


   private static string GetRtfRunText(string text, ref int currentLang)
   {
  
      StringBuilder sb = new();

      foreach (char c in text)
      {
         int newLang = GetLanguageForChar(c);

         if (newLang != currentLang)
         {
            sb.Append($@"\lang{newLang} ");
            currentLang = newLang;
         }

         if (c is '\\' or '{' or '}')
            sb.Append(@"\" + c); // RTF control characters
         else if (c > 127) // Non-ASCII (double-byte characters)
            sb.Append(@"\u" + (int)c + "?"); // Unicode escape
         else
            sb.Append(c);

      }
      return sb.ToString();
   }


}
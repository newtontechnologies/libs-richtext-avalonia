using Avalonia;
using Avalonia.Media;
using System.Text;
using System.Text.Json;

namespace AvRichTextBox;

public partial class FlowDocument
{
   /// <summary>
   /// Deterministic serialization of the document's model for unit tests.
   /// Intended for semantic equality checks across Undo/Redo.
   /// </summary>
   internal string SerializeForTests()
   {
      var sb = new StringBuilder();

      sb.AppendLine($"PagePadding={SerializeThickness(PagePadding)}");
      sb.AppendLine($"Selection={Selection.Start},{Selection.End},Bias={Selection.BiasForwardStart}/{Selection.BiasForwardEnd},Extend={SelectionExtendMode}");
      sb.AppendLine($"Blocks={Blocks.Count}");

      for (int bi = 0; bi < Blocks.Count; bi++)
      {
         if (Blocks[bi] is not Paragraph p)
         {
            sb.AppendLine($"B[{bi}]={Blocks[bi].GetType().Name}");
            continue;
         }

         sb.AppendLine($"P[{bi}]:Margin={SerializeThickness(p.Margin)};Bg={SerializeBrush(p.Background)};Border={SerializeBrush(p.BorderBrush)}@{SerializeThickness(p.BorderThickness)};FF={p.FontFamily?.Name};FS={p.FontSize};FW={p.FontWeight};FStyle={p.FontStyle};Align={p.TextAlignment};LH={p.LineHeight};LS={p.LineSpacing}");

         for (int ii = 0; ii < p.Inlines.Count; ii++)
         {
            var il = p.Inlines[ii];
            switch (il)
            {
               case EditableRun r:
                  sb.AppendLine($"  R[{ii}]:T={JsonSerializer.Serialize(r.InlineText)};FF={r.FontFamily?.Name};FS={r.FontSize};FW={r.FontWeight};FStyle={r.FontStyle};FG={SerializeBrush(r.Foreground)};BG={SerializeBrush(r.Background)};TD={SerializeTextDecorations(r.TextDecorations)};Base={r.BaselineAlignment};Stretch={r.FontStretch}");
                  break;
               case EditableLineBreak:
                  sb.AppendLine($"  LB[{ii}]");
                  break;
               case EditableInlineUiContainer uic:
                  sb.AppendLine($"  UI[{ii}]:Child={uic.Child?.GetType().Name};ImgNo={uic.ImageNo};H={uic.InlineHeight}");
                  break;
               default:
                  sb.AppendLine($"  I[{ii}]={il.GetType().Name}");
                  break;
            }
         }
      }

      return sb.ToString();
   }

   private static string SerializeThickness(Thickness t) => $"{t.Left},{t.Top},{t.Right},{t.Bottom}";

   private static string SerializeBrush(IBrush? b)
   {
      if (b is null) return "null";
      if (b is SolidColorBrush scb)
      {
         var c = scb.Color;
         return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
      }
      return b.GetType().Name;
   }

   private static string SerializeTextDecorations(TextDecorationCollection? td)
   {
      if (td is null) return "null";
      if (td == TextDecorations.Underline) return "Underline";
      if (td == TextDecorations.Strikethrough) return "Strikethrough";
      return td.Count.ToString();
   }
}



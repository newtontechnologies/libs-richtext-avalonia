using System;
using System.Collections.Generic;
using System.Linq;

namespace AvRichTextBox;

public partial class FlowDocument
{
   /// <summary>
   /// Cursor-like position inside an inline.
   /// Convention:
   /// - For EditableRun: CharIndex in [0..TextLength]. CharIndex==TextLength means "after the run".
   /// - For non-run inline: CharIndex in {0,1}. CharIndex==1 means "after the inline".
   /// </summary>
   internal readonly record struct TextPos(IEditable Inline, int CharIndex);

   internal TextPos GetTextPosFromGlobalIndex(int globalIndex)
   {
      if (Blocks.Count == 0) throw new InvalidOperationException("Document has no blocks.");

      int idx = Math.Max(0, globalIndex);
      int cursor = 0;

      foreach (var p in Blocks.OfType<Paragraph>())
      {
         if (p.Inlines.Count == 0) throw new InvalidOperationException("Paragraph must have at least one inline.");

         foreach (var il in p.Inlines)
         {
            int span = CursorSpanLength(il);
            if (idx < cursor + span)
               return new TextPos(il, idx - cursor);

            if (idx == cursor + span)
            {
               // Prefer "before next inline" if there is one; otherwise it's after this inline.
               int i = p.Inlines.IndexOf(il);
               if (i >= 0 && i < p.Inlines.Count - 1)
                  return new TextPos(p.Inlines[i + 1], 0);
               return new TextPos(il, span);
            }

            cursor += span;
         }

         // Paragraph boundary (1 char). Represent as start of next paragraph, if any.
         if (idx == cursor)
         {
            var next = GetNextParagraph(p);
            if (next != null)
               return new TextPos(next.Inlines[0], 0);
            // End of document: clamp to "after last inline"
            var last = p.Inlines[^1];
            return new TextPos(last, CursorSpanLength(last));
         }

         cursor += 1;
      }

      // Beyond end: clamp to end of last paragraph
      var lastPar = Blocks.OfType<Paragraph>().Last();
      var lastInline = lastPar.Inlines[^1];
      return new TextPos(lastInline, CursorSpanLength(lastInline));
   }

   internal int GetGlobalIndexFromTextPos(TextPos pos)
   {
      int cursor = 0;

      foreach (var p in Blocks.OfType<Paragraph>())
      {
         if (p.Inlines.Count == 0) throw new InvalidOperationException("Paragraph must have at least one inline.");

         foreach (var il in p.Inlines)
         {
            int span = CursorSpanLength(il);
            if (ReferenceEquals(il, pos.Inline))
               return cursor + Math.Clamp(pos.CharIndex, 0, span);

            cursor += span;
         }

         cursor += 1; // paragraph boundary
      }

      throw new InvalidOperationException("TextPos.Inline is not part of the document.");
   }

   private static int CursorSpanLength(IEditable inline)
   {
      if (inline is EditableRun r)
         return r.InlineText.Length;
      return 1;
   }
}



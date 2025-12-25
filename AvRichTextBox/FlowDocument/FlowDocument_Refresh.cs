using System;
using System.Collections.Generic;
using System.Linq;

namespace AvRichTextBox;

public partial class FlowDocument
{
   internal void RefreshAfterAtomicEdits(int fromBlockIndex, IReadOnlyList<Paragraph> paragraphs)
   {
      if (Blocks.Count == 0) return;

      UpdateBlockAndInlineStarts(Math.Max(0, fromBlockIndex));

      foreach (var p in paragraphs.Distinct())
      {
         p.CallRequestInlinesUpdate();
         p.CallRequestInvalidateVisual();
      }

      // Ensure selection rects/caret calculations have a consistent basis.
      UpdateSelection();
      Selection.StartParagraph.CallRequestTextLayoutInfoStart();
      Selection.EndParagraph.CallRequestTextLayoutInfoEnd();
   }
}



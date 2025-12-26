using System;
using System.Collections.Generic;
using System.Linq;

namespace AvRichTextBox;

public partial class FlowDocument
{
   internal int GetLocalOffsetInParagraph(Paragraph p, int inlineIndex)
   {
      int local = 0;
      int count = Math.Clamp(inlineIndex, 0, p.Inlines.Count);
      for (int i = 0; i < count; i++)
         local += p.Inlines[i].CursorSpanLength;
      return local;
   }


}



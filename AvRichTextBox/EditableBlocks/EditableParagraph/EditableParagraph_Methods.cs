using Avalonia.Controls.Documents;
using Avalonia.Media;
using System;
using System.Diagnostics;

namespace AvRichTextBox;

public partial class EditableParagraph
{

   private InlineCollection GetFormattedInlines()
   {

      InlineCollection returnInlines = [];
      foreach (IEditable ied in ((Paragraph)this.DataContext!).Inlines)
         returnInlines.Add(ied.BaseInline);

      return returnInlines;

   }


   private int GetClosestIndex(int lineNo, double distanceFromLeft, int direction)
   {
      CharacterHit chit = this.TextLayout.TextLines[lineNo + direction].GetCharacterHitFromDistance(distanceFromLeft);

      double charDistanceDiffThis = Math.Abs(distanceFromLeft - this.TextLayout.HitTestTextPosition(chit.FirstCharacterIndex).Left);
      double charDistanceDiffNext = Math.Abs(distanceFromLeft - this.TextLayout.HitTestTextPosition(chit.FirstCharacterIndex + 1).Left);

      if (charDistanceDiffThis > charDistanceDiffNext)
         return chit.FirstCharacterIndex + 1;
      else
         return chit.FirstCharacterIndex;


   }


}


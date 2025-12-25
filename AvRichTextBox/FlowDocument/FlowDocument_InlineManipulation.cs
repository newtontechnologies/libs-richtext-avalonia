using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AvRichTextBox;

public partial class FlowDocument
{

   internal List<IEditable> GetRangeInlines(TextRange trange)
   {
      Paragraph? startPar = trange.GetStartPar();
      Paragraph? endPar = trange.GetEndPar();
      if (startPar == null || endPar == null) return [];

      //Create clones of all inlines
      List<IEditable> allSelectedInlines = Blocks.Where(b => b.IsParagraph).SelectMany( b =>
            ((Paragraph)b).Inlines.Where(iline => 
            {
               double absInlineStart = b.StartInDoc + iline.TextPositionOfInlineInParagraph;
               double absInlineEnd = b.StartInDoc + iline.TextPositionOfInlineInParagraph + iline.InlineLength;
               iline.IsLastInlineOfParagraph = iline == ((Paragraph)b).Inlines[^1];
               return absInlineEnd > trange.Start && absInlineStart < trange.End;
            })
      ).ToList().ConvertAll(il => 
      {
         IEditable clonedInline = il.Clone();
         if (il.IsLastInlineOfParagraph)  //replace paragraph ends with \r char
            clonedInline.InlineText += "\r";
         return clonedInline; 
      });

      //Edge case
      if (allSelectedInlines.Count == 0)
         allSelectedInlines = Blocks.Where(b => b.IsParagraph).SelectMany(b =>
            ((Paragraph)b).Inlines.Where(iline => b.StartInDoc + iline.TextPositionOfInlineInParagraph + iline.InlineLength >= trange.Start &&
             b.StartInDoc + iline.TextPositionOfInlineInParagraph < trange.End)).ToList().ConvertAll(il => il.Clone());

      IEditable firstInline = allSelectedInlines[0];
      int firstInlineSplitIndex = Math.Min(trange.Start - startPar!.StartInDoc - firstInline.TextPositionOfInlineInParagraph, firstInline.InlineText.Length);

      if (allSelectedInlines.Count == 1)
      {
         int lastInlineSplitIndex = trange.End - endPar!.StartInDoc - firstInline.TextPositionOfInlineInParagraph;
         //firstInline.InlineText = firstInline.InlineText[firstInlineSplitIndex..lastInlineSplitIndex];
         firstInline.InlineText = firstInline.IsEmpty ? "" : firstInline.InlineText[firstInlineSplitIndex..lastInlineSplitIndex];
      }
      else
      {
         IEditable lastInline = allSelectedInlines[^1];
         int lastInlineSplitIndex = trange.End - endPar!.StartInDoc - lastInline.TextPositionOfInlineInParagraph;
         firstInline.InlineText = firstInline.InlineText[firstInlineSplitIndex ..];
         lastInline.InlineText = lastInline.InlineText[..lastInlineSplitIndex];
      }

      return allSelectedInlines;
   }

   internal List<IEditable> CreateNewInlinesForRange(TextRange trange)
   {
      Paragraph? startPar = trange.GetStartPar();
      Paragraph? endPar = trange.GetEndPar();
      if (startPar == null || endPar == null) return [];

      List<IEditable> allSelectedInlines = Blocks.Where(b => b.IsParagraph).SelectMany(b =>
         ((Paragraph)b).Inlines.Where(iline => b.StartInDoc + iline.TextPositionOfInlineInParagraph + iline.InlineLength > trange.Start &&
             b.StartInDoc + iline.TextPositionOfInlineInParagraph < trange.End)).ToList();
      
      //Edge case
      if (allSelectedInlines.Count == 0)
         allSelectedInlines = Blocks.Where(b => b.IsParagraph).SelectMany(b =>
            ((Paragraph)b).Inlines.Where(iline => b.StartInDoc + iline.TextPositionOfInlineInParagraph + iline.InlineLength >= trange.Start &&
             b.StartInDoc + iline.TextPositionOfInlineInParagraph < trange.End)).ToList();

      if (allSelectedInlines.Count == 0)
         return [];

      IEditable firstInline = allSelectedInlines[0];
      IEditable lastInline = allSelectedInlines[^1];
      IEditable insertLastInline = lastInline.Clone();

      int lastInlineSplitIndex = trange.End - endPar!.StartInDoc - lastInline.TextPositionOfInlineInParagraph;
      bool rangeEndsAtInlineEnd = lastInlineSplitIndex >= lastInline.InlineLength;

      string lastInlineText = lastInline.InlineText;
      int indexOfLastInline = endPar.Inlines.IndexOf(lastInline);

      if (allSelectedInlines.Count == 1)
      {
         if (!rangeEndsAtInlineEnd)
         {
            insertLastInline.InlineText = lastInlineText[..lastInlineSplitIndex];
            lastInline.InlineText = lastInlineText[lastInlineSplitIndex..];
            allSelectedInlines.RemoveAt(allSelectedInlines.Count - 1);
            allSelectedInlines.Add(insertLastInline);

            endPar.Inlines.Insert(indexOfLastInline, insertLastInline);

         }

         IEditable insertFirstInline = insertLastInline.Clone();
         string firstInlineText = insertLastInline.InlineText;
         int firstInlineSplitIndex = Math.Min(trange.Start - startPar!.StartInDoc - firstInline.TextPositionOfInlineInParagraph, firstInlineText.Length);

         bool rangeStartsAtInlineStart = firstInlineSplitIndex <= 0;

         if (!rangeStartsAtInlineStart)
         {
            insertFirstInline.InlineText = firstInlineText[..firstInlineSplitIndex];
            insertLastInline.InlineText = firstInlineText[firstInlineSplitIndex..];

            startPar.Inlines.Insert(indexOfLastInline, insertFirstInline);
         }
      }
      else
      {
         //split last run and remove trailing excess run from list
         if (!rangeEndsAtInlineEnd)
         {
            lastInline.InlineText = lastInlineText[..lastInlineSplitIndex];
            allSelectedInlines.Add(lastInline);

            insertLastInline.InlineText = lastInlineText[lastInlineSplitIndex..];
            endPar.Inlines.Insert(indexOfLastInline + 1, insertLastInline);

            firstInline = allSelectedInlines[0];
         }

         IEditable insertFirstInline = firstInline.Clone();
         string firstInlineText = firstInline.InlineText;
         int indexOfFirstInline = startPar!.Inlines.IndexOf(firstInline);

         // split first run and remove initial excess run from list
         int firstInlineSplitIndex = Math.Min(trange.Start - startPar.StartInDoc - firstInline.TextPositionOfInlineInParagraph, firstInlineText.Length);
         bool rangeStartsAtInlineStart = firstInlineSplitIndex <= 0;

         if (!rangeStartsAtInlineStart)
         {
            firstInline.InlineText = firstInlineText[..firstInlineSplitIndex];
            insertFirstInline.InlineText = firstInlineText[firstInlineSplitIndex..];
            allSelectedInlines.Remove(firstInline);
            allSelectedInlines.Insert(0, insertFirstInline);
            
            startPar.Inlines.Insert(indexOfFirstInline + 1, insertFirstInline);
         }
      }

      startPar.CallRequestInlinesUpdate();
      endPar.CallRequestInlinesUpdate();
      UpdateBlockAndInlineStarts(Blocks.IndexOf(startPar));
      
      return allSelectedInlines;
   }

   internal List<IEditable> SplitRunAtPos(int charPos, IEditable inlineToSplit, int splitPos)
   {
      //if (inlineToSplit.IsUIContainer)
      //   return [new EditableRun(""), inlineToSplit];

      ObservableCollection<IEditable> inlines = GetContainingParagraph(charPos).Inlines;
      int runIdx = inlines.IndexOf(inlineToSplit);

      //splitPos = Math.Min(splitPos, inlineToSplit.InlineLength);

      string part2Text = inlineToSplit.InlineText[splitPos..];


      inlineToSplit.InlineText = inlineToSplit.InlineText[..splitPos];
      IEditable insertInline = inlineToSplit.Clone();
      insertInline.InlineText = part2Text;
      inlines.Insert(runIdx + 1, insertInline);

      return [inlineToSplit, insertInline];
   }

   internal Paragraph? GetNextParagraph(Paragraph par)
   {
      int myindex = Blocks.IndexOf(par);
      return myindex == Blocks.Count - 1 ? null : (Paragraph)Blocks[myindex + 1];
   }
   
   internal IEditable? GetNextInline(IEditable inline)
   {
      IEditable returnIed = null!;
      int myindex = inline.MyParagraph!.Inlines.IndexOf(inline);
     
      if (myindex < inline.MyParagraph.Inlines.Count - 1)
         returnIed = inline.MyParagraph!.Inlines[myindex + 1];
      else
      {
         Paragraph? nextPar = GetNextParagraph(inline.MyParagraph);
         if (nextPar == null)
            return null!;
         if (nextPar.Inlines.Count > 0)
            returnIed = nextPar.Inlines[0];
      }
      return returnIed;
   }
}
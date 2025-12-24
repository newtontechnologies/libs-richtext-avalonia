using Avalonia.Controls.Documents;
using DynamicData;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AvRichTextBox;

internal class InsertCharUndo (Undo? previous, int insertParIndex, int insertInlineIdx, int insertPos, FlowDocument flowDoc, int origSelectionStart) : Undo(previous)
{
   public override int UndoEditOffset => -1;
   public override bool UpdateTextRanges => true;

   public override void PerformUndo()
   {
      try
      {
         if (flowDoc.Blocks[insertParIndex] is not Paragraph thisPar) return;
         if (thisPar.Inlines[insertInlineIdx] is not Run thisRun) return;
         thisRun!.Text = thisRun.Text!.Remove(insertPos, 1);
         thisPar.CallRequestInlinesUpdate();
         flowDoc.UpdateBlockAndInlineStarts(insertParIndex);
         flowDoc.Selection.Start = origSelectionStart;
         flowDoc.Selection.End = flowDoc.Selection.Start;
      }
      catch { Debug.WriteLine("Failed InsertCharUndo at inline idx: " + insertInlineIdx); }

   }
}

internal class DeleteCharUndo(Undo? previous, int deleteParIndex, int deleteInlineIdx, IEditable deletedRun, string deleteChar, int deletePos, FlowDocument flowDoc, int origSelectionStart) : Undo(previous)
{  
   public override int UndoEditOffset => 1;
   public override bool UpdateTextRanges => true;
   internal int DeleteInlineIdx => deleteInlineIdx;

   public override void PerformUndo()
   {
      try
      {
         Paragraph thisPar = (Paragraph)flowDoc.Blocks[deleteParIndex];
         if (deletedRun != null)
            thisPar.Inlines.Insert(deleteInlineIdx, deletedRun);
         else
         {
            Run? thisRun = thisPar.Inlines[deleteInlineIdx] as Run;
            thisRun!.Text = thisRun.Text!.Insert(deletePos, deleteChar);
         }
         
         thisPar.CallRequestInlinesUpdate();
         flowDoc.UpdateBlockAndInlineStarts(deleteParIndex);
         flowDoc.Selection.Start = origSelectionStart;
         flowDoc.Selection.End = flowDoc.Selection.Start;
      }
      catch { Debug.WriteLine("Failed DeleteCharUndo at delete pos: " + deletePos); }
   }
      
}

internal class DeleteImageUndo(Undo? previous, int deleteParIndex, IEditable deletedIUC, int deletedInlineIdx, FlowDocument flowDoc, int origSelectionStart, bool emptyRunAdded) : Undo(previous)
{
   public override int UndoEditOffset => 1;
   public override bool UpdateTextRanges => true;
   
   public override void PerformUndo()
   {
      try
      {
         Paragraph thisPar = (Paragraph)flowDoc.Blocks[deleteParIndex];
         if (emptyRunAdded)
            thisPar.Inlines.RemoveAt(deletedInlineIdx);
         thisPar.Inlines.Insert(deletedInlineIdx, deletedIUC);
         thisPar.CallRequestInlinesUpdate();
         flowDoc.UpdateBlockAndInlineStarts(deleteParIndex);
         flowDoc.Selection.Start = origSelectionStart;
         flowDoc.Selection.End = flowDoc.Selection.Start;
      }
      catch { Debug.WriteLine("Failed DeleteImageUndo at delete pos: " + origSelectionStart); }
   }
      
}

internal class PasteUndo(Undo? previous, Dictionary<Block, List<IEditable>> keptParsAndInlines, int parIndex, FlowDocument flowDoc, int origSelectionStart, int undoEditOffset) : Undo(previous)
{
   public override int UndoEditOffset => undoEditOffset;
   public override bool UpdateTextRanges => true;

   public override void PerformUndo()
   {
      try
      {
         flowDoc.RestoreDeletedBlocks(keptParsAndInlines, parIndex);

         flowDoc.Selection.Start = 0;  //??? why necessary for caret?
         flowDoc.Selection.End = 0;
         flowDoc.Selection.Start = origSelectionStart;
         flowDoc.Selection.End = origSelectionStart;
         flowDoc.UpdateSelection();
      }
      catch { Debug.WriteLine("Failed PasteUndo at OrigSelectionStart: " + origSelectionStart); }
   }
}

internal class DeleteRangeUndo (Undo? previous, Dictionary<Block, List<IEditable>> keptParsAndInlines, int parIndex, FlowDocument flowDoc, int origSelectionStart, int undoEditOffset) : Undo(previous)
{  //parInlines are cloned inlines

   public override int UndoEditOffset => undoEditOffset;
   public override bool UpdateTextRanges => true;

   public override void PerformUndo()
   {
      try
      {
         flowDoc.RestoreDeletedBlocks(keptParsAndInlines, parIndex);

         flowDoc.Selection.Start = 0;  //??? why necessary for caret?
         flowDoc.Selection.End = 0;
         flowDoc.Selection.Start = origSelectionStart;
         flowDoc.Selection.End = origSelectionStart;

         flowDoc.UpdateSelection();
      }
      catch { Debug.WriteLine("Failed DeleteRangeUndo at ParIndex: " + parIndex); }
   }

}


internal class InsertParagraphUndo (Undo? previous, FlowDocument flowDoc, int insertedParIndex, List<IEditable> keepParInlines, int origSelectionStart, int undoEditOffset) : Undo(previous)
{  
   public override int UndoEditOffset => undoEditOffset;
   public override bool UpdateTextRanges => true;

   public override void PerformUndo()
   {
      try
      {
         Paragraph insertedPar = (Paragraph)flowDoc.Blocks[insertedParIndex];
         insertedPar.Inlines.Clear();
         insertedPar.Inlines.AddRange(keepParInlines);
         flowDoc.Blocks.RemoveAt(insertedParIndex + 1);
         flowDoc.UpdateBlockAndInlineStarts(insertedParIndex);
         //flowDoc.MergeParagraphForward(insertedIndex, false, origSelectionStart);
         flowDoc.Selection.Start = origSelectionStart;
         flowDoc.Selection.End = flowDoc.Selection.Start;
      }
      catch { Debug.WriteLine("Failed InsertParagraphUndo at InsertedIndex: " + insertedParIndex); }

   }
}


internal class MergeParagraphUndo (Undo? previous, int origMergedParInlinesCount, int mergedParIndex, Paragraph removedPar, FlowDocument flowDoc, int originalSelectionStart) : Undo(previous) 
{ //removedPar is a clone

   public override int UndoEditOffset => 1;
   public override bool UpdateTextRanges => false;

   public override void PerformUndo()
   {
      try
      {
         //flowDoc.InsertParagraph(false, mergedCharIndex);

         Paragraph mergedPar = (Paragraph)flowDoc.Blocks[mergedParIndex];

         for (int rno = mergedPar.Inlines.Count - 1; rno >= origMergedParInlinesCount; rno--)
            mergedPar.Inlines.RemoveAt(rno);

         flowDoc.Blocks.Insert(mergedParIndex + 1, removedPar);

         flowDoc.UpdateBlockAndInlineStarts(mergedParIndex);
         flowDoc.Selection.End = originalSelectionStart;
         flowDoc.Selection.Start = originalSelectionStart;
                  
      }
      catch { Debug.WriteLine("Failed MergeParagraphUndo at MergedParIndex: " + mergedParIndex); }
   }
}


internal class ApplyFormattingUndo (Undo? previous, FlowDocument flowDoc, List<IEditablePropertyAssociation> propertyAssociations, int originalSelection, TextRange tRange) : Undo(previous) 
{
   public override int UndoEditOffset => 0;
   public override bool UpdateTextRanges => false;

   public override void PerformUndo()
   {

      foreach (IEditablePropertyAssociation propassoc in propertyAssociations)
         if (propassoc.FormatRun != null)
            flowDoc.ApplyFormattingInline(propassoc.FormatRun, propassoc.InlineItem, propassoc.PropertyValue);

      foreach (Paragraph p in flowDoc.GetRangeBlocks(tRange).Where(b=>b.IsParagraph))
         p.CallRequestInlinesUpdate();
      
      flowDoc.Selection.Start = originalSelection;
      flowDoc.Selection.End = originalSelection;

   }
}


internal class InsertLineBreakUndo(Undo? previous, int insertParIndex, int insertInlineIdx, FlowDocument flowDoc, int origSelectionStart) : Undo(previous)
{
   public override int UndoEditOffset => -1;
   public override bool UpdateTextRanges => true;

   public override void PerformUndo()
   {
      try
      {
         if (flowDoc.Blocks[insertParIndex] is not Paragraph thisPar) return;
         if (thisPar.Inlines[insertInlineIdx] is not EditableLineBreak thisELB) return;
         thisPar.Inlines.Remove(thisELB);
         thisPar.CallRequestInlinesUpdate();
         flowDoc.UpdateBlockAndInlineStarts(insertParIndex);
         flowDoc.Selection.Start = origSelectionStart;
         flowDoc.Selection.End = flowDoc.Selection.Start;
      }
      catch { Debug.WriteLine("Failed InsertCharUndo at inline idx: " + insertInlineIdx); }

   }
}


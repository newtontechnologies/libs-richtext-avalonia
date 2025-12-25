using DynamicData;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AvRichTextBox;

public partial class FlowDocument
{
   internal void InsertText(string? insertText)
   {
      if (string.IsNullOrEmpty(insertText)) return;

      // Do not type into UI containers.
      if (Selection.GetStartInline() is EditableInlineUiContainer) return;

      // Coalesce simple typing (single char, collapsed selection, no caret movement)
      bool isSingleChar = insertText.Length == 1 && insertText[0] != '\r' && insertText[0] != '\n';
      if (isSingleChar && Selection.Length == 0)
      {
         int caret = Selection.Start;

         if (_canCoalesceTyping && _typing != null && caret == _typing.Paragraph.StartInDoc + _typing.Run.TextPositionOfInlineInParagraph + _typing.RunInsertOffsetEnd)
         {
            _isTypingEdit = true;
            try
            {
               // Apply incremental change in-place (no intermediate undo/redo), then replace top undo action with merged immutable one.
               _typing.Run.InlineText = _typing.Run.InlineText.Insert(_typing.RunInsertOffsetEnd, insertText);
               _typing.RunInsertOffsetEnd += 1;

               // Keep TextRanges consistent: shift at the insertion point.
               UpdateTextRanges(caret, 1);
               _typing.ShiftPoints.Add(caret);

               // Update selection/caret and refresh.
               SelectionParagraphs.Clear();
               Selection.Start = caret + 1;
               Selection.End = caret + 1;
               SelectionStart_Changed(Selection, Selection.Start);
               SelectionEnd_Changed(Selection, Selection.End);
               EnsureSelectionContinuity();

               // Replace last action in undo stack with merged action; clear redo.
            RedoStack.Clear();
            UndoStack.Pop();
            UndoStack.Push(BuildMergedTypingAction(_typing));

               RefreshAfterAtomicEdits(_typing.RefreshFromBlockIndex, [_typing.Paragraph]);
               _canCoalesceTyping = true;
               return;
            }
            finally
            {
               _isTypingEdit = false;
            }
         }

         // Start a new typing coalescing sequence (must be based on stable references from current state).
         var typingAction = BuildTypingInsertAction(caret, insertText);
         _isTypingEdit = true;
         try
         {
            RedoStack.Clear();
            RedoStack.Push(typingAction);
            Redo();
            _canCoalesceTyping = true;
            return;
         }
         finally
         {
            _isTypingEdit = false;
         }
      }

      ExecuteEdit(BuildReplaceRangeAction(Selection.Start, Selection.End, [new EditableRun(insertText)]));

   }

   private EditAction BuildTypingInsertAction(int caret, string text)
   {
      var selectionBefore = CaptureSelectionState();

      var p = ResolveStartParagraph(caret);
      p.UpdateEditableRunPositions();

      int local = Math.Min(caret - p.StartInDoc, p.Text.Length);
      EditableRun run = p.Inlines.OfType<EditableRun>().LastOrDefault(r => r.TextPositionOfInlineInParagraph <= local) ?? new EditableRun("");
      int runIndex = p.Inlines.IndexOf(run);

      var edits = new List<IAtomicEdit>(4);
      if (runIndex < 0)
      {
         // Insert the new run at start if none found.
         edits.Add(new InsertInlineEdit(p, 0, run));
         runIndex = 0;
      }

      int off = Math.Clamp(local - run.TextPositionOfInlineInParagraph, 0, run.InlineText.Length);
      string oldText = run.InlineText;
      string newText = oldText.Insert(off, text);

      edits.Add(new SetRunTextEdit(run, oldText, newText));
      edits.Add(new ShiftTextRangesEdit(this, caret, 1));

      int refreshFrom = Blocks.IndexOf(p);

      var selectionAfter = new SelectionState(caret + 1, caret + 1, ExtendMode.ExtendModeNone, BiasForwardStart: false, BiasForwardEnd: false);
      _typing = new TypingCoalesceState
      {
         Paragraph = p,
         Run = run,
         RunInsertOffsetStart = off,
         RunInsertOffsetEnd = off + 1,
         OldRunText = oldText,
         SelectionBefore = selectionBefore,
         RefreshFromBlockIndex = refreshFrom,
         ShiftPoints = [caret]
      };

      return new EditAction(edits, selectionBefore, selectionAfter, refreshFrom, [p]);
   }

   private EditAction BuildMergedTypingAction(TypingCoalesceState state)
   {
      // Build a merged action representing all typed chars as one undo entry.
      var edits = new List<IAtomicEdit>(1 + state.ShiftPoints.Count);

      string newText = state.Run.InlineText;
      edits.Add(new SetRunTextEdit(state.Run, state.OldRunText, newText));

      foreach (int pt in state.ShiftPoints)
         edits.Add(new ShiftTextRangesEdit(this, pt, 1));

      int caretAfter = state.ShiftPoints[^1] + 1;
      var selectionAfter = new SelectionState(caretAfter, caretAfter, ExtendMode.ExtendModeNone, BiasForwardStart: false, BiasForwardEnd: false);

      return new EditAction(edits, state.SelectionBefore, selectionAfter, state.RefreshFromBlockIndex, [state.Paragraph]);
   }

   internal void DeleteChar(bool backspace)
   {
      if (Selection.Length > 0)
      {
         DeleteSelection();
         return;
      }

      if (backspace)
      {
         if (Selection.Start <= 0) return;
         ExecuteEdit(BuildReplaceRangeAction(Selection.Start - 1, Selection.Start, []));
      }
      else
      {
         if (Selection.Start >= DocEndPoint - 1) return;
         ExecuteEdit(BuildReplaceRangeAction(Selection.Start, Selection.Start + 1, []));
      }



   }

   internal void InsertLineBreak()
   {
      ExecuteEdit(BuildReplaceRangeAction(Selection.Start, Selection.End, [new EditableLineBreak()]));

   }


   internal void DeleteSelection()
   {
      ExecuteEdit(BuildReplaceRangeAction(Selection.Start, Selection.End, []));

   }

   internal void DeleteRange(TextRange trange, bool saveUndo)
   {
      int originalSelectionStart = trange.Start;
      int originalTRangeLength = trange.Length;

      Dictionary<Block, List<IEditable>> keepParsAndInlines = KeepParsAndInlines(trange);
      List<Block> allBlocks = keepParsAndInlines.ToList().ConvertAll(keepP => keepP.Key);

      List<IEditable> rangeInlines = CreateNewInlinesForRange(trange);

      //Delete the created inlines
      foreach (IEditable toDeleteRun in rangeInlines)
         foreach (Block b in allBlocks)
         {
            if (b.IsParagraph)
            {
               Paragraph p = (Paragraph)b;
               p.Inlines.Remove(toDeleteRun);
               p.CallRequestInlinesUpdate();
            }
         }

      //Delete any full blocks contained within the range
      int idxStartPar = Blocks.IndexOf(allBlocks[0]);
      for (int i = idxStartPar + allBlocks.Count - 2; i > idxStartPar; i--)
      {
         if (Blocks[i].IsParagraph)
         {
            ((Paragraph)Blocks[i]).Inlines.Clear();
            ((Paragraph)Blocks[i]).CallRequestInlinesUpdate();
         }
         Blocks.RemoveAt(i);
      }

      //Add a blank run if all runs were deleted in one paragraph
      if (allBlocks.Count == 1 && ((Paragraph)allBlocks[0]).Inlines.Count == 0)
         ((Paragraph)allBlocks[0]).Inlines.Add(new EditableRun(""));

      //Merge inlines of last paragraph with first
      if (allBlocks.Count > 1)
      {
         Paragraph? lastPar = allBlocks[^1] as Paragraph;
         List<IEditable> moveInlines = new(lastPar!.Inlines);
         lastPar.Inlines.RemoveMany(moveInlines);
         lastPar.CallRequestInlinesUpdate();
         ((Paragraph)Blocks[idxStartPar]).Inlines.AddRange(moveInlines);
         ((Paragraph)Blocks[idxStartPar]).CallRequestInlinesUpdate(); // ensure any image containers are updated
         Blocks.Remove(lastPar);
      }

      //Special case where all content was deleted leaving one empty block
      if (Blocks.Count == 1 && ((Paragraph)Blocks[0]).Inlines.Count == 0)
          ((Paragraph)Blocks[0]).Inlines.Add(new EditableRun(""));


      UpdateTextRanges(originalSelectionStart, -originalTRangeLength);


      UpdateSelection();

      trange.CollapseToStart();
      SelectionExtendMode = ExtendMode.ExtendModeNone;


   }

   internal void InsertParagraph(bool addUndo, int insertCharIndex)
   {  //The delete range and InsertParagraph should constitute one Undo operation

      Paragraph insertPar = GetContainingParagraph(insertCharIndex);
      List<IEditable> keepParInlines = insertPar.Inlines.Select(il=>il.Clone()).ToList(); 

      int originalSelStart = insertCharIndex;
      int parIndex = Blocks.IndexOf(insertPar);
      int selectionLength = 0;

      // This method is now used as a core operation by EditActions; it should not record undo/redo itself.
      selectionLength = Selection.Length;
      if (Selection.Length > 0)
      {
         DeleteRange(Selection, false);
         Selection.CollapseToStart();
         SelectionExtendMode = ExtendMode.ExtendModeNone;
      }

      IEditable startInline = GetStartInline(insertCharIndex);
      int startRunIdx = insertPar.Inlines.IndexOf(startInline);

      //Split at selection
      List<IEditable> parSplitRuns = SplitRunAtPos(insertCharIndex, startInline, startInline.GetCharPosInInline(insertCharIndex));


      List<IEditable> runList1 = [.. insertPar.Inlines.Take(new Range(0, startRunIdx)).ToList().ConvertAll(r => r)];
      if (parSplitRuns[0].InlineText != "" || runList1.Count == 0)
         runList1.Add(parSplitRuns[0]);
      List<IEditable> runList2 = [.. insertPar.Inlines.Take(new Range(startRunIdx + 1, insertPar.Inlines.Count)).ToList().ConvertAll(r => r as IEditable)];
      
      Paragraph originalPar = insertPar;
      
      originalPar.Inlines.Clear();
      originalPar.Inlines.AddRange(runList1);
      originalPar.SelectionStartInBlock = 0;
      originalPar.CollapseToStart();

      if (originalPar.Inlines.Last() is EditableLineBreak elb)
      {
         originalPar.Inlines.Insert(originalPar.Inlines.Count, new EditableRun(""));
      }

      Paragraph parToInsert = originalPar.PropertyClone();

      parToInsert.Inlines.AddRange(runList2);
      Blocks.Insert(parIndex + 1, parToInsert);

      if (parToInsert.Inlines.Count == 0)
      {
         EditableRun erun = (EditableRun)originalPar.Inlines.Last().Clone();
         erun.Text = "";
         parToInsert.Inlines.Add(erun);
      }
      
      UpdateTextRanges(insertCharIndex, 1);

      UpdateBlockAndInlineStarts(parIndex);
      originalPar.CallRequestInlinesUpdate();
      parToInsert.CallRequestInlinesUpdate();


      // Undo/Redo is handled by EditActions.

      Selection.BiasForwardStart = true;
      Selection.BiasForwardEnd = true;
      Selection.End += 1;
      Selection.CollapseToEnd();

      originalPar.CallRequestTextLayoutInfoStart();
      parToInsert.CallRequestTextLayoutInfoStart();
      originalPar.CallRequestTextLayoutInfoEnd();
      parToInsert.CallRequestTextLayoutInfoEnd();

      ScrollInDirection?.Invoke(1);

   }

   internal void MergeParagraphForward(int mergeCharIndex, bool saveUndo, int originalSelectionStart)
   {
      Paragraph thisPar = GetContainingParagraph(mergeCharIndex);
      int thisParIndex = Blocks.IndexOf(thisPar);
      if (thisParIndex == Blocks.Count - 1) return; //is last Paragraph, can't merge forward
      int origParInlinesCount = thisPar.Inlines.Count;

      Paragraph nextPar = (Paragraph)Blocks[thisParIndex + 1];
      bool isNextParagraphEmpty = nextPar.Inlines.Count == 1 && nextPar.Inlines[0].IsEmpty;
      bool isThisParagraphEmpty = thisPar.Inlines.Count == 1 && thisPar.Inlines[0].IsEmpty;

      // Undo/Redo is handled by EditActions.

      if (isThisParagraphEmpty)
         thisPar.Inlines.Clear();

      //bool runAdded = false;
      if (isNextParagraphEmpty)
      {
         if (isThisParagraphEmpty)
         {
            thisPar.Inlines.Add(new EditableRun(""));
            //runAdded = true;
         }
      }
      else
      {
         List<IEditable> inlinesToMove = new(nextPar.Inlines);
         nextPar.Inlines.Clear();
         nextPar.CallRequestInlinesUpdate(); // ensure image containers are updated
         thisPar.Inlines.AddRange(inlinesToMove);
      }
           
      Blocks.Remove(nextPar);

      Selection!.BiasForwardStart = true;
      Selection!.BiasForwardEnd = true;

      UpdateTextRanges(mergeCharIndex, -1);

      thisPar.CallRequestInlinesUpdate();
      UpdateBlockAndInlineStarts(thisParIndex);

      thisPar.CallRequestTextBoxFocus();

      UpdateSelectedParagraphs();

#if DEBUG
      UpdateDebuggerSelectionParagraphs();
#endif



   }

   internal void DeleteWord(bool backspace)
   {
      int caret = Selection.Start;

      if (backspace)
      {
         if (caret <= 0) return;

         var p = Selection.StartParagraph;
         int localCaret = caret - p.StartInDoc;
         localCaret = Math.Max(0, localCaret - 1);
         int ws = p.Text.LastIndexOfAny(" \v".ToCharArray(), localCaret);
         int start = p.StartInDoc + (ws < 0 ? 0 : ws + 1);

         ExecuteEdit(BuildReplaceRangeAction(start, caret, []));
         return;
      }

      if (caret >= DocEndPoint - 1) return;

      var par = Selection.StartParagraph;
      int local = caret - par.StartInDoc;

      // Delete paragraph boundary (merge forward) if caret is at paragraph end.
      if (local >= par.Text.Length)
      {
         ExecuteEdit(BuildReplaceRangeAction(caret, caret + 1, []));
         return;
      }

      IEditable startInline = Selection.GetStartInline();
      if (startInline.IsUiContainer || startInline.IsLineBreak)
      {
         ExecuteEdit(BuildReplaceRangeAction(caret, caret + 1, []));
         return;
      }

      int nextSpace = par.Text.IndexOf(' ', local);
      int end = nextSpace < 0 ? (par.StartInDoc + par.Text.Length) : (par.StartInDoc + nextSpace + 1);
      ExecuteEdit(BuildReplaceRangeAction(caret, end, []));

   }


}
using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls.Documents;
using static AvRichTextBox.FlowDocument;

namespace AvRichTextBox;

internal class EditBuilder(FlowDocument doc)
{
   List<IAtomicEdit> _edits = new(capacity: 64);
   readonly List<Paragraph> _refreshPars = new(capacity: 8);

   private readonly List<IEditable> _tmpWholeInlinesInRange = new(capacity: 64);
   private readonly List<IAtomicEdit> _tmpInsertionEdits = new(capacity: 32);
   private readonly List<IEditable> _tmpMoveInlines = new(capacity: 64);

   internal EditAction BuildTypingInsertAction(int caret, string text, out FlowDocument.TypingCoalesceState typing)
   {
      var selectionBefore = doc.CaptureSelectionState();

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
      edits.Add(new ShiftTextRangesEdit(doc, caret, 1));

      int refreshFrom = doc.Blocks.IndexOf(p);

      var selectionAfter = new SelectionState(caret + 1, caret + 1, ExtendMode.ExtendModeNone, BiasForwardStart: false, BiasForwardEnd: false);
      typing = new TypingCoalesceState
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

   internal EditAction BuildReplaceRangeAction(TextPos start, TextPos end, List<IEditable> insertInlines)
   {
      int startIndex = doc.GetGlobalIndexFromTextPos(start);
      int endIndex = doc.GetGlobalIndexFromTextPos(end);
      if (endIndex < startIndex) throw new ArgumentOutOfRangeException(nameof(end));

      var selectionBefore = doc.CaptureSelectionState();

      // We'll build the edit by applying atomic edits step-by-step (to keep reasoning simple),
      // then roll them back before returning the immutable EditAction.

      // Normalize to current doc range (by global index, but deletion logic below is TextPos-based).
      int startIdx = Math.Max(0, startIndex);
      int endIdx = Math.Min(Math.Max(0, doc.DocEndPoint - 1), endIndex);

      int preDocEndMinus1 = Math.Max(0, doc.DocEndPoint - 1);
      int removedLen = endIdx - startIdx;

      // ---- 1) DELETE (2-phase) ----
      // Phase A: split boundary runs so everything inside the range is composed of whole inlines.
      // Phase B: remove whole inlines in-range, and if across paragraphs, merge and remove blocks between.
      var startPos = GetTextPosFromGlobalIndexPreferLeftAtParagraphBoundary(startIdx);
      var endPos = doc.GetTextPosFromGlobalIndex(endIdx);

      _tmpWholeInlinesInRange.Clear();
      PrepareRangeForWholeInlineOps(
         ref startPos,
         ref endPos,
         preserveStartAfterLastInlineAtParagraphBoundary: removedLen > 0,
         out Paragraph startPar,
         out int startInlineIndex,
         out Paragraph endPar,
         out int endExclusiveInlineIndex);

      if (removedLen > 0)
         DeleteWholeInlinesInRange(startPar, startInlineIndex, endPar, endExclusiveInlineIndex);

      // ---- 2) INSERT (replace) ----
      {
         var pIns = startPar;
         int local = doc.GetLocalOffsetInParagraph(pIns, startInlineIndex);
         var insertionEdits = _tmpInsertionEdits;
         insertionEdits.Clear();
         int insertedDocLen;

         // Enter (paragraph break) inside a paragraph needs to split/move suffix (not just insert a marker).
         if (IsEnterInsert(insertInlines))
         {
            insertedDocLen = 1;

            pIns.UpdateEditableRunPositions();

            // Find the run containing the insertion point.
            var run = pIns.Inlines
               .OfType<EditableRun>()
               .LastOrDefault(r => r.TextPositionOfInlineInParagraph <= local);
            if (run is null)
            {
               run = new EditableRun("");
               ApplyAndRecord(new InsertInlineEdit(pIns, 0, run));
            }

            int runIndex = pIns.Inlines.IndexOf(run);
            int runStart = run.TextPositionOfInlineInParagraph;
            int splitAt = Math.Clamp(local - runStart, 0, run.InlineText.Length);

            string oldText = run.InlineText;
            string prefix = oldText.Substring(0, splitAt);
            string suffix = oldText.Substring(splitAt);

            ApplyAndRecord(new SetRunTextEdit(run, oldText, prefix));

            // New paragraph after current.
            var newPar = pIns.PropertyClone();
            newPar.Inlines.Clear();
            ApplyAndRecord(new InsertBlockEdit(doc, doc.Blocks.IndexOf(pIns) + 1, newPar));

            int newParInsertIndex = 0;
            if (suffix.Length > 0)
               ApplyAndRecord(new InsertInlineEdit(newPar, newParInsertIndex++, CloneRunWithText(run, suffix)));

            // Move all following inlines after the run to the new paragraph.
            _tmpMoveInlines.Clear();
            for (int i = runIndex + 1; i < pIns.Inlines.Count; i++)
               _tmpMoveInlines.Add(pIns.Inlines[i]);

            for (int i = 0; i < _tmpMoveInlines.Count; i++)
            {
               var il = _tmpMoveInlines[i];
               ApplyAndRecord(new RemoveInlineEdit(pIns, runIndex + 1 + i, il));
               ApplyAndRecord(new InsertInlineEdit(newPar, newParInsertIndex++, il));
            }

            if (newParInsertIndex == 0)
               ApplyAndRecord(new InsertInlineEdit(newPar, 0, CloneRunWithText(run, "")));

            _refreshPars.Add(pIns);
            _refreshPars.Add(newPar);
         }
         else
         {
            insertedDocLen = BuildInsertAt(insertionEdits, _refreshPars, pIns, local, insertInlines);
            for (int i = 0; i < insertionEdits.Count; i++)
               ApplyAndRecord(insertionEdits[i]);
         }

         // ---- 3) SHIFT TEXTRANGES ----
         int delta = insertedDocLen - removedLen;
         if (delta != 0)
            ApplyAndRecord(new ShiftTextRangesEdit(doc, startIdx, delta));

         int newDocEndMinus1 = Math.Max(0, preDocEndMinus1 + delta);
         int caret = Math.Clamp(startIdx + insertedDocLen, 0, newDocEndMinus1);
         var selectionAfter = new SelectionState(caret, caret, ExtendMode.ExtendModeNone, BiasForwardStart: false, BiasForwardEnd: false);

         int refreshFromIndex = doc.Blocks.IndexOf(pIns);

         // Rollback the temp mutations so building has no side-effects.
         for (int i = _edits.Count - 1; i >= 0; i--)
            _edits[i].Unapply();

         return new EditAction(_edits, selectionBefore, selectionAfter, refreshFromIndex, _refreshPars.Distinct().ToList());
      }
   }

   internal void ApplyFormattingRange(AvaloniaProperty avProperty, object value, TextRange textRange)
   {
      if (!doc._formatRunsActions.ContainsKey(avProperty))
         throw new NotSupportedException($"Formatting for {avProperty.Name} is not supported.");

      var selectionBefore = doc.CaptureSelectionState();

      var startPos = doc.GetTextPosFromGlobalIndex(textRange.Start);
      var endPos = doc.GetTextPosFromGlobalIndex(textRange.End);

      _tmpWholeInlinesInRange.Clear();
      PrepareRangeForWholeInlineOps(
         ref startPos,
         ref endPos,
         preserveStartAfterLastInlineAtParagraphBoundary: false,
         out Paragraph startPar,
         out _,
         out Paragraph endPar,
         out _);

      // Collect old values per run (stored in the action; no snapshots).
      var oldValuesByRun = new Dictionary<EditableRun, object?>(capacity: _tmpWholeInlinesInRange.Count);
      for (int i = 0; i < _tmpWholeInlinesInRange.Count; i++)
      {
         if (_tmpWholeInlinesInRange[i] is EditableRun run && !oldValuesByRun.ContainsKey(run))
            oldValuesByRun.Add(run, run.GetValue(avProperty));
      }

      if (oldValuesByRun.Count == 0)
      {
         // Roll back any boundary splits (no visible change => no action).
         for (int i = _edits.Count - 1; i >= 0; i--)
            _edits[i].Unapply();
         return;
      }

      object? applyValue = value;
      if (avProperty == Inline.FontWeightProperty && value is FontWeight.Bold)
      {
         bool anyNormal = false;
         foreach (var r in oldValuesByRun.Keys)
            if (r.FontWeight == FontWeight.Normal) { anyNormal = true; break; }
         applyValue = anyNormal ? FontWeight.Bold : FontWeight.Normal;
      }
      else if (avProperty == Inline.FontStyleProperty && value is FontStyle.Italic)
      {
         bool anyNormal = false;
         foreach (var r in oldValuesByRun.Keys)
            if (r.FontStyle == FontStyle.Normal) { anyNormal = true; break; }
         applyValue = anyNormal ? FontStyle.Italic : FontStyle.Normal;
      }
      else if (avProperty == Inline.TextDecorationsProperty && value == TextDecorations.Underline)
      {
         bool anyNull = false;
         foreach (var r in oldValuesByRun.Keys)
            if (r.TextDecorations is null) { anyNull = true; break; }
         applyValue = anyNull ? TextDecorations.Underline : null;
      }

      ApplyAndRecord(new SetRunsPropertyEdit(avProperty, applyValue, oldValuesByRun));

      _refreshPars.Add(startPar);
      if (!ReferenceEquals(startPar, endPar))
         _refreshPars.Add(endPar);

      // Formatting doesn't change doc length => selection stays identical.
      var selectionAfter = selectionBefore;

      int refreshFromIndex = doc.Blocks.IndexOf(startPar);

      // Rollback the temp mutations so building has no side-effects.
      for (int i = _edits.Count - 1; i >= 0; i--)
         _edits[i].Unapply();

      doc.ExecuteEdit(new EditAction(_edits.ToList(), selectionBefore, selectionAfter, refreshFromIndex, _refreshPars.Distinct().ToList()));
   }

   void ApplyAndRecord(IAtomicEdit e)
   {
      e.Apply();
      _edits.Add(e);
   }

   private void PrepareRangeForWholeInlineOps(
      ref TextPos start,
      ref TextPos end,
      bool preserveStartAfterLastInlineAtParagraphBoundary,
      out Paragraph startPar,
      out int startInlineIndex,
      out Paragraph endPar,
      out int endExclusiveInlineIndex)
   {
      // Boundary splits: first and last run so the range is alignable to whole inlines.
      SplitStartRunIfNeeded(ref start, ref end, ApplyAndRecord);
      SplitEndRunIfNeeded(ref start, ref end, ApplyAndRecord);

      // Normalize boundaries to "start of an inline" (for start) and an "exclusive end index" (for end).
      start = NormalizeStartBoundary(start, preserveStartAfterLastInlineAtParagraphBoundary);
      end = NormalizeEndBoundary(end);

      startPar = GetParagraphFromTextPos(start);
      endPar = GetParagraphFromTextPos(end);

      startInlineIndex = GetInlineBoundaryIndex(startPar, start, isStart: true);
      endExclusiveInlineIndex = GetInlineBoundaryIndex(endPar, end, isStart: false);

      _tmpWholeInlinesInRange.Clear();
      EnumerateWholeInlinesInRange(startPar, startInlineIndex, endPar, endExclusiveInlineIndex);
   }

   private void DeleteWholeInlinesInRange(
      Paragraph startPar,
      int startInlineIndex,
      Paragraph endPar,
      int endExclusiveInlineIndex)
   {
      if (ReferenceEquals(startPar, endPar))
      {
         for (int i = endExclusiveInlineIndex - 1; i >= startInlineIndex; i--)
         {
            var inline = startPar.Inlines[i];
            ApplyAndRecord(new RemoveInlineEdit(startPar, i, inline));
         }

         EnsureParagraphHasInline(startPar);
         _refreshPars.Add(startPar);
         return;
      }

      // Delete tail of start paragraph.
      for (int i = startPar.Inlines.Count - 1; i >= startInlineIndex; i--)
      {
         var inline = startPar.Inlines[i];
         ApplyAndRecord(new RemoveInlineEdit(startPar, i, inline));
      }

      // Delete prefix of end paragraph.
      for (int i = endExclusiveInlineIndex - 1; i >= 0; i--)
      {
         var inline = endPar.Inlines[i];
         ApplyAndRecord(new RemoveInlineEdit(endPar, i, inline));
      }

      // Move remaining suffix inlines from endPar into startPar (preserve order).
      while (endPar.Inlines.Count > 0)
      {
         var il = endPar.Inlines[0];
         ApplyAndRecord(new RemoveInlineEdit(endPar, 0, il));
         ApplyAndRecord(new InsertInlineEdit(startPar, startPar.Inlines.Count, il));
      }

      EnsureParagraphHasInline(startPar);

      // Remove blocks between (including endPar).
      int startBlockIndex = doc.Blocks.IndexOf(startPar);
      int endBlockIndex = doc.Blocks.IndexOf(endPar);
      for (int bi = endBlockIndex; bi > startBlockIndex; bi--)
      {
         if (doc.Blocks[bi] is Paragraph p)
         {
            ApplyAndRecord(new RemoveBlockEdit(doc, bi, p));
            _refreshPars.Add(p);
         }
      }

      _refreshPars.Add(startPar);
      _refreshPars.Add(endPar);
   }

   private void SplitStartRunIfNeeded(ref TextPos start, ref TextPos end, Action<IAtomicEdit> applyAndRecord)
   {
      if (start.Inline is not EditableRun run) return;
      int ci = Math.Clamp(start.CharIndex, 0, run.InlineText.Length);
      if (ci == 0 || ci == run.InlineText.Length) return;

      var p = GetParagraphFromTextPos(start);
      int idx = p.Inlines.IndexOf(run);

      string oldText = run.InlineText;
      string left = oldText.Substring(0, ci);
      string right = oldText.Substring(ci);

      var rightRun = CloneRunWithText(run, right);
      applyAndRecord(new SetRunTextEdit(run, oldText, left));
      applyAndRecord(new InsertInlineEdit(p, idx + 1, rightRun));

      // start moves to the new "right" run.
      start = new TextPos(rightRun, 0);

      // If end was in the original run and after the split point, shift it into the new run.
      if (ReferenceEquals(end.Inline, run) && end.CharIndex > ci)
         end = new TextPos(rightRun, end.CharIndex - ci);
   }

   private void SplitEndRunIfNeeded(ref TextPos start, ref TextPos end, Action<IAtomicEdit> applyAndRecord)
   {
      if (end.Inline is not EditableRun run) return;
      int ci = Math.Clamp(end.CharIndex, 0, run.InlineText.Length);
      if (ci == 0 || ci == run.InlineText.Length) return;

      var p = GetParagraphFromTextPos(end);
      int idx = p.Inlines.IndexOf(run);

      string oldText = run.InlineText;
      string left = oldText.Substring(0, ci);
      string right = oldText.Substring(ci);

      var rightRun = CloneRunWithText(run, right);
      applyAndRecord(new SetRunTextEdit(run, oldText, left));
      applyAndRecord(new InsertInlineEdit(p, idx + 1, rightRun));

      // end becomes "after" the left run.
      end = new TextPos(run, left.Length);

      // If start was in the original run and after the split point (rare), shift it into the new run.
      if (ReferenceEquals(start.Inline, run) && start.CharIndex > ci)
         start = new TextPos(rightRun, start.CharIndex - ci);
   }

   private int BuildInsertAt(List<IAtomicEdit> edits, List<Paragraph> refreshPars, Paragraph p, int startLocal, List<IEditable> insertInlines)
   {
      if (insertInlines.Count == 0) return 0;

      // Split insert inlines by paragraph breaks signaled via trailing '\r' on runs.
      var segments = new List<List<IEditable>>();
      var current = new List<IEditable>();
      int paraBreaks = 0;

      foreach (var il in insertInlines)
      {
         if (il is EditableRun r && r.InlineText.EndsWith("\r", StringComparison.Ordinal))
         {
            var trimmed = r.Clone() as EditableRun;
            trimmed!.InlineText = trimmed.InlineText[..^1];
            if (trimmed.InlineText.Length > 0)
               current.Add(trimmed);
            segments.Add(current);
            current = new List<IEditable>();
            paraBreaks++;
         }
         else
         {
            current.Add(il);
         }
      }
      segments.Add(current);

      p.UpdateEditableRunPositions();

      // Insert within a run if possible and inserted is a single run (common typing case).
      if (segments.Count == 1 && segments[0].Count == 1 && segments[0][0] is EditableRun insertRun)
      {
         var target = p.Inlines
            .OfType<EditableRun>()
            .LastOrDefault(r => r.TextPositionOfInlineInParagraph <= startLocal);

         if (target != null)
         {
            int off = Math.Clamp(startLocal - target.TextPositionOfInlineInParagraph, 0, target.InlineText.Length);
            string oldText = target.InlineText;
            string newText = oldText.Insert(off, insertRun.InlineText);
            edits.Add(new SetRunTextEdit(target, oldText, newText));
            refreshPars.Add(p);
            return insertRun.InlineLength;
         }
      }

      int insertedInlineLen = 0;

      // Otherwise insert new inline instances (possibly with paragraph breaks) at computed insertion index.
      int idx = ComputeInsertionInlineIndex(p, startLocal);

      // If there are paragraph breaks, we must split the paragraph and create new ones.
      if (segments.Count > 1)
      {
         // Move suffix inlines into a list (will be appended to the last inserted paragraph).
         var suffix = p.Inlines.Skip(idx).ToList();
         for (int si = suffix.Count - 1; si >= 0; si--)
         {
            var il = suffix[si];
            edits.Add(new RemoveInlineEdit(p, idx + si, il));
         }

         // Insert first segment into current paragraph
         for (int i = 0; i < segments[0].Count; i++)
         {
            edits.Add(new InsertInlineEdit(p, idx + i, segments[0][i]));
            insertedInlineLen += segments[0][i].InlineLength;
         }

         // Insert new paragraphs for following segments
         int blockIdx = doc.Blocks.IndexOf(p) + 1;
         Paragraph prevPar = p;
         for (int s = 1; s < segments.Count; s++)
         {
            var newPar = prevPar.PropertyClone();
            newPar.Inlines.Clear();
            if (segments[s].Count == 0)
               newPar.Inlines.Add(new EditableRun(""));
            else
            {
               foreach (var il in segments[s])
                  newPar.Inlines.Add(il);
            }

            edits.Add(new InsertBlockEdit(doc, blockIdx + (s - 1), newPar));
            refreshPars.Add(newPar);

            insertedInlineLen += segments[s].Sum(x => x.InlineLength);
            prevPar = newPar;
         }

         // Append suffix to the last inserted paragraph.
         int suffixInsertIdx = prevPar.Inlines.Count;
         for (int i = 0; i < suffix.Count; i++)
            edits.Add(new InsertInlineEdit(prevPar, suffixInsertIdx + i, suffix[i]));

         refreshPars.Add(p);
         return insertedInlineLen + paraBreaks; // paragraph boundaries count as 1 each
      }

      // Single paragraph insertion
      for (int i = 0; i < segments[0].Count; i++)
      {
         edits.Add(new InsertInlineEdit(p, idx + i, segments[0][i]));
         insertedInlineLen += segments[0][i].InlineLength;
      }
      refreshPars.Add(p);
      return insertedInlineLen;
   }

   private static int ComputeInsertionInlineIndex(Paragraph p, int startLocal)
   {
      int idx = 0;
      for (int i = 0; i < p.Inlines.Count; i++)
      {
         var il = p.Inlines[i];
         int ilStart = il.TextPositionOfInlineInParagraph;
         int ilEnd = ilStart + il.InlineLength;
         if (startLocal < ilEnd)
         {
            idx = i + 1;
            break;
         }
         idx = i + 1;
      }
      return idx;
   }

   private TextPos NormalizeStartBoundary(TextPos pos, bool preserveAfterLastInlineAtParagraphBoundary)
   {
      var p = GetParagraphFromTextPos(pos);
      int idx = p.Inlines.IndexOf(pos.Inline);
      if (idx < 0) return pos;

      int span = pos.Inline.CursorSpanLength;
      int max = pos.Inline is EditableRun r ? r.InlineText.Length : span;
      int ci = Math.Clamp(pos.CharIndex, 0, max);

      // If we're "after" the inline, advance to the next inline (or next paragraph).
      if (ci == max)
      {
         if (idx < p.Inlines.Count - 1)
            return new TextPos(p.Inlines[idx + 1], 0);

         var nextPar = doc.GetNextParagraph(p);
         if (nextPar is not null)
         {
            if (preserveAfterLastInlineAtParagraphBoundary)
               return new TextPos(pos.Inline, max); // left side of the paragraph boundary
            return new TextPos(nextPar.Inlines[0], 0);
         }

         // End of document: keep the "after last inline" position.
         return new TextPos(pos.Inline, max);
      }

      // For start-boundary reasoning we normalize to "before this inline".
      return new TextPos(pos.Inline, 0);
   }

   private TextPos NormalizeEndBoundary(TextPos pos)
   {
      var p = GetParagraphFromTextPos(pos);
      int idx = p.Inlines.IndexOf(pos.Inline);
      if (idx < 0) return pos;

      int span = pos.Inline.CursorSpanLength;
      int max = pos.Inline is EditableRun r ? r.InlineText.Length : span;
      int ci = Math.Clamp(pos.CharIndex, 0, max);

      // Prefer representing "after inline" as "before next inline" when possible (exclusive end).
      if (ci == max)
      {
         if (idx < p.Inlines.Count - 1)
            return new TextPos(p.Inlines[idx + 1], 0);

         var nextPar = doc.GetNextParagraph(p);
         if (nextPar is not null)
            return new TextPos(nextPar.Inlines[0], 0);

         return new TextPos(pos.Inline, max);
      }

      // For end-boundary, keep "before this inline".
      return new TextPos(pos.Inline, 0);
   }

   private static Paragraph GetParagraphFromTextPos(TextPos pos)
      => pos.Inline.MyParagraph as Paragraph
         ?? throw new InvalidOperationException("TextPos inline has no paragraph.");

   private TextPos GetTextPosFromGlobalIndexPreferLeftAtParagraphBoundary(int globalIndex)
   {
      int idx = Math.Max(0, globalIndex);
      int cursor = 0;

      foreach (var p in doc.Blocks.OfType<Paragraph>())
      {
         if (p.Inlines.Count == 0) throw new InvalidOperationException("Paragraph must have at least one inline.");

         int parLen = 0;
         for (int i = 0; i < p.Inlines.Count; i++)
            parLen += p.Inlines[i].CursorSpanLength;

         int boundaryIndex = cursor + parLen;
         if (idx == boundaryIndex)
         {
            var last = p.Inlines[^1];
            return new TextPos(last, last.CursorSpanLength); // "after last inline" (left side of boundary)
         }

         if (idx < boundaryIndex)
            break;

         cursor = boundaryIndex + 1; // move past paragraph boundary
      }

      return doc.GetTextPosFromGlobalIndex(idx);
   }

   private int GetInlineBoundaryIndex(Paragraph p, TextPos boundary, bool isStart)
   {
      int idx = p.Inlines.IndexOf(boundary.Inline);
      if (idx < 0) return 0;

      if (boundary.Inline is EditableRun r)
      {
         int ci = Math.Clamp(boundary.CharIndex, 0, r.InlineText.Length);
         if (isStart)
            return ci == r.InlineText.Length ? idx + 1 : idx;
         return ci == 0 ? idx : idx + 1;
      }
      else
      {
         int span = boundary.Inline.CursorSpanLength;
         int ci = Math.Clamp(boundary.CharIndex, 0, span);
         if (isStart)
            return ci == span ? idx + 1 : idx;
         return ci == 0 ? idx : idx + 1;
      }
   }

   private void EnumerateWholeInlinesInRange(
      Paragraph startPar,
      int startInlineIndex,
      Paragraph endPar,
      int endExclusiveInlineIndex)
   {
      if (ReferenceEquals(startPar, endPar))
      {
         for (int i = startInlineIndex; i < endExclusiveInlineIndex; i++)
            _tmpWholeInlinesInRange.Add(startPar.Inlines[i]);
         return;
      }

      // Start paragraph tail.
      for (int i = startInlineIndex; i < startPar.Inlines.Count; i++)
         _tmpWholeInlinesInRange.Add(startPar.Inlines[i]);

      // Middle paragraphs.
      int startBlockIndex = doc.Blocks.IndexOf(startPar);
      int endBlockIndex = doc.Blocks.IndexOf(endPar);
      for (int bi = startBlockIndex + 1; bi < endBlockIndex; bi++)
      {
         if (doc.Blocks[bi] is Paragraph p)
         {
            for (int i = 0; i < p.Inlines.Count; i++)
               _tmpWholeInlinesInRange.Add(p.Inlines[i]);
         }
      }

      // End paragraph prefix.
      for (int i = 0; i < endExclusiveInlineIndex; i++)
         _tmpWholeInlinesInRange.Add(endPar.Inlines[i]);
   }

   private static bool IsEnterInsert(List<IEditable> insertInlines)
      => insertInlines.Count == 1 && insertInlines[0] is EditableRun r && r.InlineText == "\r";

   private static EditableRun CloneRunWithText(EditableRun template, string text)
   {
      return new EditableRun(text)
      {
         FontStyle = template.FontStyle,
         FontWeight = template.FontWeight,
         TextDecorations = template.TextDecorations,
         FontSize = template.FontSize,
         FontFamily = template.FontFamily,
         Background = template.Background,
         Foreground = template.Foreground,
         BaselineAlignment = template.BaselineAlignment,
         FontStretch = template.FontStretch
      };
   }
   private void EnsureParagraphHasInline(Paragraph p)
   {
      if (p.Inlines.Count != 0) return;
      ApplyAndRecord(new InsertInlineEdit(p, 0, new EditableRun("")));
   }

   internal Paragraph ResolveStartParagraph(int docIndex)
   {
      var p = doc.GetContainingParagraph(docIndex);
      // If starting at paragraph boundary and not last paragraph, move to next paragraph.
      if (p != doc.Blocks.OfType<Paragraph>().Last() && p.EndInDoc == docIndex)
      {
         int pIndex = doc.Blocks.IndexOf(p);
         return (Paragraph)doc.Blocks[pIndex + 1];
      }
      return p;
   }

   internal Paragraph ResolveEndParagraph(int docIndex)
   {
      // end uses '<' to keep within end of paragraph
      var first = (Paragraph)doc.Blocks.First(b => b.IsParagraph);
      if (docIndex <= first.StartInDoc) return first;

      var p = (Paragraph)doc.Blocks.Last(b => b.IsParagraph && b.StartInDoc < docIndex);

      // If end is exactly at a paragraph boundary (EndInDoc) and there is a next paragraph,
      // treat the end as belonging to the next paragraph so a 1-length range can delete the boundary and merge.
      if (p != doc.Blocks.OfType<Paragraph>().Last() && p.EndInDoc == docIndex)
      {
         int pIndex = doc.Blocks.IndexOf(p);
         return (Paragraph)doc.Blocks[pIndex + 1];
      }

      return p;
   }
}
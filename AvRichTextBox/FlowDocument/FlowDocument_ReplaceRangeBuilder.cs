using System;
using System.Collections.Generic;
using System.Linq;

namespace AvRichTextBox;

public partial class FlowDocument
{
   internal EditAction BuildReplaceRangeAction(int start, int end, List<IEditable> insertInlines)
   {
      if (end < start) throw new ArgumentOutOfRangeException(nameof(end));

      var selectionBefore = CaptureSelectionState();

      // Normalize bounds.
      start = Math.Max(0, start);
      end = Math.Min(DocEndPoint - 1, end);

      // Resolve paragraphs for start/end in the same way as TextRange does.
      var startPar = ResolveStartParagraph(start);
      var endPar = ResolveEndParagraph(end);

      // Local offsets in paragraph text space (0..sumInlineLen).
      int startLocal = Math.Min(start - startPar.StartInDoc, startPar.Text.Length);
      int endLocal = Math.Min(end - endPar.StartInDoc, endPar.Text.Length);

      var edits = new List<IAtomicEdit>(capacity: 32);
      var refreshPars = new List<Paragraph>();

      // 1) Delete range [start,end)
      if (startPar == endPar)
      {
         BuildDeleteWithinParagraph(edits, refreshPars, startPar, startLocal, endLocal);
      }
      else
      {
         BuildDeleteAcrossParagraphs(edits, refreshPars, startPar, startLocal, endPar, endLocal);
      }

      // 2) Insert new content at start position.
      int insertedDocLen = BuildInsertAt(edits, refreshPars, startPar, startLocal, insertInlines);

      // 3) Shift TextRanges by delta.
      int removedLen = end - start;
      int delta = insertedDocLen - removedLen;
      if (delta != 0)
         edits.Add(new ShiftTextRangesEdit(this, start, delta));

      // Selection after: caret at start + insertedLen
      int caret = Math.Min(start + insertedDocLen, DocEndPoint - 1);
      var selectionAfter = new SelectionState(caret, caret, ExtendMode.ExtendModeNone, BiasForwardStart: false, BiasForwardEnd: false);

      int refreshFromIndex = Blocks.IndexOf(startPar);
      return new EditAction(edits, selectionBefore, selectionAfter, refreshFromIndex, refreshPars.Distinct().ToList());
   }

   internal Paragraph ResolveStartParagraph(int docIndex)
   {
      var p = GetContainingParagraph(docIndex);
      // If starting at paragraph boundary and not last paragraph, move to next paragraph.
      if (p != Blocks.OfType<Paragraph>().Last() && p.EndInDoc == docIndex)
      {
         int pIndex = Blocks.IndexOf(p);
         return (Paragraph)Blocks[pIndex + 1];
      }
      return p;
   }

   internal Paragraph ResolveEndParagraph(int docIndex)
   {
      // end uses '<' to keep within end of paragraph
      var first = (Paragraph)Blocks.First(b => b.IsParagraph);
      if (docIndex <= first.StartInDoc) return first;

      return (Paragraph)Blocks.Last(b => b.IsParagraph && b.StartInDoc < docIndex);
   }

   private static void BuildDeleteWithinParagraph(List<IAtomicEdit> edits, List<Paragraph> refreshPars, Paragraph p, int startLocal, int endLocal)
   {
      if (endLocal <= startLocal) return;

      p.UpdateEditableRunPositions();

      // Collect inlines that intersect [startLocal,endLocal)
      var hits = p.Inlines
         .Select((il, idx) => (il, idx, start: il.TextPositionOfInlineInParagraph, end: il.TextPositionOfInlineInParagraph + il.InlineLength))
         .Where(x => x.end > startLocal && x.start < endLocal)
         .ToList();

      if (hits.Count == 0) return;

      // First + last may be partial if they are runs.
      var first = hits[0];
      var last = hits[^1];

      // Middle inlines: remove completely (from end to start so indices stay valid on Apply).
      for (int i = hits.Count - 2; i >= 1; i--)
      {
         var mid = hits[i];
         edits.Add(new RevertAtomicEdit(new InsertInlineEdit(p, mid.idx, mid.il)));
      }

      if (ReferenceEquals(first.il, last.il))
      {
         if (first.il is EditableRun r)
         {
            int a = Math.Clamp(startLocal - first.start, 0, r.InlineText.Length);
            int b = Math.Clamp(endLocal - first.start, 0, r.InlineText.Length);
            string oldText = r.InlineText;
            string newText = oldText.Remove(a, Math.Max(0, b - a));
            edits.Add(new SetRunTextEdit(r, oldText, newText));
         }
         else
         {
            // Non-run (linebreak/ui): remove whole
            edits.Add(new RevertAtomicEdit(new InsertInlineEdit(p, first.idx, first.il)));
         }
      }
      else
      {
         // Tail partial
         if (last.il is EditableRun rLast)
         {
            int cut = Math.Clamp(endLocal - last.start, 0, rLast.InlineText.Length);
            string oldText = rLast.InlineText;
            string newText = oldText.Substring(cut);
            edits.Add(new SetRunTextEdit(rLast, oldText, newText));
         }
         else
         {
            edits.Add(new RevertAtomicEdit(new InsertInlineEdit(p, last.idx, last.il)));
         }

         // Remove fully covered inlines between first and last (already did middle; but need to handle first/last removals if fully covered)
         // First partial
         if (first.il is EditableRun rFirst)
         {
            int keep = Math.Clamp(startLocal - first.start, 0, rFirst.InlineText.Length);
            string oldText = rFirst.InlineText;
            string newText = oldText.Substring(0, keep);
            edits.Add(new SetRunTextEdit(rFirst, oldText, newText));
         }
         else
         {
            edits.Add(new RevertAtomicEdit(new InsertInlineEdit(p, first.idx, first.il)));
         }
      }

      refreshPars.Add(p);
   }

   private void BuildDeleteAcrossParagraphs(List<IAtomicEdit> edits, List<Paragraph> refreshPars, Paragraph startPar, int startLocal, Paragraph endPar, int endLocal)
   {
      // Delete suffix in startPar
      BuildDeleteWithinParagraph(edits, refreshPars, startPar, startLocal, startPar.Text.Length);

      // Delete prefix in endPar
      BuildDeleteWithinParagraph(edits, refreshPars, endPar, 0, endLocal);

      // Move remaining inlines of endPar into startPar (after current inlines)
      startPar.UpdateEditableRunPositions();
      endPar.UpdateEditableRunPositions();

      int insertIndex = startPar.Inlines.Count;
      var toMove = endPar.Inlines.ToList();
      for (int i = 0; i < toMove.Count; i++)
      {
         var il = toMove[i];
         // remove from endPar, then insert into startPar
         edits.Add(new RevertAtomicEdit(new InsertInlineEdit(endPar, i, il)));
         edits.Add(new InsertInlineEdit(startPar, insertIndex + i, il));
      }

      refreshPars.Add(startPar);
      refreshPars.Add(endPar);

      // Remove endPar and any blocks between startPar and endPar (exclusive startPar).
      int startIndex = Blocks.IndexOf(startPar);
      int endIndex = Blocks.IndexOf(endPar);
      for (int bi = endIndex; bi > startIndex; bi--)
      {
         if (Blocks[bi] is Block b)
            edits.Add(new RevertAtomicEdit(new InsertBlockEdit(this, bi, b)));
      }
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
            edits.Add(new RevertAtomicEdit(new InsertInlineEdit(p, idx + si, il)));
         }

         // Insert first segment into current paragraph
         for (int i = 0; i < segments[0].Count; i++)
         {
            edits.Add(new InsertInlineEdit(p, idx + i, segments[0][i]));
            insertedInlineLen += segments[0][i].InlineLength;
         }

         // Insert new paragraphs for following segments
         int blockIdx = Blocks.IndexOf(p) + 1;
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

            edits.Add(new InsertBlockEdit(this, blockIdx + (s - 1), newPar));
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
}


